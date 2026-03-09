using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class CraftNetKinematics : UdonSharpBehaviour
{
    [Header("Wiring")]
    public SimClock clock;
    public CraftStateModel craft;
    public CraftNetState core;

    [Header("Publish rate")]
    [Tooltip("Kinematics publish rate (Hz) while integrated. 0 disables.")]
    public float kinHz = 20f;

    [Header("Owner sample time")]
    [Tooltip("Exact owner sim-time corresponding to the current craft state. SimManager should set this before publishing integrated snapshots.")]
    public double currentOwnerSimT = 0.0;

    [Tooltip("If true, publish currentOwnerSimT as the snapshot sim-time. If false, fallback to clock.Now().")]
    public bool useCurrentOwnerSimT = true;

    [Header("Remote reconstruction")]
    public bool remoteDeadReckon = true;

    [Tooltip("Clamp dt for dead-reckon / extrapolation to avoid huge jumps if packets stall (seconds).")]
    public float deadReckonClampSeconds = 0.25f;

    [Header("Remote interpolation (render sampling)")]
    [Tooltip("Snapshot ring buffer size (>=4 recommended).")]
    public int snapBufferSize = 8;

    private double[] _tBuf;     // network/server time
    private double[] _simTBuf;  // corresponding owner sim-time
    private double[] _rxBuf;
    private double[] _ryBuf;
    private double[] _rzBuf;
    private double[] _vxBuf;
    private double[] _vyBuf;
    private double[] _vzBuf;
    private int _bufCount = 0;
    private int _bufHead = 0;


    [Header("Read-only sampled render cache")]
    public double cachedSampleRenderNetT;
    public int cachedSampleFrame = -1;

    public double cachedSimRenderT;
    public double cachedRx, cachedRy, cachedRz;
    public double cachedVx, cachedVy, cachedVz;

    [UdonSynced] private int _rev;
    [UdonSynced] private double _epochT;    // NETWORK/server time for render buffering
    [UdonSynced] private double _simEpochT; // OWNER sim-time corresponding to this kinematic sample
    [UdonSynced] private double _rx, _ry, _rz;
    [UdonSynced] private double _vx, _vy, _vz;

    private float _accum;
    private int _appliedRev = -1;

    private float Period => (kinHz > 0f) ? (1f / kinHz) : 999999f;


    void Start()
    {
        int n = snapBufferSize;
        if (n < 4) n = 4;
        snapBufferSize = n;

        _tBuf = new double[n];
        _simTBuf = new double[n];
        _rxBuf = new double[n];
        _ryBuf = new double[n];
        _rzBuf = new double[n];
        _vxBuf = new double[n];
        _vyBuf = new double[n];
        _vzBuf = new double[n];
        _bufCount = 0;
        _bufHead = 0;
    }

    public void PublishKinematics()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (clock == null || craft == null || core == null) return;
        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;
        _accum = 0f;

        WriteSnapshotAndSerialize();
    }

    public void ForcePublishKinematics()
    {
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

        _rx = craft.rx; _ry = craft.ry; _rz = craft.rz;
        _vx = craft.vx; _vy = craft.vy; _vz = craft.vz;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;
    }

    public void ApplyRemoteKinematics()
    {
        if (Networking.IsOwner(gameObject)) return;
        if (clock == null || craft == null || core == null) return;
        if (core.GetMode() != CraftNetState.MODE_INTEGRATED) return;

        double rx = _rx, ry = _ry, rz = _rz;
        double vx = _vx, vy = _vy, vz = _vz;

        if (remoteDeadReckon)
        {
            double dt = clock.NowNetwork() - _epochT;
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

    /// <summary>
    /// Remote-only: sample buffered craft state for rendering at tRenderNet (NETWORK/server time).
    /// Also returns the matching interpolated owner sim-time for this sample.
    /// </summary>
    public void SampleRenderState(
        double tRenderNet,
        out double simRenderT,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        simRenderT = _simEpochT;
        rx = _rx; ry = _ry; rz = _rz;
        vx = _vx; vy = _vy; vz = _vz;

        if (_tBuf == null || _bufCount <= 0)
        {
            // if (remoteDeadReckon)
            // {
            //     double dt0 = tRenderNet - _epochT;
            //     double c0 = (double)deadReckonClampSeconds;
            //     if (dt0 >  c0) dt0 =  c0;
            //     if (dt0 < -c0) dt0 = -c0;

            //     rx += vx * dt0;
            //     ry += vy * dt0;
            //     rz += vz * dt0;
            //     simRenderT += dt0; // approximate matching sim-time forward too
            // }
            // return;
        }

        int n = snapBufferSize;
        int oldest = (_bufHead - _bufCount + n) % n;

        double tPrev = _tBuf[oldest];
        double simPrev = _simTBuf[oldest];
        double rxPrev = _rxBuf[oldest];
        double ryPrev = _ryBuf[oldest];
        double rzPrev = _rzBuf[oldest];
        double vxPrev = _vxBuf[oldest];
        double vyPrev = _vyBuf[oldest];
        double vzPrev = _vzBuf[oldest];

        if (tRenderNet <= tPrev)
        {
            simRenderT = simPrev;
            rx = rxPrev; ry = ryPrev; rz = rzPrev;
            vx = vxPrev; vy = vyPrev; vz = vzPrev;
            return;
        }

        for (int k = 1; k < _bufCount; k++)
        {
            int idx = (oldest + k) % n;
            double tCur = _tBuf[idx];

            if (tRenderNet <= tCur)
            {
                double dt = tCur - tPrev;
                double u = (dt > 1e-9) ? ((tRenderNet - tPrev) / dt) : 1.0;

                double simCur = _simTBuf[idx];
                double rxCur = _rxBuf[idx];
                double ryCur = _ryBuf[idx];
                double rzCur = _rzBuf[idx];
                double vxCur = _vxBuf[idx];
                double vyCur = _vyBuf[idx];
                double vzCur = _vzBuf[idx];

                simRenderT = simPrev + (simCur - simPrev) * u;

                double dtSeg = simCur - simPrev;
                if (dtSeg < 1e-9) dtSeg = 1e-9;
                double u2 = u * u;
                double u3 = u2 * u;

                double h00 =  2.0 * u3 - 3.0 * u2 + 1.0;
                double h10 =        u3 - 2.0 * u2 + u;
                double h01 = -2.0 * u3 + 3.0 * u2;
                double h11 =        u3 -       u2;

                rx = h00 * rxPrev + h10 * dtSeg * vxPrev + h01 * rxCur + h11 * dtSeg * vxCur;
                ry = h00 * ryPrev + h10 * dtSeg * vyPrev + h01 * ryCur + h11 * dtSeg * vyCur;
                rz = h00 * rzPrev + h10 * dtSeg * vzPrev + h01 * rzCur + h11 * dtSeg * vzCur;

                vx = vxPrev + (vxCur - vxPrev) * u;
                vy = vyPrev + (vyCur - vyPrev) * u;
                vz = vzPrev + (vzCur - vzPrev) * u;
                return;
            }

            tPrev = tCur;
            simPrev = _simTBuf[idx];
            rxPrev = _rxBuf[idx];
            ryPrev = _ryBuf[idx];
            rzPrev = _rzBuf[idx];
            vxPrev = _vxBuf[idx];
            vyPrev = _vyBuf[idx];
            vzPrev = _vzBuf[idx];
        }

        int newest = (_bufHead - 1 + n) % n;
        double tn = _tBuf[newest];

        simRenderT = _simTBuf[newest];
        rx = _rxBuf[newest];
        ry = _ryBuf[newest];
        rz = _rzBuf[newest];
        vx = _vxBuf[newest];
        vy = _vyBuf[newest];
        vz = _vzBuf[newest];

        if (remoteDeadReckon)
        {
            double dtEx = tRenderNet - tn;
            double c = (double)deadReckonClampSeconds;
            if (dtEx >  c) dtEx =  c;
            if (dtEx < -c) dtEx = -c;

            rx += vx * dtEx;
            ry += vy * dtEx;
            rz += vz * dtEx;
            simRenderT += dtEx; // approximate matching sim-time forward too
        }
    }

    public void UpdateRenderSampleCache(double tRenderNet)
    {
        if (cachedSampleFrame == Time.frameCount && cachedSampleRenderNetT == tRenderNet)
            return;

        cachedSampleRenderNetT = tRenderNet;
        cachedSampleFrame = Time.frameCount;

        SampleRenderState(
            tRenderNet,
            out cachedSimRenderT,
            out cachedRx, out cachedRy, out cachedRz,
            out cachedVx, out cachedVy, out cachedVz
        );
    }

    public override void OnPostSerialization(VRC.Udon.Common.SerializationResult result)
    {
        Debug.Log("[NetKin] success=" + result.success + " bytes=" + result.byteCount);
    }

    public override void OnDeserialization()
    {
        if (Networking.IsOwner(gameObject))
        {
            _appliedRev = _rev;
            return;
        }

        if (_tBuf == null || _tBuf.Length == 0) Start();

        int n = snapBufferSize;

        // Reject stale / duplicate revisions
        if (_rev <= _appliedRev)
            return;

        // Reject non-monotonic time inserts, since SampleRenderState assumes
        // the ring buffer is ordered by increasing _epochT.
        if (_bufCount > 0)
        {
            int newest = (_bufHead - 1 + n) % n;
            double lastT = _tBuf[newest];

            // Small epsilon to avoid duplicate-time inserts from precision noise.
            if (_epochT <= lastT + 1e-9)
            {
                _appliedRev = _rev;
                return;
            }
        }

        int i = _bufHead;

        _tBuf[i] = _epochT;
        _simTBuf[i] = _simEpochT;
        _rxBuf[i] = _rx;
        _ryBuf[i] = _ry;
        _rzBuf[i] = _rz;
        _vxBuf[i] = _vx;
        _vyBuf[i] = _vy;
        _vzBuf[i] = _vz;

        _bufHead = (i + 1) % n;
        if (_bufCount < n) _bufCount++;

        _appliedRev = _rev;
    }
}