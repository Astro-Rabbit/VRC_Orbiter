using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// CraftNetKinematics
///
/// Manual-sync network stream for craft translational kinematics while in INTEGRATED mode.
///
/// Philosophy (cleaned-up version):
/// - Remote raw snapshot state is the authoritative received craft translation state.
/// - Remote craft state can be written directly from that raw snapshot.
/// - Optional visual smoothing is maintained as a SEPARATE presentation output.
/// - No synthetic interpolation timeline / playback axis / interpolated sim-time.
/// - Attitude keeps using its own shared interp-back path elsewhere.
///
/// This avoids the previous failure mode where interpolated position + interpolated
/// sim-time could become visually inconsistent and create along-track jitter.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CraftNetKinematics : UdonSharpBehaviour
{
    // -------------------------------------------------------------------------
    // Wiring
    // -------------------------------------------------------------------------

    [Header("Wiring")]
    public SimClock clock;
    public CraftStateModel craft;
    public CraftNetState core;
    [Header("Authority")]
    public SimManager simManager;
    // -------------------------------------------------------------------------
    // Owner publish policy
    // -------------------------------------------------------------------------

    [Header("Publish rate")]
    [Tooltip("Kinematics publish rate (Hz) while integrated. 0 disables periodic publishing.")]
    public float kinHz = 4f;

    [Header("Owner sample time")]
    [Tooltip("Exact owner sim-time corresponding to the current craft state. SimManager should set this before publishing integrated snapshots.")]
    public double currentOwnerSimT = 0.0;

    [Tooltip("If true, publish currentOwnerSimT as the snapshot sim-time. If false, fallback to clock.Now().")]
    public bool useCurrentOwnerSimT = true;

    // -------------------------------------------------------------------------
    // Remote raw application policy
    // -------------------------------------------------------------------------

    [Header("Remote raw application")]
    [Tooltip("If true, ApplyRemoteRawToCraft() dead-reckons the raw snapshot forward slightly before writing craft state. Usually leave OFF.")]
    public bool applyRawDeadReckonToCraft = false;

    [Tooltip("Clamp dt for raw dead-reckon / extrapolation (seconds).")]
    public float deadReckonClampSeconds = 0.25f;

    // -------------------------------------------------------------------------
    // Optional visual presentation smoothing
    // -------------------------------------------------------------------------

    [Header("Visual presentation smoothing")]
    [Tooltip("If true, maintain a separate smoothed visual translation output from latest raw snapshots.")]
    public bool enableVisualSmoothing = true;

    [Tooltip("If true, dead-reckon the VISUAL TARGET slightly from the latest raw snapshot before smoothing.")]
    public bool visualDeadReckonTarget = false;

    [Tooltip("Smoothing time constant in seconds. Smaller = tighter / less lag.")]
    public float visualSmoothTimeSeconds = 0.12f;

    [Tooltip("Clamp dt for visual target dead-reckon (seconds).")]
    public float visualTargetDeadReckonClampSeconds = 0.15f;

    // -------------------------------------------------------------------------
    // Debug
    // -------------------------------------------------------------------------

    [Header("Debug")]
    public bool debugNetKin = false;
    public float debugLogPeriod = 1.0f;

    [Header("Read-only raw snapshot state")]
    public bool rawValid = false;
    public int rawRevision = -1;
    public double rawReceiveTime;
    public double rawSendTime;
    public double rawEpochT;
    public double rawSimT;
    public double rawRx, rawRy, rawRz;
    public double rawVx, rawVy, rawVz;

    [Header("Read-only presented visual state")]
    public bool presentedValid = false;
    public double presentedRx, presentedRy, presentedRz;
    public double presentedVx, presentedVy, presentedVz;

    [Header("Read-only visual target state")]
    public double targetRx, targetRy, targetRz;
    public double targetVx, targetVy, targetVz;
    public double targetDtEx;

    [Header("Read-only debug")]
    public double dbgLastReceiveDelta;
    public double dbgAvgReceiveDelta;
    public double dbgSimLagSeconds;
    public double dbgPresentedOffsetMeters;
    public double dbgPresentedVelOffset;
    public double dbgAppliedRawDtEx;

    // -------------------------------------------------------------------------
    // Synced snapshot fields
    // -------------------------------------------------------------------------

    [UdonSynced] private int _rev;
    [UdonSynced] private double _epochT;
    [UdonSynced] private double _simEpochT;
    [UdonSynced] private double _rx, _ry, _rz;
    [UdonSynced] private double _vx, _vy, _vz;

    // -------------------------------------------------------------------------
    // Local bookkeeping
    // -------------------------------------------------------------------------

    private float _accum;
    private float _debugAccum;
    private int _appliedRev = -1;

    private float Period => (kinHz > 0f) ? (1f / kinHz) : 999999f;

    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    private bool HasSimAuthority()
    {
        return simManager != null && simManager.IsSimOwner();
    }

    void Start()
    {
        if (HasSimAuthority())
        {
            SnapPresentedToCraft();
        }
    }

    void Update()
    {
        if (!debugNetKin) return;
        if (HasSimAuthority()) return;

        _debugAccum += Time.deltaTime;
        if (_debugAccum < debugLogPeriod) return;
        _debugAccum = 0f;

        dbgSimLagSeconds = (double)Time.realtimeSinceStartup - Networking.SimulationTime(gameObject);

        Debug.Log(
            "[NetKinDbg] rawValid=" + rawValid +
            " rev=" + rawRevision +
            " recvDt=" + dbgLastReceiveDelta.ToString("F3") +
            " avgRecvDt=" + dbgAvgReceiveDelta.ToString("F3") +
            " simLag=" + dbgSimLagSeconds.ToString("F3") +
            " rawSimT=" + rawSimT.ToString("F3") +
            " rawDtEx=" + dbgAppliedRawDtEx.ToString("F3") +
            " targetDtEx=" + targetDtEx.ToString("F3") +
            " posOff=" + dbgPresentedOffsetMeters.ToString("F3") +
            " velOff=" + dbgPresentedVelOffset.ToString("F3")
        );
    }

    // -------------------------------------------------------------------------
    // Owner publish API
    // -------------------------------------------------------------------------

    public void PublishKinematics()
    {
        if (!HasSimAuthority()) return;        
        if (!Networking.IsOwner(gameObject)) return;
        if (clock == null || craft == null || core == null) return;
        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;

        _accum -= Period;
        if (_accum > Period) _accum = 0f;

        WriteSnapshotAndSerialize();
    }

    public void ForcePublishKinematics()
    {
        if (!HasSimAuthority()) return;        
        if (!Networking.IsOwner(gameObject)) return;
        if (clock == null || craft == null || core == null) return;
        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        _accum = 0f;
        WriteSnapshotAndSerialize();
    }

    private void WriteSnapshotAndSerialize()
    {
        double simSampleT = useCurrentOwnerSimT ? currentOwnerSimT : clock.Now();
        double netSampleT = useCurrentOwnerSimT
            ? clock.ServerTimeForSimTime(simSampleT)
            : clock.NowNetwork();

        _simEpochT = simSampleT;
        _epochT = netSampleT;

        _rx = craft.rx;
        _ry = craft.ry;
        _rz = craft.rz;

        _vx = craft.vx;
        _vy = craft.vy;
        _vz = craft.vz;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;
    }

    // -------------------------------------------------------------------------
    // Remote raw application
    // -------------------------------------------------------------------------

    /// <summary>
    /// Apply latest raw received snapshot to local craft state on remotes.
    /// This is the coherent network snapshot path, not the smoothed visual path.
    /// </summary>
    public void ApplyRemoteRawToCraft()
    {
        if (HasSimAuthority()) return;
        if (clock == null || craft == null || core == null) return;
        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        double rx = rawValid ? rawRx : _rx;
        double ry = rawValid ? rawRy : _ry;
        double rz = rawValid ? rawRz : _rz;

        double vx = rawValid ? rawVx : _vx;
        double vy = rawValid ? rawVy : _vy;
        double vz = rawValid ? rawVz : _vz;

        dbgAppliedRawDtEx = 0.0;

        if (applyRawDeadReckonToCraft)
        {
            double refT = rawValid ? rawReceiveTime : (double)Time.realtimeSinceStartup;
            double dt = (double)Time.realtimeSinceStartup - refT;
            double c = (double)deadReckonClampSeconds;

            if (dt > c) dt = c;
            if (dt < -c) dt = -c;

            dbgAppliedRawDtEx = dt;

            rx += vx * dt;
            ry += vy * dt;
            rz += vz * dt;
        }

        craft.rx = rx;
        craft.ry = ry;
        craft.rz = rz;

        craft.vx = vx;
        craft.vy = vy;
        craft.vz = vz;
    }

    // -------------------------------------------------------------------------
    // Visual presentation state
    // -------------------------------------------------------------------------

    /// <summary>
    /// Update the separate visual presentation translation from latest raw snapshot.
    /// This does NOT write into craft state.
    /// </summary>
    public void UpdatePresentedState()
    {
        if (HasSimAuthority())
        {
            SnapPresentedToCraft();
            return;
        }

        // choose coherent latest raw target
        double rx = rawValid ? rawRx : _rx;
        double ry = rawValid ? rawRy : _ry;
        double rz = rawValid ? rawRz : _rz;

        double vx = rawValid ? rawVx : _vx;
        double vy = rawValid ? rawVy : _vy;
        double vz = rawValid ? rawVz : _vz;

        targetDtEx = 0.0;

        if (visualDeadReckonTarget)
        {
            double refT = rawValid ? rawReceiveTime : (double)Time.realtimeSinceStartup;
            double dt = (double)Time.realtimeSinceStartup - refT;
            double c = (double)visualTargetDeadReckonClampSeconds;

            if (dt > c) dt = c;
            if (dt < -c) dt = -c;

            targetDtEx = dt;

            rx += vx * dt;
            ry += vy * dt;
            rz += vz * dt;
        }

        targetRx = rx;
        targetRy = ry;
        targetRz = rz;
        targetVx = vx;
        targetVy = vy;
        targetVz = vz;

        if (!presentedValid || !enableVisualSmoothing)
        {
            presentedRx = targetRx;
            presentedRy = targetRy;
            presentedRz = targetRz;

            presentedVx = targetVx;
            presentedVy = targetVy;
            presentedVz = targetVz;

            presentedValid = true;
            UpdatePresentedDebugOffsets();
            return;
        }

        float dtFrame = Time.deltaTime;
        if (dtFrame < 0f) dtFrame = 0f;
        if (dtFrame > 0.25f) dtFrame = 0.25f;

        float tau = visualSmoothTimeSeconds;
        if (tau < 0.0001f) tau = 0.0001f;

        float alpha = 1f - Mathf.Exp(-dtFrame / tau);

        presentedRx += (targetRx - presentedRx) * (double)alpha;
        presentedRy += (targetRy - presentedRy) * (double)alpha;
        presentedRz += (targetRz - presentedRz) * (double)alpha;

        presentedVx += (targetVx - presentedVx) * (double)alpha;
        presentedVy += (targetVy - presentedVy) * (double)alpha;
        presentedVz += (targetVz - presentedVz) * (double)alpha;

        UpdatePresentedDebugOffsets();
    }

    private void SnapPresentedToCraft()
    {
        if (craft == null) return;

        presentedRx = craft.rx;
        presentedRy = craft.ry;
        presentedRz = craft.rz;

        presentedVx = craft.vx;
        presentedVy = craft.vy;
        presentedVz = craft.vz;

        targetRx = presentedRx;
        targetRy = presentedRy;
        targetRz = presentedRz;

        targetVx = presentedVx;
        targetVy = presentedVy;
        targetVz = presentedVz;

        presentedValid = true;
        UpdatePresentedDebugOffsets();
    }

    private void UpdatePresentedDebugOffsets()
    {
        double dx = targetRx - presentedRx;
        double dy = targetRy - presentedRy;
        double dz = targetRz - presentedRz;
        dbgPresentedOffsetMeters = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);

        double dvx = targetVx - presentedVx;
        double dvy = targetVy - presentedVy;
        double dvz = targetVz - presentedVz;
        dbgPresentedVelOffset = System.Math.Sqrt(dvx * dvx + dvy * dvy + dvz * dvz);
    }

    // -------------------------------------------------------------------------
    // Networking callbacks
    // -------------------------------------------------------------------------

    public override void OnPostSerialization(VRC.Udon.Common.SerializationResult result)
    {
        if (!debugNetKin) return;
        Debug.Log("[NetKin] success=" + result.success + " bytes=" + result.byteCount);
    }

    public override void OnDeserialization(VRC.Udon.Common.DeserializationResult result)
    {
        if (HasSimAuthority())
        {
            _appliedRev = _rev;
            return;
        }

        if (_rev <= _appliedRev)
            return;

        double recvT = result.receiveTime;
        double sendT = result.sendTime;

        if (rawValid)
        {
            dbgLastReceiveDelta = recvT - rawReceiveTime;

            if (dbgAvgReceiveDelta <= 0.0) dbgAvgReceiveDelta = dbgLastReceiveDelta;
            else dbgAvgReceiveDelta = dbgAvgReceiveDelta * 0.85 + dbgLastReceiveDelta * 0.15;
        }

        rawValid = true;
        rawRevision = _rev;

        rawReceiveTime = recvT;
        rawSendTime = sendT;
        rawEpochT = _epochT;
        rawSimT = _simEpochT;

        rawRx = _rx;
        rawRy = _ry;
        rawRz = _rz;

        rawVx = _vx;
        rawVy = _vy;
        rawVz = _vz;

        if (!presentedValid)
        {
            presentedRx = rawRx;
            presentedRy = rawRy;
            presentedRz = rawRz;

            presentedVx = rawVx;
            presentedVy = rawVy;
            presentedVz = rawVz;

            targetRx = rawRx;
            targetRy = rawRy;
            targetRz = rawRz;

            targetVx = rawVx;
            targetVy = rawVy;
            targetVz = rawVz;

            presentedValid = true;
            UpdatePresentedDebugOffsets();
        }

        _appliedRev = _rev;
    }
}