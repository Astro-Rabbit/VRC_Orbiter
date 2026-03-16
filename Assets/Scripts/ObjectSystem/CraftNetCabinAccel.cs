using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CraftNetCabinAccel : UdonSharpBehaviour
{
    [Header("Wiring")]
    public SimClock clock;
    public SimManager simManager;
    public CraftNetState core;

    [Header("Publish rate")]
    [Tooltip("Body-frame felt translational acceleration publish rate (Hz) during integrated mode.")]
    public float accelHz = 10f;

    [Header("Owner sample")]
    [Tooltip("Owner-populated felt translational acceleration in BODY frame.")]
    public Vector3 currentOwnerAccelB = Vector3.zero;

    [Tooltip("Owner-populated sim time corresponding to currentOwnerAccelB.")]
    public double currentOwnerSimT = 0.0;

    [Header("Remote interpolation")]
    [Tooltip("Render this many seconds behind remote time to enable interpolation.")]
    public float interpBackTimeSeconds = 0.2f;

    [Tooltip("Snapshot ring buffer size (>=4 recommended).")]
    public int snapBufferSize = 8;

    [Header("Read-only latest raw")]
    public int rawRevision = -1;
    public double rawEpochT = 0.0;
    public Vector3 rawAccelB = Vector3.zero;

    // --- synced ---
    [UdonSynced] private int _rev;
    [UdonSynced] private double _epochT;
    [UdonSynced] private float _ax, _ay, _az;

    // --- ring buffer ---
    private double[] _tBuf;
    private Vector3[] _aBuf;
    private int _bufCount = 0;
    private int _bufHead = 0;

    private float _accum = 0f;
    private int _appliedRev = -1;

    private float Period => (accelHz > 0f) ? (1f / accelHz) : 999999f;

    private bool HasSimAuthority()
    {
        return simManager != null && simManager.IsSimOwner();
    }

    private void EnsureBuffers()
    {
        int n = snapBufferSize;
        if (n < 4) n = 4;
        if (_tBuf != null && _tBuf.Length == n) return;

        snapBufferSize = n;
        _tBuf = new double[n];
        _aBuf = new Vector3[n];
        _bufCount = 0;
        _bufHead = 0;
    }

    void Start()
    {
        EnsureBuffers();
    }

    public void PublishAccel()
    {
        if (!HasSimAuthority()) return;
        if (!Networking.IsOwner(gameObject)) return;
        if (clock == null || core == null) return;
        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;
        _accum = 0f;

        WriteSnapshotAndSerialize();
    }

    public void ForcePublishAccel()
    {
        if (!HasSimAuthority()) return;
        if (!Networking.IsOwner(gameObject)) return;
        if (clock == null || core == null) return;
        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        _accum = 0f;
        WriteSnapshotAndSerialize();
    }

    private void WriteSnapshotAndSerialize()
    {
        _epochT = clock.ServerTimeForSimTime(currentOwnerSimT);

        _ax = currentOwnerAccelB.x;
        _ay = currentOwnerAccelB.y;
        _az = currentOwnerAccelB.z;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;

        rawRevision = _rev;
        rawEpochT = _epochT;
        rawAccelB = currentOwnerAccelB;
    }

    public Vector3 GetImmediateOwnerAccelB()
    {
        return currentOwnerAccelB;
    }

    public Vector3 GetLatestReceivedAccelB()
    {
        return new Vector3(_ax, _ay, _az);
    }

    public Vector3 SampleRenderAccelB(double tRender)
    {
        EnsureBuffers();

        if (_bufCount <= 0)
            return new Vector3(_ax, _ay, _az);

        int n = snapBufferSize;
        int oldest = (_bufHead - _bufCount + n) % n;

        double tPrev = _tBuf[oldest];
        Vector3 aPrev = _aBuf[oldest];

        if (tRender <= tPrev)
            return aPrev;

        for (int k = 1; k < _bufCount; k++)
        {
            int idx = (oldest + k) % n;
            double tCur = _tBuf[idx];

            if (tRender <= tCur)
            {
                double dt = tCur - tPrev;
                float u = (dt > 1e-6) ? (float)((tRender - tPrev) / dt) : 1f;
                return Vector3.Lerp(aPrev, _aBuf[idx], u);
            }

            tPrev = tCur;
            aPrev = _aBuf[idx];
        }

        int newest = (_bufHead - 1 + n) % n;
        return _aBuf[newest];
    }

    public void ForceZeroAccel(double simT)
    {
        if (!HasSimAuthority()) return;
        if (!Networking.IsOwner(gameObject)) return;
        if (clock == null) return;

        _accum = 0f;

        currentOwnerAccelB = Vector3.zero;
        currentOwnerSimT = simT;

        _epochT = clock.ServerTimeForSimTime(currentOwnerSimT);
        _ax = 0f;
        _ay = 0f;
        _az = 0f;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;

        rawRevision = _rev;
        rawEpochT = _epochT;
        rawAccelB = Vector3.zero;
    }
    public override void OnDeserialization()
    {
        if (HasSimAuthority())
        {
            _appliedRev = _rev;
            return;
        }

        if (_rev <= _appliedRev)
            return;

        EnsureBuffers();

        rawRevision = _rev;
        rawEpochT = _epochT;
        rawAccelB = new Vector3(_ax, _ay, _az);

        int n = snapBufferSize;
        int i = _bufHead;

        _tBuf[i] = _epochT;
        _aBuf[i] = rawAccelB;

        _bufHead = (i + 1) % n;
        if (_bufCount < n) _bufCount++;

        _appliedRev = _rev;
    }
}