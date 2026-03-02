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

    [Header("State Containers (owned by GC_Core)")]
    public GuidanceNavCoreState nav;
    public GC_RuntimeState runtime;
    public GC_ModeParams modeParams;
    public GC_ManualDraft manual;
    public GC_ActuatorOverrideState overrides;
    public NodePlanState plan;

    [Header("Output Register")]
    public GuidanceCommandIntentState intent;

    [Header("Optional: craft defaults for intent.ClearToSafeDefaults")]
    public CraftCommandState craftDefaults;

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

    // Exec draft payload (reserved; inactive V1)
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

    // Mode translation payload (unused for now)
    private bool _modeWritesXlat;
    private Vector3 _modeTranslate_B;
    private byte _modeRcsMode;

    // Exec translation payload (unused for now)
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
        // Validate critical references (fail safe: do nothing but keep outputs safe).
        if (intent == null || nav == null || runtime == null || modeParams == null || manual == null) return;
        if (craft == null || craftAtt == null || bodies == null || ephem == null) return;

        // 1) Clear output register to safe baseline.
        //    Ensures no stale commands persist if any early-out happens.
        intent.ClearToSafeDefaults(craftDefaults);
        intent.commandSource = 1;

        // 2) Build nav snapshot (deterministic, once per tick).
        BuildNavCoreSnapshot();

        // 3) Acquire manual inputs (routed through guidance) -> manual draft.
        BuildManualDraftFromManualState();

        ApplyManualAutoModeSwitch();

        // 4) Step executor slot (reserved; inactive in V1).
        // BuildExecutorDraft_None();
        BuildExecutorDraft_FromPlan();

        // 5) Step active continuous mode -> mode draft.
        BuildModeDraft();

        // 6) Two-channel arbitration + write final intent.
        ArbitrateAndWriteIntent();

        UpdateActiveProgramIndicator();
    }

    // =====================================================================
    // 2) NAV SNAPSHOT (uses OrbitHelpers from project)
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

        // RTN basis (OrbitHelpers)
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

        // Conic + invariants (OrbitHelpers)
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

    // =====================================================================
    // 3) MANUAL INPUT ACQUISITION -> manual draft (human inputs only)
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

        // Manual does not command these targets in V1; set safe defaults
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
    // 4) EXECUTOR SLOT (future) -> exec draft (inactive V1)
    // =====================================================================

    private void BuildExecutorDraft_None()
    {
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

        _execActMode = CraftCommandState.ATT_ACT_AUTO;
        _execAllowWheels = true;
        _execAllowRCS = true;
        _execAllowGimbal = true;
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
                // Mode writes nothing; manual wins by arbitration.
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
        }

        // V1 choice: throttle is manual-owned unless an executor later overrides it.
    }

    private static Vector3 ResolveRtnDirection(GuidanceNavCoreState nav, byte rtnDir)
    {
        Vector3 R = nav.Rhat_E;
        Vector3 T = nav.That_E;
        Vector3 N = nav.Nhat_E;

        switch (rtnDir)
        {
            default:
            case GC_ModeParams.RTN_T_PLUS:  return T;   // prograde
            case GC_ModeParams.RTN_T_MINUS: return -T;  // retrograde
            case GC_ModeParams.RTN_R_PLUS:  return R;   // radial out
            case GC_ModeParams.RTN_R_MINUS: return -R;  // radial in
            case GC_ModeParams.RTN_N_PLUS:  return N;   // normal
            case GC_ModeParams.RTN_N_MINUS: return -N;  // anti-normal
        }
    }

    private static byte ClampAxis012(byte axis)
    {
        return (axis > 2) ? (byte)2 : axis;
    }

    // =====================================================================
    // 6) ARBITRATION + FINAL INTENT WRITE
    // =====================================================================

    private void ArbitrateAndWriteIntent()
    {
        // SAFETY hook (V1 minimal): fault => throttle cut
        bool safetyCutThrottle = (runtime.status == GC_RuntimeState.STATUS_FAULT);

        // --------------------
        // ATTITUDE CHANNEL: EXECUTOR > MODE > MANUAL
        // --------------------
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

        ApplyActuatorOverrides();


        // --------------------
        // TRANSLATION CHANNEL: EXECUTOR > MODE > MANUAL
        // --------------------
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


        // --------------------
        // THROTTLE CHANNEL: SAFETY > EXECUTOR > MODE > MANUAL
        // --------------------
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

        // allowWheels
        if (overrides.overrideAllowWheels == GC_ActuatorOverrideState.FORCE_DISABLE) intent.allowWheels = false;
        else if (overrides.overrideAllowWheels == GC_ActuatorOverrideState.FORCE_ENABLE) intent.allowWheels = true;

        // allowRCS
        if (overrides.overrideAllowRCS == GC_ActuatorOverrideState.FORCE_DISABLE) intent.allowRCS = false;
        else if (overrides.overrideAllowRCS == GC_ActuatorOverrideState.FORCE_ENABLE) intent.allowRCS = true;

        // allowGimbal
        if (overrides.overrideAllowGimbal == GC_ActuatorOverrideState.FORCE_DISABLE) intent.allowGimbal = false;
        else if (overrides.overrideAllowGimbal == GC_ActuatorOverrideState.FORCE_ENABLE) intent.allowGimbal = true;

        // actuator selection mode override (0 means no override)
        if (overrides.overrideAttitudeActuatorMode != 0)
            intent.attitudeActuatorMode = overrides.overrideAttitudeActuatorMode;
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

        // Throttle "active" meaning non-trivial command (you can switch to "delta from last" later)
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
        // later: translation activity
        // if (manual.translateCmd_B.sqrMagnitude > dz*dz) return true;
        return false;
    }

    private void ApplyManualAutoModeSwitch()
    {
        if (!runtime.autoSwitchToManualOnInput) return;

        // Don't auto-switch modes while executor is actively steering (slew/burn/post)
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

        // If not active: in latched mode, do nothing.
        if (runtime.latchManualTakeover) return;

        // Momentary mode: return to previous mode after timeout
        double dtSince = nav.t - runtime.lastManualInputTime;
        if (dtSince >= runtime.manualReleaseTimeoutSec)
        {
            if (runtime.activeModeId == GC_RuntimeState.MODE_MANUAL)
            {
                // Restore only if we have something sensible to restore
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

        // Manual intervention during active phases (SLEW/BURN/POST)
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

        // If not running a node executor, ensure sensible idle
        if (runtime.activeExecutorId == GC_RuntimeState.EXEC_NONE ||
            runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_NONE)
        {
            runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
            runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
            runtime.executorNodeIndex = -1;
            plan.activeIndex = -1;
        }

        // WAIT: find next armed node and decide when to start slewing (relative to burn start)
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

            // Node "execution time" (center time)
            double tExec = ComputeNodeTriggerTime(next, nowT);

            // Burn parameters (precomputed at creation)
            float tBurn = 0f;
            if (plan.burnDurationSec != null && next >= 0 && next < plan.burnDurationSec.Length)
                tBurn = Mathf.Max(0f, plan.burnDurationSec[next]);

            // Burn window (straddle around tExec)
            double tBurnStart = tExec - 0.5 * (double)tBurn;
            double tBurnEnd   = tExec + 0.5 * (double)tBurn;

            // Clamp: if burn start already passed, start now and keep full duration
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
                // Not time yet; executor idle
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

        // If node got edited/deleted/disarmed, bail
        if (plan.status[idx] != NodePlanState.STATUS_ACTIVE && plan.status[idx] != NodePlanState.STATUS_ARMED)
        {
            runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
            runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
            runtime.executorNodeIndex = -1;
            plan.activeIndex = -1;
            return;
        }

        // Mark active
        plan.status[idx] = NodePlanState.STATUS_ACTIVE;
        plan.activeIndex = idx;

        // Burn direction
        Vector3 dvE = plan.dV_E[idx];
        float dvMag = dvE.magnitude;
        Vector3 burnDirE = (dvMag > 1e-6f) ? (dvE / dvMag) : nav.That_E;

        byte axisToPoint = ClampAxis012(plan.bodyAxisToPoint[idx]);

        // Node "execution time" (center time)
        double tExecNow = ComputeNodeTriggerTime(idx, nowT);

        // Burn params
        float tBurnNow = 0f;
        float burnThrottle01 = 0f;
        if (plan.burnDurationSec != null && idx < plan.burnDurationSec.Length)
            tBurnNow = Mathf.Max(0f, plan.burnDurationSec[idx]);
        if (plan.burnThrottle01 != null && idx < plan.burnThrottle01.Length)
            burnThrottle01 = Mathf.Clamp01(plan.burnThrottle01[idx]);

        // Burn window (straddle)
        double tBurnStartNow = tExecNow - 0.5 * (double)tBurnNow;
        double tBurnEndNow   = tExecNow + 0.5 * (double)tBurnNow;

        // Clamp: if burn start already passed, start now and keep full duration
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
                // Point along burn direction
                _execWritesAtt = true;
                _execAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _execPointDir_E = burnDirE;
                _execAxis = axisToPoint;

                // Enter BURN at burn start (if burn duration is nonzero), otherwise go straight to POST at exec time
                if (tBurnNow > 0f)
                {
                    if (nowT >= _exec_tBurnStart)
                    {
                        runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_BURN;
                        runtime.executorStartTime = nowT; // used as phase timer if needed
                    }
                }
                else
                {
                    // No burn -> treat as "attitude-only node": start POST at exec time
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
                // Keep pointing during burn
                _execWritesAtt = true;
                _execAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _execPointDir_E = burnDirE;
                _execAxis = axisToPoint;

                // Command mains throttle during burn
                _execWritesThr = true;
                _execMainT = burnThrottle01;

                // Transition to POST at burn end
                if (nowT >= _exec_tBurnEnd)
                {
                    runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_POST;
                    runtime.executorStartTime = nowT;
                }
                break;
            }

            case GC_RuntimeState.EXEC_PHASE_POST:
            {
                // Hold pointing through post window, throttle off
                _execWritesAtt = true;
                _execAttCmd = CraftCommandState.ATT_CMD_POINT_VECTOR;
                _execPointDir_E = burnDirE;
                _execAxis = axisToPoint;

                _execWritesThr = true;
                _execMainT = 0f;

                if (postHold <= 0f || (nowT - runtime.executorStartTime) >= postHold)
                {
                    FinishExecutor(idx, nowT); // you'll modify FinishExecutor to delete node automatically
                }
                break;
            }

            default:
            {
                // Unexpected -> recover to WAIT
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
        // Choose the ARMED node with the smallest computed trigger time.
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

        // True anomaly scheduling: compute dt using OrbitHelpers helper.
        double dt;
        bool ok = OrbitHelpers.TryTimeToTrueAnomaly(
            nav.a, nav.e, nav.muPrimary,
            nav.nuRad,
            plan.triggerNuRad[idx],
            eTol,
            out dt);

        if (!ok) return nowT;   // treat as "immediate" if we can't compute
        if (dt < 0.0) dt = 0.0; // forward-only behavior
        return nowT + dt;
    }

    private void BeginExecutorForNode(int idx, double nowT, double tExec, double tBurnStart, double tBurnEnd)
    {
        runtime.activeExecutorId = GC_RuntimeState.EXEC_NODE_SIMPLE;
        runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_SLEW;
        runtime.executorStartTime = nowT;
        runtime.executorNodeIndex = idx;

        // Cache continuous mode to resume afterward
        runtime.cachedModeBeforeExec = runtime.activeModeId;

        // Freeze schedule for this execution
        _exec_tExec = tExec;
        _exec_tBurnStart = tBurnStart;
        _exec_tBurnEnd = tBurnEnd;

        // Mark active
        plan.status[idx] = NodePlanState.STATUS_ACTIVE;
        plan.activeIndex = idx;
    }

    private void FinishExecutor(int idx, double nowT)
    {
        // Mark done
        plan.status[idx] = NodePlanState.STATUS_DONE;
        plan.API_DeleteNode(idx);   // frees slot immediately

        plan.activeIndex = -1;

        // Clear executor
        runtime.activeExecutorId = GC_RuntimeState.EXEC_NONE;
        runtime.executorPhase = GC_RuntimeState.EXEC_PHASE_WAIT;
        runtime.executorStartTime = nowT;
        runtime.executorNodeIndex = -1;

        // Resume previous continuous mode if configured
        if (runtime.resumeModeOnExecutorDone)
        {
            runtime.activeModeId = runtime.cachedModeBeforeExec;
            runtime.modeStartTime = nowT;
        }
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

        // Per earlier policy: abort -> MANUAL
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

        // If no Isp provided, we can’t compute proper duration; treat as failure
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

        // rocket eq
        double m1 = m0 / System.Math.Exp((double)dv_mps / ve);
        double mPropReq = m0 - m1;
        if (mPropReq < 0.0) mPropReq = 0.0;

        // clamp to available prop
        double mpAvail = craft.propMassKg;
        if (mpAvail < 0.0) mpAvail = 0.0;
        if (mPropReq > mpAvail) mPropReq = mpAvail;

        double mdot = (double)TmaxN / ve; // throttle=1
        if (mdot <= 1e-9) return false;

        double t = mPropReq / mdot;
        tBurnSec = (float)((t > 0.0) ? t : 0.0);
        return true;
    }

    private void UpdateActiveProgramIndicator()
    {
        // Executor has priority for "what are we doing"
        if (runtime.activeExecutorId != GC_RuntimeState.EXEC_NONE &&
            (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_SLEW ||
            runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
            runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST))
        {
            runtime.activeProgramId = GC_RuntimeState.PROG_EXEC_NODE;
            return;
        }

        // Otherwise reflect the continuous mode
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
                // Optional: detect kill-rot specifically (zero target rate)
                if (modeParams != null && modeParams.rateTarget_B.sqrMagnitude < 1e-10f)
                    runtime.activeProgramId = GC_RuntimeState.PROG_KILL_ROT;
                else
                    runtime.activeProgramId = GC_RuntimeState.PROG_KILL_ROT; // or define PROG_RATE_HOLD
                return;

            case GC_RuntimeState.MODE_HOLD_RTN_DIR:
            {
                if (modeParams == null) { runtime.activeProgramId = GC_RuntimeState.PROG_NONE; return; }
                switch (modeParams.rtnDir)
                {
                    case GC_ModeParams.RTN_T_PLUS:  runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_PROGRADE; return;
                    case GC_ModeParams.RTN_T_MINUS: runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_RETRO;    return;
                    case GC_ModeParams.RTN_R_PLUS:  runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_RAD_OUT;  return;
                    case GC_ModeParams.RTN_R_MINUS: runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_RAD_IN;   return;
                    case GC_ModeParams.RTN_N_PLUS:  runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_NORMAL;   return;
                    case GC_ModeParams.RTN_N_MINUS: runtime.activeProgramId = GC_RuntimeState.PROG_HOLD_ANTINORM; return;
                    default:                        runtime.activeProgramId = GC_RuntimeState.PROG_NONE;         return;
                }
            }

            case GC_RuntimeState.MODE_DIRECT_TORQUE:
                runtime.activeProgramId = GC_RuntimeState.PROG_MANUAL; // or add PROG_DIRECT_TORQUE
                return;

            default:
                runtime.activeProgramId = GC_RuntimeState.PROG_NONE;
                return;
        }
    }


    // =====================================================================
    // Minimal API methods (optional convenience)
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



    // =====================================================================
    // Thin RTN convenience APIs (UI-friendly)
    // - These do NOT change internals; they just set modeParams + runtime mode.
    // - Default axis is +Z (2) unless you prefer +X (0) for "nose".
    // =====================================================================

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


    // Hold whatever attitude the craft currently has (captures qBE and switches mode).
    public void API_Attitude_HoldCurrent()
    {
        if (craftAtt == null) return;
        API_Attitude_HoldQuaternion(craftAtt.qBE);
    }

    // Optional: hold current attitude but also immediately command zero body rate (helps settle)
    public void API_Attitude_HoldCurrentAndKillRot()
    {
        if (craftAtt == null) return;

        // Capture attitude target
        modeParams.qTarget_BE = craftAtt.qBE;
        runtime.activeModeId = GC_RuntimeState.MODE_HOLD_QUAT;
        runtime.modeStartTime = nav.t;

        // Also set rate target to zero as a separate mode param for future use.
        // (This does not change the selected mode; it just ensures any blending logic has a sane default.)
        modeParams.rateTarget_B = Vector3.zero;
    }

    public void API_Attitude_PointDirE_DefaultAxis(Vector3 dirE)
    {
        API_Attitude_PointDirE(dirE, defaultBodyAxisToPoint);
    }


    public byte defaultNodeAxisToPoint = 2; // set in inspector if desired

    public int API_Node_CreateAtTime(Vector3 dvE, double execTime)
    {
        if (plan == null) return -1;

        int idx = plan.API_CreateNode_Time(dvE, execTime, ClampAxis012(defaultNodeAxisToPoint));
        if (idx < 0) return -1;

        float dv = dvE.magnitude;
        float tBurn;
        bool ok = TryComputeBurnDurationSec(dv, out tBurn);

        plan.burnDurationSec[idx] = ok ? tBurn : 0f;
        plan.burnThrottle01[idx] = ok ? 1.0f : 0f; // if can’t compute, burn disabled

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

        return idx;
    }

    public void API_Node_ClearAll()
    {
        if (plan == null) return;
        plan.ClearAll();
    }

    public void API_Node_Delete(int i)
    {
        if (plan == null) return;
        plan.API_DeleteNode(i);
    }




}