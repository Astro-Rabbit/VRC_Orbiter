using UdonSharp;
using UnityEngine;

/// <summary>
/// GlobusTextureDriver
///
/// Static globe display:
/// - Mesh stays fixed
/// - Shader rotates texture lookup only
/// - Current subpoint on active primary body is brought to the display center
///
/// Frame contract used here:
///
/// SIM body-fixed frame:
///   +X = lon 0 on equator
///   +Y = +90 deg longitude on equator
///   +Z = north pole
///
/// Shader texture-geographic frame (because shader uses atan2(d.x, d.z), asin(d.y)):
///   +Z = lon 0 on equator
///   +X = +90 deg longitude on equator
///   +Y = north pole
///
/// Therefore:
///   simBody(x,y,z) -> texGeo(y,z,x)
///
/// Longitude adjustment:
/// - In texture-geographic frame, longitude offset is a rotation about +Y (north axis).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GlobusTextureDriver : UdonSharpBehaviour
{
    [Header("References")]
    public CraftStateModel craft;
    public BodyCatalog bodies;
    public Renderer globeRenderer;
    public Material globeMaterial;

    [Header("Body textures")]
    public Texture earthTexture;
    public Texture moonTexture;

    [Header("Display center")]
    [Tooltip("Object-space direction on the sphere that sits under the fixed crosshair.")]
    public Vector3 displayCenterDirOS = Vector3.forward;

    [Header("Per-body longitude adjustment")]
    [Tooltip("Applied in texture-geographic frame about +Y (north axis).")]
    public float earthLongitudeOffsetDeg = 0.0f;
    public float moonLongitudeOffsetDeg = 0.0f;

    [Header("Per-body texture calibration")]
    [Tooltip("Extra authored-texture calibration after longitude adjustment.")]
    public Vector3 earthTextureCalibrationEulerDeg = Vector3.zero;
    public Vector3 moonTextureCalibrationEulerDeg = Vector3.zero;

    [Header("Motion smoothing")]
    public bool useSmoothing = true;
    public float generalFollowRate = 6.0f;
    public float bodyChangeFollowRate = 2.0f;
    public float bodyChangeBlendSeconds = 1.5f;

    [Header("E-paper transition")]
    public bool useEpaperRefresh = true;
    public float refreshDuration = 0.8f;

    [Header("Debug geo override")]
    public bool useDebugGeoOverride = false;

    [Range(-90f, 90f)]
    public float debugLatitudeDeg = 0.0f;

    [Range(-180f, 180f)]
    public float debugLongitudeDeg = 0.0f;

    [Header("Optional tint")]
    public Color tint = Color.white;

    private byte _currentBodyId = 255;
    private Texture _currentTexture;
    private Quaternion _currentGeoToAuthoredTex = Quaternion.identity;

    private bool _refreshActive = false;
    private float _refreshT = 1.0f;

    private bool _displayDirValid = false;
    private Vector3 _displayDirAuthoredTex = Vector3.forward;

    private bool _bodyChangeSmoothingActive = false;
    private float _bodyChangeElapsed = 0.0f;

    void Start()
    {
        if (globeRenderer != null && globeMaterial != null)
            globeRenderer.sharedMaterial = globeMaterial;

        if (globeMaterial != null)
        {
            globeMaterial.SetColor("_Tint", tint);
            globeMaterial.SetFloat("_RefreshPhase", 1.0f);
        }

        _currentBodyId = 255;
        _currentTexture = null;
        _currentGeoToAuthoredTex = Quaternion.identity;

        _refreshActive = false;
        _refreshT = 1.0f;

        _displayDirValid = false;
        _displayDirAuthoredTex = Vector3.forward;

        _bodyChangeSmoothingActive = false;
        _bodyChangeElapsed = 0.0f;
    }

    void Update()
    {
        if (craft == null || bodies == null || globeMaterial == null)
            return;

        byte bodyId = craft.primaryBodyId;
        Texture nextTex = GetTextureForBody(bodyId);
        if (nextTex == null)
            return;

        if (bodyId != _currentBodyId)
        {
            Quaternion nextGeoToAuthoredTex = GetGeoToAuthoredTextureRotation(bodyId);
            HandleBodyChange(bodyId, nextTex, nextGeoToAuthoredTex);
        }

        UpdateRefreshAnimation();

        Vector3 desiredDirAuthoredTex;

        if (useDebugGeoOverride)
        {
            Vector3 dirSimBody = LatLonDegToSimBodyDir(debugLatitudeDeg, debugLongitudeDeg);
            Vector3 dirTexGeo = MapSimBodyToTextureGeo(dirSimBody);
            desiredDirAuthoredTex = _currentGeoToAuthoredTex * dirTexGeo;
        }
        else
        {
            double bx, by, bz;
            bodies.GetBodyPos(bodyId, out bx, out by, out bz);

            Vector3 craftFromBody_I = new Vector3(
                (float)(craft.rx - bx),
                (float)(craft.ry - by),
                (float)(craft.rz - bz)
            );

            if (craftFromBody_I.sqrMagnitude < 1e-10f)
                return;

            craftFromBody_I.Normalize();

            Quaternion qBodyToInertial = bodies.GetBodyFixedToInertial(bodyId);
            Quaternion qInertialToBody = Quaternion.Inverse(qBodyToInertial);

            Vector3 dirSimBody = qInertialToBody * craftFromBody_I;
            if (dirSimBody.sqrMagnitude < 1e-10f)
                return;
            dirSimBody.Normalize();

            Vector3 dirTexGeo = MapSimBodyToTextureGeo(dirSimBody);
            desiredDirAuthoredTex = _currentGeoToAuthoredTex * dirTexGeo;
        }

        if (desiredDirAuthoredTex.sqrMagnitude < 1e-10f)
            return;
        desiredDirAuthoredTex.Normalize();

        UpdateDisplayedDirection(desiredDirAuthoredTex);

        Vector3 centerDirOS = displayCenterDirOS;
        if (centerDirOS.sqrMagnitude < 1e-10f)
            centerDirOS = Vector3.forward;
        else
            centerDirOS.Normalize();

        Quaternion qTex = BuildTextureRotation(centerDirOS, _displayDirAuthoredTex);

        globeMaterial.SetVector("_GlobeRot", new Vector4(qTex.x, qTex.y, qTex.z, qTex.w));
        globeMaterial.SetColor("_Tint", tint);
    }

    private void UpdateDisplayedDirection(Vector3 desiredDirAuthoredTex)
    {
        if (!_displayDirValid)
        {
            _displayDirAuthoredTex = desiredDirAuthoredTex;
            _displayDirValid = true;
            _bodyChangeSmoothingActive = false;
            _bodyChangeElapsed = 0.0f;
            return;
        }

        if (!useSmoothing)
        {
            _displayDirAuthoredTex = desiredDirAuthoredTex;
            _bodyChangeSmoothingActive = false;
            _bodyChangeElapsed = 0.0f;
            return;
        }

        float dt = Time.deltaTime;
        if (dt < 0.0f) dt = 0.0f;

        float rate = generalFollowRate;

        if (_bodyChangeSmoothingActive)
        {
            _bodyChangeElapsed += dt;
            if (_bodyChangeElapsed < bodyChangeBlendSeconds)
                rate = bodyChangeFollowRate;
            else
            {
                _bodyChangeSmoothingActive = false;
                rate = generalFollowRate;
            }
        }

        if (rate < 0.01f) rate = 0.01f;

        float t = 1.0f - Mathf.Exp(-rate * dt);
        t = Mathf.Clamp01(t);

        _displayDirAuthoredTex = Vector3.Slerp(_displayDirAuthoredTex, desiredDirAuthoredTex, t);
        if (_displayDirAuthoredTex.sqrMagnitude < 1e-10f)
            _displayDirAuthoredTex = desiredDirAuthoredTex;
        else
            _displayDirAuthoredTex.Normalize();
    }

    private void HandleBodyChange(byte newBodyId, Texture newTex, Quaternion newGeoToAuthoredTex)
    {
        Texture oldTex = _currentTexture;
        if (oldTex == null) oldTex = newTex;

        _currentBodyId = newBodyId;
        _currentTexture = newTex;
        _currentGeoToAuthoredTex = newGeoToAuthoredTex;

        globeMaterial.SetTexture("_PrevGlobeTex", oldTex);
        globeMaterial.SetTexture("_GlobeTex", newTex);

        if (useEpaperRefresh && refreshDuration > 0.01f)
        {
            _refreshActive = true;
            _refreshT = 0.0f;
            globeMaterial.SetFloat("_RefreshPhase", 0.0f);
        }
        else
        {
            _refreshActive = false;
            _refreshT = 1.0f;
            globeMaterial.SetFloat("_RefreshPhase", 1.0f);
        }

        if (_displayDirValid)
        {
            _bodyChangeSmoothingActive = true;
            _bodyChangeElapsed = 0.0f;
        }
        else
        {
            _bodyChangeSmoothingActive = false;
            _bodyChangeElapsed = 0.0f;
        }
    }

    private void UpdateRefreshAnimation()
    {
        if (!_refreshActive) return;

        float dt = Time.deltaTime;
        if (dt < 0.0f) dt = 0.0f;

        float dur = refreshDuration;
        if (dur < 0.01f) dur = 0.01f;

        _refreshT += dt / dur;

        if (_refreshT >= 1.0f)
        {
            _refreshT = 1.0f;
            _refreshActive = false;
        }

        globeMaterial.SetFloat("_RefreshPhase", _refreshT);
    }

    private Texture GetTextureForBody(byte bodyId)
    {
        if (bodyId == bodies.earthId) return earthTexture;
        if (bodyId == bodies.moonId) return moonTexture;
        return null;
    }

    private Quaternion GetGeoToAuthoredTextureRotation(byte bodyId)
    {
        float lonOffset = 0.0f;
        Vector3 calibEuler = Vector3.zero;

        if (bodyId == bodies.earthId)
        {
            lonOffset = earthLongitudeOffsetDeg;
            calibEuler = earthTextureCalibrationEulerDeg;
        }
        else if (bodyId == bodies.moonId)
        {
            lonOffset = moonLongitudeOffsetDeg;
            calibEuler = moonTextureCalibrationEulerDeg;
        }

        // In texture-geographic frame, +Y is north, so longitude offset is about +Y.
        Quaternion qLon = Quaternion.AngleAxis(lonOffset, Vector3.up);
        Quaternion qCal = Quaternion.Euler(calibEuler);

        return qCal * qLon;
    }

    private Vector3 MapSimBodyToTextureGeo(Vector3 vSimBody)
    {
        // simBody:  +X lon0, +Y +90 lon, +Z north
        // texGeo:   +Z lon0, +X +90 lon, +Y north
        Vector3 vTexGeo = new Vector3(
            -vSimBody.y,
            -vSimBody.z,
            vSimBody.x
        );

        if (vTexGeo.sqrMagnitude < 1e-10f)
            return Vector3.forward;

        return vTexGeo.normalized;
    }


    private Quaternion BuildTextureRotation(Vector3 srcDir, Vector3 dstDir)
    {
        srcDir.Normalize();
        dstDir.Normalize();

        // Authored-texture north pole direction in authored-texture frame.
        // In texGeo, north is +Y. After calibration/longitude offsets, that axis
        // must be rotated into authored-texture space the same way as the map.
        Vector3 authoredNorth = _currentGeoToAuthoredTex * Vector3.up;

        Vector3 srcUp = GetLocalNorthTangent(srcDir, authoredNorth);
        Vector3 dstUp = GetLocalNorthTangent(dstDir, authoredNorth);

        Quaternion qSrc = Quaternion.LookRotation(srcDir, srcUp);
        Quaternion qDst = Quaternion.LookRotation(dstDir, dstUp);

        return qDst * Quaternion.Inverse(qSrc);
    }

    private Vector3 GetLocalNorthTangent(Vector3 surfaceDir, Vector3 authoredNorth)
    {
        // Project geographic north onto the tangent plane at surfaceDir.
        Vector3 northTangent = authoredNorth - surfaceDir * Vector3.Dot(authoredNorth, surfaceDir);

        if (northTangent.sqrMagnitude > 1e-10f)
            return northTangent.normalized;

        // Pole fallback: north is undefined here, so derive a stable tangent from a
        // secondary authored axis. In texGeo, lon 0 on equator is +Z.
        Vector3 authoredLon0 = _currentGeoToAuthoredTex * Vector3.forward;
        Vector3 fallback = authoredLon0 - surfaceDir * Vector3.Dot(authoredLon0, surfaceDir);

        if (fallback.sqrMagnitude > 1e-10f)
        {
            // Build a pseudo-north from the fallback east-like axis so that
            // LookRotation still gets a consistent up vector.
            Vector3 east = fallback.normalized;
            Vector3 north = Vector3.Cross(surfaceDir, east);
            if (north.sqrMagnitude > 1e-10f)
                return north.normalized;
        }

        // Last-ditch fallback.
        Vector3 c = Vector3.Cross(surfaceDir, Vector3.right);
        if (c.sqrMagnitude < 1e-10f)
            c = Vector3.Cross(surfaceDir, Vector3.up);

        Vector3 east2 = c.normalized;
        Vector3 north2 = Vector3.Cross(surfaceDir, east2);
        return north2.normalized;
    }

    private Vector3 LatLonDegToSimBodyDir(float latDeg, float lonDeg)
    {
        float lat = latDeg * Mathf.Deg2Rad;
        float lon = lonDeg * Mathf.Deg2Rad;

        float cosLat = Mathf.Cos(lat);
        float sinLat = Mathf.Sin(lat);
        float cosLon = Mathf.Cos(lon);
        float sinLon = Mathf.Sin(lon);

        // simBody:
        // +X = lon 0 on equator
        // +Y = +90 longitude on equator
        // +Z = north
        Vector3 d = new Vector3(
            cosLat * cosLon,
            cosLat * sinLon,
            sinLat
        );

        if (d.sqrMagnitude < 1e-10f)
            return Vector3.right;

        return d.normalized;
    }
}