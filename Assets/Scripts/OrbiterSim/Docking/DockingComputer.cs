using UdonSharp;
using UnityEngine;
using System;
using VRC.SDKBase;
/// <summary>
/// DockingComputer (Latch-driven V1)
///
/// Uses your friend's latch system (DockingController/DockingPort/DockingLatch) for detection,
/// but uses YOUR solver state as motion authority.
///
/// Flow:
/// - When DockingController reports "latched" (SoftCapture), we:
///     * freeze pairing (stationIndex, stationPortIndex, craftPortIndex)
///     * capture current relative pose in station body frame (relPos_SB, qCraftToStation)
///     * compute deterministic hard target pose from your cached port frames
///     * set DockingRuntimeState active + phase=SOFT
/// - While active:
///     * SOFT: hold pose + match station kinematics (stops drift)
///     * RETRACT: retractS drives interpolation toward hard target
///     * HARD: weld to hard target
///
/// IMPORTANT:
/// - This writes craft state/attitude directly.
/// - SimManager should bypass free-flight integration/actuation while dock.active.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingComputer : UdonSharpBehaviour
{
    [Header("Refs")]
    public DockingRuntimeState dock;
    public SimManager simManager;
    [Tooltip("Friend docking controller attached to the craft (or active craft proxy).")]
    public DockingController dockCtrl;

    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;

    public StationStateModel[] stations;

    [Tooltip("Your craft port cache (relative to craft CG in craft body frame).")]
    public CraftDockingPorts craftPorts;

    [Header("Policy")]
    public bool autoAdvance = true;
    public bool flipPortForward = false;

    [Header("Outputs (read by SimManager/GC)")]
    [Tooltip("Set true for one frame when capture occurs; SimManager should switch craft to MODE_DOCKED.")]
    public bool requestEnterDocked = false;

    [Tooltip("If true, other systems (GC, manual controls) should be disabled.")]
    public bool suggestKillControls = false;

    [Header("Undock request")]
    [Tooltip("Set true for one frame when undock has released the craft and SimManager should switch to integrated.")]
    public bool requestLeaveDockedToRails = false;
    public bool requestUndock = false;
    [Header("Undock")]
    [Tooltip("Extra separation distance applied instantly on release, along station port outward axis (meters).")]
    public float undockSeparationMeters = 0.01f;

    [Tooltip("Initial separation speed applied on release, along station port outward axis (m/s).")]
    public float undockSeparationSpeedMps = 0.10f;

    [Tooltip("Block fresh docking capture for a short time after undock (seconds).")]
    public float recaptureBlockSeconds = 10.0f;
    

    [Header("Networking (optional but recommended)")]
    public CraftNetState netCore;   // to publish dock snapshot
    public SimClock clock;          // to stamp capture/retract times consistently

    [Header("Debug")]
    public bool log = false;

    private double _recaptureBlockedUntil = -1.0;

    private byte _dbgLastOwnerPhase = 255;
    private byte _dbgLastRemoteNetPhase = 255;
    private byte _dbgLastRemotePresentedPhase = 255;
    private int _dbgLastRetractBucket = -999;

    private bool HasSimAuthority()
    {
        if (simManager != null) return simManager.IsSimOwner();
        return Networking.IsOwner(gameObject);
    }

    // --------------------
    // Public entry points
    // --------------------

    /// <summary>
    /// Call this in Update AFTER latch triggers have updated (normal Unity order is fine).
    /// This does not move the craft; it only enters capture state when latches say so.
    /// </summary>
    public void EvaluateLatchAndStart(double tNow)
    {
        if (!HasSimAuthority()) return;        
        requestEnterDocked = false;
        suggestKillControls = false;

        if (tNow < _recaptureBlockedUntil) return;

        if (dock == null || dockCtrl == null || craft == null || craftAtt == null || stations == null) return;

        // Already active => GC should disable controls
        if (dock.active)
        {
            suggestKillControls = true;
            return;
        }

        // Gate: friend controller must be in SoftCapture or HardCapture
        if (dockCtrl.state == DockingState.Ready) return;
        if (dockCtrl.localPort == null) return;

        // Must be fully latched per friend logic
        if (!dockCtrl.localPort.IsFullyLatched()) return;

        DockingPort targetPort = dockCtrl.activeTargetPort;
        if (targetPort == null) return;

        // Resolve indices via DockingPortMeta tags
        // Resolve indices via DockingPortMeta tags
        DockingPortMeta localMeta = dockCtrl.localPort.GetComponent<DockingPortMeta>();
        DockingPortMeta targMeta  = targetPort.GetComponent<DockingPortMeta>();

        if (localMeta == null || targMeta == null) return;

        // Local must be craft port
        if (localMeta.isStationPort) return;

        // Target must be station port
        if (!targMeta.isStationPort) return;

        int stIdx = targMeta.stationIndex;
        int sPort = targMeta.stationPortIndex;
        int cPort = localMeta.craftPortIndex;

        if (stIdx < 0 || stIdx >= stations.Length) return;
        StationStateModel st = stations[stIdx];
        if (st == null || !st.valid) return;

        // Freeze pairing
        dock.dockedStationIndex = stIdx;
        dock.stationPortIndex = sPort;
        dock.craftPortIndex = cPort;
        dock.captureTime = tNow;

        // Capture relative pose
        CaptureCurrentRelativePose(st);

        // Compute hard target pose from cached port frames
        ComputeHardTargetRelativePose(st);

        ApplyDockedKinematics(st, dock.relPos_SB, dock.qCraftToStation);


        // Publish dock snapshot (phase + indices + capture timing + captured pose).
        if (netCore != null)
        {
            byte primary = st.primaryBodyId;

            // Use tNow passed in (already mission time). If you prefer, you can use clock.Now() here.
            netCore.SetDocked(
                stIdx,
                (byte)sPort,
                (byte)cPort,
                DockingRuntimeState.DOCK_SOFT,
                tNow,          // captureT0
                0.0,           // retractT0 not started
                dock.relPos_SB,
                dock.qCraftToStation,
                primary,
                true
            );
        }


        dock.phase = DockingRuntimeState.DOCK_SOFT;
        dock.active = true;
        dock.retractS = 0f;

        suggestKillControls = true;
        requestEnterDocked = true;

        if (log) Debug.Log($"[Docking] CAPTURE -> SOFT  station={stIdx} sPort={sPort} cPort={cPort} ctrlState={dockCtrl.state}");
    }



    /// <summary>
    /// Call this in FixedUpdate (owner integrated stepping).
    /// While dock.active, this writes craft state/attitude deterministically.
    /// </summary>
    public void EvaluateDocked(float dt, double tNow)
    {
        if (!HasSimAuthority()) return;        
        requestEnterDocked = false;
        suggestKillControls = false;

        if (dock == null || craft == null || craftAtt == null || stations == null) return;
        if (!dock.active || dock.phase == DockingRuntimeState.DOCK_NONE) return;

        int stIdx = dock.dockedStationIndex;
        if (stIdx < 0 || stIdx >= stations.Length) { ForceUndock(); return; }

        StationStateModel st = stations[stIdx];
        if (st == null || !st.valid) { ForceUndock(); return; }


        if (dock.phase != _dbgLastOwnerPhase)
        {
            Debug.Log(
                "[Docking][OWNER] phase=" + DockPhaseName(dock.phase) +
                " active=" + dock.active +
                " retractCmd=" + dock.retractCommanded +
                " retractS=" + dock.retractS.ToString("F3") +
                " tNow=" + tNow.ToString("F3") +
                " station=" + dock.dockedStationIndex +
                " sport=" + dock.stationPortIndex +
                " cport=" + dock.craftPortIndex
            );
            _dbgLastOwnerPhase = dock.phase;
        }


        suggestKillControls = true;

        // Manual retract path:
        // Stay in DOCK_SOFT until commanded.
        if (dock.phase == DockingRuntimeState.DOCK_SOFT && dock.retractCommanded)
        {
            dock.phase = DockingRuntimeState.DOCK_RETRACT;
            dock.retractS = 0f;
            dock.retractCommanded = false;

            if (netCore != null)
            {
                byte primary = st.primaryBodyId;

                netCore.SetDocked(
                    dock.dockedStationIndex,
                    (byte)dock.stationPortIndex,
                    (byte)dock.craftPortIndex,
                    DockingRuntimeState.DOCK_RETRACT,
                    dock.captureTime,
                    tNow, // retract start time
                    dock.relPos_SB,
                    dock.qCraftToStation,
                    primary,
                    true
                );
            }

            if (log) Debug.Log("[Docking] SOFT -> RETRACT (manual command).");
        }

        if (dock.phase == DockingRuntimeState.DOCK_SOFT)
        {
            // Hold captured pose, but enforce consistent kinematics (prevents drift)
            ApplyDockedKinematics(st, dock.relPos_SB, dock.qCraftToStation);
            return;
        }

        if (dock.phase == DockingRuntimeState.DOCK_RETRACT)
        {
            dock.retractS = Mathf.MoveTowards(dock.retractS, 1f, dock.retractSpeed * dt);

            Vector3 relPos = Vector3.Lerp(dock.relPos_SB, dock.targetRelPos_SB, dock.retractS);
            Quaternion qCtoS = Quaternion.Slerp(dock.qCraftToStation, dock.target_qCraftToStation, dock.retractS);

            ApplyDockedKinematics(st, relPos, qCtoS);

            // Hard once retract complete + very near target
            if (IsNearHardTarget())
            {
                dock.phase = DockingRuntimeState.DOCK_HARD;

                if (netCore != null)
                {
                    byte primary = st.primaryBodyId;
                    netCore.SetDocked(
                        dock.dockedStationIndex,
                        (byte)dock.stationPortIndex,
                        (byte)dock.craftPortIndex,
                        DockingRuntimeState.DOCK_HARD,
                        dock.captureTime,
                        (netCore.dockRetractT0 > 0.0 ? netCore.dockRetractT0 : tNow),
                        dock.targetRelPos_SB,
                        dock.target_qCraftToStation,
                        primary,
                        true
                    );
                }

                if (log) Debug.Log("[Docking] RETRACT -> HARD.");
            }

            return;
        }

        if (dock.phase == DockingRuntimeState.DOCK_HARD)
        {
            ApplyDockedKinematics(st, dock.targetRelPos_SB, dock.target_qCraftToStation);
            return;
        }
    }

    public void ForceUndock()
    {
        if (dock == null) return;
        if (log) Debug.Log("[Docking] ForceUndock.");
        dock.ResetState();
    }

    // --------------------
    // Core math
    // --------------------

    private void CaptureCurrentRelativePose(StationStateModel st)
    {
        // dr_E = craft - station (SSB inertial)
        Vector3 drE = new Vector3(
            (float)(craft.rx - st.rx),
            (float)(craft.ry - st.ry),
            (float)(craft.rz - st.rz)
        );

        Quaternion qS = st.q_B2E;              // station BODY -> E
        Quaternion qEtoS = Quaternion.Inverse(qS);

        dock.relPos_SB = qEtoS * drE;          // station body axes
        dock.qCraftToStation = qEtoS * craftAtt.qBE;  // craft BODY -> station BODY
    }

    public void ComputeHardTargetRelativePose(StationStateModel st)
    {
        if (craftPorts == null) return;

        int sPort = dock.stationPortIndex;
        int cPort = dock.craftPortIndex;

        // These fields must exist in your StationStateModel and CraftDockingPorts.
        // If your actual names differ, we’ll rename to match your project.
        if (sPort < 0 || sPort >= st.dockingPortCount) return;
        if (cPort < 0 || cPort >= craftPorts.dockingPortCount) return;

        // Station port pose in station BODY
        Vector3 pS_SB = new Vector3(
            (float)st.dock_px_B[sPort],
            (float)st.dock_py_B[sPort],
            (float)st.dock_pz_B[sPort]
        );
        Quaternion qS_SB = st.dock_q_B[sPort];

        // Craft port pose in craft BODY
        Vector3 pC_B = new Vector3(
            (float)craftPorts.dock_px_B[cPort],
            (float)craftPorts.dock_py_B[cPort],
            (float)craftPorts.dock_pz_B[cPort]
        );
        Quaternion qC_B = craftPorts.dock_q_B[cPort];

        // Desired craft BODY -> station BODY at hard dock:
        // qCraftToStation_target = qS_SB * qMate * inv(qC_B)

        Quaternion qMate = flipPortForward
            ? Quaternion.AngleAxis(180f, Vector3.up)
            : Quaternion.identity;        
            
        dock.target_qCraftToStation = qS_SB * dock.GetQMate() * Quaternion.Inverse(qC_B);

        // Desired craft CG position in station body:
        // relPos_SB_target = pS_SB - (qCraftToStation_target * pC_B)
        dock.targetRelPos_SB = pS_SB - (dock.target_qCraftToStation * pC_B);
    }

    private bool IsNearHardTarget()
    {
        Vector3 dp = dock.targetRelPos_SB - dock.relPos_SB;
        float posErr = dp.magnitude;

        Quaternion qErr = Quaternion.Inverse(dock.qCraftToStation) * dock.target_qCraftToStation;
        float angErr = Quaternion.Angle(Quaternion.identity, qErr);

        return (posErr <= (float)dock.hardCapturePosTolM) && (angErr <= dock.hardCaptureAngTolDeg);
    }

    private void ApplyDockedKinematics(StationStateModel st, Vector3 relPos_SB, Quaternion qCraftToStation)
    {
        // Station attitude
        Quaternion qS = st.q_B2E; // station BODY -> E

        // Relative position in inertial, still small enough for float math
        Vector3 relPos_E = qS * relPos_SB;

        // Station angular rate (E)
        Vector3 wS_E = ComputeStationOmegaE(st);

        // Relative velocity contribution from station frame rotation
        Vector3 relVel_E = Vector3.Cross(wS_E, relPos_E);

        // Craft inertial attitude:
        // qCraft_E = qStation_E * inv(qCraftToStation)
        Quaternion qC_E = qS * qCraftToStation;

        // --- Write translational state in DOUBLE, using station doubles directly ---
        craft.rx = st.rx + (double)relPos_E.x;
        craft.ry = st.ry + (double)relPos_E.y;
        craft.rz = st.rz + (double)relPos_E.z;

        craft.vx = st.vx + (double)relVel_E.x;
        craft.vy = st.vy + (double)relVel_E.y;
        craft.vz = st.vz + (double)relVel_E.z;

        craft.primaryBodyId = st.primaryBodyId;

        // Write craft attitude
        craftAtt.qBE = qC_E;

        // Write craft body rates to match station frame rate
        Quaternion qEB = Quaternion.Inverse(qC_E);
        Vector3 wC_B = qEB * wS_E;
        craftAtt.wx = (double)wC_B.x;
        craftAtt.wy = (double)wC_B.y;
        craftAtt.wz = (double)wC_B.z;

        // Persist current relative pose
        dock.relPos_SB = relPos_SB;
        dock.qCraftToStation = qCraftToStation;
    }

    private Vector3 ComputeStationOmegaE(StationStateModel st)
    {
        // Fixed inertial => zero spin
        if (st.attitudeMode == StationStateModel.ATT_MODE_FIXED_INERTIAL)
            return Vector3.zero;

        // LVLH/RTN => approximate frame rate ω = h / r^2 along orbit normal
        Vector3 r = new Vector3((float)st.rrx, (float)st.rry, (float)st.rrz);
        Vector3 v = new Vector3((float)st.rvx, (float)st.rvy, (float)st.rvz);

        float r2 = r.sqrMagnitude;
        if (r2 < 1e-6f) return Vector3.zero;

        Vector3 h = Vector3.Cross(r, v); // m^2/s
        float rMag = Mathf.Sqrt(r2);
        if (rMag < 1e-6f) return Vector3.zero;
        return h / (r2); // h / r^3                  // rad/s approx
    }


    /// <summary>
    /// Remote-only: reconstruct docked craft pose deterministically from station state + netCore dock snapshot.
    /// Call this from SimManager.Update when netCore.mode == MODE_DOCKED.
    /// </summary>
    public void EvaluateDockedRemote(double tNow)
    {
        if (HasSimAuthority()) return;
        if (netCore == null || craft == null || craftAtt == null || stations == null) return;

        int stIdx = netCore.dockStationIndex;
        if (stIdx < 0 || stIdx >= stations.Length) return;

        StationStateModel st = stations[stIdx];
        if (st == null || !st.valid) return;

        // Base pose: captured relative pose (station body frame)
        Vector3 rel0 = netCore.dockRelPos_SB;
        Quaternion q0 = netCore.dock_qCraftToStation;

        // If retract is in progress or hard, compute target and interpolate by time (deterministic)
        byte phase = netCore.dockPhase;


        if (phase != _dbgLastRemotePresentedPhase)
        {
            Debug.Log(
                "[Docking][REMOTE][PRESENT] phase=" + DockPhaseName(phase) +
                " tNow=" + tNow.ToString("F3") +
                " retractT0=" + netCore.dockRetractT0.ToString("F3") +
                " station=" + netCore.dockStationIndex +
                " sport=" + netCore.dockStationPortIndex +
                " cport=" + netCore.dockCraftPortIndex
            );
            _dbgLastRemotePresentedPhase = phase;
        }


        if (phase == DockingRuntimeState.DOCK_SOFT)
        {
            ApplyDockedKinematics(st, rel0, q0);
            return;
        }

        // Need target pose for retract/hard
        // Ensure DockingRuntimeState pairing indices match, then compute target from caches.
        if (dock != null)
        {
            dock.dockedStationIndex = stIdx;
            dock.stationPortIndex = netCore.dockStationPortIndex;
            dock.craftPortIndex = netCore.dockCraftPortIndex;
        }

        // Compute target from cached port frames (purely deterministic)
        // This uses your existing method (writes dock.targetRelPos_SB and dock.target_qCraftToStation).
        // If dock is null, we can't store target, so just hold captured.
        if (dock == null || craftPorts == null)
        {
            ApplyDockedKinematics(st, rel0, q0);
            return;
        }

        ComputeHardTargetRelativePose(st);

        if (phase == DockingRuntimeState.DOCK_HARD)
        {
            ApplyDockedKinematics(st, dock.targetRelPos_SB, dock.target_qCraftToStation);
            return;
        }

        // DOCK_RETRACT: time-derived retract fraction
        double t0 = netCore.dockRetractT0;
        if (t0 <= 0.0)
        {
            // If retract time wasn't published for some reason, fall back to captured pose
            ApplyDockedKinematics(st, rel0, q0);
            return;
        }

        float s = (float)((tNow - t0) * (double)dock.retractSpeed);
        if (s < 0f) s = 0f;
        if (s > 1f) s = 1f;

        Vector3 rel = Vector3.Lerp(rel0, dock.targetRelPos_SB, s);
        Quaternion q = Quaternion.Slerp(q0, dock.target_qCraftToStation, s);

        ApplyDockedKinematics(st, rel, q);
    }

    public bool CanUndock()
    {
        if (dock == null) return false;
        return dock.active && dock.phase == DockingRuntimeState.DOCK_HARD;
    }
    public void CommandUndock()
    {

        Debug.Log(
            "[Docking] CommandUndock called. " +
            "active=" + (dock != null && dock.active) +
            " phase=" + (dock != null ? dock.phase.ToString() : "null") +
            " netMode=" + (netCore != null ? netCore.mode.ToString() : "null")
        );

        if (dock == null || netCore == null) return;
        if (!HasSimAuthority()) return;
        if (!dock.active || dock.phase != DockingRuntimeState.DOCK_HARD) return;

        requestUndock = true;

        if (log) Debug.Log("[Docking] CommandUndock -> request queued.");
    }
    public void ExecuteUndockRelease(double tNow)
    {
        if (dock == null || craft == null || craftAtt == null || stations == null) return;
        if (netCore == null) return;
        if (!HasSimAuthority()) return;
        if (!dock.active || dock.phase != DockingRuntimeState.DOCK_HARD) return;

        int stIdx = dock.dockedStationIndex;
        if (stIdx < 0 || stIdx >= stations.Length) return;

        StationStateModel st = stations[stIdx];
        if (st == null || !st.valid) return;

        int sPort = dock.stationPortIndex;
        if (sPort < 0 || sPort >= st.dockingPortCount) return;

        Quaternion qS_SB = st.dock_q_B[sPort];
        Quaternion qS_E = st.q_B2E;

        Vector3 sepDir_E = qS_E * (qS_SB * Vector3.forward);
        if (sepDir_E.sqrMagnitude > 1e-10f) sepDir_E.Normalize();
        else sepDir_E = Vector3.forward;

        // IMPORTANT: apply release from the CURRENT already-updated docked craft state
        craft.rx += (double)(sepDir_E.x * undockSeparationMeters);
        craft.ry += (double)(sepDir_E.y * undockSeparationMeters);
        craft.rz += (double)(sepDir_E.z * undockSeparationMeters);

        craft.vx += (double)(sepDir_E.x * undockSeparationSpeedMps);
        craft.vy += (double)(sepDir_E.y * undockSeparationSpeedMps);
        craft.vz += (double)(sepDir_E.z * undockSeparationSpeedMps);

        craft.primaryBodyId = st.primaryBodyId;

        _recaptureBlockedUntil = tNow + (double)recaptureBlockSeconds;

        requestLeaveDockedToRails = true;
        requestUndock = false;

        if (log) Debug.Log("[Docking] ExecuteUndockRelease -> released from current docked pose.");
    }


    public static string DockPhaseName(byte phase)
    {
        switch (phase)
        {
            case DockingRuntimeState.DOCK_NONE:    return "NONE";
            case DockingRuntimeState.DOCK_SOFT:    return "SOFT";
            case DockingRuntimeState.DOCK_RETRACT: return "RETRACT";
            case DockingRuntimeState.DOCK_HARD:    return "HARD";
        }
        return "UNKNOWN(" + phase + ")";
    }

}