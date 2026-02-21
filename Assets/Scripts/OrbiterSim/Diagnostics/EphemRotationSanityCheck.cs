using UdonSharp;
using UnityEngine;
using System;

public class EphemRotationSanityCheck : UdonSharpBehaviour
{
    public EphemSnapshot ephem;   // must already be updated each frame by EphemerisSystem

    [Header("Step for finite difference (seconds)")]
    public double dt = 10.0;

    [Header("Earth constants (match your EarthRotationModelSimple)")]
    public double obliquityRad = 23.4392911 * Math.PI / 180.0;
    public double earthOmegaRadSec = 7.2921150e-5;
    public double moonOmegaRadSec  = 2.6616995e-6;

    [Header("Logging")]
    public bool logEveryFrame = false;
    public float logHz = 1f;

    private float _accum;

    void Update()
    {
        if (ephem == null) return;

        if (!logEveryFrame)
        {
            _accum += Time.deltaTime;
            if (_accum < (1f / Mathf.Max(0.1f, logHz))) return;
            _accum = 0f;
        }

        // We need q(t) and q(t+dt).
        // If you only have snapshot at current time, easiest is:
        // - temporarily store last snapshot q and time
        // - approximate omega from successive frames
        // But here we’ll do a simple successive-frame FD using snapshot.t spacing.

        // So: use per-frame FD (recommended):
        // This script expects you to keep the previous sample internally.
        SamplePerFrameFD();
    }

    private bool _hasPrev = false;
    private double _tPrev;
    private Quaternion _qEarthPrev;
    private Quaternion _qMoonPrev;

    private double _exPrev, _eyPrev, _ezPrev, _evxPrev, _evyPrev, _evzPrev;
    private double _mxPrev, _myPrev, _mzPrev, _mvxPrev, _mvyPrev, _mvzPrev;

    private void SamplePerFrameFD()
    {
        double tNow = ephem.t;

        Quaternion qEarth = new Quaternion(ephem.earth_qx, ephem.earth_qy, ephem.earth_qz, ephem.earth_qw);
        Quaternion qMoon  = new Quaternion(ephem.moon_qx,  ephem.moon_qy,  ephem.moon_qz,  ephem.moon_qw);

        if (!_hasPrev)
        {
            _hasPrev = true;
            _tPrev = tNow;
            _qEarthPrev = qEarth;
            _qMoonPrev = qMoon;

            _exPrev = ephem.earth_rx; _eyPrev = ephem.earth_ry; _ezPrev = ephem.earth_rz;
            _evxPrev = ephem.earth_vx; _evyPrev = ephem.earth_vy; _evzPrev = ephem.earth_vz;

            _mxPrev = ephem.moon_rx; _myPrev = ephem.moon_ry; _mzPrev = ephem.moon_rz;
            _mvxPrev = ephem.moon_vx; _mvyPrev = ephem.moon_vy; _mvzPrev = ephem.moon_vz;

            return;
        }

        double dtReal = tNow - _tPrev;
        if (dtReal <= 1e-6) return;

        // ---- Earth ω from quaternion delta (in inertial/ecliptic coords) ----
        Vector3 wEarth_E = OmegaFromDelta(_qEarthPrev, qEarth, (float)dtReal);

        // Expected Earth spin axis in ecliptic inertial from your model:
        // s = (0, sin(eps), cos(eps))
        Vector3 sEarth_E = new Vector3(0f, (float)Math.Sin(obliquityRad), (float)Math.Cos(obliquityRad));
        sEarth_E.Normalize();

        float dotEarth = Vector3.Dot(wEarth_E, sEarth_E);
        float magEarth = wEarth_E.magnitude;

        // ---- Moon ω from quaternion delta ----
        Vector3 wMoon_E = OmegaFromDelta(_qMoonPrev, qMoon, (float)dtReal);

        // Expected Moon prograde axis: ĥ of Earth->Moon orbit (in ecliptic inertial)
        Vector3 rEM = new Vector3((float)(ephem.moon_rx - ephem.earth_rx),
                                 (float)(ephem.moon_ry - ephem.earth_ry),
                                 (float)(ephem.moon_rz - ephem.earth_rz));
        Vector3 vEM = new Vector3((float)(ephem.moon_vx - ephem.earth_vx),
                                 (float)(ephem.moon_vy - ephem.earth_vy),
                                 (float)(ephem.moon_vz - ephem.earth_vz));
        Vector3 h = Vector3.Cross(rEM, vEM);
        Vector3 hHat = (h.sqrMagnitude > 1e-12f) ? h.normalized : Vector3.forward;

        float dotMoon = Vector3.Dot(wMoon_E, hHat);
        float magMoon = wMoon_E.magnitude;

        Debug.Log(
            $"[RotCheck] dt={dtReal:F3}s | " +
            $"Earth: dot(w,s)={dotEarth:E3} mag(w)={magEarth:E3} (expected ~{earthOmegaRadSec:E3}) | " +
            $"Moon: dot(w,h)={dotMoon:E3} mag(w)={magMoon:E3} (expected ~{moonOmegaRadSec:E3})"
        );

        // update prev
        _tPrev = tNow;
        _qEarthPrev = qEarth;
        _qMoonPrev = qMoon;
    }

    // dq = q1 * inv(q0); axis-angle from dq; ω = axis * angle/dt
    // Returns ω in the same frame as the quaternion (your ecliptic inertial).
    private Vector3 OmegaFromDelta(Quaternion q0, Quaternion q1, float dt)
    {
        Quaternion dq = q1 * Quaternion.Inverse(q0);

        // Ensure shortest path
        if (dq.w < 0f)
        {
            dq.x = -dq.x; dq.y = -dq.y; dq.z = -dq.z; dq.w = -dq.w;
        }

        dq.Normalize();

        float angle = 2f * Mathf.Acos(Mathf.Clamp(dq.w, -1f, 1f)); // radians
        float s = Mathf.Sqrt(Mathf.Max(0f, 1f - dq.w * dq.w));

        Vector3 axis;
        if (s < 1e-6f || angle < 1e-6f)
        {
            axis = Vector3.zero;
            angle = 0f;
        }
        else
        {
            axis = new Vector3(dq.x / s, dq.y / s, dq.z / s);
        }

        return (dt > 1e-6f) ? axis * (angle / dt) : Vector3.zero;
    }
}
