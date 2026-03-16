using UdonSharp;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class OrreryCraftOrbitRibbon : UdonSharpBehaviour
{

    [Header("Display Enable")]
    public bool displayEnabled = true;

    [Header("References")]
    public OrreryController orrery;
    public GuidanceNavCoreState nav;
    public BodyCatalog bodies;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;

    [Header("Sampling")]
    [Range(16, 512)]
    public int ellipseSegments = 180;

    [Range(16, 256)]
    public int hyperbolaSegments = 96;

    [Tooltip("Shrink hyperbolic anomaly limit slightly to avoid huge endpoint excursions.")]
    [Range(0.5f, 0.999f)]
    public float hyperbolaNuLimitScale = 0.96f;

    [Header("Numerics")]
    [Tooltip("Treat orbits below this eccentricity as circular for basis construction.")]
    public double circularETol = 1e-3;

    [Tooltip("Extra margin around e=1 for orbit-type classification.")]
    public double parabolicTol = 1e-4;

    [Header("Rebuild Policy")]
    [Tooltip("Maximum rebuild rate in Hz.")]
    public float maxRebuildHz = 5.0f;

    [Header("Orbit Change Thresholds")]
    public double eccentricityThreshold = 1e-4;
    public double pRelativeThreshold = 1e-3;
    public float normalAngleThresholdDeg = 0.5f;
    public float periapsisAngleThresholdDeg = 1.0f;

    [Header("Orrery View Change Thresholds")]
    [Tooltip("Relative change in orrery scene scale required to force ribbon rebuild.")]
    public float sceneScaleRelativeThreshold = 0.05f;

    [Header("Visibility")]
    public bool hideWhenInvalid = true;



    [Header("Display Basis Stabilization")]
    [Tooltip("Enter circular-display mode below this eccentricity.")]
    public double circularDisplayEnterE = 0.01;

    [Tooltip("Leave circular-display mode above this eccentricity.")]
    public double circularDisplayExitE = 0.03;

    [Tooltip("Angle change needed before updating cached display basis while circular.")]
    public float circularBasisRefreshAngleDeg = 8.0f;

    [Header("Ticking")]
    [Tooltip("If true, ribbon updates from its own LateUpdate. If false, another system must call TickRibbon().")]
    public bool useInternalLateUpdate = false;
    private bool _displayCircularMode = false;
    private bool _hasCachedDisplayBasis = false;
    private Vector3 _cachedDisplayP = Vector3.right;
    private Vector3 _cachedDisplayQ = Vector3.up;

    // ---------------------------------------------------------------------
    // Internal mesh data
    // ---------------------------------------------------------------------
    private Mesh _mesh;

    private Vector3[] _centerPoints;
    private Vector3[] _vertices;
    private Vector3[] _normals;
    private Vector2[] _uvs;
    private int[] _triangles;

    // ---------------------------------------------------------------------
    // Cached previous orbit/view state for invalidation
    // ---------------------------------------------------------------------
    private bool _hasPrevOrbit = false;
    private bool _hasPrevView = false;

    private byte _prevPrimaryId = 255;
    private double _prevE = 0.0;
    private double _prevP = 0.0;
    private Vector3 _prevHDir = Vector3.forward;
    private Vector3 _prevPDir = Vector3.right;

    private byte _prevFocusMode = 255;
    private float _prevSceneScale = 0.0f;

    private float _nextAllowedRebuildTime = 0.0f;

    void Start()
    {
        if (meshFilter == null)
            meshFilter = (MeshFilter)GetComponent(typeof(MeshFilter));

        if (meshRenderer == null)
            meshRenderer = (MeshRenderer)GetComponent(typeof(MeshRenderer));

        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "OrreryCraftOrbitRibbonMesh";
            meshFilter.mesh = _mesh;
        }
    }

    void LateUpdate()
    {
        if (!useInternalLateUpdate) return;
        TickRibbon();
    }
    public void TickRibbon()
    {
        if (orrery == null || nav == null || bodies == null || meshFilter == null)
            return;

        if (!displayEnabled)
        {
            ClearMesh();
            _hasPrevOrbit = false;
            _hasPrevView = false;
            return;
        }

        if (!nav.valid || nav.muPrimary <= 0.0 || nav.p <= 0.0)
        {
            if (hideWhenInvalid)
                ClearMesh();
            _hasPrevOrbit = false;
            _hasPrevView = false;
            return;
        }


        if (orrery != null && orrery.focusMode == OrreryController.FOCUS_CRAFT)
        {
            ClearMesh();
            _hasPrevOrbit = false;
            _hasPrevView = false;
            return;
        }
        
        float hz = maxRebuildHz;
        if (hz < 0.1f) hz = 0.1f;
        float minDt = 1.0f / hz;

        bool force = OrbitChangedEnough();

        if (force || Time.time >= _nextAllowedRebuildTime)
        {
            RebuildRibbonMesh();
            _nextAllowedRebuildTime = Time.time + minDt;
            CacheCurrentOrbitState();
        }

        UpdateMaterialParams();
    }

    // ---------------------------------------------------------------------
    // Orbit/view invalidation logic
    // ---------------------------------------------------------------------
    private bool OrbitChangedEnough()
    {
        if (!_hasPrevOrbit) return true;
        if (!_hasPrevView) return true;

        if (orrery != null)
        {
            if (orrery.focusMode != _prevFocusMode)
                return true;

            float sceneScale = orrery.GetCurrentSceneScale();
            float denom = Mathf.Abs(_prevSceneScale);
            if (denom < 1e-8f) denom = 1e-8f;

            float rel = Mathf.Abs(sceneScale - _prevSceneScale) / denom;
            if (rel > sceneScaleRelativeThreshold)
                return true;
        }

        if (nav.primaryId != _prevPrimaryId) return true;

        double e = nav.e;
        double p = nav.p;

        if (System.Math.Abs(e - _prevE) > eccentricityThreshold)
            return true;

        double denomP = System.Math.Abs(_prevP);
        if (denomP < 1.0) denomP = 1.0;

        if (System.Math.Abs(p - _prevP) / denomP > pRelativeThreshold)
            return true;

        Vector3 hDir = nav.h_E;
        if (hDir.sqrMagnitude < 1e-12f) return true;
        hDir.Normalize();

        float hDot = Mathf.Clamp(Vector3.Dot(hDir, _prevHDir), -1.0f, 1.0f);
        float hAng = Mathf.Acos(hDot) * Mathf.Rad2Deg;
        if (hAng > normalAngleThresholdDeg)
            return true;

        Vector3 pDir = BuildStablePeriapsisDirection(hDir);
        if (pDir.sqrMagnitude < 1e-12f) return true;
        pDir.Normalize();

        float pDot = Mathf.Clamp(Vector3.Dot(pDir, _prevPDir), -1.0f, 1.0f);
        float pAng = Mathf.Acos(pDot) * Mathf.Rad2Deg;
        if (pAng > periapsisAngleThresholdDeg)
            return true;

        return false;
    }

    private void CacheCurrentOrbitState()
    {
        _hasPrevOrbit = true;
        _prevPrimaryId = nav.primaryId;
        _prevE = nav.e;
        _prevP = nav.p;

        Vector3 hDir = nav.h_E;
        if (hDir.sqrMagnitude > 1e-12f) hDir.Normalize();
        else hDir = Vector3.forward;
        _prevHDir = hDir;

        Vector3 pDir = BuildStablePeriapsisDirection(hDir);
        if (pDir.sqrMagnitude > 1e-12f) pDir.Normalize();
        else pDir = Vector3.right;
        _prevPDir = pDir;

        if (orrery != null)
        {
            _hasPrevView = true;
            _prevFocusMode = orrery.focusMode;
            _prevSceneScale = orrery.GetCurrentSceneScale();
        }
    }

    // ---------------------------------------------------------------------
    // Mesh rebuild
    // ---------------------------------------------------------------------
    private void RebuildRibbonMesh()
    {
        double wx = (double)nav.h_E.x;
        double wy = (double)nav.h_E.y;
        double wz = (double)nav.h_E.z;
        if (!NormalizeD(ref wx, ref wy, ref wz))
        {
            if (hideWhenInvalid) ClearMesh();
            return;
        }


        Vector3 hDir = new Vector3((float)wx, (float)wy, (float)wz);
        hDir.Normalize();

        UpdateDisplayCircularMode();
        UpdateCachedDisplayBasis(hDir);

        if (!_hasCachedDisplayBasis)
        {
            if (hideWhenInvalid) ClearMesh();
            return;
        }

        double px = _cachedDisplayP.x;
        double py = _cachedDisplayP.y;
        double pz = _cachedDisplayP.z;

        double qx = _cachedDisplayQ.x;
        double qy = _cachedDisplayQ.y;
        double qz = _cachedDisplayQ.z;

        if (!BuildStablePeriapsisDirectionD(wx, wy, wz, out px, out py, out pz))
        {
            if (hideWhenInvalid) ClearMesh();
            return;
        }

        // Q = W x P

        if (!NormalizeD(ref qx, ref qy, ref qz))
        {
            if (hideWhenInvalid) ClearMesh();
            return;
        }


        if (!NormalizeD(ref px, ref py, ref pz))
        {
            if (hideWhenInvalid) ClearMesh();
            return;
        }

        double bx, by, bz;
        bodies.GetBodyPos(nav.primaryId, out bx, out by, out bz);

        double e = nav.e;
        bool isEllipse = e < (1.0 - parabolicTol);
        bool isHyperbola = e > (1.0 + parabolicTol);

        if (!isEllipse && !isHyperbola)
        {
            if (hideWhenInvalid) ClearMesh();
            return;
        }

        int pointCount = isEllipse ? (ellipseSegments + 1) : (hyperbolaSegments + 1);
        EnsurePointArray(pointCount);

        if (isEllipse)
        {
            for (int i = 0; i < pointCount; i++)
            {
                double u = (double)i / (double)ellipseSegments;
                double nu = u * (2.0 * System.Math.PI);

                double rx, ry, rz;
                SampleOrbitPointInE(nav.p, e, nu, px, py, pz, qx, qy, qz, out rx, out ry, out rz);

                _centerPoints[i] = orrery.MapWorldPointEToOrreryLocal(
                    bx + rx,
                    by + ry,
                    bz + rz
                );
            }
        }
        else
        {
            double nuLimit = System.Math.Acos(-1.0 / e);
            nuLimit *= (double)hyperbolaNuLimitScale;

            for (int i = 0; i < pointCount; i++)
            {
                double u = (double)i / (double)hyperbolaSegments;
                double nu = (-nuLimit) + (2.0 * nuLimit * u);

                double rx, ry, rz;
                SampleOrbitPointInE(nav.p, e, nu, px, py, pz, qx, qy, qz, out rx, out ry, out rz);

                _centerPoints[i] = orrery.MapWorldPointEToOrreryLocal(
                    bx + rx,
                    by + ry,
                    bz + rz
                );
            }
        }

        EnsureRibbonArrays(pointCount);

        float invDenomU = (pointCount > 1) ? (1.0f / (float)(pointCount - 1)) : 0.0f;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 p0 = _centerPoints[i];
            Vector3 tangent;

            if (i == 0)
                tangent = _centerPoints[1] - _centerPoints[0];
            else if (i == pointCount - 1)
                tangent = _centerPoints[pointCount - 1] - _centerPoints[pointCount - 2];
            else
                tangent = _centerPoints[i + 1] - _centerPoints[i - 1];

            if (tangent.sqrMagnitude < 1e-12f)
                tangent = Vector3.right;
            tangent.Normalize();

            int vi = 2 * i;

            // Both vertices share same centerline position.
            _vertices[vi + 0] = p0;
            _vertices[vi + 1] = p0;

            // Store tangent in normals for shader use.
            _normals[vi + 0] = tangent;
            _normals[vi + 1] = tangent;

            float uCoord = invDenomU * (float)i;

            // uv.x = along-orbit coordinate
            // uv.y = side sign encoded as 0 (left) / 1 (right)
            _uvs[vi + 0] = new Vector2(uCoord, 0.0f);
            _uvs[vi + 1] = new Vector2(uCoord, 1.0f);
        }

        int tri = 0;
        for (int i = 0; i < pointCount - 1; i++)
        {
            int li = 2 * i;
            int ri = li + 1;
            int ln = li + 2;
            int rn = li + 3;

            // Winding chosen so "front" is reasonable, but shader should use Cull Off anyway.
            _triangles[tri++] = li;
            _triangles[tri++] = ln;
            _triangles[tri++] = ri;

            _triangles[tri++] = ri;
            _triangles[tri++] = ln;
            _triangles[tri++] = rn;
        }

        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "OrreryCraftOrbitRibbonMesh";
            meshFilter.mesh = _mesh;
        }
        else
        {
            _mesh.Clear();
        }

        _mesh.vertices = _vertices;
        _mesh.normals = _normals;
        _mesh.uv = _uvs;
        _mesh.triangles = _triangles;
        _mesh.RecalculateBounds();
    }

    private void UpdateMaterialParams()
    {
        if (meshRenderer == null) return;
        Material mat = meshRenderer.material;
        if (mat == null) return;

        float craftU = 0.0f;

        if (nav != null)
        {
            double twoPi = 2.0 * System.Math.PI;
            double nu = nav.nuRad;

            while (nu < 0.0) nu += twoPi;
            while (nu >= twoPi) nu -= twoPi;

            craftU = (float)(nu / twoPi);
        }

        mat.SetFloat("_CraftU", craftU);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------
    private Vector3 BuildStablePeriapsisDirection(Vector3 hDir)
    {
        Vector3 pDir;

        if (nav.e > circularETol && nav.eVec_E.sqrMagnitude > 1e-12f)
        {
            pDir = nav.eVec_E;
            pDir -= Vector3.Dot(pDir, hDir) * hDir;

            if (pDir.sqrMagnitude < 1e-12f)
            {
                pDir = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);
                pDir -= Vector3.Dot(pDir, hDir) * hDir;
            }
        }
        else
        {
            pDir = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);
            pDir -= Vector3.Dot(pDir, hDir) * hDir;
        }

        return pDir;
    }

    private bool BuildStablePeriapsisDirectionD(double wx, double wy, double wz,
        out double px, out double py, out double pz)
    {
        if (nav.e > circularETol && nav.eVec_E.sqrMagnitude > 1e-12f)
        {
            px = (double)nav.eVec_E.x;
            py = (double)nav.eVec_E.y;
            pz = (double)nav.eVec_E.z;

            double pdotw = px * wx + py * wy + pz * wz;
            px -= pdotw * wx;
            py -= pdotw * wy;
            pz -= pdotw * wz;

            if (NormalizeD(ref px, ref py, ref pz))
                return true;
        }

        px = nav.r_x;
        py = nav.r_y;
        pz = nav.r_z;

        double rdotw = px * wx + py * wy + pz * wz;
        px -= rdotw * wx;
        py -= rdotw * wy;
        pz -= rdotw * wz;

        return NormalizeD(ref px, ref py, ref pz);
    }

    private void EnsurePointArray(int pointCount)
    {
        if (_centerPoints == null || _centerPoints.Length != pointCount)
            _centerPoints = new Vector3[pointCount];
    }

    private void EnsureRibbonArrays(int pointCount)
    {
        int vertexCount = 2 * pointCount;
        int triCount = 6 * (pointCount - 1);

        if (_vertices == null || _vertices.Length != vertexCount)
            _vertices = new Vector3[vertexCount];

        if (_normals == null || _normals.Length != vertexCount)
            _normals = new Vector3[vertexCount];

        if (_uvs == null || _uvs.Length != vertexCount)
            _uvs = new Vector2[vertexCount];

        if (_triangles == null || _triangles.Length != triCount)
            _triangles = new int[triCount];
    }

    private void ClearMesh()
    {
        if (_mesh == null)
        {
            if (meshFilter != null && meshFilter.mesh != null)
                meshFilter.mesh.Clear();
            return;
        }

        _mesh.Clear();
    }

    private static void SampleOrbitPointInE(
        double p, double e, double nu,
        double px, double py, double pz,
        double qx, double qy, double qz,
        out double rx, out double ry, out double rz)
    {
        double c = System.Math.Cos(nu);
        double s = System.Math.Sin(nu);

        double denom = 1.0 + e * c;
        if (System.Math.Abs(denom) < 1e-12)
            denom = (denom >= 0.0) ? 1e-12 : -1e-12;

        double r = p / denom;

        rx = r * (c * px + s * qx);
        ry = r * (c * py + s * qy);
        rz = r * (c * pz + s * qz);
    }
    private void UpdateDisplayCircularMode()
    {
        double e = nav.e;

        if (_displayCircularMode)
        {
            if (e > circularDisplayExitE)
                _displayCircularMode = false;
        }
        else
        {
            if (e < circularDisplayEnterE)
                _displayCircularMode = true;
        }
    }

    private void UpdateCachedDisplayBasis(Vector3 hDir)
    {
        Vector3 pCandidate = BuildStablePeriapsisDirection(hDir);
        if (pCandidate.sqrMagnitude < 1e-12f)
            return;

        pCandidate.Normalize();
        Vector3 qCandidate = Vector3.Cross(hDir, pCandidate);
        if (qCandidate.sqrMagnitude < 1e-12f)
            return;
        qCandidate.Normalize();

        if (!_hasCachedDisplayBasis)
        {
            _cachedDisplayP = pCandidate;
            _cachedDisplayQ = qCandidate;
            _hasCachedDisplayBasis = true;
            return;
        }

        if (_displayCircularMode)
        {
            float dotP = Mathf.Clamp(Vector3.Dot(_cachedDisplayP, pCandidate), -1.0f, 1.0f);
            float angP = Mathf.Acos(dotP) * Mathf.Rad2Deg;

            // Only refresh circular display basis if it moved a lot
            if (angP > circularBasisRefreshAngleDeg)
            {
                _cachedDisplayP = pCandidate;
                _cachedDisplayQ = qCandidate;
            }
        }
        else
        {
            _cachedDisplayP = pCandidate;
            _cachedDisplayQ = qCandidate;
        }
    }
    private static bool NormalizeD(ref double x, ref double y, ref double z)
    {
        double m2 = x * x + y * y + z * z;
        if (m2 < 1e-24) return false;

        double inv = 1.0 / System.Math.Sqrt(m2);
        x *= inv;
        y *= inv;
        z *= inv;
        return true;
    }
}