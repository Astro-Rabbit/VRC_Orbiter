using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// GroundTrackDisplayDriver
///
/// GPU ground-track pipeline:
///   Pass 0: propagate future ground-track samples into outputRT
///           outputRT is a 1D/strip data texture:
///             RGB = packed body-fixed unit vector
///             A   = altitude/visibility scalar
///
///   Pass 1: composite base map + track into mapRT
///
/// This driver is independent of the MFD. The MFD can later sample mapRT
/// as a generic image input.
///
/// Assumes the material uses shader:
///   "Orbiter/MFD/GroundTrackCombined_TwoPass"
/// </summary>
public class GroundTrackDisplayDriver : UdonSharpBehaviour
{
    [Header("Required References")]
    public GuidanceNavCoreState nav;
    public BodyCatalog bodies;

    [Header("Combined Two-Pass Material")]
    public Material groundTrackMaterial;

    [Header("Track Data Output RT (Pass 0)")]
    public RenderTexture outputRT;

    [Header("Map Output RT (Pass 1)")]
    public RenderTexture mapRT;

    [Header("Base Maps")]
    public Texture2D earthMapTex;
    public Texture2D moonMapTex;

    [Header("Track Sampling")]
    [Tooltip("Number of future track samples to generate.")]
    public int sampleCount = 128;




    [Tooltip("Seconds between adjacent samples.")]
    public float sampleStepSec = 30f;

    [Header("Map RT Size")]
    public int mapWidth = 512;
    public int mapHeight = 256;

    [Header("Display")]
    [Tooltip("Altitude scale used in pass 0 alpha output.")]
    public float altDisplayScale = 1000000f;

    [Tooltip("Longitude UV offset to align the base map.")]
    public float earthLonOffset = 0f;

    [Tooltip("Longitude UV offset to align the base map.")]
    public float moonLonOffset = 0f;

    [Header("Map Styling")]
    public Color mapTint = Color.white;
    public Color trackColor = Color.green;
    public float trackWidthUV = 0.004f;
    public float trackSoftnessUV = 0.0015f;

    public Color markerColor = Color.yellow;
    public float markerRadiusUV = 0.01f;
    public float markerSoftnessUV = 0.002f;
    public bool showCurrentMarker = true;

    [Header("Update Policy")]
    [Tooltip("If false, driver does nothing until EvaluateAndDispatch is called manually.")]
    public bool updateEveryFrame = true;

    [Tooltip("If > 0, limit updates to this cadence in seconds.")]
    public float minUpdateIntervalSec = 0.25f;

    private int _lastSampleCount = -1;
    private int _lastMapWidth = -1;
    private int _lastMapHeight = -1;
    private double _lastDispatchT = double.NegativeInfinity;


    private int _lastRenderedBodyId = -1;
    private bool _mapMatchesCurrentBody = false;

    void Update()
    {
        if (!updateEveryFrame) return;
        EvaluateAndDispatch();
    }

    /// <summary>
    /// Main public entry point.
    /// </summary>
    public void EvaluateAndDispatch()
    {
        if (nav == null || !nav.valid) return;
        if (bodies == null) return;
        if (groundTrackMaterial == null) return;
        if (outputRT == null) return;
        if (mapRT == null) return;

        int bodyId = nav.primaryId;
        bool bodyChanged = (bodyId != _lastRenderedBodyId);

        if (minUpdateIntervalSec > 0f && !bodyChanged)
        {
            double nowT = nav.t;
            if (nowT - _lastDispatchT < (double)minUpdateIntervalSec)
                return;
            _lastDispatchT = nowT;
        }
        else
        {
            _lastDispatchT = nav.t;
        }

        int desiredSamples = sampleCount;
        if (desiredSamples < 1) desiredSamples = 1;
        if (desiredSamples > 256) desiredSamples = 256; // shader loop cap for pass 1

        EnsureTrackDataResources(desiredSamples);
        EnsureMapResources();

        float bodyRadius = (float)nav.radiusPrimary;

        PushPropagationUniforms(bodyId, bodyRadius, desiredSamples);
        DispatchTrackDataPass();

        PushMapUniforms(bodyId, desiredSamples);
        DispatchMapPass();

        _lastRenderedBodyId = bodyId;
        _mapMatchesCurrentBody = true;


    }

    public void ForceRefresh()
    {
        _lastDispatchT = double.NegativeInfinity;
        _mapMatchesCurrentBody = false;
        EvaluateAndDispatch();
    }


    public bool MapMatchesBody(int bodyId)
    {
        return _mapMatchesCurrentBody &&
            mapRT != null &&
            mapRT.IsCreated() &&
            _lastRenderedBodyId == bodyId;
    }
    private void EnsureTrackDataResources(int desiredSamples)
    {
        if (outputRT == null) return;

        bool needsRecreate =
            (_lastSampleCount != desiredSamples) ||
            !outputRT.IsCreated() ||
            outputRT.width != desiredSamples ||
            outputRT.height != 1;

        if (!needsRecreate) return;

        _lastSampleCount = desiredSamples;

        if (outputRT.IsCreated())
            outputRT.Release();

        outputRT.width = desiredSamples;
        outputRT.height = 1;
        outputRT.depth = 0;
        outputRT.useMipMap = false;
        outputRT.autoGenerateMips = false;
        outputRT.wrapMode = TextureWrapMode.Clamp;
        outputRT.filterMode = FilterMode.Point;
        outputRT.enableRandomWrite = false;
        outputRT.Create();
    }

    private void EnsureMapResources()
    {
        if (mapRT == null) return;

        int w = (mapWidth < 1) ? 1 : mapWidth;
        int h = (mapHeight < 1) ? 1 : mapHeight;

        bool needsRecreate =
            (_lastMapWidth != w) ||
            (_lastMapHeight != h) ||
            !mapRT.IsCreated() ||
            mapRT.width != w ||
            mapRT.height != h;

        if (!needsRecreate) return;

        _lastMapWidth = w;
        _lastMapHeight = h;

        if (mapRT.IsCreated())
            mapRT.Release();

        mapRT.width = w;
        mapRT.height = h;
        mapRT.depth = 0;
        mapRT.useMipMap = false;
        mapRT.autoGenerateMips = false;
        mapRT.wrapMode = TextureWrapMode.Clamp;
        mapRT.filterMode = FilterMode.Bilinear;
        mapRT.enableRandomWrite = false;
        mapRT.Create();
    }

    private void PushPropagationUniforms(int bodyId, float bodyRadius, int desiredSamples)
    {
        // Sample generation
        groundTrackMaterial.SetFloat("_SampleCount", (float)desiredSamples);
        groundTrackMaterial.SetFloat("_SampleStepSec", sampleStepSec);

        // Current osculating craft orbit from nav
        groundTrackMaterial.SetFloat("_A", (float)nav.a);
        groundTrackMaterial.SetFloat("_E", (float)nav.e);
        groundTrackMaterial.SetFloat("_Inc", (float)nav.iInertialRad);
        groundTrackMaterial.SetFloat("_RAAN", (float)nav.raanInertialRad);
        groundTrackMaterial.SetFloat("_ArgP", (float)nav.argpInertialRad);
        groundTrackMaterial.SetFloat("_Nu0", (float)nav.nuRad);
        groundTrackMaterial.SetFloat("_Mu", (float)nav.muPrimary);
        groundTrackMaterial.SetFloat("_T0", (float)nav.t);

        // Primary body rotation / size
        groundTrackMaterial.SetFloat("_BodyRadius", bodyRadius);
        groundTrackMaterial.SetFloat("_AltDisplayScale", altDisplayScale);

        Quaternion qPF2E = nav.qPF2E;
        groundTrackMaterial.SetVector(
            "_BodyQPF2E",
            new Vector4(qPF2E.x, qPF2E.y, qPF2E.z, qPF2E.w)
        );

        Vector3 omega = new Vector3(
            (float)nav.omegaP_x,
            (float)nav.omegaP_y,
            (float)nav.omegaP_z
        );
        groundTrackMaterial.SetVector("_BodyOmega", new Vector4(omega.x, omega.y, omega.z, 0f));
    }

    private void PushMapUniforms(int bodyId, int desiredSamples)
    {
        Texture2D baseMap = null;
        float lonOffset = 0f;

        if (bodyId == bodies.earthId)
        {
            baseMap = earthMapTex;
            lonOffset = earthLonOffset;
        }
        else if (bodyId == bodies.moonId)
        {
            baseMap = moonMapTex;
            lonOffset = moonLonOffset;
        }
        else
        {
            baseMap = earthMapTex;
            lonOffset = earthLonOffset;
        }

        if (baseMap != null)
            groundTrackMaterial.SetTexture("_MapTex", baseMap);

        groundTrackMaterial.SetColor("_Color", mapTint);

        groundTrackMaterial.SetTexture("_TrackTex", outputRT);
        groundTrackMaterial.SetFloat("_TrackSampleCount", (float)desiredSamples);

        groundTrackMaterial.SetColor("_TrackColor", trackColor);
        groundTrackMaterial.SetFloat("_TrackWidth", trackWidthUV);
        groundTrackMaterial.SetFloat("_TrackSoftness", trackSoftnessUV);

        groundTrackMaterial.SetColor("_MarkerColor", markerColor);
        groundTrackMaterial.SetFloat("_MarkerRadius", markerRadiusUV);
        groundTrackMaterial.SetFloat("_MarkerSoftness", markerSoftnessUV);
        groundTrackMaterial.SetFloat("_ShowCurrentMarker", showCurrentMarker ? 1f : 0f);

        groundTrackMaterial.SetFloat("_LonOffset", lonOffset);

        float aspect = 1f;
        if (mapRT != null && mapRT.height > 0)
            aspect = (float)mapRT.width / (float)mapRT.height;

        groundTrackMaterial.SetFloat("_MapAspect", aspect);


    }

    private void DispatchTrackDataPass()
    {
        if (groundTrackMaterial == null || outputRT == null) return;

        // Pass 0 = PROPAGATE_TRACK_DATA
        VRCGraphics.Blit(Texture2D.whiteTexture, outputRT, groundTrackMaterial, 0);
    }

    private void DispatchMapPass()
    {
        if (groundTrackMaterial == null || mapRT == null) return;

        // Pass 1 = COMPOSITE_MAP
        VRCGraphics.Blit(Texture2D.whiteTexture, mapRT, groundTrackMaterial, 1);
    }

    public Texture GetMapTexture()
    {
        return mapRT;
    }

    public Texture GetTrackDataTexture()
    {
        return outputRT;
    }

    public bool HasValidMapTexture()
    {
        return mapRT != null && mapRT.IsCreated();
    }

    public bool HasValidTrackDataTexture()
    {
        return outputRT != null && outputRT.IsCreated();
    }


    void OnDisable()
    {
        // Intentionally do not release RTs here unless you know
        // this object is temporary. Usually these are scene-owned.
    }
}