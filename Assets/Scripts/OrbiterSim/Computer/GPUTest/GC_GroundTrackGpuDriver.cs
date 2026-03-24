using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class GC_GroundTrackGpuDriver : UdonSharpBehaviour
{
    [Header("References")]
    public GuidanceNavCoreState nav;
    public BodyCatalog bodies;

    [Header("Materials / Targets")]
    [Tooltip("Material using the propagation shader.")]
    public Material computeMaterial;

    [Tooltip("Output texture written by VRCGraphics.Blit. Width = sample count, Height = 1.")]
    public RenderTexture outputRT;

    [Header("Map Output")]
    public Material mapMaterial;
    public float mapLonOffset = 0f;
    public bool showCurrentMarker = true;

    [Header("Map Textures")]
    public Texture earthMapTexture;
    public Texture moonMapTexture;

    [Header("Map Calibration")]
    public float earthLonOffset = 0f;
    public float moonLonOffset = 0f;
    public float mapLonOffsetFallback = 0f;

    [Header("Sampling")]
    public double horizonSec = 5400.0;
    public double sampleStepSec = 30.0;
    public int maxSamples = 256;

    [Header("Policy")]
    public bool requireValidNav = true;
    public bool currentPrimaryOnly = true;

    [Header("Debug")]
    public bool autoUpdate = true;
    public float recomputeIntervalSec = 0.5f;
    public bool logStatus = false;

    private int _lastSampleCount = -1;
    private float _lastUpdateRT = -9999f;

    void Start()
    {
        EnsureResources(1);
        PushDummyUniforms();
        DispatchPass();
        PushMapUniforms(nav != null ? nav.primaryId : bodies.earthId, 1);;
    }

    void Update()
    {
        if (!autoUpdate) return;

        if (Time.time - _lastUpdateRT >= recomputeIntervalSec)
        {
            EvaluateAndDispatch();
        }
    }

    public void ForceRecompute()
    {
        EvaluateAndDispatch();
    }

    private void EvaluateAndDispatch()
    {
        if (computeMaterial == null || outputRT == null)
        {
            if (logStatus) Debug.LogWarning("[GT GPU] Missing compute material or outputRT.");
            return;
        }

        if (nav == null || bodies == null)
        {
            if (logStatus) Debug.LogWarning("[GT GPU] Missing nav/bodies.");
            return;
        }

        if (requireValidNav && !nav.valid)
        {
            if (logStatus) Debug.LogWarning("[GT GPU] Nav invalid.");
            return;
        }

        byte bodyId = nav.primaryId;
        double bodyRadius = bodies.GetRadius(bodyId);
        if (currentPrimaryOnly && bodyRadius <= 0.0)
        {
            if (logStatus) Debug.LogWarning("[GT GPU] Unsupported or invalid primary body.");
            return;
        }

        if (sampleStepSec <= 0.0)
        {
            if (logStatus) Debug.LogWarning("[GT GPU] sampleStepSec must be > 0.");
            return;
        }

        int desiredSamples = 1 + (int)System.Math.Floor(horizonSec / sampleStepSec);
        if (desiredSamples < 1) desiredSamples = 1;
        if (desiredSamples > maxSamples) desiredSamples = maxSamples;

        EnsureResources(desiredSamples);
        PushCommonUniforms(bodyId, bodyRadius, desiredSamples);
        DispatchPass();
        PushMapUniforms(bodyId, desiredSamples);

        _lastUpdateRT = Time.time;
    }

    private void EnsureResources(int sampleCount)
    {
        if (sampleCount < 1) sampleCount = 1;

        if (_lastSampleCount != sampleCount)
        {
            _lastSampleCount = sampleCount;
        }

        if (outputRT.width != sampleCount || outputRT.height != 1)
        {
            outputRT.Release();
            outputRT.width = sampleCount;
            outputRT.height = 1;
            outputRT.enableRandomWrite = false;
            outputRT.useMipMap = false;
            outputRT.autoGenerateMips = false;
            outputRT.wrapMode = TextureWrapMode.Clamp;
            outputRT.filterMode = FilterMode.Point;
            outputRT.Create();
        }
    }

    private void PushCommonUniforms(byte bodyId, double bodyRadius, int sampleCount)
    {
        Quaternion qPF2E = bodies.GetBodyFixedToInertial(bodyId);

        double ox, oy, oz;
        bodies.GetBodyOmega(bodyId, out ox, out oy, out oz);

        computeMaterial.SetFloat("_SampleCount", (float)sampleCount);
        computeMaterial.SetFloat("_BodyId", (float)bodyId);
        computeMaterial.SetFloat("_BodyRadius", (float)bodyRadius);

        computeMaterial.SetFloat("_A", (float)nav.a);
        computeMaterial.SetFloat("_E", (float)nav.e);
        computeMaterial.SetFloat("_Inc", (float)nav.iInertialRad);
        computeMaterial.SetFloat("_RAAN", (float)nav.raanInertialRad);
        computeMaterial.SetFloat("_ArgP", (float)nav.argpInertialRad);
        computeMaterial.SetFloat("_Nu0", (float)nav.nuRad);
        computeMaterial.SetFloat("_Mu", (float)nav.muPrimary);
        computeMaterial.SetFloat("_T0", (float)nav.t);
        computeMaterial.SetFloat("_SampleStepSec", (float)sampleStepSec);

        computeMaterial.SetVector("_BodyQPF2E", new Vector4(qPF2E.x, qPF2E.y, qPF2E.z, qPF2E.w));
        computeMaterial.SetVector("_BodyOmega", new Vector4((float)ox, (float)oy, (float)oz, 0f));
    }

    private void PushMapUniforms(byte bodyId, int sampleCount)
    {
        if (mapMaterial == null || outputRT == null || bodies == null) return;

        mapMaterial.SetTexture("_TrackTex", outputRT);
        mapMaterial.SetFloat("_TrackSampleCount", (float)sampleCount);
        mapMaterial.SetFloat("_ShowCurrentMarker", showCurrentMarker ? 1f : 0f);

        if (bodyId == bodies.earthId)
        {
            if (earthMapTexture != null)
                mapMaterial.SetTexture("_MainTex", earthMapTexture);

            mapMaterial.SetFloat("_LonOffset", earthLonOffset);
        }
        else if (bodyId == bodies.moonId)
        {
            if (moonMapTexture != null)
                mapMaterial.SetTexture("_MainTex", moonMapTexture);

            mapMaterial.SetFloat("_LonOffset", moonLonOffset);
        }
        else
        {
            mapMaterial.SetFloat("_LonOffset", mapLonOffsetFallback);
        }
    }


    private void PushDummyUniforms()
    {
        if (computeMaterial == null) return;

        computeMaterial.SetFloat("_SampleCount", 1f);
        computeMaterial.SetVector("_BodyQPF2E", new Vector4(0f, 0f, 0f, 1f));
        computeMaterial.SetVector("_BodyOmega", Vector4.zero);
    }

    private void DispatchPass()
    {
        if (computeMaterial == null || outputRT == null) return;

        // Source texture is unused by the shader; null is fine for a pure generated blit in many cases,
        // but some pipelines prefer a dummy texture. Use Texture2D.whiteTexture if needed.
        VRCGraphics.Blit(Texture2D.whiteTexture, outputRT, computeMaterial);
    }
}