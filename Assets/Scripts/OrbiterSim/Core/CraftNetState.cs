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
    public SimManager simManager;
    public SimClock clock;
    public CraftStateModel craft;

    [Header("Optional local mirrors")]
    public DockingRuntimeState dock;
    public DockingComputer dockingComp;

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

    [Header("Read-only (ownership transfer policy mirrors synced)")]
    public bool ownershipTransferHardLocked = false;

    // --- synced core ---
    [UdonSynced] private int _rev;
    [UdonSynced] private int _craftId;
    [UdonSynced] private byte _mode;
    [UdonSynced] private byte _primaryBodyId;

    [UdonSynced] private bool _ownershipTransferHardLocked;
    // Delayed remote presentation transition support
    [UdonSynced] private byte _prevMode;
    [UdonSynced] private byte _prevPrimaryBodyId;
    [UdonSynced] private double _modeChangeNetT;

    // Mass/fuel state (doubles)
    [UdonSynced] private float _propMassKg;



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

    [Header("Read-only handoff ack")]
    public int handoffEstablishedTxnId = -1;

    [UdonSynced] private int _handoffEstablishedTxnId = -1;

    private float _accum;
    private int _appliedRev = -1;

    private float Period => (coreHz > 0f) ? (1f / coreHz) : 999999f;


    private bool HasSimAuthority()
    {
        return simManager != null && simManager.IsSimOwner();
    }
    void Start()
    {
        mode = _mode;
        primaryBodyId = _primaryBodyId;

        prevMode = _prevMode;
        prevPrimaryBodyId = _prevPrimaryBodyId;
        modeChangeNetT = _modeChangeNetT;
        ownershipTransferHardLocked = _ownershipTransferHardLocked;    
        handoffEstablishedTxnId = _handoffEstablishedTxnId;    
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
        if (HasSimAuthority()) return _mode;
        if (tRender < _modeChangeNetT) return _prevMode;
        return _mode;
    }

    /// <summary>
    /// Remote presentation helper matching GetPresentedMode().
    /// </summary>
    public byte GetPresentedPrimaryBodyId(double tRender)
    {
        if (HasSimAuthority()) return _primaryBodyId;
        if (tRender < _modeChangeNetT) return _prevPrimaryBodyId;
        return _primaryBodyId;
    }

    /// <summary>Owner: set mode (and optionally primary) and publish immediately.</summary>
    public void SetMode(byte newMode, byte newPrimaryBodyId, bool forcePublish = true)
    {
        if (!HasSimAuthority()) return;        
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
        if (!HasSimAuthority()) return;        
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
        _propMassKg = (float)craft.propMassKg;

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

        if (simManager != null)
        {
            _ownershipTransferHardLocked = simManager.ownershipTransferHardLocked;
        }

        ownershipTransferHardLocked = _ownershipTransferHardLocked;

        if (bumpRevision) _rev++;

        mode = _mode;
        primaryBodyId = _primaryBodyId;
        handoffEstablishedTxnId = _handoffEstablishedTxnId;

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

        ownershipTransferHardLocked = _ownershipTransferHardLocked;

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

        if (!HasSimAuthority())
        {
            MirrorDockRuntimeFromSyncedState();
        }

        // Apply mass/fuel to local craft model (remote follow / UI)
        if (!HasSimAuthority() && craft != null)
        {
            craft.primaryBodyId = _primaryBodyId;
            craft.propMassKg = (double)_propMassKg;
            craft.RecomputeMass();
        }
        handoffEstablishedTxnId = _handoffEstablishedTxnId;
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
        if (!HasSimAuthority()) return;
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

    public void ResetPresentationState()
    {
        _accum = 0f;
        _appliedRev = -1;
    }

    public void ResetSyncedStateFromCurrent()
    {
        _accum = 0f;

        _prevMode = _mode;
        _prevPrimaryBodyId = _primaryBodyId;
        _modeChangeNetT = (clock != null) ? clock.NowNetwork() : Networking.GetServerTimeInSeconds();

        mode = _mode;
        primaryBodyId = _primaryBodyId;
        prevMode = _prevMode;
        prevPrimaryBodyId = _prevPrimaryBodyId;
        modeChangeNetT = _modeChangeNetT;
    }
    public bool GetOwnershipTransferHardLocked() => _ownershipTransferHardLocked;

    private void MirrorDockRuntimeFromSyncedState()
    {
        if (dock == null) return;

        if (_mode != MODE_DOCKED)
        {
            dock.ResetState();
            return;
        }

        dock.active = true;
        dock.phase = _dockPhase;

        dock.dockedStationIndex = _dockStationIndex;
        dock.stationPortIndex = _dockStationPortIndex;
        dock.craftPortIndex = _dockCraftPortIndex;

        dock.captureTime = _dockCaptureT0;
        dock.relPos_SB = _dockRelPos_SB;
        dock.qCraftToStation = _dock_qCraftToStation;
        dock.retractCommanded = false;

        if (dock.phase == DockingRuntimeState.DOCK_SOFT)
        {
            dock.retractS = 0f;
        }
        else if (dock.phase == DockingRuntimeState.DOCK_RETRACT)
        {
            double tNow = (clock != null) ? clock.Now() : 0.0;
            double t0 = _dockRetractT0;

            float s = 0f;
            if (t0 > 0.0)
                s = (float)((tNow - t0) * (double)dock.retractSpeed);

            if (s < 0f) s = 0f;
            if (s > 1f) s = 1f;
            dock.retractS = s;
        }
        else if (dock.phase == DockingRuntimeState.DOCK_HARD)
        {
            dock.retractS = 1f;
        }

        if (dockingComp != null &&
            dockingComp.stations != null &&
            _dockStationIndex >= 0 &&
            _dockStationIndex < dockingComp.stations.Length &&
            dockingComp.stations[_dockStationIndex] != null)
        {
            dockingComp.ComputeHardTargetRelativePose(dockingComp.stations[_dockStationIndex]);
        }
    }

    public void SetHandoffEstablishedTxnId(int txnId)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (!HasSimAuthority()) return;

        _handoffEstablishedTxnId = txnId;
        handoffEstablishedTxnId = txnId;
    }

    public void AdoptModeImmediate(byte adoptedMode, byte adoptedPrimaryBodyId)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (!HasSimAuthority()) return;

        _mode = adoptedMode;
        _primaryBodyId = adoptedPrimaryBodyId;

        // Keep prev/current aligned so remotes don't see a fake transition caused by takeover
        _prevMode = adoptedMode;
        _prevPrimaryBodyId = adoptedPrimaryBodyId;

        _modeChangeNetT = (clock != null) ? clock.NowNetwork() : Networking.GetServerTimeInSeconds();

        if (_mode != MODE_DOCKED)
            ClearDockSynced();

        mode = _mode;
        primaryBodyId = _primaryBodyId;
        prevMode = _prevMode;
        prevPrimaryBodyId = _prevPrimaryBodyId;
        modeChangeNetT = _modeChangeNetT;
    }

}