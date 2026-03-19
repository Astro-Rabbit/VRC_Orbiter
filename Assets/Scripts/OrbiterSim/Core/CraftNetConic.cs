using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CraftNetConic : UdonSharpBehaviour
{
    [Header("Wiring")]
    public SimClock clock;
    public ConicState conic;
    [Header("Authority")]
    public SimManager simManager;

    public CraftNetState core;

    [Header("Publish rate")]
    [Tooltip("Conic publish rate (Hz) in rails. 0 disables periodic publishing (use ForcePublishConic).")]
    public float conicHz = 1f;

    // --- synced conic ---
    [UdonSynced] private int _rev;
    [UdonSynced] private byte _primaryBodyId;
    [UdonSynced] private bool _valid;

    [UdonSynced] private double _epochT0;
    [UdonSynced] private double _M0;

    [UdonSynced] private double _a;
    [UdonSynced] private double _e;
    [UdonSynced] private double _i;
    [UdonSynced] private double _raan;
    [UdonSynced] private double _argp;

    private float _accum;
    private int _appliedRev = -1;

    private float Period => (conicHz > 0f) ? (1f / conicHz) : 999999f;

    private bool HasSimAuthority()
    {
        return simManager != null && simManager.IsSimOwner();
    }

    /// <summary>Owner: publish conic at cadence while in MODE_RAILS. Safe to call every frame.</summary>
    public void PublishConic()
    {
        if (!HasSimAuthority()) return;        
        if (!Networking.IsOwner(gameObject)) return;
        if (conic == null || core == null) return;

        if (core.GetMode() != CraftNetState.MODE_RAILS) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;
        _accum = 0f;

        DoWriteAndSerialize(bumpRevision: true);
    }

    /// <summary>Owner: force publish conic immediately (SOI switch, mode transitions, burn end).</summary>
    public void ForcePublishConic()
    {
        if (!HasSimAuthority()) return;        
        if (!Networking.IsOwner(gameObject)) return;
        if (conic == null) return;

        _accum = 0f;
        DoWriteAndSerialize(bumpRevision: true);
    }

    private void DoWriteAndSerialize(bool bumpRevision)
    {
        _primaryBodyId = conic.primaryBodyId;
        _valid = conic.valid;

        _epochT0 = conic.epochT0;
        _M0 = conic.M0Rad;

        _a = conic.aMeters;
        _e = conic.e;
        _i = conic.iRad;
        _raan = conic.raanRad;
        _argp = conic.argpRad;

        if (bumpRevision) _rev++;

        RequestSerialization();
        _appliedRev = _rev;
    }

    /// <summary>Remote: apply synced conic into the local ConicState (useful for late joiners).</summary>
    public void ApplyRemoteConic()
    {
        if (HasSimAuthority()) return;
        if (conic == null) return;

        conic.primaryBodyId = _primaryBodyId;
        conic.valid = _valid;

        conic.epochT0 = _epochT0;
        conic.M0Rad = _M0;

        conic.aMeters = _a;
        conic.e = _e;
        conic.iRad = _i;
        conic.raanRad = _raan;
        conic.argpRad = _argp;
    }

    public override void OnDeserialization()
    {
        if (_rev == _appliedRev) return;
        _appliedRev = _rev;

        // Apply immediately so rails propagation uses the same conic on remotes
        if (!HasSimAuthority())
            ApplyRemoteConic();
    }
}

