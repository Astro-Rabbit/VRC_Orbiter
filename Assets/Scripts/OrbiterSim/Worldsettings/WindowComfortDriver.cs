using UdonSharp;
using UnityEngine;

public class WindowComfortDriver : UdonSharpBehaviour
{
    public const int TARGET_COCKPIT = 0;
    public const int TARGET_LOUNGE = 1;

    public const int MODE_OFF = 0;
    public const int MODE_VIGNETTE = 1;
    public const int MODE_LINES = 2;
    public const int MODE_BOTH = 3;

    [Header("Source")]
    public CraftAttitudeState attitudeState;

    [Header("Materials")]
    public Material[] cockpitMaterials;
    public Material[] loungeMaterials;

    [Header("Defaults")]
    public bool cockpitEnabled = false;
    public bool loungeEnabled = true;

    public bool cockpitUsesGlobalProfile = true;
    public bool loungeUsesGlobalProfile = true;

    [Header("Global Profile")]
    [Range(0, 3)] public int globalMode = MODE_BOTH;
    public float globalMaxAngularRateRad = 0.8f;
    public float globalResponseExponent = 1.5f;
    public float globalWxWeight = 1f;
    public float globalWyWeight = 1f;
    public float globalWzWeight = 1f;

    [Range(0f, 1f)] public float globalVignetteStartLevel = 0f;
    [Range(0f, 1f)] public float globalVignetteMaxLevel = 1f;

    [Range(0f, 1f)] public float globalLineStartLevel = 0f;
    [Range(0f, 1f)] public float globalLineMaxLevel = 1f;

    [Header("Cockpit Override")]
    [Range(0, 3)] public int cockpitMode = MODE_BOTH;
    public float cockpitMaxAngularRateRad = 0.8f;
    public float cockpitResponseExponent = 1.5f;
    public float cockpitWxWeight = 1f;
    public float cockpitWyWeight = 1f;
    public float cockpitWzWeight = 1f;

    [Range(0f, 1f)] public float cockpitVignetteStartLevel = 0f;
    [Range(0f, 1f)] public float cockpitVignetteMaxLevel = 1f;

    [Range(0f, 1f)] public float cockpitLineStartLevel = 0f;
    [Range(0f, 1f)] public float cockpitLineMaxLevel = 1f;

    [Header("Lounge Override")]
    [Range(0, 3)] public int loungeMode = MODE_BOTH;
    public float loungeMaxAngularRateRad = 0.8f;
    public float loungeResponseExponent = 1.5f;
    public float loungeWxWeight = 1f;
    public float loungeWyWeight = 1f;
    public float loungeWzWeight = 1f;

    [Range(0f, 1f)] public float loungeVignetteStartLevel = 0f;
    [Range(0f, 1f)] public float loungeVignetteMaxLevel = 1f;

    [Range(0f, 1f)] public float loungeLineStartLevel = 0f;
    [Range(0f, 1f)] public float loungeLineMaxLevel = 1f;

    [Header("Apply In Start")]
    public bool applyMaterialVisualSettingsOnStart = true;

    [Header("Global Grid Visual Settings")]
    public float globalGridEnable = 1f;
    public float globalGridUVSelect = 0f;
    public float globalGridScale = 14f;
    public float globalGridLineWidth = 0.01f;
    public float globalGridRateWidthBoost = 1.5f;
    public float globalGridIntensity = 0.05f;
    public float globalGridRateIntensityBoost = 0.4f;
    public Color globalGridColor = new Color(0.75f, 0.88f, 1f, 1f);
    public float globalGridEdgeBias = 0.75f;
    public float globalGridEdgeBiasPower = 1.5f;

    private const string PROP_VIGNETTE = "_VignetteStrength";
    private const string PROP_RATE = "_MotionRate";

    private const string PROP_GRID_ENABLE = "_GridEnable";
    private const string PROP_GRID_UV_SELECT = "_GridUVSelect";
    private const string PROP_GRID_SCALE = "_GridScale";
    private const string PROP_GRID_LINE_WIDTH = "_GridLineWidth";
    private const string PROP_GRID_RATE_WIDTH_BOOST = "_GridRateWidthBoost";
    private const string PROP_GRID_INTENSITY = "_GridIntensity";
    private const string PROP_GRID_RATE_INTENSITY_BOOST = "_GridRateIntensityBoost";
    private const string PROP_GRID_COLOR = "_GridColor";
    private const string PROP_GRID_EDGE_BIAS = "_GridEdgeBias";
    private const string PROP_GRID_EDGE_BIAS_POWER = "_GridEdgeBiasPower";

    private void Start()
    {
        if (applyMaterialVisualSettingsOnStart)
        {
            ApplyGlobalGridVisualSettingsToAll();
        }

        ApplyNow();
    }

    private void Update()
    {
        ApplyNow();
    }

    public void ApplyNow()
    {
        ApplyTarget(TARGET_COCKPIT);
        ApplyTarget(TARGET_LOUNGE);
    }

    private void ApplyTarget(int target)
    {
        bool enabled = GetTargetEnabled(target);
        Material[] mats = GetTargetMaterials(target);
        if (mats == null || mats.Length == 0) return;

        if (!enabled)
        {
            WriteDrive(mats, 0f, 0f);
            return;
        }

        int mode = GetMode(target);
        if (mode == MODE_OFF)
        {
            WriteDrive(mats, 0f, 0f);
            return;
        }

        float maxRate = Mathf.Max(0.0001f, GetMaxAngularRate(target));
        float exponent = Mathf.Max(0.0001f, GetResponseExponent(target));

        float wxWeight = Mathf.Max(0f, GetWxWeight(target));
        float wyWeight = Mathf.Max(0f, GetWyWeight(target));
        float wzWeight = Mathf.Max(0f, GetWzWeight(target));

        float wx = 0f;
        float wy = 0f;
        float wz = 0f;

        if (attitudeState != null)
        {
            wx = (float)attitudeState.wx;
            wy = (float)attitudeState.wy;
            wz = (float)attitudeState.wz;
        }

        float weightedWx = wx * wxWeight;
        float weightedWy = wy * wyWeight;
        float weightedWz = wz * wzWeight;

        float rateMag = Mathf.Sqrt(
            weightedWx * weightedWx +
            weightedWy * weightedWy +
            weightedWz * weightedWz
        );

        float normalized = Mathf.Clamp01(rateMag / maxRate);
        float curve01 = Mathf.Pow(normalized, exponent);

        float vignetteStart = Mathf.Clamp01(GetVignetteStartLevel(target));
        float vignetteMax = Mathf.Clamp01(GetVignetteMaxLevel(target));
        float lineStart = Mathf.Clamp01(GetLineStartLevel(target));
        float lineMax = Mathf.Clamp01(GetLineMaxLevel(target));

        float vignetteValue = Mathf.Lerp(vignetteStart, vignetteMax, curve01);
        float lineValue = Mathf.Lerp(lineStart, lineMax, curve01);

        if (mode == MODE_VIGNETTE)
        {
            lineValue = 0f;
        }
        else if (mode == MODE_LINES)
        {
            vignetteValue = 0f;
        }

        WriteDrive(mats, vignetteValue, lineValue);
    }

    private void WriteDrive(Material[] mats, float vignetteValue, float lineValue)
    {
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null) continue;

            m.SetFloat(PROP_VIGNETTE, vignetteValue);
            m.SetFloat(PROP_RATE, lineValue);
        }
    }

    private Material[] GetTargetMaterials(int target)
    {
        return target == TARGET_COCKPIT ? cockpitMaterials : loungeMaterials;
    }

    private bool GetTargetEnabled(int target)
    {
        return target == TARGET_COCKPIT ? cockpitEnabled : loungeEnabled;
    }

    private bool UsesGlobalProfile(int target)
    {
        return target == TARGET_COCKPIT ? cockpitUsesGlobalProfile : loungeUsesGlobalProfile;
    }

    private int GetMode(int target)
    {
        if (UsesGlobalProfile(target)) return globalMode;
        return target == TARGET_COCKPIT ? cockpitMode : loungeMode;
    }

    private float GetMaxAngularRate(int target)
    {
        if (UsesGlobalProfile(target)) return globalMaxAngularRateRad;
        return target == TARGET_COCKPIT ? cockpitMaxAngularRateRad : loungeMaxAngularRateRad;
    }

    private float GetResponseExponent(int target)
    {
        if (UsesGlobalProfile(target)) return globalResponseExponent;
        return target == TARGET_COCKPIT ? cockpitResponseExponent : loungeResponseExponent;
    }

    private float GetWxWeight(int target)
    {
        if (UsesGlobalProfile(target)) return globalWxWeight;
        return target == TARGET_COCKPIT ? cockpitWxWeight : loungeWxWeight;
    }

    private float GetWyWeight(int target)
    {
        if (UsesGlobalProfile(target)) return globalWyWeight;
        return target == TARGET_COCKPIT ? cockpitWyWeight : loungeWyWeight;
    }

    private float GetWzWeight(int target)
    {
        if (UsesGlobalProfile(target)) return globalWzWeight;
        return target == TARGET_COCKPIT ? cockpitWzWeight : loungeWzWeight;
    }

    private float GetVignetteStartLevel(int target)
    {
        if (UsesGlobalProfile(target)) return globalVignetteStartLevel;
        return target == TARGET_COCKPIT ? cockpitVignetteStartLevel : loungeVignetteStartLevel;
    }

    private float GetVignetteMaxLevel(int target)
    {
        if (UsesGlobalProfile(target)) return globalVignetteMaxLevel;
        return target == TARGET_COCKPIT ? cockpitVignetteMaxLevel : loungeVignetteMaxLevel;
    }

    private float GetLineStartLevel(int target)
    {
        if (UsesGlobalProfile(target)) return globalLineStartLevel;
        return target == TARGET_COCKPIT ? cockpitLineStartLevel : loungeLineStartLevel;
    }

    private float GetLineMaxLevel(int target)
    {
        if (UsesGlobalProfile(target)) return globalLineMaxLevel;
        return target == TARGET_COCKPIT ? cockpitLineMaxLevel : loungeLineMaxLevel;
    }

    // --------------------------------------------------------------------
    // Convenience API
    // --------------------------------------------------------------------

    public void EnableCockpit() { cockpitEnabled = true; }
    public void DisableCockpit() { cockpitEnabled = false; }
    public void EnableLounge() { loungeEnabled = true; }
    public void DisableLounge() { loungeEnabled = false; }

    public void SetCockpitUsesGlobalProfile(bool value) { cockpitUsesGlobalProfile = value; }
    public void SetLoungeUsesGlobalProfile(bool value) { loungeUsesGlobalProfile = value; }

    public void SetGlobalModeOff() { globalMode = MODE_OFF; }
    public void SetGlobalModeVignette() { globalMode = MODE_VIGNETTE; }
    public void SetGlobalModeLines() { globalMode = MODE_LINES; }
    public void SetGlobalModeBoth() { globalMode = MODE_BOTH; }

    public void SetCockpitModeOff() { cockpitMode = MODE_OFF; }
    public void SetCockpitModeVignette() { cockpitMode = MODE_VIGNETTE; }
    public void SetCockpitModeLines() { cockpitMode = MODE_LINES; }
    public void SetCockpitModeBoth() { cockpitMode = MODE_BOTH; }

    public void SetLoungeModeOff() { loungeMode = MODE_OFF; }
    public void SetLoungeModeVignette() { loungeMode = MODE_VIGNETTE; }
    public void SetLoungeModeLines() { loungeMode = MODE_LINES; }
    public void SetLoungeModeBoth() { loungeMode = MODE_BOTH; }

    // --------------------------------------------------------------------
    // General API
    // --------------------------------------------------------------------

    public void SetGlobalRateMapping(float maxAngularRateRad, float responseExponent)
    {
        globalMaxAngularRateRad = Mathf.Max(0.0001f, maxAngularRateRad);
        globalResponseExponent = Mathf.Max(0.0001f, responseExponent);
    }

    public void SetGlobalAxisWeights(float wxWeight, float wyWeight, float wzWeight)
    {
        globalWxWeight = Mathf.Max(0f, wxWeight);
        globalWyWeight = Mathf.Max(0f, wyWeight);
        globalWzWeight = Mathf.Max(0f, wzWeight);
    }

    public void SetGlobalEffectRanges(float vignetteStart, float vignetteMax, float lineStart, float lineMax)
    {
        globalVignetteStartLevel = Mathf.Clamp01(vignetteStart);
        globalVignetteMaxLevel = Mathf.Clamp01(vignetteMax);
        globalLineStartLevel = Mathf.Clamp01(lineStart);
        globalLineMaxLevel = Mathf.Clamp01(lineMax);
    }

    public void SetCockpitRateMapping(float maxAngularRateRad, float responseExponent)
    {
        cockpitMaxAngularRateRad = Mathf.Max(0.0001f, maxAngularRateRad);
        cockpitResponseExponent = Mathf.Max(0.0001f, responseExponent);
    }

    public void SetCockpitAxisWeights(float wxWeight, float wyWeight, float wzWeight)
    {
        cockpitWxWeight = Mathf.Max(0f, wxWeight);
        cockpitWyWeight = Mathf.Max(0f, wyWeight);
        cockpitWzWeight = Mathf.Max(0f, wzWeight);
    }

    public void SetCockpitEffectRanges(float vignetteStart, float vignetteMax, float lineStart, float lineMax)
    {
        cockpitVignetteStartLevel = Mathf.Clamp01(vignetteStart);
        cockpitVignetteMaxLevel = Mathf.Clamp01(vignetteMax);
        cockpitLineStartLevel = Mathf.Clamp01(lineStart);
        cockpitLineMaxLevel = Mathf.Clamp01(lineMax);
    }

    public void SetLoungeRateMapping(float maxAngularRateRad, float responseExponent)
    {
        loungeMaxAngularRateRad = Mathf.Max(0.0001f, maxAngularRateRad);
        loungeResponseExponent = Mathf.Max(0.0001f, responseExponent);
    }

    public void SetLoungeAxisWeights(float wxWeight, float wyWeight, float wzWeight)
    {
        loungeWxWeight = Mathf.Max(0f, wxWeight);
        loungeWyWeight = Mathf.Max(0f, wyWeight);
        loungeWzWeight = Mathf.Max(0f, wzWeight);
    }

    public void SetLoungeEffectRanges(float vignetteStart, float vignetteMax, float lineStart, float lineMax)
    {
        loungeVignetteStartLevel = Mathf.Clamp01(vignetteStart);
        loungeVignetteMaxLevel = Mathf.Clamp01(vignetteMax);
        loungeLineStartLevel = Mathf.Clamp01(lineStart);
        loungeLineMaxLevel = Mathf.Clamp01(lineMax);
    }

    // --------------------------------------------------------------------
    // Grid visual settings API
    // These are shader visual params, not the motion mapping.
    // --------------------------------------------------------------------

    public void ApplyGlobalGridVisualSettingsToAll()
    {
        ApplyGridVisualSettingsToMaterials(cockpitMaterials,
            globalGridEnable, globalGridUVSelect, globalGridScale,
            globalGridLineWidth, globalGridRateWidthBoost,
            globalGridIntensity, globalGridRateIntensityBoost,
            globalGridColor, globalGridEdgeBias, globalGridEdgeBiasPower);

        ApplyGridVisualSettingsToMaterials(loungeMaterials,
            globalGridEnable, globalGridUVSelect, globalGridScale,
            globalGridLineWidth, globalGridRateWidthBoost,
            globalGridIntensity, globalGridRateIntensityBoost,
            globalGridColor, globalGridEdgeBias, globalGridEdgeBiasPower);
    }

    public void SetCockpitGridVisualSettings(
        float enable,
        float uvSelect,
        float scale,
        float lineWidth,
        float rateWidthBoost,
        float intensity,
        float rateIntensityBoost,
        Color color,
        float edgeBias,
        float edgeBiasPower)
    {
        ApplyGridVisualSettingsToMaterials(cockpitMaterials,
            enable, uvSelect, scale, lineWidth, rateWidthBoost,
            intensity, rateIntensityBoost, color, edgeBias, edgeBiasPower);
    }

    public void SetLoungeGridVisualSettings(
        float enable,
        float uvSelect,
        float scale,
        float lineWidth,
        float rateWidthBoost,
        float intensity,
        float rateIntensityBoost,
        Color color,
        float edgeBias,
        float edgeBiasPower)
    {
        ApplyGridVisualSettingsToMaterials(loungeMaterials,
            enable, uvSelect, scale, lineWidth, rateWidthBoost,
            intensity, rateIntensityBoost, color, edgeBias, edgeBiasPower);
    }

    private void ApplyGridVisualSettingsToMaterials(
        Material[] mats,
        float enable,
        float uvSelect,
        float scale,
        float lineWidth,
        float rateWidthBoost,
        float intensity,
        float rateIntensityBoost,
        Color color,
        float edgeBias,
        float edgeBiasPower)
    {
        if (mats == null) return;

        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null) continue;

            m.SetFloat(PROP_GRID_ENABLE, Mathf.Clamp01(enable));
            m.SetFloat(PROP_GRID_UV_SELECT, Mathf.Clamp01(uvSelect));
            m.SetFloat(PROP_GRID_SCALE, Mathf.Max(0.0001f, scale));
            m.SetFloat(PROP_GRID_LINE_WIDTH, Mathf.Max(0.0001f, lineWidth));
            m.SetFloat(PROP_GRID_RATE_WIDTH_BOOST, Mathf.Max(0f, rateWidthBoost));
            m.SetFloat(PROP_GRID_INTENSITY, Mathf.Clamp01(intensity));
            m.SetFloat(PROP_GRID_RATE_INTENSITY_BOOST, Mathf.Max(0f, rateIntensityBoost));
            m.SetColor(PROP_GRID_COLOR, color);
            m.SetFloat(PROP_GRID_EDGE_BIAS, Mathf.Max(0f, edgeBias));
            m.SetFloat(PROP_GRID_EDGE_BIAS_POWER, Mathf.Max(0.0001f, edgeBiasPower));
        }
    }
}