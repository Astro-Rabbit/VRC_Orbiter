using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class MoonKeplerOrbitDriver : UdonSharpBehaviour
{
    [Header("Target skybox material (instance)")]
    public Material skyboxMat;

    [Header("Grav parameter (Moon)")]
    public float muMoon = 4.9048695e12f; // m^3/s^2

    [Header("Orbit elements at epoch (Moon-centered)")]
    [Tooltip("Semi-major axis (m). For ~100km circular: a = Rmoon + 100000")]
    public float aMeters = 1827400f;

    [Range(0f, 0.99f)]
    public float e = 0.1f;

    [Tooltip("Inclination (deg)")]
    public float iDeg = 0f;

    [Tooltip("RAAN Ω (deg)")]
    public float raanDeg = 0f;

    [Tooltip("Argument of periapsis ω (deg)")]
    public float argPeriDeg = 0f;

    [Tooltip("Mean anomaly at epoch M0 (deg)")]
    public float M0Deg = 0f;

    [Header("Time / sync")]
    public float timeScale = 1f;

    [Tooltip("If 0, uses the first time Start() runs.")]
    public long epochServerMs = 0;

    [Header("Apply Moon body orientation (matches your shader controls)")]
    public bool applyMoonBodyRotation = true;
    public float moonYawDeg = 0f;   // about +Z
    public float moonPitchDeg = 0f; // about +X
    public float moonRollDeg = 0f;  // about +Y

    // Shader property IDs
    public string _MoonCenterWS_ID;

    // Cached radians
    private float iRad, raanRad, argPeriRad, M0Rad;

    void Start()
    {
        // _MoonCenterWS_ID = Shader.PropertyToID("_MoonCenterWS");

        if (epochServerMs == 0)
            epochServerMs = Networking.GetServerTimeInMilliseconds();

        CacheAngles();
    }

    void OnValidate()
    {
        CacheAngles();
    }

    private void CacheAngles()
    {
        iRad = iDeg * Mathf.Deg2Rad;
        raanRad = raanDeg * Mathf.Deg2Rad;
        argPeriRad = argPeriDeg * Mathf.Deg2Rad;
        M0Rad = M0Deg * Mathf.Deg2Rad;
    }

    void LateUpdate()
    {
        if (skyboxMat == null) return;

        // Basic guards
        float a = Mathf.Max(aMeters, 1f);
        float ecc = Mathf.Clamp(e, 0f, 0.9999f);

        long nowMs = Networking.GetServerTimeInMilliseconds();
        float t = (nowMs - epochServerMs) * 0.001f * timeScale;

        // Mean motion
        float n = Mathf.Sqrt(muMoon / (a * a * a)); // rad/s

        // Mean anomaly
        float M = M0Rad + n * t;
        M = WrapPi(M);

        // Solve Kepler for E
        float E = SolveKeplerElliptic(M, ecc);

        // True anomaly nu
        float sinE2 = Mathf.Sin(E * 0.5f);
        float cosE2 = Mathf.Cos(E * 0.5f);
        float s = Mathf.Sqrt(1f + ecc) * sinE2;
        float c = Mathf.Sqrt(1f - ecc) * cosE2;
        float nu = 2f * Mathf.Atan2(s, c);

        // Radius
        float r = a * (1f - ecc * Mathf.Cos(E));

        // Position in PQW (perifocal)
        float cosNu = Mathf.Cos(nu);
        float sinNu = Mathf.Sin(nu);
        Vector3 rPQW = new Vector3(r * cosNu, r * sinNu, 0f);

        // Rotate PQW -> reference frame: Rz(Ω)*Rx(i)*Rz(ω)
        Vector3 rRef = RotatePQWToRef(rPQW, raanRad, iRad, argPeriRad);

        // Optional final rotation to match your shader's Moon body orientation
        Vector3 rWorld = rRef;
        if (applyMoonBodyRotation)
        {
            Quaternion qYaw   = Quaternion.AngleAxis(moonYawDeg,   Vector3.forward); // +Z
            Quaternion qPitch = Quaternion.AngleAxis(moonPitchDeg, Vector3.right);   // +X
            Quaternion qRoll  = Quaternion.AngleAxis(moonRollDeg,  Vector3.up);      // +Y
            Quaternion qBodyToWorld = qRoll * qPitch * qYaw;
            rWorld = qBodyToWorld * rRef;
        }

        // Feed shader: camera->moon = -(moon->camera)
        Vector3 moonCenterFromCamera = -rWorld;

        skyboxMat.SetVector(_MoonCenterWS_ID, new Vector4(
            moonCenterFromCamera.x,
            moonCenterFromCamera.y,
            moonCenterFromCamera.z,
            1f
        ));
    }

    // --- Helpers ---

    // Wrap angle to [-pi, pi]
    private float WrapPi(float x)
    {
        const float TWO_PI = 6.28318530718f;
        x = x % TWO_PI;
        if (x > Mathf.PI) x -= TWO_PI;
        if (x < -Mathf.PI) x += TWO_PI;
        return x;
    }

    // Newton solve for E in M = E - e sin E (elliptic)
    private float SolveKeplerElliptic(float M, float e)
    {
        // Good initial guess:
        // For small e, E ~ M. For higher e, use a slightly better heuristic.
        float E = (e < 0.8f) ? M : Mathf.PI * Mathf.Sign(M);

        // 6-8 iterations is plenty for visuals
        for (int k = 0; k < 8; k++)
        {
            float f = E - e * Mathf.Sin(E) - M;
            float fp = 1f - e * Mathf.Cos(E);
            float dE = f / Mathf.Max(fp, 1e-6f);
            E -= dE;

            if (Mathf.Abs(dE) < 1e-6f) break;
        }
        return E;
    }

    // Apply Rz(Ω)*Rx(i)*Rz(ω) to PQW vector
    private Vector3 RotatePQWToRef(Vector3 v, float Om, float inc, float w)
    {
        // Rz(w)
        float cw = Mathf.Cos(w); float sw = Mathf.Sin(w);
        float x1 = cw * v.x - sw * v.y;
        float y1 = sw * v.x + cw * v.y;
        float z1 = v.z;

        // Rx(i)
        float ci = Mathf.Cos(inc); float si = Mathf.Sin(inc);
        float x2 = x1;
        float y2 = ci * y1 - si * z1;
        float z2 = si * y1 + ci * z1;

        // Rz(Ω)
        float cO = Mathf.Cos(Om); float sO = Mathf.Sin(Om);
        float x3 = cO * x2 - sO * y2;
        float y3 = sO * x2 + cO * y2;
        float z3 = z2;

        return new Vector3(x3, y3, z3);
    }
}
