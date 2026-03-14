using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CraftNetState : UdonSharpBehaviour
{
    public const byte MODE_RAILS = 0;
    public const byte MODE_INTEGRATED = 1;
    public const byte MODE_DOCKED = 2;

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

    [Header("Read-only (presentation transition mirrors synced)")]
    [Tooltip("Previous authoritative mode before the latest mode change. Used by remotes for delayed presentation switching.")]
    public byte prevMode = MODE_RAILS;

    [Tooltip("Previous authoritative primary before the latest mode change. Used by remotes for delayed presentation switching.")]
    public byte prevPrimaryBodyId = 1;

    [Tooltip("Network/server time when the latest mode change was published.")]
    public double modeChangeNetT = 0.0;

    [Header("Read-only (docking mirrors synced)")]
    public byte dockPhase = 0;
    public int dockStationIndex = -1;
    public byte dockStationPortIndex = 0;
    public byte dockCraftPortIndex = 0;
    public double dockCaptureT0 = 0.0;
    public double dockRetractT0 = 0.0;

    [Header("Read-only (docking pose mirrors synced)")]
    public Vector3 dockRelPos_SB;
    public Quaternion dock_qCraftToStation;

    // --- synced core ---
    [UdonSynced] private int _rev;
    [UdonSynced] private int _craftId;
    [UdonSynced] private byte _mode;
    [UdonSynced] private byte _primaryBodyId;

    // Delayed remote presentation transition support
    [UdonSynced] private byte _prevMode;
    [UdonSynced] private byte _prevPrimaryBodyId;
    [UdonSynced] private double _modeChangeNetT;

    // Mass/fuel state (doubles)
    [UdonSynced] private double _dryMassKg;
    [UdonSynced] private double _propMassKg;
    [UdonSynced] private double _massKg;

    // Optional: a generic "fuel fraction" convenience (0..1). If you don't want it, ignore.
    [UdonSynced] private float _fuel01;

    // Optional: time the mode/meta was last published (sim time)
    [UdonSynced] private double _coreEpochT;

    // --- synced dock attachment (valid when _mode == MODE_DOCKED) ---
    [UdonSynced] private byte _dockPhase;
    [UdonSynced] private int _dockStationIndex;
    [UdonSynced] private byte _dockStationPortIndex;
    [UdonSynced] private byte _dockCraftPortIndex;

    // Deterministic animation timing (mission time)
    [UdonSynced] private double _dockCaptureT0;
    [UdonSynced] private double _dockRetractT0;

    // --- synced dock pose (station body frame) ---
    [UdonSynced] private Vector3 _dockRelPos_SB;
    [UdonSynced] private Quaternion _dock_qCraftToStation;

    private float _accum;
    private int _appliedRev = -1;

    private float Period => (coreHz > 0f) ? (1f / coreHz) : 999999f;

    void Start()
    {
        mode = _mode;
        primaryBodyId = _primaryBodyId;

        prevMode = _prevMode;
        prevPrimaryBodyId = _prevPrimaryBodyId;
        modeChangeNetT = _modeChangeNetT;
    }

    public byte GetMode() => _mode;
    public byte GetPrimaryBodyId() => _primaryBodyId;

    public byte GetPrevMode() => _prevMode;
    public byte GetPrevPrimaryBodyId() => _prevPrimaryBodyId;
    public double GetModeChangeNetT() => _modeChangeNetT;

    /// <summary>
    /// Remote presentation helper:
    /// - Before tRender reaches the mode-change network time, present the PREVIOUS mode.
    /// - After that, present the CURRENT mode.
    /// Owners always use current authoritative mode.
    /// </summary>
    public byte GetPresentedMode(double tRender)
    {
        if (Networking.IsOwner(gameObject)) return _mode;
        if (tRender < _modeChangeNetT) return _prevMode;
        return _mode;
    }

    /// <summary>
    /// Remote presentation helper matching GetPresentedMode().
    /// </summary>
    public byte GetPresentedPrimaryBodyId(double tRender)
    {
        if (Networking.IsOwner(gameObject)) return _primaryBodyId;
        if (tRender < _modeChangeNetT) return _prevPrimaryBodyId;
        return _primaryBodyId;
    }

    /// <summary>Owner: set mode (and optionally primary) and publish immediately.</summary>
    public void SetMode(byte newMode, byte newPrimaryBodyId, bool forcePublish = true)
    {
        if (!Networking.IsOwner(gameObject)) return;

        // Record previous authoritative values for delayed remote presentation
        _prevMode = _mode;
        _prevPrimaryBodyId = _primaryBodyId;

        _mode = newMode;
        _primaryBodyId = newPrimaryBodyId;

        // Stamp the transition in NETWORK/server time for render-delay logic
        _modeChangeNetT = (clock != null) ? clock.NowNetwork() : Networking.GetServerTimeInSeconds();

        if (_mode != MODE_DOCKED)
            ClearDockSynced();

        mode = _mode;
        primaryBodyId = _primaryBodyId;
        prevMode = _prevMode;
        prevPrimaryBodyId = _prevPrimaryBodyId;
        modeChangeNetT = _modeChangeNetT;

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

        // Mirror publics for inspector/debug
        dockPhase = _dockPhase;
        dockStationIndex = _dockStationIndex;
        dockStationPortIndex = _dockStationPortIndex;
        dockCraftPortIndex = _dockCraftPortIndex;
        dockCaptureT0 = _dockCaptureT0;
        dockRetractT0 = _dockRetractT0;
        dockRelPos_SB = _dockRelPos_SB;
        dock_qCraftToStation = _dock_qCraftToStation;

        prevMode = _prevMode;
        prevPrimaryBodyId = _prevPrimaryBodyId;
        modeChangeNetT = _modeChangeNetT;

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

        prevMode = _prevMode;
        prevPrimaryBodyId = _prevPrimaryBodyId;
        modeChangeNetT = _modeChangeNetT;

        dockPhase = _dockPhase;
        dockStationIndex = _dockStationIndex;
        dockStationPortIndex = _dockStationPortIndex;
        dockCraftPortIndex = _dockCraftPortIndex;
        dockCaptureT0 = _dockCaptureT0;
        dockRetractT0 = _dockRetractT0;
        dockRelPos_SB = _dockRelPos_SB;
        dock_qCraftToStation = _dock_qCraftToStation;

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

    public void SetDocked(
        int stationIndex,
        byte stationPortIndex,
        byte craftPortIndex,
        byte phase,
        double captureT0,
        double retractT0,
        Vector3 relPos_SB,
        Quaternion qCraftToStation,
        byte primary,
        bool forcePublish = true)
    {
        if (!Networking.IsOwner(gameObject)) return;

        _dockStationIndex = stationIndex;
        _dockStationPortIndex = stationPortIndex;
        _dockCraftPortIndex = craftPortIndex;
        _dockPhase = phase;

        _dockCaptureT0 = captureT0;
        _dockRetractT0 = retractT0;
        _dockRelPos_SB = relPos_SB;
        _dock_qCraftToStation = qCraftToStation;

        dockRelPos_SB = relPos_SB;
        dock_qCraftToStation = qCraftToStation;

        // Record previous authoritative values for delayed remote presentation
        _prevMode = _mode;
        _prevPrimaryBodyId = _primaryBodyId;

        // Docked mode implies the craft primary matches the station’s primary
        _mode = MODE_DOCKED;
        _primaryBodyId = primary;

        // Stamp the transition in NETWORK/server time
        _modeChangeNetT = (clock != null) ? clock.NowNetwork() : Networking.GetServerTimeInSeconds();

        // Mirror public
        mode = _mode;
        primaryBodyId = _primaryBodyId;
        prevMode = _prevMode;
        prevPrimaryBodyId = _prevPrimaryBodyId;
        modeChangeNetT = _modeChangeNetT;

        if (forcePublish) ForcePublishCore();
    }

    private void ClearDockSynced()
    {
        _dockPhase = 0;
        _dockStationIndex = -1;
        _dockStationPortIndex = 0;
        _dockCraftPortIndex = 0;
        _dockCaptureT0 = 0.0;
        _dockRetractT0 = 0.0;

        _dockRelPos_SB = Vector3.zero;
        _dock_qCraftToStation = Quaternion.identity;

        dockRelPos_SB = Vector3.zero;
        dock_qCraftToStation = Quaternion.identity;
    }
}