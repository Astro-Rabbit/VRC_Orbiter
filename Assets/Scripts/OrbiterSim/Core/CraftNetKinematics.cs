using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CraftNetKinematics : UdonSharpBehaviour
{
    [Header("Wiring")]
    public SimClock clock;
    public CraftStateModel craft;
    public CraftNetState core;

    [Header("Publish rate")]
    [Tooltip("Kinematics publish rate (Hz) while integrated. 0 disables.")]
    public float kinHz = 10f;

    [Header("Remote reconstruction")]
    public bool remoteDeadReckon = true;
    [Tooltip("Clamp dt for dead-reckon to avoid huge jumps if packets stall (seconds).")]
    public float deadReckonClampSeconds = 2f;

    // --- synced kinematics ---
    [UdonSynced] private int _rev;
    [UdonSynced] private double _epochT;
    [UdonSynced] private double _rx, _ry, _rz;
    [UdonSynced] private double _vx, _vy, _vz;

    private float _accum;
    private int _appliedRev = -1;

    private float Period => (kinHz > 0f) ? (1f / kinHz) : 999999f;

    /// <summary>Owner: publish R/V at configured cadence (ONLY meaningful in MODE_INTEGRATED). Safe to call every frame.</summary>
    public void PublishKinematics()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (clock == null || craft == null || core == null) return;

        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;
        _accum = 0f;

        _epochT = clock.Now();
        _rx = craft.rx; _ry = craft.ry; _rz = craft.rz;
        _vx = craft.vx; _vy = craft.vy; _vz = craft.vz;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;
    }

    /// <summary>Remote: apply snapshot (with optional dead-reckon) each frame during integrated mode.</summary>
    public void ApplyRemoteKinematics()
    {
        if (Networking.IsOwner(gameObject)) return;
        if (clock == null || craft == null || core == null) return;

        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        double rx = _rx, ry = _ry, rz = _rz;
        double vx = _vx, vy = _vy, vz = _vz;

        if (remoteDeadReckon)
        {
            double dt = clock.Now() - _epochT;
            double c = (double)deadReckonClampSeconds;
            if (dt >  c) dt =  c;
            if (dt < -c) dt = -c;

            rx += vx * dt;
            ry += vy * dt;
            rz += vz * dt;
        }

        craft.rx = rx; craft.ry = ry; craft.rz = rz;
        craft.vx = vx; craft.vy = vy; craft.vz = vz;
    }

    public void ForcePublishKinematics()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (clock == null || craft == null || core == null) return;
        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        _accum = 0f;
        _epochT = clock.Now();
        _rx = craft.rx; _ry = craft.ry; _rz = craft.rz;
        _vx = craft.vx; _vy = craft.vy; _vz = craft.vz;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;
    }

    public override void OnDeserialization()
    {
        // Nothing forced on receipt; SimManager calls ApplyRemoteKinematics() per-frame when needed.
        _appliedRev = _rev;
    }
}
