using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GC_RuntimeNetState : UdonSharpBehaviour
{
    [Header("Authority")]
    public SimManager simManager;

    [Header("Source state")]
    public GC_RuntimeState runtime;
    public GC_ModeParams modeParams;

    [Header("Publish")]
    public float minPublishInterval = 0.10f;
    public float heartbeatSeconds = 30.0f;

    [Header("Read-only mirrors")]
    public byte status;
    public byte activeModeId;
    public byte activeTranslateModeId;
    public byte activeProgramId;
    public byte activeExecutorId;
    public byte executorPhase;
    public byte bodyAxisToPoint;
    public byte rtnDir;
    public int selectedNodeIndex = -1;

    [UdonSynced] private int _rev;
    [UdonSynced] private byte _status;
    [UdonSynced] private byte _activeModeId;
    [UdonSynced] private byte _activeTranslateModeId;
    [UdonSynced] private byte _activeProgramId;
    [UdonSynced] private byte _activeExecutorId;
    [UdonSynced] private byte _executorPhase;
    [UdonSynced] private byte _bodyAxisToPoint;
    [UdonSynced] private byte _rtnDir;
    [UdonSynced] private byte _selectedNodeIndex = 255;

    private int _appliedRev = -1;
    private float _publishCooldown = 0f;
    private float _heartbeatAccum = 0f;

    private bool HasAuthority()
    {
        bool goOwner = Networking.IsOwner(gameObject);
        bool simAuth = (simManager == null) ? true : simManager.IsSimOwner();
        return goOwner && simAuth;
    }

    void Start()
    {
        if (HasAuthority())
        {
            CaptureFromLocal();
            ApplySyncedToLocals();
            ForcePublish();
        }
    }

    void Update()
    {
        if (_publishCooldown > 0f) _publishCooldown -= Time.deltaTime;

        if (!HasAuthority()) return;

        bool changed = CaptureFromLocal();
        if (changed && _publishCooldown <= 0f)
        {
            PublishNow();
        }
        else if (heartbeatSeconds > 0f)
        {
            _heartbeatAccum += Time.deltaTime;
            if (_heartbeatAccum >= heartbeatSeconds && _publishCooldown <= 0f)
            {
                PublishNow();
            }
        }
    }

    private bool CaptureFromLocal()
    {
        if (runtime == null) return false;

        bool changed = false;

        if (_status != runtime.status) { _status = runtime.status; changed = true; }
        if (_activeModeId != runtime.activeModeId) { _activeModeId = runtime.activeModeId; changed = true; }
        if (_activeTranslateModeId != runtime.activeTranslateModeId) { _activeTranslateModeId = runtime.activeTranslateModeId; changed = true; }
        if (_activeProgramId != runtime.activeProgramId) { _activeProgramId = runtime.activeProgramId; changed = true; }
        if (_activeExecutorId != runtime.activeExecutorId) { _activeExecutorId = runtime.activeExecutorId; changed = true; }
        if (_executorPhase != runtime.executorPhase) { _executorPhase = runtime.executorPhase; changed = true; }

        if (modeParams != null)
        {
            if (_bodyAxisToPoint != modeParams.bodyAxisToPoint) { _bodyAxisToPoint = modeParams.bodyAxisToPoint; changed = true; }
            if (_rtnDir != modeParams.rtnDir) { _rtnDir = modeParams.rtnDir; changed = true; }
        }

        if (changed) _rev++;


        byte selectedNode = EncodeNodeIndexOrNone((int)modeParams.selectedNodeIndex);
        if (_selectedNodeIndex != selectedNode) { _selectedNodeIndex = selectedNode; changed = true; }

        return changed;
    }

    private void ApplySyncedToLocals()
    {
        status = _status;
        activeModeId = _activeModeId;
        activeTranslateModeId = _activeTranslateModeId;
        activeProgramId = _activeProgramId;
        activeExecutorId = _activeExecutorId;
        executorPhase = _executorPhase;
        bodyAxisToPoint = _bodyAxisToPoint;
        rtnDir = _rtnDir;
        selectedNodeIndex = DecodeNodeIndexOrNone(_selectedNodeIndex);
        if (runtime != null)
        {
            runtime.status = _status;
            runtime.activeModeId = _activeModeId;
            runtime.activeTranslateModeId = _activeTranslateModeId;
            runtime.activeProgramId = _activeProgramId;
            runtime.activeExecutorId = _activeExecutorId;
            runtime.executorPhase = _executorPhase;
        }

        if (modeParams != null)
        {
            modeParams.bodyAxisToPoint = _bodyAxisToPoint;
            modeParams.rtnDir = _rtnDir;
            modeParams.selectedNodeIndex = (byte)((selectedNodeIndex >= 0) ? selectedNodeIndex : 0);
        }

        _appliedRev = _rev;
    }

    private void PublishNow()
    {
        _heartbeatAccum = 0f;
        _publishCooldown = minPublishInterval;
        ApplySyncedToLocals();
        RequestSerialization();
    }

    public void ForcePublish()
    {
        if (!HasAuthority()) return;

        bool changed = CaptureFromLocal();
        if (!changed) _rev++;

        PublishNow();
    }

    public override void OnDeserialization()
    {
        if (_rev == _appliedRev) return;
        ApplySyncedToLocals();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!HasAuthority()) return;

        // New owner republishes the current locally-applied state.
        CaptureFromLocal();
        PublishNow();
    }


    public void ResetPresentationState()
    {
        _publishCooldown = 0f;
        _heartbeatAccum = 0f;
        _appliedRev = -1;
    }

    public void ResetSyncedStateFromCurrent()
    {
        _publishCooldown = 0f;
        _heartbeatAccum = 0f;

        if (runtime != null)
        {
            _status = runtime.status;
            _activeModeId = runtime.activeModeId;
            _activeTranslateModeId = runtime.activeTranslateModeId;
            _activeProgramId = runtime.activeProgramId;
            _activeExecutorId = runtime.activeExecutorId;
            _executorPhase = runtime.executorPhase;
        }
        else
        {
            _status = 0;
            _activeModeId = 0;
            _activeTranslateModeId = 0;
            _activeProgramId = 0;
            _activeExecutorId = 0;
            _executorPhase = 0;
        }

        if (modeParams != null)
        {
            _bodyAxisToPoint = modeParams.bodyAxisToPoint;
            _rtnDir = modeParams.rtnDir;
            _selectedNodeIndex = EncodeNodeIndexOrNone((int)modeParams.selectedNodeIndex);
        }
        else
        {
            _bodyAxisToPoint = 0;
            _rtnDir = 0;
            _selectedNodeIndex = 255;
        }

        ApplySyncedToLocals();
    }

    private byte EncodeNodeIndexOrNone(int value)
    {
        if (value < 0 || value >= 255) return 255;
        return (byte)value;
    }

    private int DecodeNodeIndexOrNone(byte value)
    {
        return (value == 255) ? -1 : (int)value;
    }

}