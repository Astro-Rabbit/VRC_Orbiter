using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CraftNetState : UdonSharpBehaviour
{
    public const byte MODE_RAILS = 0;
    public const byte MODE_INTEGRATED = 1;

    [Header("Wiring")]
    public SimClock clock;
    public CraftStateModel craft;

    [Header("Identity")]
    public int craftId = 0;

    [Header("Publish rate")]
    [Tooltip("Core/meta publish rate (Hz). Set low (e.g. 1-5). 0 disables periodic publishing (use ForcePublishCore).")]
    public float coreHz = 2f;

    [Header("Read-only (mirrors synced)")]
    public byte mode = MODE_RAILS;
    public byte primaryBodyId = 1;

    // --- synced core ---
    [UdonSynced] private int _rev;
    [UdonSynced] private int _craftId;
    [UdonSynced] private byte _mode;
    [UdonSynced] private byte _primaryBodyId;

    // Mass/fuel state (doubles)
    [UdonSynced] private double _dryMassKg;
    [UdonSynced] private double _propMassKg;
    [UdonSynced] private double _massKg;

    // Optional: a generic "fuel fraction" convenience (0..1). If you don't want it, ignore.
    [UdonSynced] private float _fuel01;

    // Optional: time the mode/meta was last published (sim time)
    [UdonSynced] private double _coreEpochT;

    private float _accum;
    private int _appliedRev = -1;

    private float Period => (coreHz > 0f) ? (1f / coreHz) : 999999f;

    void Start()
    {
        mode = _mode;
        primaryBodyId = _primaryBodyId;
    }

    public byte GetMode() => _mode;
    public byte GetPrimaryBodyId() => _primaryBodyId;

    /// <summary>Owner: set mode (and optionally primary) and publish immediately.</summary>
    public void SetMode(byte newMode, byte newPrimaryBodyId, bool forcePublish = true)
    {
        if (!Networking.IsOwner(gameObject)) return;

        _mode = newMode;
        _primaryBodyId = newPrimaryBodyId;

        mode = _mode;
        primaryBodyId = _primaryBodyId;

        if (forcePublish) ForcePublishCore();
    }

    /// <summary>Owner: publish core/meta at its configured cadence. Safe to call every frame.</summary>
    public void PublishCore()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (craft == null || clock == null) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;
        _accum = 0f;

        DoWriteCoreAndSerialize(bumpRevision: true);
    }

    /// <summary>Owner: force publish core/meta immediately (e.g., mode changes, fuel changes).</summary>
    public void ForcePublishCore()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (craft == null || clock == null) return;

        _accum = 0f;
        DoWriteCoreAndSerialize(bumpRevision: true);
    }

    private void DoWriteCoreAndSerialize(bool bumpRevision)
    {
        _craftId = craftId;
        _coreEpochT = clock.Now();

        // Mirror from craft state
        _dryMassKg = craft.dryMassKg;
        _propMassKg = craft.propMassKg;
        _massKg = craft.massKg;

        // Fuel fraction convenience (avoid div by 0)
        double denom = craft.dryMassKg + craft.propMassKg;
        _fuel01 = (denom > 1e-6) ? (float)(craft.propMassKg / denom) : 0f;

        if (bumpRevision) _rev++;

        mode = _mode;
        primaryBodyId = _primaryBodyId;

        RequestSerialization();
        _appliedRev = _rev;
    }

    public override void OnDeserialization()
    {
        mode = _mode;
        primaryBodyId = _primaryBodyId;

        if (_rev == _appliedRev) return;
        _appliedRev = _rev;

        // Apply mass/fuel to local craft model (remote follow / UI)
        if (!Networking.IsOwner(gameObject) && craft != null)
        {
            craft.primaryBodyId = _primaryBodyId;
            craft.dryMassKg = _dryMassKg;
            craft.propMassKg = _propMassKg;
            craft.massKg = _massKg;
        }
    }
}
