using UdonSharp;
using UnityEngine;
using System;

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

    [Tooltip("Friend docking controller attached to the craft (or active craft proxy).")]
    public DockingController dockCtrl;

    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;

    public StationStateModel[] stations;

    [Tooltip("Your craft port cache (relative to craft CG in craft body frame).")]
    public CraftDockingPorts craftPorts;

    [Header("Policy")]
    public bool autoAdvance = true;

    [Header("Outputs (read by SimManager/GC)")]
    [Tooltip("Set true for one frame when capture occurs; SimManager should switch craft to MODE_DOCKED.")]
    public bool requestEnterDocked = false;

    [Tooltip("If true, other systems (GC, manual controls) should be disabled.")]
    public bool suggestKillControls = false;

    [Header("Debug")]
    public bool log = false;

    // --------------------
    // Public entry points
    // --------------------

    /// <summary>
    /// Call this in Update AFTER latch triggers have updated (normal Unity order is fine).
    /// This does not move the craft; it only enters capture state when latches say so.
    /// </summary>
    public void EvaluateLatchAndStart(double tNow)
    {
        requestEnterDocked = false;
        suggestKillControls = false;

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
        DockingPortMeta localMeta = (DockingPortMeta)dockCtrl.localPort.GetComponent(typeof(DockingPortMeta));
        DockingPortMeta targMeta  = (DockingPortMeta)targetPort.GetComponent(typeof(DockingPortMeta));

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
        requestEnterDocked = false;
        suggestKillControls = false;

        if (dock == null || craft == null || craftAtt == null || stations == null) return;
        if (!dock.active || dock.phase == DockingRuntimeState.DOCK_NONE) return;

        int stIdx = dock.dockedStationIndex;
        if (stIdx < 0 || stIdx >= stations.Length) { ForceUndock(); return; }

        StationStateModel st = stations[stIdx];
        if (st == null || !st.valid) { ForceUndock(); return; }

        suggestKillControls = true;

        if (autoAdvance && dockCtrl != null)
        {
            // Use friend's controller state as phase gate:
            // - SoftCapture => DOCK_SOFT (hold pose, damp drift)
            // - HardCapture => DOCK_RETRACT (start retract animation)
            if (dock.phase == DockingRuntimeState.DOCK_SOFT && dockCtrl.state == DockingState.HardCapture)
            {
                dock.phase = DockingRuntimeState.DOCK_RETRACT;
                if (log) Debug.Log("[Docking] SOFT -> RETRACT (DockingController.HardCapture).");
            }
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

            // Hard once retract complete + very near target (optional)
            if (dock.retractS >= 0.999f && IsNearHardTarget())
            {
                dock.phase = DockingRuntimeState.DOCK_HARD;
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

    private void ComputeHardTargetRelativePose(StationStateModel st)
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
        dock.target_qCraftToStation = qS_SB * dock.qMate * Quaternion.Inverse(qC_B);

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
        // Station inertial pose
        Vector3 rS_E = new Vector3((float)st.rx, (float)st.ry, (float)st.rz);
        Vector3 vS_E = new Vector3((float)st.vx, (float)st.vy, (float)st.vz);
        Quaternion qS = st.q_B2E; // station BODY -> E

        // Craft inertial position
        Vector3 relPos_E = qS * relPos_SB;
        Vector3 rC_E = rS_E + relPos_E;

        // Station angular rate (E)
        Vector3 wS_E = ComputeStationOmegaE(st);

        // Craft inertial velocity
        Vector3 vC_E = vS_E + Vector3.Cross(wS_E, relPos_E);

        // Craft inertial attitude:
        // qCraft_E = qStation_E * inv(qCraftToStation)
        Quaternion qC_E = qS * Quaternion.Inverse(qCraftToStation);

        // Write craft translational state (double)
        craft.rx = (double)rC_E.x; craft.ry = (double)rC_E.y; craft.rz = (double)rC_E.z;
        craft.vx = (double)vC_E.x; craft.vy = (double)vC_E.y; craft.vz = (double)vC_E.z;
        craft.primaryBodyId = st.primaryBodyId;

        // Write craft attitude
        craftAtt.qBE = qC_E;

        // Write craft body rates to match station frame rate
        Quaternion qEB = Quaternion.Inverse(qC_E);
        Vector3 wC_B = qEB * wS_E;
        craftAtt.wx = (double)wC_B.x;
        craftAtt.wy = (double)wC_B.y;
        craftAtt.wz = (double)wC_B.z;

        // Persist "current" relative pose for error tests / smooth progression
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
        return h / (r2 * rMag); // h / r^3                  // rad/s approx
    }
}