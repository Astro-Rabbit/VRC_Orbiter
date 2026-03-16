using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GC_NodePlanNetState : UdonSharpBehaviour
{
    [Header("Authority")]
    public SimManager simManager;

    [Header("Source / target plan")]
    public NodePlanState plan;

    [Header("Publish")]
    public float minPublishInterval = 0.15f;
    public float heartbeatSeconds = 180.0f;

    [Header("Read-only mirror")]
    public int activeIndex = -1;

    [UdonSynced] private int _rev = 0;
    [UdonSynced] private int _activeIndex = -1;

    [UdonSynced] private byte[] _status;
    [UdonSynced] private byte[] _trigType;
    [UdonSynced] private double[] _triggerTime;
    [UdonSynced] private double[] _triggerNuRad;
    [UdonSynced] private Vector3[] _dV_E;
    [UdonSynced] private byte[] _bodyAxisToPoint;
    [UdonSynced] private float[] _preSlewLeadSec;
    [UdonSynced] private float[] _postHoldSec;
    [UdonSynced] private float[] _burnDurationSec;
    [UdonSynced] private float[] _burnThrottle01;

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
        EnsureNetArrays();

        if (HasAuthority())
        {
            CaptureFromPlan();
            ApplySyncedToPlan();
            ForcePublish();
        }
    }

    void Update()
    {
        if (_publishCooldown > 0f) _publishCooldown -= Time.deltaTime;

        if (!HasAuthority()) return;


        if (heartbeatSeconds > 0f)
        {
            _heartbeatAccum += Time.deltaTime;
            if (_heartbeatAccum >= heartbeatSeconds && _publishCooldown <= 0f)
            {
                PublishNow();
            }
        }
    }

    private void EnsureNetArrays()
    {
        int n = 1;
        if (plan != null && plan.maxNodes > 0) n = plan.maxNodes;

        if (_status == null || _status.Length != n) _status = new byte[n];
        if (_trigType == null || _trigType.Length != n) _trigType = new byte[n];
        if (_triggerTime == null || _triggerTime.Length != n) _triggerTime = new double[n];
        if (_triggerNuRad == null || _triggerNuRad.Length != n) _triggerNuRad = new double[n];
        if (_dV_E == null || _dV_E.Length != n) _dV_E = new Vector3[n];
        if (_bodyAxisToPoint == null || _bodyAxisToPoint.Length != n) _bodyAxisToPoint = new byte[n];
        if (_preSlewLeadSec == null || _preSlewLeadSec.Length != n) _preSlewLeadSec = new float[n];
        if (_postHoldSec == null || _postHoldSec.Length != n) _postHoldSec = new float[n];
        if (_burnDurationSec == null || _burnDurationSec.Length != n) _burnDurationSec = new float[n];
        if (_burnThrottle01 == null || _burnThrottle01.Length != n) _burnThrottle01 = new float[n];
    }

    private bool CaptureFromPlan()
    {
        if (plan == null) return false;

        plan.EnsureArrays();
        EnsureNetArrays();

        bool changed = false;
        int n = plan.maxNodes;

        if (_activeIndex != plan.activeIndex)
        {
            _activeIndex = plan.activeIndex;
            changed = true;
        }

        for (int i = 0; i < n; i++)
        {
            if (_status[i] != plan.status[i]) { _status[i] = plan.status[i]; changed = true; }
            if (_trigType[i] != plan.trigType[i]) { _trigType[i] = plan.trigType[i]; changed = true; }
            if (_triggerTime[i] != plan.triggerTime[i]) { _triggerTime[i] = plan.triggerTime[i]; changed = true; }
            if (_triggerNuRad[i] != plan.triggerNuRad[i]) { _triggerNuRad[i] = plan.triggerNuRad[i]; changed = true; }
            if (_dV_E[i] != plan.dV_E[i]) { _dV_E[i] = plan.dV_E[i]; changed = true; }
            if (_bodyAxisToPoint[i] != plan.bodyAxisToPoint[i]) { _bodyAxisToPoint[i] = plan.bodyAxisToPoint[i]; changed = true; }
            if (_preSlewLeadSec[i] != plan.preSlewLeadSec[i]) { _preSlewLeadSec[i] = plan.preSlewLeadSec[i]; changed = true; }
            if (_postHoldSec[i] != plan.postHoldSec[i]) { _postHoldSec[i] = plan.postHoldSec[i]; changed = true; }
            if (_burnDurationSec[i] != plan.burnDurationSec[i]) { _burnDurationSec[i] = plan.burnDurationSec[i]; changed = true; }
            if (_burnThrottle01[i] != plan.burnThrottle01[i]) { _burnThrottle01[i] = plan.burnThrottle01[i]; changed = true; }
        }

        if (changed) _rev++;
        return changed;
    }

    private void ApplySyncedToPlan()
    {
        if (plan == null) return;

        plan.EnsureArrays();
        EnsureNetArrays();

        int n = plan.maxNodes;

        plan.activeIndex = _activeIndex;
        activeIndex = _activeIndex;

        for (int i = 0; i < n; i++)
        {
            plan.status[i] = _status[i];
            plan.trigType[i] = _trigType[i];
            plan.triggerTime[i] = _triggerTime[i];
            plan.triggerNuRad[i] = _triggerNuRad[i];
            plan.dV_E[i] = _dV_E[i];
            plan.bodyAxisToPoint[i] = _bodyAxisToPoint[i];
            plan.preSlewLeadSec[i] = _preSlewLeadSec[i];
            plan.postHoldSec[i] = _postHoldSec[i];
            plan.burnDurationSec[i] = _burnDurationSec[i];
            plan.burnThrottle01[i] = _burnThrottle01[i];
        }

        _appliedRev = _rev;
    }

    private void PublishNow()
    {
        _heartbeatAccum = 0f;
        _publishCooldown = minPublishInterval;
        ApplySyncedToPlan();
        RequestSerialization();
    }

    public void ForcePublish()
    {
        if (!HasAuthority()) return;

        bool changed = CaptureFromPlan();
        if (!changed) _rev++;

        PublishNow();
    }

    public override void OnDeserialization()
    {
        if (_rev == _appliedRev) return;
        ApplySyncedToPlan();
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

        EnsureNetArrays();

        if (plan != null)
        {
            plan.EnsureArrays();

            _activeIndex = plan.activeIndex;

            int n = plan.maxNodes;
            for (int i = 0; i < n; i++)
            {
                _status[i] = plan.status[i];
                _trigType[i] = plan.trigType[i];
                _triggerTime[i] = plan.triggerTime[i];
                _triggerNuRad[i] = plan.triggerNuRad[i];
                _dV_E[i] = plan.dV_E[i];
                _bodyAxisToPoint[i] = plan.bodyAxisToPoint[i];
                _preSlewLeadSec[i] = plan.preSlewLeadSec[i];
                _postHoldSec[i] = plan.postHoldSec[i];
                _burnDurationSec[i] = plan.burnDurationSec[i];
                _burnThrottle01[i] = plan.burnThrottle01[i];
            }
        }
        else
        {
            _activeIndex = -1;

            int n = _status.Length;
            for (int i = 0; i < n; i++)
            {
                _status[i] = NodePlanState.STATUS_EMPTY;
                _trigType[i] = NodePlanState.TRIG_TIME;
                _triggerTime[i] = 0.0;
                _triggerNuRad[i] = 0.0;
                _dV_E[i] = Vector3.zero;
                _bodyAxisToPoint[i] = 2;
                _preSlewLeadSec[i] = 30f;
                _postHoldSec[i] = 5f;
                _burnDurationSec[i] = 0f;
                _burnThrottle01[i] = 0f;
            }
        }

        ApplySyncedToPlan();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!HasAuthority()) return;

        // New owner republishes the already-applied shared node plan.
        CaptureFromPlan();
        PublishNow();
    }
}