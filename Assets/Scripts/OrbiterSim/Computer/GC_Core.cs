using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// GC_Core
/// Guidance Computer core loop (V1):
/// - Build per-tick nav snapshot (GuidanceNavCoreState) using OrbitHelpers
/// - Build manual draft from GC_ManualDraft (human inputs)
/// - Step active continuous mode (GC_RuntimeState.activeModeId) using GC_ModeParams
/// - Executor slot reserved (inactive V1)
/// - Two-channel arbitration (Attitude + Throttle): SAFETY > EXECUTOR > MODE > MANUAL
/// - Write final GuidanceCommandIntentState each tick (single output register)
///
/// Conventions:
/// - E: solver inertial frame (heliocentric/SSB ecliptic inertial)
/// - B: craft body frame
/// - Primary body-fixed: +Z is north/pole axis (Vector3.forward), consistent with project helpers.
/// </summary>
public class GC_Core : UdonSharpBehaviour
{
    // --------------------
    // References
    // --------------------
    [Header("Required References")]
    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;
    public EphemSnapshot ephem;
    public BodyCatalog bodies;
    public ThrusterCatalog thrusters;
    public GC_NodePlanNetState nodeNet;

    [Header("Optional docking contacts (for docking helper APIs)")]
    public GuidanceNavContactsState contacts;

    [Header("State Containers (owned by GC_Core)")]
    public GuidanceNavCoreState nav;
    public GC_RuntimeState runtime;
    public GC_ModeParams modeParams;
    public GC_ManualDraft manual;
    public GC_ActuatorOverrideState overrides;
    public NodePlanState plan;
    public GC_AlertState alerts;

    [Header("Output Register")]
    public GuidanceCommandIntentState intent;

    [Header("Optional: craft defaults for intent.ClearToSafeDefaults")]
    public CraftCommandState craftDefaults;

    // =====================================================================
    // Docking helper tuning (V1)
    // =====================================================================
    [Header("Relative translation helper defaults")]
    public float rel_kVel = 0.25f;
    public float rel_kPos = 0.00f;
    public float rel_maxCmd = 1.0f;
    public Vector3 rel_axisWeights = new Vector3(1f, 1f, 0.5f);
    public float rel_velDeadband = 0.02f;
    public float rel_posDeadband = 0.05f;
    public byte rel_rcsMode = CraftCommandState.RCS_MODE_BLENDED;

    // --------------------
    // Tolerances (match helper expectations; tweak later if needed)
    // --------------------
    [Header("Nav Fit Tolerances")]
    public double eTol = 1e-6;
    public double nTol = 1e-9;
    public double hTol = 1e-9;
    public double energyTol = 1e-12;

    [Header("RTN basis tolerances")]
    public double rtn_rTol = 1e-6;
    public double rtn_hTol = 1e-9;

    [Header("Optional displays")]
    public OrreryController orrery;
    public OrreryCraftOrbitRibbon orreryCraftOrbitRibbon;
    public OrreryCraftDirectionMarkers orreryCraftDirectionMarkers;

    // Executor schedule (frozen once when a node is selected)
    private double _exec_tExec = 0.0;
    private double _exec_tBurnStart = 0.0;
    private double _exec_tBurnEnd = 0.0;

    // --------------------
    // Internals: timekeeping for dt
    // --------------------
    private double _lastT = double.NaN;

    // --------------------
    // Draft flags (no structs: Udon-friendly)
    // --------------------
    private bool _manualWritesAtt, _manualWritesThr;
    private bool _modeWritesAtt, _modeWritesThr;
    private bool _execWritesAtt, _execWritesThr;

    // Manual draft payload
    private byte _manAttCmd;
    private Vector3 _manTau_B, _manRate_B;
    private Quaternion _manQ_BE;
    private Vector3 _manPointDir_E;
    private byte _manAxis;
    private bool _manBlend;
    private float _manMainT, _manHoverT;
    private byte _manActMode;
    private bool _manAllowWheels, _manAllowRCS, _manAllowGimbal;

    // Mode draft payload
    private byte _modeAttCmd;
    private Vector3 _modeTau_B, _modeRate_B;
    private Quaternion _modeQ_BE;
    private Vector3 _modePointDir_E;
    private byte _modeAxis;
    private bool _modeBlend;
    private float _modeMainT, _modeHoverT;
    private byte _modeActMode;
    private bool _modeAllowWheels, _modeAllowRCS, _modeAllowGimbal;

    // Exec draft payload
    private byte _execAttCmd;
    private Vector3 _execTau_B, _execRate_B;
    private Quaternion _execQ_BE;
    private Vector3 _execPointDir_E;
    private byte _execAxis;
    private bool _execBlend;
    private float _execMainT, _execHoverT;
    private byte _execActMode;
    private bool _execAllowWheels, _execAllowRCS, _execAllowGimbal;

    // Manual translation payload
    private bool _manualWritesXlat;
    private Vector3 _manTranslate_B;
    private byte _manRcsMode;

    // Mode translation payload
    private bool _modeWritesXlat;
    private Vector3 _modeTranslate_B;
    private byte _modeRcsMode;

    // Exec translation payload
    private bool _execWritesXlat;
    private Vector3 _execTranslate_B;
    private byte _execRcsMode;

    // --------------------
    // Unity Loop
    // --------------------
    void LateUpdate()
    {
        Tick();
    }

    /// <summary>
    /// One guidance tick. This is the ONLY place we write the final intent.
    /// </summary>
    public void Tick()
    {
        if (intent == null || nav == null || runtime == null || modeParams == null || manual == null) return;
        if (craft == null || craftAtt == null || bodies == null || ephem == null) return;

        // 1) Clear output register to safe baseline.
        intent.ClearToSafeDefaults(craftDefaults);
        intent.commandSource = 1;

        // 2) Build nav snapshot.
        BuildNavCoreSnapshot();
        UpdateSelectedNodeNavExport();
        UpdateAlerts();

        // 3) Acquire manual inputs.
        BuildManualDraftFromManualState();

        ApplyManualAutoModeSwitch();
        ApplyManualTranslateAutoModeSwitch();

        // 4) Step executor slot.
        BuildExecutorDraft_FromPlan();

        // 5) Step active continuous mode.
        BuildModeDraft();

        // 6) Two-channel arbitration + final intent.
        ArbitrateAndWriteIntent();

        UpdateActiveProgramIndicator();

        if (orrery != null)
            orrery.TickOrrery();

        if (orreryCraftOrbitRibbon != null)
            orreryCraftOrbitRibbon.TickRibbon();

        if (orreryCraftDirectionMarkers != null)
        {
            orreryCraftDirectionMarkers.TickMarkers();

            if (orrery != null)
            {
                Vector3 clipCenterWorld;
                float clipRadiusWorld;
                orrery.GetCurrentClipSphereWorld(out clipCenterWorld, out clipRadiusWorld);
                orreryCraftDirectionMarkers.ApplyClipVolumeParams(clipCenterWorld, clipRadiusWorld);
            }
        }
    }

    // =====================================================================
    // 2) NAV SNAPSHOT
    // =====================================================================

    private void BuildNavCoreSnapshot()
    {
        // Time
        nav.t = ephem.t;
        nav.jd = ephem.jd;

        // dt from mission time
        if (double.IsNaN(_lastT)) nav.dt = 0.0;
        else
        {
            double dt = nav.t - _lastT;
            nav.dt = (dt > 0.0) ? dt : 0.0;
        }
        _lastT = nav.t;

        // Craft heliocentric inertial
        nav.rC_x = craft.rx; nav.rC_y = craft.ry; nav.rC_z = craft.rz;
        nav.vC_x = craft.vx; nav.vC_y = craft.vy; nav.vC_z = craft.vz;

        // Attitude + rates
        nav.qBE = craftAtt.qBE;
        nav.wB_x = craftAtt.wx;
        nav.wB_y = craftAtt.wy;
        nav.wB_z = craftAtt.wz;

        // Primary selection
        nav.primaryId = craft.primaryBodyId;

        // Primary constants
        nav.muPrimary = bodies.GetMu(nav.primaryId);
        nav.radiusPrimary = bodies.GetRadius(nav.primaryId);
        nav.soiRadiusPrimary = bodies.GetSOIRadius(nav.primaryId);

        // Primary state (E)
        bodies.GetBodyState(nav.primaryId,
            out nav.rP_x, out nav.rP_y, out nav.rP_z,
            out nav.vP_x, out nav.vP_y, out nav.vP_z);

        bodies.GetBodyOmega(nav.primaryId,
            out nav.omegaP_x, out nav.omegaP_y, out nav.omegaP_z);

        nav.qPF2E = bodies.GetBodyFixedToInertial(nav.primaryId);

        // Primary equator basis in E (+Z is north)
        nav.Ieq_E = nav.qPF2E * Vector3.right;
        nav.Jeq_E = nav.qPF2E * Vector3.up;
        nav.Keq_E = nav.qPF2E * Vector3.forward;

        // Primary-relative craft state (still expressed in E basis)
        bodies.ToPrimaryRelative(nav.primaryId, craft,
            out nav.r_x, out nav.r_y, out nav.r_z,
            out nav.v_x, out nav.v_y, out nav.v_z);

        // Magnitudes
        nav.rMag = System.Math.Sqrt(nav.r_x * nav.r_x + nav.r_y * nav.r_y + nav.r_z * nav.r_z);
        double v2 = nav.v_x * nav.v_x + nav.v_y * nav.v_y + nav.v_z * nav.v_z;
        nav.vMag = System.Math.Sqrt(v2);

        nav.valid = (nav.muPrimary > 0.0 && nav.rMag > 0.0);

        // RTN basis
        Vector3 rHat, tHat, nHat;
        bool rtnOk = OrbitHelpers.TryBuildRTNBasis(
            nav.r_x, nav.r_y, nav.r_z,
            nav.v_x, nav.v_y, nav.v_z,
            rtn_rTol, rtn_hTol,
            out rHat, out tHat, out nHat);

        if (rtnOk)
        {
            nav.Rhat_E = rHat;
            nav.That_E = tHat;
            nav.Nhat_E = nHat;
        }
        else
        {
            nav.Rhat_E = Vector3.right;
            nav.That_E = Vector3.up;
            nav.Nhat_E = Vector3.forward;
            nav.valid = false;
        }

        // Conic + invariants
        double aMeters, eMag, iRad, raanRad, argpRad, nuRad;
        double hx, hy, hz;
        double ex, ey, ez;
        double rMeters, vMetersPerSec, specificEnergy;

        bool conicOk = OrbitHelpers.TryConicFromState(
            nav.r_x, nav.r_y, nav.r_z,
            nav.v_x, nav.v_y, nav.v_z,
            nav.muPrimary,
            eTol, nTol, hTol, energyTol,
            out aMeters, out eMag,
            out iRad, out raanRad, out argpRad, out nuRad,
            out hx, out hy, out hz,
            out ex, out ey, out ez,
            out rMeters, out vMetersPerSec, out specificEnergy);

        if (conicOk)
        {
            nav.a = aMeters;
            nav.e = eMag;
            nav.energy = specificEnergy;

            nav.h_E = new Vector3((float)hx, (float)hy, (float)hz);
            nav.hMag = System.Math.Sqrt(hx * hx + hy * hy + hz * hz);

            nav.eVec_E = new Vector3((float)ex, (float)ey, (float)ez);

            nav.p = (nav.muPrimary > 0.0) ? (nav.hMag * nav.hMag / nav.muPrimary) : 0.0;


            nav.iInertialRad = iRad;
            nav.raanInertialRad = raanRad;
            nav.argpInertialRad = argpRad;

            // Convert (i, Ω, ω) into primary-equatorial reference plane (+Z north).
            double iEq, raanEq, argpEq;
            bool angOk = OrbitHelpers.TryConvertAnglesToBodyEquatorial(
                hx, hy, hz,
                ex, ey, ez,
                nav.qPF2E,
                eTol, nTol, hTol,
                out iEq, out raanEq, out argpEq);

            if (angOk)
            {
                nav.iRad = iEq;
                nav.raanRad = raanEq;
                nav.argpRad = argpEq;
            }
            else
            {
                nav.iRad = iRad;
                nav.raanRad = raanRad;
                nav.argpRad = argpRad;
            }

            nav.nuRad = nuRad;
        }
        else
        {
            nav.valid = false;
            nav.a = nav.e = nav.energy = nav.p = 0.0;
            nav.h_E = Vector3.forward; nav.hMag = 0.0;
            nav.eVec_E = Vector3.zero;
            nav.iRad = nav.raanRad = nav.argpRad = nav.nuRad = 0.0;
        }

        nav.lastBuildTime = nav.t;
    }

    private void UpdateSelectedNodeNavExport()
    {
        nav.selectedNodeVectorValid = false;
        nav.selectedNodeIndex = -1;
        nav.selectedNodeDV_E = Vector3.zero;
        nav.selectedNodeDir_E = Vector3.zero;
        nav.selectedNodeDVmag_mps = 0f;
        nav.selectedNodeRemainingDV_mps = 0f;

        if (plan == null || modeParams == null) return;

        int idx;
        Vector3 dvE;
        float dvMag;
        float dvRemain;

        if (!TryGetSelectedNodeVector(out idx, out dvE, out dvMag, out dvRemain))
            return;

        nav.selectedNodeVectorValid = true;
        nav.selectedNodeIndex = idx;
        nav.selectedNodeDV_E = dvE;
        nav.selectedNodeDir_E = (dvMag > 1e-6f) ? (dvE / dvMag) : Vector3.zero;
        nav.selectedNodeDVmag_mps = dvMag;
        nav.selectedNodeRemainingDV_mps = dvRemain;
    }

    private void UpdateAlerts()
    {
        if (alerts == null) return;

        alerts.ClearEvaluatedState();

        UpdatePeriapsisAlerts();
        UpdateSelectedTargetAlerts();
        UpdateNodeAlerts();

        // Auto-clear acknowledgement when underlying condition is gone.
        if (!alerts.cond_periapsisWarn) alerts.ackPeriapsisWarn = false;
        if (!alerts.cond_periapsisCritical) alerts.ackPeriapsisCritical = false;
        if (!alerts.cond_selectedClosureHigh) alerts.ackSelectedClosureHigh = false;
        if (!alerts.cond_nodeSoon) alerts.ackNodeSoon = false;
        if (!alerts.cond_nodeAutoExecuteDisabled) alerts.ackNodeAutoExecuteDisabled = false;

        alerts.RebuildOutputs();
    }

    private void UpdatePeriapsisAlerts()
    {
        if (alerts == null || nav == null) return;
        if (!nav.valid) return;
        if (nav.radiusPrimary <= 0.0) return;

        double rp = 0.0;
        bool haveRp = false;

        // Prefer p / (1 + e) for general conic consistency if valid.
        if (nav.p > 0.0 && nav.e >= 0.0)
        {
            double denom = 1.0 + nav.e;
            if (denom > 1e-12)
            {
                rp = nav.p / denom;
                haveRp = true;
            }
        }

        // Fallback to a(1-e) if needed.
        if (!haveRp && nav.a > 0.0 && nav.e >= 0.0)
        {
            rp = nav.a * (1.0 - nav.e);
            haveRp = true;
        }

        if (!haveRp) return;

        double alt = rp - nav.radiusPrimary;

        alerts.periapsisRadiusMeters = rp;
        alerts.periapsisAltitudeMeters = alt;

        if (alt <= alerts.periapsisCriticalAltMeters)
            alerts.cond_periapsisCritical = true;

        if (alt <= alerts.periapsisWarnAltMeters)
            alerts.cond_periapsisWarn = true;
    }

    private void UpdateSelectedTargetAlerts()
    {
        if (alerts == null || contacts == null) return;
        if (!contacts.selValid) return;

        double drx = contacts.sel_drx_E;
        double dry = contacts.sel_dry_E;
        double drz = contacts.sel_drz_E;

        double dvx = contacts.sel_dvx_E;
        double dvy = contacts.sel_dvy_E;
        double dvz = contacts.sel_dvz_E;

        double r2 = drx * drx + dry * dry + drz * drz;
        double v2 = dvx * dvx + dvy * dvy + dvz * dvz;

        if (r2 <= 1e-12) return;

        double r = System.Math.Sqrt(r2);
        double v = (v2 > 0.0) ? System.Math.Sqrt(v2) : 0.0;

        // dr = target - craft, dv = target - craft
        // Positive closure means approaching -> -(dv dot rhat)
        double invR = 1.0 / r;
        double closure = -((dvx * drx + dvy * dry + dvz * drz) * invR);

        alerts.cond_selectedTargetValid = true;
        alerts.selectedTargetRangeMeters = r;
        alerts.selectedRelSpeedMps = v;
        alerts.selectedClosureMps = closure;

        if (r <= alerts.selectedClosureWarnRangeMeters &&
            closure >= alerts.selectedClosureWarnMps)
        {
            alerts.cond_selectedClosureHigh = true;
        }
    }

    private void UpdateNodeAlerts()
    {
        if (alerts == null) return;

        // Selected node export from nav
        if (nav != null && nav.selectedNodeVectorValid)
        {
            alerts.cond_nodeSelectedValid = true;
            alerts.nodeSelectedIndex = nav.selectedNodeIndex;
            alerts.nodeRemainingDV_mps = nav.selectedNodeRemainingDV_mps;
        }

        bool anyArmed = false;
        if (plan != null && plan.status != null)
        {
            int n = plan.maxNodes;
            if (plan.status.Length < n) n = plan.status.Length;

            for (int i = 0; i < n; i++)
            {
                if (plan.status[i] == NodePlanState.STATUS_ARMED)
                {
                    anyArmed = true;
                    break;
                }
            }
        }

        alerts.cond_armedNodeExists = anyArmed;

        if (anyArmed && runtime != null && !runtime.autoExecuteArmedNodes)
            alerts.cond_nodeAutoExecuteDisabled = true;

        double tGo;
        if (TryGetNextArmedNodeTimeToGo(out tGo))
        {
            alerts.nodeTimeToGoSec = tGo;

            if (tGo >= 0.0 && tGo <= alerts.nodeSoonLeadSec)
                alerts.cond_nodeSoon = true;
        }
        else
        {
            alerts.nodeTimeToGoSec = 0.0;
        }
    }

    private bool TryGetNextArmedNodeTimeToGo(out double tGoSec)
    {
        tGoSec = 0.0;

        if (plan == null || nav == null) return false;
        if (plan.status == null) return false;

        int best = -1;
        double bestT = 0.0;
        double nowT = nav.t;

        int n = plan.maxNodes;
        if (plan.status.Length < n) n = plan.status.Length;

        for (int i = 0; i < n; i++)
        {
            if (plan.status[i] != NodePlanState.STATUS_ARMED) continue;

            double t = ComputeNodeTriggerTime(i, nowT);

            if (best < 0 || t < bestT)
            {
                best = i;
                bestT = t;
            }
        }

        if (best < 0) return false;

        tGoSec = bestT - nowT;
        return true;
    }

    // =====================================================================
    // 3) MANUAL INPUT ACQUISITION
    // =====================================================================

    private void BuildManualDraftFromManualState()
    {
        _manualWritesAtt = true;
        _manualWritesThr = true;

        if (manual.useRateControl)
        {
            _manAttCmd = CraftCommandState.ATT_CMD_RATE_TARGET;
            _manRate_B = manual.rateCmd_B;
            _manTau_B = Vector3.zero;
        }
        else
        {
            _manAttCmd = CraftCommandState.ATT_CMD_TORQUE_DIRECT;
            _manTau_B = manual.tauCmd_B;
            _manRate_B = Vector3.zero;
        }

        _manQ_BE = Quaternion.identity;
        _manPointDir_E = Vector3.forward;
        _manAxis = 2;
        _manBlend = true;

        _manMainT = manual.mainThrottle01;
        _manHoverT = manual.hoverThrottle01;

        _manActMode = manual.attitudeActuatorMode;
        _manAllowWheels = manual.allowWheels;
        _manAllowRCS = manual.allowRCS;
        _manAllowGimbal = manual.allowGimbal;

        _manualWritesXlat = true;
        _manTranslate_B = manual.translateCmd_B;
        _manRcsMode = manual.rcsMode;
    }

    // =====================================================================
    // 5) ACTIVE MODE PROGRAM -> mode draft
    // =====================================================================

    private void BuildModeDraft()
    {
        _modeWritesAtt = false;
        _modeWritesThr = false;

        // defaults
        _modeAttCmd = 0;
        _modeTau_B = Vector3.zero;
        _modeRate_B = Vector3.zero;
        _modeQ_BE = Quaternion.identity;
        _modePointDir_E = Vector3.forward;
        _modeAxis = 2;
        _modeBlend = true;

        _modeMainT = 0f;
        _modeHoverT = 0f;

        _modeWritesXlat = false;
        _modeTranslate_B = Vector3.zero;
        _modeRcsMode = 2;

        _modeActMode = 3;
        _modeAllowWheels = true;
        _modeAllowRCS = true;
        _modeAllowGimbal = true;

        switch (runtime.activeModeId)
        {
            default:
            case GC_RuntimeState.MODE_MANUAL:
                break;

            case GC_RuntimeState.MODE_HOLD_QUAT:
                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_ATTITUDE_TARGET;
                _modeQ_BE = modeParams.qTarget_BE;
                break;

            case GC_RuntimeState.MODE_POINT_DIR_E:
                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _modePointDir_E = modeParams.pointDirTarget_E;
                _modeAxis = ClampAxis012(modeParams.bodyAxisToPoint);
                break;

            case GC_RuntimeState.MODE_HOLD_RTN_DIR:
                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _modeAxis = ClampAxis012(modeParams.bodyAxisToPoint);
                _modePointDir_E = ResolveRtnDirection(nav, modeParams.rtnDir);
                break;

            case GC_RuntimeState.MODE_RATE_TARGET:
                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_RATE_TARGET;
                _modeRate_B = modeParams.rateTarget_B;
                break;

            case GC_RuntimeState.MODE_DIRECT_TORQUE:
                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_TORQUE_DIRECT;
                _modeTau_B = modeParams.tauDirect_B;
                _modeBlend = modeParams.blendDirectTorqueWithPD;
                break;

            case GC_RuntimeState.MODE_HOLD_HORIZON:
            {
                Quaternion qTarget;
                if (!TryBuildHorizonHoldTarget(out qTarget))
                    break;

                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_ATTITUDE_TARGET;
                _modeQ_BE = qTarget;
                break;
            }

            case GC_RuntimeState.MODE_POINT_NODE_VECTOR:
            {
                int idx;
                Vector3 dvE;
                float dvMag;
                float dvRemain;

                if (!TryGetSelectedNodeVector(out idx, out dvE, out dvMag, out dvRemain))
                    break;

                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _modePointDir_E = dvE / dvMag;
                _modeAxis = ClampAxis012(modeParams.bodyAxisToPoint);
                break;
            }

            case GC_RuntimeState.MODE_DOCK_POINT_SHIPZ_TO_PORT:
            {
                if (contacts == null || !contacts.dockValid0) break;

                Vector3 craftPort_B = new Vector3((float)contacts.craftPort_px_B0, (float)contacts.craftPort_py_B0, (float)contacts.craftPort_pz_B0);
                Vector3 targetPort_B = new Vector3((float)contacts.targetPort_px_B0, (float)contacts.targetPort_py_B0, (float)contacts.targetPort_pz_B0);

                Vector3 err_B = targetPort_B - craftPort_B;
                if (err_B.sqrMagnitude < 1e-8f) break;

                Vector3 los_E = (nav.qBE * err_B).normalized;
                Vector3 shipAxis_E = (nav.qBE * Vector3.forward).normalized;

                Quaternion qErr_E = Quaternion.FromToRotation(shipAxis_E, los_E);
                Quaternion qDesired_BE = qErr_E * nav.qBE;

                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_ATTITUDE_TARGET;
                _modeQ_BE = qDesired_BE;
                break;
            }

            case GC_RuntimeState.MODE_DOCK_ALIGN_PORTS:
            {
                if (contacts == null || !contacts.dockValid0) break;

                Quaternion qTargetPort_E = contacts.qTargetPort_E0;
                Quaternion qCraftPort_B = contacts.qCraftPort_B0;

                Quaternion qFlip = Quaternion.AngleAxis(180f, Vector3.up);
                Quaternion qDesired_BE = qTargetPort_E * qFlip * Quaternion.Inverse(qCraftPort_B);

                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_ATTITUDE_TARGET;
                _modeQ_BE = qDesired_BE;
                break;
            }

            case GC_RuntimeState.MODE_RELVEL_PROGRADE:
            case GC_RuntimeState.MODE_RELVEL_RETROGRADE:
            {
                if (contacts == null || !contacts.fullValid0) break;

                Vector3 dv_E = new Vector3((float)contacts.dvx_E0, (float)contacts.dvy_E0, (float)contacts.dvz_E0);
                if (dv_E.sqrMagnitude < 1e-10f) break;

                Vector3 dir_E = (runtime.activeModeId == GC_RuntimeState.MODE_RELVEL_RETROGRADE) ? (-dv_E) : dv_E;
                dir_E.Normalize();

                _modeWritesAtt = true;
                _modeAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _modePointDir_E = dir_E;
                _modeAxis = ClampAxis012(modeParams.bodyAxisToPoint);
                break;
            }
        }

        // --------------------
        // Translation assist
        // --------------------
        switch (runtime.activeTranslateModeId)
        {
            default:
            case GC_RuntimeState.XLAT_MANUAL:
                break;

            case GC_RuntimeState.XLAT_KILL_RELVEL:
            {
                if (contacts == null || !contacts.fullValid0) break;

                Vector3 dv_E = new Vector3((float)contacts.dvx_E0, (float)contacts.dvy_E0, (float)contacts.dvz_E0);

                Quaternion qEB = Quaternion.Inverse(nav.qBE);
                Vector3 dv_B = qEB * dv_E;

                if (dv_B.magnitude < rel_velDeadband) dv_B = Vector3.zero;

                Vector3 dr_B = new Vector3((float)contacts.drx_B0, (float)contacts.dry_B0, (float)contacts.drz_B0);
                if (rel_kPos > 0f && dr_B.magnitude < rel_posDeadband) dr_B = Vector3.zero;

                Vector3 cmd_B = (-rel_kVel * dv_B) + (-rel_kPos * dr_B);

                cmd_B = new Vector3(
                    cmd_B.x * rel_axisWeights.x,
                    cmd_B.y * rel_axisWeights.y,
                    cmd_B.z * rel_axisWeights.z
                );

                float mag = cmd_B.magnitude;
                if (mag > rel_maxCmd && mag > 1e-6f)
                    cmd_B = (rel_maxCmd / mag) * cmd_B;

                _modeWritesXlat = true;
                _modeTranslate_B = cmd_B;
                _modeRcsMode = rel_rcsMode;
                break;
            }
        }
    }

    private static Vector3 ResolveRtnDirection(GuidanceNavCoreState nav, byte rtnDir)
    {
        Vector3 R = nav.Rhat_E;
        Vector3 T = nav.That_E;
        Vector3 N = nav.Nhat_E;

        switch (rtnDir)
        {
            default:
            case GC_ModeParams.RTN_T_PLUS:  return T;
            case GC_ModeParams.RTN_T_MINUS: return -T;
            case GC_ModeParams.RTN_R_PLUS:  return R;
            case GC_ModeParams.RTN_R_MINUS: return -R;
            case GC_ModeParams.RTN_N_PLUS:  return N;
            case GC_ModeParams.RTN_N_MINUS: return -N;
        }
    }

    private static byte ClampAxis012(byte axis)
    {
        return (axis > 2) ? (byte)2 : axis;
    }

    private bool TryGetSelectedNodeVector(out int idx, out Vector3 dvE, out float dvMag, out float dvRemain)
    {
        idx = -1;
        dvE = Vector3.zero;
        dvMag = 0f;
        dvRemain = 0f;

        if (plan == null || modeParams == null) return false;

        int i = (int)modeParams.selectedNodeIndex;
        if (i < 0 || i >= plan.maxNodes) return false;

        if (plan.status == null || i >= plan.status.Length) return false;
        byte st = plan.status[i];
        if (st == NodePlanState.STATUS_EMPTY) return false;

        if (plan.dV_E == null || i >= plan.dV_E.Length) return false;
        Vector3 v = plan.dV_E[i];
        float mag = v.magnitude;
        if (mag <= 1e-6f) return false;

        float rem = mag;
        if (plan.remainingDV_mps != null && i < plan.remainingDV_mps.Length)
            rem = Mathf.Max(0f, plan.remainingDV_mps[i]);

        idx = i;
        dvE = v;
        dvMag = mag;
        dvRemain = rem;
        return true;
    }

    private bool TryBuildHorizonHoldTarget(out Quaternion qTargetBE)
    {
        qTargetBE = Quaternion.identity;
        if (nav == null || !nav.valid) return false;

        Vector3 forward_E = nav.That_E;
        Vector3 up_E = nav.Rhat_E;

        if (forward_E.sqrMagnitude < 1e-8f || up_E.sqrMagnitude < 1e-8f)
            return false;

        forward_E.Normalize();
        up_E.Normalize();

        // Re-orthogonalize for safety
        Vector3 right_E = Vector3.Cross(up_E, forward_E);
        if (right_E.sqrMagnitude < 1e-8f)
            return false;

        right_E.Normalize();
        up_E = Vector3.Cross(forward_E, right_E).normalized;

        qTargetBE = Quaternion.LookRotation(forward_E, up_E);
        return true;
    }

    // =====================================================================
    // 6) ARBITRATION + FINAL INTENT WRITE
    // =====================================================================

    private void ArbitrateAndWriteIntent()
    {
        bool safetyCutThrottle = (runtime.status == GC_RuntimeState.STATUS_FAULT);

        // ATTITUDE: EXECUTOR > MODE > MANUAL
        if (_execWritesAtt)
        {
            WriteAttitudeToIntent(
                _execAttCmd, _execTau_B, _execRate_B, _execQ_BE, _execPointDir_E,
                _execAxis, _execBlend,
                _execActMode, _execAllowWheels, _execAllowRCS, _execAllowGimbal);
        }
        else if (_modeWritesAtt)
        {
            WriteAttitudeToIntent(
                _modeAttCmd, _modeTau_B, _modeRate_B, _modeQ_BE, _modePointDir_E,
                _modeAxis, _modeBlend,
                _modeActMode, _modeAllowWheels, _modeAllowRCS, _modeAllowGimbal);
        }
        else
        {
            WriteAttitudeToIntent(
                _manAttCmd, _manTau_B, _manRate_B, _manQ_BE, _manPointDir_E,
                _manAxis, _manBlend,
                _manActMode, _manAllowWheels, _manAllowRCS, _manAllowGimbal);
        }

        // TRANSLATION: EXECUTOR > MODE > MANUAL
        Vector3 xlat = _manTranslate_B;
        byte rcsMode = _manRcsMode;

        if (_modeWritesXlat)
        {
            xlat = _modeTranslate_B;
            rcsMode = _modeRcsMode;
        }
        if (_execWritesXlat)
        {
            xlat = _execTranslate_B;
            rcsMode = _execRcsMode;
        }

        intent.translateCmd_B = xlat;
        intent.rcsMode = rcsMode;

        ApplyActuatorOverrides();

        // THROTTLE: SAFETY > EXECUTOR > MODE > MANUAL
        float mainT = _manMainT;
        float hoverT = _manHoverT;

        if (_modeWritesThr)
        {
            mainT = _modeMainT;
            hoverT = _modeHoverT;
        }

        if (_execWritesThr)
        {
            mainT = _execMainT;
            hoverT = _execHoverT;
        }

        if (safetyCutThrottle)
        {
            mainT = 0f;
            hoverT = 0f;
        }

        intent.mainThrottle01 = Mathf.Clamp01(mainT);
        intent.hoverThrottle01 = Mathf.Clamp01(hoverT);
    }

    private void WriteAttitudeToIntent(
        byte cmdMode,
        Vector3 tau_B,
        Vector3 rate_B,
        Quaternion q_BE,
        Vector3 pointDir_E,
        byte axisToPoint,
        bool blendTau,
        byte attActMode,
        bool allowWheels,
        bool allowRCS,
        bool allowGimbal)
    {
        intent.attitudeCmdMode = cmdMode;

        intent.tauDirect_B = tau_B;
        intent.rateTarget_B = rate_B;
        intent.qTarget_BE = q_BE;

        intent.pointDirTarget_E = pointDir_E;
        intent.bodyAxisToPoint = ClampAxis012(axisToPoint);
        intent.blendDirectTorqueWithPD = blendTau;

        intent.attitudeActuatorMode = attActMode;
        intent.allowWheels = allowWheels;
        intent.allowRCS = allowRCS;
        intent.allowGimbal = allowGimbal;
    }

    private void ApplyActuatorOverrides()
    {
        if (overrides == null) return;

        if (overrides.overrideAllowWheels == GC_ActuatorOverrideState.FORCE_DISABLE) intent.allowWheels = false;
        else if (overrides.overrideAllowWheels == GC_ActuatorOverrideState.FORCE_ENABLE) intent.allowWheels = true;

        if (overrides.overrideAllowRCS == GC_ActuatorOverrideState.FORCE_DISABLE) intent.allowRCS = false;
        else if (overrides.overrideAllowRCS == GC_ActuatorOverrideState.FORCE_ENABLE) intent.allowRCS = true;

        if (overrides.overrideAllowGimbal == GC_ActuatorOverrideState.FORCE_DISABLE) intent.allowGimbal = false;
        else if (overrides.overrideAllowGimbal == GC_ActuatorOverrideState.FORCE_ENABLE) intent.allowGimbal = true;

        if (overrides.overrideAttitudeActuatorMode != 0)
            intent.attitudeActuatorMode = overrides.overrideAttitudeActuatorMode;

        if (overrides.overrideRcsMode == GC_ActuatorOverrideState.RCSMODE_FORCE_TRANSLATE)
            intent.rcsMode = CraftCommandState.RCS_MODE_TRANSLATE;
        else if (overrides.overrideRcsMode == GC_ActuatorOverrideState.RCSMODE_FORCE_ROTATE)
            intent.rcsMode = CraftCommandState.RCS_MODE_ROTATE;
        else if (overrides.overrideRcsMode == GC_ActuatorOverrideState.RCSMODE_FORCE_BLENDED)
            intent.rcsMode = CraftCommandState.RCS_MODE_BLENDED;
    }

    private bool ManualAttitudeIsActive()
    {
        float dz = runtime.manualTakeoverDeadzone;

        if (manual == null) return false;

        if (manual.useRateControl)
            return manual.rateCmd_B.sqrMagnitude > dz * dz;

        return manual.tauCmd_B.sqrMagnitude > dz * dz;
    }

    private bool ManualThrottleIsActive()
    {
        float dz = runtime.manualTakeoverDeadzone;
        if (manual == null) return false;
        return (manual.mainThrottle01 > dz) || (manual.hoverThrottle01 > dz);
    }

    private bool ManualTranslateIsActive()
    {
        float dz = runtime.manualTakeoverDeadzone;
        if (manual == null) return false;
        return manual.translateCmd_B.sqrMagnitude > dz * dz;
    }

    private bool ManualInputIsActive()
    {
        if (ManualAttitudeIsActive()) return true;
        if (ManualThrottleIsActive()) return true;
        if (ManualTranslateIsActive()) return true;
        return false;
    }

    private void ApplyManualTranslateAutoModeSwitch()
    {
        if (!runtime.autoSwitchTranslateToManualOnInput) return;

        if (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_SLEW ||
            runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
            runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST)
        {
            // intentionally allowed to fall through for now
        }

        bool active = ManualTranslateIsActive();
        if (active)
        {
            runtime.lastManualTranslateInputTime = nav.t;

            if (runtime.activeTranslateModeId != GC_RuntimeState.XLAT_MANUAL)
            {
                runtime.lastNonManualTranslateModeId = runtime.activeTranslateModeId;
                runtime.activeTranslateModeId = GC_RuntimeState.XLAT_MANUAL;
            }
            return;
        }

        if (runtime.latchTranslateTakeover) return;

        double dtSince = nav.t - runtime.lastManualTranslateInputTime;
        if (dtSince >= runtime.translateReleaseTimeoutSec)
        {
            if (runtime.activeTranslateModeId == GC_RuntimeState.XLAT_MANUAL)
            {
                byte prev = runtime.lastNonManualTranslateModeId;
                if (prev != GC_RuntimeState.XLAT_MANUAL)
                    runtime.activeTranslateModeId = prev;
            }
        }
    }

    private void ApplyManualAutoModeSwitch()
    {
        if (!runtime.autoSwitchToManualOnInput) return;

        if (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_SLEW ||
            runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
            runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST)
        {
            return;
        }

        bool active = ManualAttitudeIsActive();
        if (active)
        {
            runtime.lastManualInputTime = nav.t;

            if (runtime.activeModeId != GC_RuntimeState.MODE_MANUAL)
            {
                runtime.lastNonManualModeId = runtime.activeModeId;
                runtime.activeModeId = GC_RuntimeState.MODE_MANUAL;
                runtime.modeStartTime = nav.t;
            }
            return;
        }

        if (runtime.latchManualTakeover) return;

        double dtSince = nav.t - runtime.lastManualInputTime;
        if (dtSince >= runtime.manualReleaseTimeoutSec)
        {
            if (runtime.activeModeId == GC_RuntimeState.MODE_MANUAL)
            {
                byte prev = runtime.lastNonManualModeId;
                if (prev != GC_RuntimeState.MODE_MANUAL)
                {
                    runtime.activeModeId = prev;
                    runtime.modeStartTime = nav.t;
                }
            }
        }
    }

    private void BuildExecutorDraft_FromPlan()
    {
        // Clear exec draft defaults
        _execWritesAtt = false;
        _execWritesThr = false;

        _execAttCmd = 0;
        _execTau_B = Vector3.zero;
        _execRate_B = Vector3.zero;
        _execQ_BE = Quaternion.identity;
        _execPointDir_E = Vector3.forward;
        _execAxis = 2;
        _execBlend = true;

        _execMainT = 0f;
        _execHoverT = 0f;

        _execWritesXlat = false;
        _execTranslate_B = Vector3.zero;
        _execRcsMode = CraftCommandState.RCS_MODE_BLENDED;

        _execActMode = CraftCommandState.ATT_ACT_AUTO;
        _execAllowWheels = true;
        _execAllowRCS = true;
        _execAllowGimbal = true;

        if (plan == null) return;

        double nowT = nav.t;

        // Hard gate: no automatic node execution when switch is off.
        if (!runtime.autoExecuteArmedNodes)
        {
            runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
            runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
            runtime.executorNodeIndex = -1;
            plan.activeIndex = -1;
            return;
        }

        if (runtime.abortExecOnManualInput &&
            (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_SLEW ||
             runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
             runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST))
        {
            if (ManualInputIsActive())
            {
                AbortExecutor(nowT);
                return;
            }
        }

        if (runtime.activeExecutorId == GC_RuntimeState.EXEC_NONE ||
            runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_NONE)
        {
            runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
            runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
            runtime.executorNodeIndex = -1;
            plan.activeIndex = -1;
        }

        // WAIT: find next armed node and decide when to start slewing
        if (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_WAIT)
        {
            int next = FindBestNextNodeIndex(nowT);
            if (next < 0)
            {
                runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
                runtime.executorNodeIndex = -1;
                plan.activeIndex = -1;
                return;
            }

            double tExec = ComputeNodeTriggerTime(next, nowT);

            float tBurn = 0f;
            if (plan.burnDurationSec != null && next >= 0 && next < plan.burnDurationSec.Length)
                tBurn = Mathf.Max(0f, plan.burnDurationSec[next]);

            double tBurnStart = tExec - 0.5 * (double)tBurn;
            double tBurnEnd = tExec + 0.5 * (double)tBurn;

            if (tBurn > 0f && tBurnStart < nowT)
            {
                tBurnStart = nowT;
                tBurnEnd = tBurnStart + (double)tBurn;
            }

            float slewLead = plan.preSlewLeadSec[next];
            double tSlewStart = tBurnStart - (double)slewLead;

            if (nowT >= tSlewStart)
            {
                BeginExecutorForNode(next, nowT, tExec, tBurnStart, tBurnEnd);
            }
            else
            {
                runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
                runtime.executorNodeIndex = -1;
                plan.activeIndex = -1;
                return;
            }
        }

        int idx = runtime.executorNodeIndex;
        if (idx < 0 || idx >= plan.maxNodes)
        {
            runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
            runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
            runtime.executorNodeIndex = -1;
            plan.activeIndex = -1;
            return;
        }

        if (plan.status[idx] != NodePlanState.STATUS_ACTIVE && plan.status[idx] != NodePlanState.STATUS_ARMED)
        {
            runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
            runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
            runtime.executorNodeIndex = -1;
            plan.activeIndex = -1;
            return;
        }

        plan.status[idx] = NodePlanState.STATUS_ACTIVE;
        plan.activeIndex = idx;

        Vector3 dvE = plan.dV_E[idx];
        float dvMag = dvE.magnitude;
        Vector3 burnDirE = (dvMag > 1e-6f) ? (dvE / dvMag) : nav.That_E;

        byte axisToPoint = ClampAxis012(plan.bodyAxisToPoint[idx]);

        double tExecNow = ComputeNodeTriggerTime(idx, nowT);

        float tBurnNow = 0f;
        float burnThrottle01 = 0f;
        if (plan.burnDurationSec != null && idx < plan.burnDurationSec.Length)
            tBurnNow = Mathf.Max(0f, plan.burnDurationSec[idx]);
        if (plan.burnThrottle01 != null && idx < plan.burnThrottle01.Length)
            burnThrottle01 = Mathf.Clamp01(plan.burnThrottle01[idx]);

        double tBurnStartNow = tExecNow - 0.5 * (double)tBurnNow;
        double tBurnEndNow = tExecNow + 0.5 * (double)tBurnNow;

        if (tBurnNow > 0f && tBurnStartNow < nowT)
        {
            tBurnStartNow = nowT;
            tBurnEndNow = tBurnStartNow + (double)tBurnNow;
        }

        float postHold = Mathf.Max(0f, plan.postHoldSec[idx]);

        switch (runtime.executorPhase)
        {
            case GC_RuntimeState.EXEC_PHASE_SLEW:
            {
                _execWritesAtt = true;
                _execAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _execPointDir_E = burnDirE;
                _execAxis = axisToPoint;

                if (tBurnNow > 0f)
                {
                    if (nowT >= _exec_tBurnStart)
                    {
                        runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_BURN;
                        runtime.executorStartTime = nowT;
                    }
                }
                else
                {
                    if (nowT >= _exec_tBurnStart)
                    {
                        runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_POST;
                        runtime.executorStartTime = nowT;
                    }
                }
                break;
            }

            case GC_RuntimeState.EXEC_PHASE_BURN:
            {
                _execWritesAtt = true;
                _execAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _execPointDir_E = burnDirE;
                _execAxis = axisToPoint;

                _execWritesThr = true;
                _execMainT = burnThrottle01;

                if (nowT >= _exec_tBurnEnd)
                {
                    runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_POST;
                    runtime.executorStartTime = nowT;
                }
                break;
            }

            case GC_RuntimeState.EXEC_PHASE_POST:
            {
                _execWritesAtt = true;
                _execAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _execPointDir_E = burnDirE;
                _execAxis = axisToPoint;

                _execWritesThr = true;
                _execMainT = 0f;

                if (postHold <= 0f || (nowT - runtime.executorStartTime) >= postHold)
                {
                    FinishExecutor(idx, nowT);
                }
                break;
            }

            default:
            {
                runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
                runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
                runtime.executorNodeIndex = -1;
                plan.activeIndex = -1;
                break;
            }
        }
    }

    private int FindBestNextNodeIndex(double nowT)
    {
        int best = -1;
        double bestT = 0.0;

        for (int i = 0; i < plan.maxNodes; i++)
        {
            if (plan.status[i] != NodePlanState.STATUS_ARMED) continue;

            double t = ComputeNodeTriggerTime(i, nowT);
            if (t <= nowT) continue;

            if (best < 0 || t < bestT)
            {
                best = i;
                bestT = t;
            }
        }
        return best;
    }

    private double ComputeNodeTriggerTime(int idx, double nowT)
    {
        if (plan.trigType[idx] == NodePlanState.TRIG_TIME)
            return plan.triggerTime[idx];

        double dt;
        bool ok = OrbitHelpers.TryTimeToTrueAnomaly(
            nav.a, nav.e, nav.muPrimary,
            nav.nuRad,
            plan.triggerNuRad[idx],
            eTol,
            out dt);

        if (!ok) return nowT;
        if (dt < 0.0) dt = 0.0;
        return nowT + dt;
    }

    private void BeginExecutorForNode(int idx, double nowT, double tExec, double tBurnStart, double tBurnEnd)
    {
        runtime.activeExecutorId = GC_RuntimeState.EXEC_NODE_SIMPLE;
        runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_SLEW;
        runtime.executorStartTime = nowT;
        runtime.executorNodeIndex = idx;

        runtime.cachedModeBeforeExec = runtime.activeModeId;

        _exec_tExec = tExec;
        _exec_tBurnStart = tBurnStart;
        _exec_tBurnEnd = tBurnEnd;

        plan.status[idx] = NodePlanState.STATUS_ACTIVE;
        plan.activeIndex = idx;
    }

    private void FinishExecutor(int idx, double nowT)
    {
        plan.status[idx] = NodePlanState.STATUS_DONE;
        plan.API_DeleteNode(idx);

        plan.activeIndex = -1;

        runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
        runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
        runtime.executorStartTime = nowT;
        runtime.executorNodeIndex = -1;

        if (runtime.resumeModeOnExecutorDone)
        {
            runtime.activeModeId = runtime.cachedModeBeforeExec;
            runtime.modeStartTime = nowT;
        }

        if (nodeNet != null) nodeNet.ForcePublish();
    }

    private void AbortExecutor(double nowT)
    {
        int idx = runtime.executorNodeIndex;

        if (idx >= 0 && idx < plan.maxNodes)
        {
            plan.status[idx] = NodePlanState.STATUS_ABORTED;
            plan.activeIndex = -1;
        }

        runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
        runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
        runtime.executorStartTime = nowT;
        runtime.executorNodeIndex = -1;

        runtime.lastAbortTime = nowT;
        runtime.status = GC_RuntimeState.STATUS_ABORTED;

        runtime.activeModeId = GC_RuntimeState.MODE_MANUAL;
        runtime.modeStartTime = nowT;
    }

    private bool TryGetMainCapability(out float totalThrustN, out float ispEffSec)
    {
        totalThrustN = 0f;
        ispEffSec = 0f;
        if (thrusters == null || thrusters.mainTf == null) return false;

        float sumT = 0f;
        float sumTIsp = 0f;

        int n = thrusters.mainTf.Length;
        for (int i = 0; i < n; i++)
        {
            if (thrusters.mainTf[i] == null) continue;

            float Ti = thrusters.GetMainMaxForceN(i);
            if (Ti <= 0f) continue;

            sumT += Ti;

            float Isp = thrusters.GetMainIspSec(i);
            if (Isp > 0f) sumTIsp += Ti * Isp;
        }

        if (sumT <= 0f) return false;

        totalThrustN = sumT;

        if (sumTIsp <= 0f) return false;

        ispEffSec = sumTIsp / sumT;
        return true;
    }

    private bool TryComputeBurnDurationSec(float dv_mps, out float tBurnSec)
    {
        tBurnSec = 0f;
        if (dv_mps <= 1e-3f) { tBurnSec = 0f; return true; }

        if (craft == null) return false;

        float TmaxN, IspSec;
        if (!TryGetMainCapability(out TmaxN, out IspSec)) return false;

        double m0 = craft.massKg;
        if (m0 <= 1.0) return false;

        const double g0 = 9.80665;
        double ve = (double)IspSec * g0;
        if (ve <= 1e-6) return false;

        double m1 = m0 / System.Math.Exp((double)dv_mps / ve);
        double mPropReq = m0 - m1;
        if (mPropReq < 0.0) mPropReq = 0.0;

        double mpAvail = craft.propMassKg;
        if (mpAvail < 0.0) mpAvail = 0.0;
        if (mPropReq > mpAvail) mPropReq = mpAvail;

        double mdot = (double)TmaxN / ve;
        if (mdot <= 1e-9) return false;

        double t = mPropReq / mdot;
        tBurnSec = (float)((t > 0.0) ? t : 0.0);
        return true;
    }

    public void ResetForScenario(double nowT)
    {
        _lastT = double.NaN;

        _exec_tExec = 0.0;
        _exec_tBurnStart = 0.0;
        _exec_tBurnEnd = 0.0;

        _manualWritesAtt = false;
        _manualWritesThr = false;
        _modeWritesAtt = false;
        _modeWritesThr = false;
        _execWritesAtt = false;
        _execWritesThr = false;

        _manualWritesXlat = false;
        _modeWritesXlat = false;
        _execWritesXlat = false;

        _manAttCmd = 0;
        _manTau_B = Vector3.zero;
        _manRate_B = Vector3.zero;
        _manQ_BE = Quaternion.identity;
        _manPointDir_E = Vector3.forward;
        _manAxis = 2;
        _manBlend = true;
        _manMainT = 0f;
        _manHoverT = 0f;
        _manActMode = CraftCommandState.ATT_ACT_AUTO;
        _manAllowWheels = true;
        _manAllowRCS = true;
        _manAllowGimbal = true;
        _manTranslate_B = Vector3.zero;
        _manRcsMode = CraftCommandState.RCS_MODE_BLENDED;

        _modeAttCmd = 0;
        _modeTau_B = Vector3.zero;
        _modeRate_B = Vector3.zero;
        _modeQ_BE = Quaternion.identity;
        _modePointDir_E = Vector3.forward;
        _modeAxis = 2;
        _modeBlend = true;
        _modeMainT = 0f;
        _modeHoverT = 0f;
        _modeActMode = CraftCommandState.ATT_ACT_AUTO;
        _modeAllowWheels = true;
        _modeAllowRCS = true;
        _modeAllowGimbal = true;
        _modeTranslate_B = Vector3.zero;
        _modeRcsMode = CraftCommandState.RCS_MODE_BLENDED;

        if (alerts != null)
        {
            alerts.ClearEvaluatedState();
            alerts.API_ResetAllAlertControls();
        }

        _execAttCmd = 0;
        _execTau_B = Vector3.zero;
        _execRate_B = Vector3.zero;
        _execQ_BE = Quaternion.identity;
        _execPointDir_E = Vector3.forward;
        _execAxis = 2;
        _execBlend = true;
        _execMainT = 0f;
        _execHoverT = 0f;
        _execActMode = CraftCommandState.ATT_ACT_AUTO;
        _execAllowWheels = true;
        _execAllowRCS = true;
        _execAllowGimbal = true;
        _execTranslate_B = Vector3.zero;
        _execRcsMode = CraftCommandState.RCS_MODE_BLENDED;

        if (intent != null)
            intent.ClearToSafeDefaults(craftDefaults);

        if (runtime != null)
            runtime.ResetState(nowT);

        if (plan != null)
            plan.ClearAll();

        if (modeParams != null)
        {
            modeParams.bodyAxisToPoint = defaultBodyAxisToPoint;
            modeParams.rtnDir = GC_ModeParams.RTN_T_PLUS;
            modeParams.qTarget_BE = Quaternion.identity;
            modeParams.pointDirTarget_E = Vector3.forward;
            modeParams.rateTarget_B = Vector3.zero;
            modeParams.tauDirect_B = Vector3.zero;
            modeParams.blendDirectTorqueWithPD = true;
            modeParams.selectedNodeIndex = 0;
        }
    }

    private void UpdateActiveProgramIndicator()
    {
        if (runtime.activeExecutorId != GC_RuntimeState.EXEC_NONE &&
            (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_SLEW ||
             runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
             runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST))
        {
            runtime.activeProgramId = GC_RuntimeState.PROG_EXEC_NODE;
            return;
        }

        switch (runtime.activeModeId)
        {
            case GC_RuntimeState.MODE_MANUAL:
                runtime.activeProgramId = GC_RuntimeState.PROG_MANUAL;
                return;

            case GC_RuntimeState.MODE_HOLD_QUAT:
                runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_ATT;
                return;

            case GC_RuntimeState.MODE_POINT_DIR_E:
                runtime.activeProgramId = GC_RuntimeState.PROG_POINT_DIR_E;
                return;

            case GC_RuntimeState.MODE_RATE_TARGET:
                runtime.activeProgramId = GC_RuntimeState.PROG_KILL_ROT;
                return;

            case GC_RuntimeState.MODE_HOLD_RTN_DIR:
            {
                if (modeParams == null) { runtime.activeProgramId = GC_RuntimeState.PROG_NONE; return; }
                switch (modeParams.rtnDir)
                {
                    case GC_ModeParams.RTN_T_PLUS:  runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_PROGRADE; return;
                    case GC_ModeParams.RTN_T_MINUS: runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_RETRO; return;
                    case GC_ModeParams.RTN_R_PLUS:  runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_RAD_OUT; return;
                    case GC_ModeParams.RTN_R_MINUS: runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_RAD_IN; return;
                    case GC_ModeParams.RTN_N_PLUS:  runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_NORMAL; return;
                    case GC_ModeParams.RTN_N_MINUS: runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_ANTINORM; return;
                    default:                        runtime.activeProgramId = GC_RuntimeState.PROG_NONE; return;
                }
            }

            case GC_RuntimeState.MODE_DIRECT_TORQUE:
                runtime.activeProgramId = GC_RuntimeState.PROG_MANUAL;
                return;

            case GC_RuntimeState.MODE_HOLD_HORIZON:
                runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_HORIZON;
                return;

            case GC_RuntimeState.MODE_POINT_NODE_VECTOR:
                runtime.activeProgramId = GC_RuntimeState.PROG_POINT_NODE_VECTOR;
                return;

            case GC_RuntimeState.MODE_RELVEL_PROGRADE:
                runtime.activeProgramId = GC_RuntimeState.PROG_RELVEL_PRO;
                return;

            case GC_RuntimeState.MODE_RELVEL_RETROGRADE:
                runtime.activeProgramId = GC_RuntimeState.PROG_RELVEL_RETRO;
                return;

            case GC_RuntimeState.MODE_DOCK_POINT_SHIPZ_TO_PORT:
                runtime.activeProgramId = GC_RuntimeState.PROG_DOCK_POINT_PORT;
                return;

            case GC_RuntimeState.MODE_DOCK_ALIGN_PORTS:
                runtime.activeProgramId = GC_RuntimeState.PROG_DOCK_ALIGN_PORTS;
                return;

            default:
                runtime.activeProgramId = GC_RuntimeState.PROG_NONE;
                return;
        }
    }

    // =====================================================================
    // Minimal API methods
    // =====================================================================

    public void API_SetModeManual()
    {
        runtime.activeModeId = GC_RuntimeState.MODE_MANUAL;
        runtime.modeStartTime = nav.t;
    }

    public void API_Attitude_HoldQuaternion(Quaternion qTargetBE)
    {
        modeParams.qTarget_BE = qTargetBE;
        runtime.activeModeId = GC_RuntimeState.MODE_HOLD_QUAT;
        runtime.modeStartTime = nav.t;
    }

    public void API_Attitude_PointDirE(Vector3 dirE, byte axis012)
    {
        modeParams.pointDirTarget_E = dirE;
        modeParams.bodyAxisToPoint = ClampAxis012(axis012);
        runtime.activeModeId = GC_RuntimeState.MODE_POINT_DIR_E;
        runtime.modeStartTime = nav.t;
    }

    public void API_Attitude_HoldRTN(byte rtnDir, byte axis012)
    {
        modeParams.rtnDir = rtnDir;
        modeParams.bodyAxisToPoint = ClampAxis012(axis012);
        runtime.activeModeId = GC_RuntimeState.MODE_HOLD_RTN_DIR;
        runtime.modeStartTime = nav.t;
    }

    public void API_Attitude_KillRot()
    {
        modeParams.rateTarget_B = Vector3.zero;
        runtime.activeModeId = GC_RuntimeState.MODE_RATE_TARGET;
        runtime.modeStartTime = nav.t;
    }

    [Header("API defaults")]
    public byte defaultBodyAxisToPoint = 2; // 0=+X, 1=+Y, 2=+Z

    public void API_HoldPrograde()
    {
        API_Attitude_HoldRTN(GC_ModeParams.RTN_T_PLUS, defaultBodyAxisToPoint);
    }

    public void API_HoldRetrograde()
    {
        API_Attitude_HoldRTN(GC_ModeParams.RTN_T_MINUS, defaultBodyAxisToPoint);
    }

    public void API_HoldRadialOut()
    {
        API_Attitude_HoldRTN(GC_ModeParams.RTN_R_PLUS, defaultBodyAxisToPoint);
    }

    public void API_HoldRadialIn()
    {
        API_Attitude_HoldRTN(GC_ModeParams.RTN_R_MINUS, defaultBodyAxisToPoint);
    }

    public void API_HoldNormal()
    {
        API_Attitude_HoldRTN(GC_ModeParams.RTN_N_PLUS, defaultBodyAxisToPoint);
    }

    public void API_HoldAntiNormal()
    {
        API_Attitude_HoldRTN(GC_ModeParams.RTN_N_MINUS, defaultBodyAxisToPoint);
    }

    public void API_Attitude_HoldCurrent()
    {
        if (craftAtt == null) return;
        API_Attitude_HoldQuaternion(craftAtt.qBE);
    }

    public void API_Attitude_HoldCurrentAndKillRot()
    {
        if (craftAtt == null) return;

        modeParams.qTarget_BE = craftAtt.qBE;
        runtime.activeModeId = GC_RuntimeState.MODE_HOLD_QUAT;
        runtime.modeStartTime = nav.t;
        modeParams.rateTarget_B = Vector3.zero;
    }

    public void API_Attitude_PointDirE_DefaultAxis(Vector3 dirE)
    {
        API_Attitude_PointDirE(dirE, defaultBodyAxisToPoint);
    }

    public void API_Attitude_HoldHorizon()
    {
        runtime.activeModeId = GC_RuntimeState.MODE_HOLD_HORIZON;
        runtime.modeStartTime = nav.t;
    }

    public bool API_Attitude_PointSelectedNodeVector(byte axis012)
    {
        if (plan == null || modeParams == null) return false;

        int idx;
        Vector3 dvE;
        float dvMag;
        float dvRemain;

        if (!TryGetSelectedNodeVector(out idx, out dvE, out dvMag, out dvRemain))
            return false;

        modeParams.bodyAxisToPoint = ClampAxis012(axis012);
        runtime.activeModeId = GC_RuntimeState.MODE_POINT_NODE_VECTOR;
        runtime.modeStartTime = nav.t;
        return true;
    }

    public void API_Node_Select(byte nodeIndex)
    {
        if (modeParams == null) return;
        modeParams.selectedNodeIndex = nodeIndex;
    }

    public void API_Node_SetAutoExecute(bool enabled)
    {
        if (runtime == null) return;
        runtime.autoExecuteArmedNodes = enabled;
    }

    public void API_Node_ToggleAutoExecute()
    {
        if (runtime == null) return;
        runtime.autoExecuteArmedNodes = !runtime.autoExecuteArmedNodes;
    }

    public byte defaultNodeAxisToPoint = 2;

    public int API_Node_CreateAtTime(Vector3 dvE, double execTime)
    {
        if (plan == null) return -1;

        int idx = plan.API_CreateNode_Time(dvE, execTime, ClampAxis012(defaultNodeAxisToPoint));
        if (idx < 0) return -1;

        float dv = dvE.magnitude;
        float tBurn;
        bool ok = TryComputeBurnDurationSec(dv, out tBurn);

        plan.burnDurationSec[idx] = ok ? tBurn : 0f;
        plan.burnThrottle01[idx] = ok ? 1.0f : 0f;

        if (nodeNet != null)
            nodeNet.ForcePublish();

        return idx;
    }

    public int API_Node_CreateAtTrueAnomaly(Vector3 dvE, double nuTargetRad)
    {
        if (plan == null) return -1;

        int idx = plan.API_CreateNode_TrueAnomaly(dvE, nuTargetRad, ClampAxis012(defaultNodeAxisToPoint));
        if (idx < 0) return -1;

        float dv = dvE.magnitude;
        float tBurn;
        bool ok = TryComputeBurnDurationSec(dv, out tBurn);

        plan.burnDurationSec[idx] = ok ? tBurn : 0f;
        plan.burnThrottle01[idx] = ok ? 1.0f : 0f;

        if (nodeNet != null)
            nodeNet.ForcePublish();

        return idx;
    }

    public void API_Node_ClearAll()
    {
        if (plan == null) return;
        plan.ClearAll();
        if (nodeNet != null)
            nodeNet.ForcePublish();
    }

    public void API_Node_Delete(int i)
    {
        if (plan == null) return;
        plan.API_DeleteNode(i);
        if (nodeNet != null)
            nodeNet.ForcePublish();
    }

    // =====================================================================
    // Docking Helper APIs
    // =====================================================================

    public bool API_Dock_PointShipZAtTargetPort()
    {
        if (contacts == null || !contacts.dockValid0) return false;
        runtime.activeModeId = GC_RuntimeState.MODE_DOCK_POINT_SHIPZ_TO_PORT;
        runtime.modeStartTime = nav.t;
        return true;
    }

    public bool API_Dock_AlignPorts()
    {
        if (contacts == null || !contacts.dockValid0) return false;
        runtime.activeModeId = GC_RuntimeState.MODE_DOCK_ALIGN_PORTS;
        runtime.modeStartTime = nav.t;
        return true;
    }

    public bool API_Relative_KillVel_SelectedStation()
    {
        if (contacts == null) return false;
        if (!contacts.fullValid0) return false;

        runtime.activeTranslateModeId = GC_RuntimeState.XLAT_KILL_RELVEL;
        runtime.lastNonManualTranslateModeId = runtime.activeTranslateModeId;
        return true;
    }

    public void API_Relative_StopTranslationAssist()
    {
        runtime.activeTranslateModeId = GC_RuntimeState.XLAT_MANUAL;
    }

    public void API_Relative_ToggleKillVel()
    {
        if (runtime.activeTranslateModeId == GC_RuntimeState.XLAT_KILL_RELVEL)
            runtime.activeTranslateModeId = GC_RuntimeState.XLAT_MANUAL;
        else if (contacts != null && contacts.fullValid0)
            runtime.activeTranslateModeId = GC_RuntimeState.XLAT_KILL_RELVEL;
    }

    public bool API_Attitude_PointAlongRelVel(byte axis012)
    {
        if (contacts == null || !contacts.fullValid0) return false;

        modeParams.bodyAxisToPoint = ClampAxis012(axis012);
        runtime.activeModeId = GC_RuntimeState.MODE_RELVEL_PROGRADE;
        runtime.modeStartTime = nav.t;
        return true;
    }

    public bool API_Attitude_PointAgainstRelVel(byte axis012)
    {
        if (contacts == null || !contacts.fullValid0) return false;

        modeParams.bodyAxisToPoint = ClampAxis012(axis012);
        runtime.activeModeId = GC_RuntimeState.MODE_RELVEL_RETROGRADE;
        runtime.modeStartTime = nav.t;
        return true;
    }
}