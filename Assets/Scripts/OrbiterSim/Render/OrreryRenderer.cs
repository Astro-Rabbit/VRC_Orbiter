using UdonSharp;
using UnityEngine;

public class OrreryRenderer : UdonSharpBehaviour
{
    [Header("Ephemeris")]
    public EphemSnapshot ephem;

    [Header("Body Transforms")]
    public Transform sun;
    public Transform earth;
    public Transform moon;

    [Header("Craft (optional)")]
    public CraftStateModel craftState;
    public CraftAttitudeState craftAtt;
    public Transform craft;                 // craft proxy transform in the orrery
    public bool showCraft = true;
    public float craftVisualScale = 0.25f;  // purely visual

    [Header("Scaling")]
    [Tooltip("Meters in sim / meters in Unity. Example: 1e7 means 10,000 km becomes 1 Unity unit.")]
    public double metersPerUnity = 1.0e7;

    [Tooltip("If true, subtract Earth's position so Earth sits at origin (nice for local view).")]
    public bool earthCenteredView = false;

    [Header("Visual sizes (Unity units, NOT physical)")]
    public float sunVisualRadius = 2.0f;
    public float earthVisualRadius = 1.0f;
    public float moonVisualRadius = 0.5f;

    [Header("Orientation")]
    [Tooltip("Apply Earth/Moon rotation quaternions from ephemeris snapshot.")]
    public bool applyBodyRotation = true;

    [Tooltip("If true, also rotate the Sun (usually leave false unless you model sun rotation).")]
    public bool rotateSun = false;

    [Tooltip("Apply craft attitude from CraftAttitudeState.")]
    public bool applyCraftRotation = true;

    [Header("Offsets")]
    [Tooltip("Optional offset in Unity space for the whole orrery.")]
    public Vector3 worldOffset = Vector3.zero;

    [Header("Handedness Fix")]
    [Tooltip("Enable this if your pole directions look right but all orbit/spin senses are reversed.")]
    public bool flipHandedness = true;

    [Tooltip("0=X, 1=Y, 2=Z. Try Z first, then X. Avoid Y if you want to preserve north up.")]
    [Range(0, 2)]
    public int flipAxis = 2;

    // Cached mapping so positions and rotations share the same transform
    private Quaternion _Qmap;
    private Quaternion _Qinv;

    void Start()
    {
        CacheMap();
        ApplyVisualSizes();

        if (craft != null)
            craft.localScale = Vector3.one * craftVisualScale;
    }

    void Update()
    {
        Apply();
    }

    private void CacheMap()
    {
        // Presentation mapping (pure rotation). Handedness flip is applied separately.
        _Qmap = Quaternion.AngleAxis(90f, Vector3.right) * Quaternion.AngleAxis(180f, Vector3.up);
        _Qinv = Quaternion.Inverse(_Qmap);
    }

    private Vector3 ApplyFlip(Vector3 v)
    {
        if (!flipHandedness) return v;

        // 0=X, 1=Y, 2=Z
        if (flipAxis == 0) v.x = -v.x;
        else if (flipAxis == 1) v.y = -v.y;
        else v.z = -v.z;

        return v;
    }

    private Vector3 MapDirSolverToUnity(float x, float y, float z)
    {
        // Direction mapping: u = Qmap * s; then optional reflection in Unity axes.
        Vector3 s = new Vector3(x, y, z);
        Vector3 u = _Qmap * s;
        return ApplyFlip(u);
    }

    private Vector3 MapPosSolverToUnity(double x, double y, double z)
    {
        Vector3 s = new Vector3((float)x, (float)y, (float)z);
        Vector3 u = _Qmap * s;
        return ApplyFlip(u);
    }

    // When flipHandedness is active, you cannot map rotations with q' = Q*q*Q^-1.
    // Instead: map basis vectors and rebuild a quaternion from that basis.
    private Quaternion MapRotationSolverToUnity(Quaternion qSolver)
    {
        // Convert solver quaternion to solver-space basis vectors (columns of rotation matrix).
        float xx = qSolver.x * qSolver.x;
        float yy = qSolver.y * qSolver.y;
        float zz = qSolver.z * qSolver.z;
        float xy = qSolver.x * qSolver.y;
        float xz = qSolver.x * qSolver.z;
        float yz = qSolver.y * qSolver.z;
        float wx = qSolver.w * qSolver.x;
        float wy = qSolver.w * qSolver.y;
        float wz = qSolver.w * qSolver.z;

        Vector3 xS = new Vector3(1f - 2f * (yy + zz), 2f * (xy + wz),       2f * (xz - wy));
        Vector3 yS = new Vector3(2f * (xy - wz),       1f - 2f * (xx + zz), 2f * (yz + wx));
        Vector3 zS = new Vector3(2f * (xz + wy),       2f * (yz - wx),       1f - 2f * (xx + yy));

        Vector3 xU = MapDirSolverToUnity(xS.x, xS.y, xS.z);
        Vector3 yU = MapDirSolverToUnity(yS.x, yS.y, yS.z);
        Vector3 zU = MapDirSolverToUnity(zS.x, zS.y, zS.z);

        // Re-orthonormalize defensively
        if (xU.sqrMagnitude < 1e-12f) xU = Vector3.right;
        if (yU.sqrMagnitude < 1e-12f) yU = Vector3.up;

        xU.Normalize();

        // Rebuild a consistent RH basis in Unity
        zU = Vector3.Cross(xU, yU);
        if (zU.sqrMagnitude < 1e-12f)
            zU = MapDirSolverToUnity(zS.x, zS.y, zS.z);
        zU.Normalize();

        yU = Vector3.Cross(zU, xU).normalized;

        // Unity expects LookRotation(forward, up)
        return Quaternion.LookRotation(zU, yU);
    }
    public Vector3 MapWorldMetersToUnity(double wx, double wy, double wz)
    {
        if (metersPerUnity <= 0.0) metersPerUnity = 1.0;
        CacheMap();

        // Map absolute solver meters -> Unity, including handedness flip
        Vector3 p = MapPosSolverToUnity(wx, wy, wz);

        // If orrery is in Earth-centered mode, apply the same recentering
        if (earthCenteredView && ephem != null)
        {
            Vector3 eP = MapPosSolverToUnity(ephem.earth_rx, ephem.earth_ry, ephem.earth_rz);
            p -= eP;
        }

        float inv = (float)(1.0 / metersPerUnity);
        return p * inv + worldOffset;
    }

    public float MetersToUnityScale()
    {
        if (metersPerUnity <= 0.0) metersPerUnity = 1.0;
        return (float)(1.0 / metersPerUnity);
    }

    public void Apply()
    {
        if (ephem == null) return;
        if (metersPerUnity <= 0.0) metersPerUnity = 1.0;

        CacheMap();

        float inv = (float)(1.0 / metersPerUnity);

        // --- Positions in solver frame (heliocentric / ecliptic inertial) ---
        Vector3 sunP   = MapPosSolverToUnity(ephem.sun_rx,   ephem.sun_ry,   ephem.sun_rz);
        Vector3 earthP = MapPosSolverToUnity(ephem.earth_rx, ephem.earth_ry, ephem.earth_rz);
        Vector3 moonP  = MapPosSolverToUnity(ephem.moon_rx,  ephem.moon_ry,  ephem.moon_rz);

        Vector3 craftP = Vector3.zero;
        bool hasCraft = showCraft && craft != null && craftState != null;
        if (hasCraft)
            craftP = MapPosSolverToUnity(craftState.rx, craftState.ry, craftState.rz);

        // --- Optional Earth-centered view ---
        if (earthCenteredView)
        {
            sunP   -= earthP;
            moonP  -= earthP;
            if (hasCraft) craftP -= earthP;
            earthP = Vector3.zero;
        }

        // --- Scale + offset ---
        Vector3 off = worldOffset;

        if (sun != null)   sun.position   = sunP   * inv + off;
        if (earth != null) earth.position = earthP * inv + off;
        if (moon != null)  moon.position  = moonP  * inv + off;

        if (hasCraft)
            craft.position = craftP * inv + off;

        // --- Rotations ---
        if (applyBodyRotation)
        {
            if (earth != null)
            {
                Quaternion qSolver = new Quaternion(ephem.earth_qx, ephem.earth_qy, ephem.earth_qz, ephem.earth_qw);
                earth.rotation = flipHandedness ? MapRotationSolverToUnity(qSolver) : (_Qmap * qSolver * _Qinv);
            }

            if (moon != null)
            {
                Quaternion qSolver = new Quaternion(ephem.moon_qx, ephem.moon_qy, ephem.moon_qz, ephem.moon_qw);
                moon.rotation = flipHandedness ? MapRotationSolverToUnity(qSolver) : (_Qmap * qSolver * _Qinv);
            }

            if (rotateSun && sun != null)
            {
                // no-op for now
            }
        }

        if (applyCraftRotation && craft != null && craftAtt != null)
        {
            Quaternion qSolver = craftAtt.qBE;
            craft.rotation = flipHandedness ? MapRotationSolverToUnity(qSolver) : (_Qmap * qSolver * _Qinv);
        }
    }

    public void ApplyVisualSizes()
    {
        if (sun != null)   sun.localScale   = Vector3.one * (sunVisualRadius * 2.0f);
        if (earth != null) earth.localScale = Vector3.one * (earthVisualRadius * 2.0f);
        if (moon != null)  moon.localScale  = Vector3.one * (moonVisualRadius * 2.0f);

        if (craft != null)
            craft.localScale = Vector3.one * craftVisualScale;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (metersPerUnity < 1.0) metersPerUnity = 1.0;
        ApplyVisualSizes();
        if (flipAxis < 0) flipAxis = 0;
        if (flipAxis > 2) flipAxis = 2;
    }
#endif
}
