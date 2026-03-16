using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CraftNetAttitude : UdonSharpBehaviour
{
    [Header("Wiring")]
    public SimClock clock;
    public CraftAttitudeState att;
    public SimManager simManager;


    [Header("Publish rate")]
    [Tooltip("Attitude publish rate (Hz). Works in both rails and integrated.")]
    public float attHz = 15f;

    [Header("Remote apply")]
    [Tooltip("If > 0, slerp toward received attitude at this rate (1/sec). 0 = hard set.")]
    public float slerpRate = 0f;


    // -------------------------
    // Remote interpolation buffer (render-only)
    // -------------------------
    [Header("Remote interpolation (render sampling)")]
    [Tooltip("Render this many seconds behind remote time to enable interpolation.")]
    public float interpBackTimeSeconds = 0.25f;

    [Tooltip("Max seconds to extrapolate beyond newest sample.")]
    public float extrapClampSeconds = 0.25f;

    [Tooltip("Snapshot ring buffer size (>=4 recommended).")]
    public int snapBufferSize = 8;

    private double[] _tBuf;
    private Quaternion[] _qBuf;
    private Vector3[] _wBuf;
    
    private int _bufCount = 0;
    private int _bufHead = 0; // next write index



    // --- synced attitude ---
    [UdonSynced] private int _rev;
    [UdonSynced] private double _epochT;
    [UdonSynced] private float _qX, _qY, _qZ, _qW;
    [UdonSynced] private float _wX, _wY, _wZ;

    private float _accum;
    private int _appliedRev = -1;

    private float Period => (attHz > 0f) ? (1f / attHz) : 999999f;


    private bool HasSimAuthority()
    {
        return simManager != null && simManager.IsSimOwner();
    }

    void Start()
    {
        int n = snapBufferSize;
        if (n < 4) n = 4;
        snapBufferSize = n;

        _tBuf = new double[n];
        _qBuf = new Quaternion[n];
        _wBuf = new Vector3[n];
        _bufCount = 0;
        _bufHead = 0;
    }


    /// <summary>Owner: publish attitude at cadence. Safe to call every frame.</summary>
    public void PublishAttitude()
    {
        if (!HasSimAuthority()) return;
        if (!Networking.IsOwner(gameObject)) return;
        if (att == null || clock == null) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;
        _accum = 0f;

        _epochT = clock.NowNetwork();

        Quaternion q = att.qBE;
        _qX = q.x; _qY = q.y; _qZ = q.z; _qW = q.w;

        _wX = (float)att.wx;
        _wY = (float)att.wy;
        _wZ = (float)att.wz;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;
    }

    /// <summary>Remote: apply attitude each frame (or on deserialization if you prefer).</summary>
    public void ApplyRemoteAttitude()
    {
        if (HasSimAuthority()) return;
        if (att == null) return;

        Quaternion target = new Quaternion(_qX, _qY, _qZ, _qW);

        if (slerpRate > 0f)
        {
            float t = 1f - Mathf.Exp(-slerpRate * Time.deltaTime);
            att.qBE = Quaternion.Slerp(att.qBE, target, t);
        }
        else
        {
            att.qBE = target;
        }

        att.wx = _wX;
        att.wy = _wY;
        att.wz = _wZ;
    }

    public void ForcePublishAttitude()
    {
        if (!HasSimAuthority()) return;    
        if (!Networking.IsOwner(gameObject)) return;
        if (att == null || clock == null) return;

        _accum = 0f;
        _epochT = clock.NowNetwork();

        Quaternion q = att.qBE;
        _qX = q.x; _qY = q.y; _qZ = q.z; _qW = q.w;

        _wX = (float)att.wx;
        _wY = (float)att.wy;
        _wZ = (float)att.wz;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;
    }



    // -------------------------
    // Render sampling API
    // -------------------------

    /// <summary>
    /// Remote-only: sample buffered attitude for rendering at tRender (SimClock time).
    /// If insufficient history, returns latest received.
    /// </summary>
    public Quaternion SampleRenderQuaternion(double tRender)
    {
        // Fallback to latest received if no buffer
        Quaternion latest = new Quaternion(_qX, _qY, _qZ, _qW);

        if (_tBuf == null || _bufCount <= 0) return latest;

        int n = snapBufferSize;
        int oldest = (_bufHead - _bufCount + n) % n;

        // Before oldest -> hold oldest
        double tPrev = _tBuf[oldest];
        Quaternion qPrev = _qBuf[oldest];
        Vector3 wPrev = _wBuf[oldest];

        if (tRender <= tPrev) return qPrev;

        // Find bracket
        for (int k = 1; k < _bufCount; k++)
        {
            int idx = (oldest + k) % n;
            double tCur = _tBuf[idx];

            if (tRender <= tCur)
            {
                double dt = tCur - tPrev;
                float u = (dt > 1e-6) ? (float)((tRender - tPrev) / dt) : 1f;

                Quaternion qCur = _qBuf[idx];
                // Ensure shortest-arc slerp (Unity handles it, but we keep it clean)
                return Quaternion.Slerp(qPrev, qCur, u);
            }

            tPrev = tCur;
            qPrev = _qBuf[idx];
            wPrev = _wBuf[idx];
        }

        // Past newest -> short extrapolate from newest using omega
        int newest = (_bufHead - 1 + n) % n;
        double tn = _tBuf[newest];
        Quaternion qN = _qBuf[newest];
        Vector3 wN = _wBuf[newest];

        double dtEx = tRender - tn;
        double c = (double)extrapClampSeconds;
        if (dtEx >  c) dtEx =  c;
        if (dtEx < -c) dtEx = -c;

        return IntegrateQuaternion(qN, wN, (float)dtEx);
    }

    private Quaternion IntegrateQuaternion(Quaternion q, Vector3 omegaRadPerSec, float dt)
    {
        // dq ~ exp(0.5 * omega * dt). Use angle-axis approximation.
        float wmag = omegaRadPerSec.magnitude;
        float angle = wmag * Mathf.Abs(dt);

        if (angle < 1e-7f) return q;

        Vector3 axis = omegaRadPerSec / wmag;
        Quaternion dq = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);

        if (dt < 0f) dq = new Quaternion(-dq.x, -dq.y, -dq.z, dq.w); // inverse for negative dt

        // q evolves by right-multiplying body-frame delta (typical for BODY rates)
        return q * dq;
    }

    public Vector3 SampleRenderOmegaB(double tRender)
    {
        Vector3 latest = new Vector3(_wX, _wY, _wZ);

        if (_tBuf == null || _bufCount <= 0) return latest;

        int n = snapBufferSize;
        int oldest = (_bufHead - _bufCount + n) % n;

        double tPrev = _tBuf[oldest];
        Vector3 wPrev = _wBuf[oldest];

        if (tRender <= tPrev) return wPrev;

        for (int k = 1; k < _bufCount; k++)
        {
            int idx = (oldest + k) % n;
            double tCur = _tBuf[idx];

            if (tRender <= tCur)
            {
                double dt = tCur - tPrev;
                float u = (dt > 1e-6) ? (float)((tRender - tPrev) / dt) : 1f;
                return Vector3.Lerp(wPrev, _wBuf[idx], u);
            }

            tPrev = tCur;
            wPrev = _wBuf[idx];
        }

        int newest = (_bufHead - 1 + n) % n;
        return _wBuf[newest];
    }


    public override void OnDeserialization()
    {
        // Keep your current preference: SimManager calls ApplyRemoteAttitude() per-frame
        // But we DO buffer the received sample for render interpolation.
        if (HasSimAuthority()) { _appliedRev = _rev; return; }

        if (_tBuf == null || _tBuf.Length == 0) Start();

        // Push snapshot into ring buffer
        int n = snapBufferSize;
        int i = _bufHead;

        _tBuf[i] = _epochT;
        _qBuf[i] = new Quaternion(_qX, _qY, _qZ, _qW);
        _wBuf[i] = new Vector3(_wX, _wY, _wZ);

        _bufHead = (i + 1) % n;
        if (_bufCount < n) _bufCount++;

        _appliedRev = _rev;
    }

    public void ResetPresentationState()
    {
        _accum = 0f;
        _appliedRev = -1;

        if (_tBuf != null)
        {
            int n = _tBuf.Length;
            for (int i = 0; i < n; i++)
            {
                _tBuf[i] = 0.0;
                _qBuf[i] = Quaternion.identity;
                _wBuf[i] = Vector3.zero;
            }
        }

        _bufCount = 0;
        _bufHead = 0;
    }

    public void ResetSyncedStateFromCurrent()
    {
        _accum = 0f;

        if (att != null)
        {
            Quaternion q = att.qBE;
            _qX = q.x; _qY = q.y; _qZ = q.z; _qW = q.w;

            _wX = (float)att.wx;
            _wY = (float)att.wy;
            _wZ = (float)att.wz;
        }
        else
        {
            _qX = 0f; _qY = 0f; _qZ = 0f; _qW = 1f;
            _wX = 0f; _wY = 0f; _wZ = 0f;
        }

        _epochT = (clock != null) ? clock.NowNetwork() : Networking.GetServerTimeInSeconds();

        ResetPresentationState();
    }


}
