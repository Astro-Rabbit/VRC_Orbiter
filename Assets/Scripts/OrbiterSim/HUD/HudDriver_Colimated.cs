using UdonSharp;
using UnityEngine;

public class HudDriver_Colimated : UdonSharpBehaviour
{
    [Header("Primary HUD Renderer / Materials")]
    public Renderer hudRenderer;
    public Material orbitHudMat;
    public Material dockHudMat;

    [Header("Secondary HUD Renderer / Materials")]
    public Renderer hudRenderer2;
    public Material orbitHudMat2;
    public Material dockHudMat2;

    [Header("References")]
    public GuidanceNavCoreState nav;
    public GuidanceNavContactsState contacts;

    [Header("Primary HUD config")]
    [Tooltip("0=OFF, 1=GROUND, 2=ORBIT, 3=DOCK")]
    public byte hudMode = 2;

    [Tooltip("Angular half-width of HUD in body-frame radians.")]
    public float hudHalfFovX = 0.25f;

    [Tooltip("Angular half-height of HUD in body-frame radians.")]
    public float hudHalfFovY = 0.18f;

    [Header("Secondary HUD config")]
    [Tooltip("0=OFF, 1=GROUND, 2=ORBIT, 3=DOCK")]
    public byte hudMode2 = 2;

    [Tooltip("Angular half-width of secondary HUD in body-frame radians.")]
    public float hudHalfFovX2 = 0.25f;

    [Tooltip("Angular half-height of secondary HUD in body-frame radians.")]
    public float hudHalfFovY2 = 0.18f;

    [Header("Debug / fallback")]
    public bool useFallbackIfInvalid = true;

    [Tooltip("Fallback prograde dir in body frame.")]
    public Vector3 fallbackPrograde_B = new Vector3(0f, 0f, 1f);

    [Tooltip("Fallback radial-out dir in body frame.")]
    public Vector3 fallbackRadialOut_B = new Vector3(1f, 0f, 0f);

    [Tooltip("Fallback normal dir in body frame.")]
    public Vector3 fallbackNormal_B = new Vector3(0f, 1f, 0f);

    [Header("Font")]
    public HudFontData fontData;

    [Header("Font layout/tuning")]
    public float fontSignWidthScale = 0.55f;
    public float fontSignHeightScale = 0.35f;
    public float fontSdfEdge = 0.5f;
    public float fontSdfSoftness = 8f;

    [Header("Temporary target name (5-char max)")]
    [Tooltip("Temporary inspector-defined target abbreviation. Later this can come from contacts/selection state.")]
    public string targetName = "ISS";

    [Header("Dock 3D Marker")]
    public Transform dockPortMarkerRoot;
    public bool dockMarkerFaceCamera = false;
    public float dockMarkerForwardOffset = 0.05f;

    [Header("Dock 3D Gates")]
    public Transform[] dockGateRoots;
    public float[] dockGateDistancesMeters = new float[] { 30f, 24f, 18f, 12f, 6f, 3f };
    public float dockGateForwardOffset = 0.0f;

    [Header("Primary Panel Control Inputs")]
    [Tooltip("Raw knob value for primary HUD mode selection.")]
    public float hudModeKnobValue = 180f;

    [Tooltip("Raw knob value for primary HUD intensity control.")]
    public float hudIntensityKnobValue = 100f;

    [Tooltip("Raw knob value for primary glass tint/dim control.")]
    public float glassTintKnobValue = 0f;

    [Header("Secondary Panel Control Inputs")]
    [Tooltip("Raw knob value for secondary HUD mode selection.")]
    public float hudModeKnobValue2 = 180f;

    [Tooltip("Raw knob value for secondary HUD intensity control.")]
    public float hudIntensityKnobValue2 = 100f;

    [Tooltip("Raw knob value for secondary glass tint/dim control.")]
    public float glassTintKnobValue2 = 0f;

    [Header("HUD panel tuning")]
    [Tooltip("Intensity multiplier at knob minimum.")]
    public float minHudIntensity = 0.0f;

    [Tooltip("Intensity multiplier at knob maximum.")]
    public float maxHudIntensity = 2.0f;

    [Tooltip("Glass alpha at tint knob minimum.")]
    public float minGlassAlpha = 0.02f;

    [Tooltip("Glass alpha at tint knob maximum.")]
    public float maxGlassAlpha = 0.18f;

    [Tooltip("Glass tint color at tint knob minimum.")]
    public Color minGlassTint = new Color(0.00f, 0.00f, 0.00f, 1.0f);

    [Tooltip("Glass tint color at tint knob maximum.")]
    public Color maxGlassTint = new Color(0.15f, 0.35f, 0.18f, 1.0f);

    // Primary active material cache
    private Material _activeMat;
    private Material _lastAssignedMat;

    // Secondary active material cache
    private Material _activeMat2;
    private Material _lastAssignedMat2;

    // Static material cache (primary)
    private Material _lastStaticMat;
    private HudFontData _lastFontData;
    private float _lastFontSdfEdge;
    private float _lastFontSdfSoftness;
    private float _lastFontSignWidthScale;
    private float _lastFontSignHeightScale;

    // Static material cache (secondary)
    private Material _lastStaticMat2;
    private HudFontData _lastFontData2;
    private float _lastFontSdfEdge2;
    private float _lastFontSdfSoftness2;
    private float _lastFontSignWidthScale2;
    private float _lastFontSignHeightScale2;

    private float _appliedHudIntensity = 1.0f;
    private float _appliedGlassAlpha = 0.08f;
    private Color _appliedGlassTint = new Color(0.15f, 0.35f, 0.18f, 1.0f);

    private float _appliedHudIntensity2 = 1.0f;
    private float _appliedGlassAlpha2 = 0.08f;
    private Color _appliedGlassTint2 = new Color(0.15f, 0.35f, 0.18f, 1.0f);

    private string _lastTargetName;
    private string _lastTargetName2;

    private void Start()
    {
        ApplyHudModeFromKnob();
        ApplyHudIntensityFromKnob();
        ApplyGlassTintFromKnob();

        ApplyHudMode2FromKnob();
        ApplyHudIntensity2FromKnob();
        ApplyGlassTint2FromKnob();

        UpdateActiveMaterial(true);
        UpdateActiveMaterial2(true);

        PushStaticMaterialState(true);
        PushStaticMaterialState2(true);
    }

    private void OnEnable()
    {
        UpdateActiveMaterial(true);
        UpdateActiveMaterial2(true);

        PushStaticMaterialState(true);
        PushStaticMaterialState2(true);
    }

    private void Update()
    {
        UpdateActiveMaterial(false);
        UpdateActiveMaterial2(false);

        if (_activeMat != null)
        {
            PushStaticMaterialState(false);

            _activeMat.SetFloat("_HudMode", (float)hudMode);
            _activeMat.SetFloat("_HudHalfFovX", hudHalfFovX);
            _activeMat.SetFloat("_HudHalfFovY", hudHalfFovY);
            _activeMat.SetFloat("_HudIntensity", _appliedHudIntensity);
            _activeMat.SetFloat("_GlassAlpha", _appliedGlassAlpha);
            _activeMat.SetColor("_GlassTint", _appliedGlassTint);

            if (hudMode == 2)
            {
                WriteOrbitModeTo(_activeMat, hudHalfFovX, hudHalfFovY);
                PushTargetNameIfNeeded();
            }
            else if (hudMode == 3)
            {
                WriteDockModeTo(_activeMat, hudHalfFovX, hudHalfFovY);
            }
            else
            {
                // Non-orbit/non-dock primary HUD
                UpdateDockPortMarker(Vector3.zero, Quaternion.identity, Vector3.forward, false);
                UpdateDockGates(Vector3.zero, Quaternion.identity, Vector3.forward, false);
            }
        }

        if (_activeMat2 != null)
        {
            PushStaticMaterialState2(false);

            _activeMat2.SetFloat("_HudMode", (float)hudMode2);
            _activeMat2.SetFloat("_HudHalfFovX", hudHalfFovX2);
            _activeMat2.SetFloat("_HudHalfFovY", hudHalfFovY2);
            _activeMat2.SetFloat("_HudIntensity", _appliedHudIntensity2);
            _activeMat2.SetFloat("_GlassAlpha", _appliedGlassAlpha2);
            _activeMat2.SetColor("_GlassTint", _appliedGlassTint2);

            if (hudMode2 == 2)
            {
                WriteOrbitModeTo(_activeMat2, hudHalfFovX2, hudHalfFovY2);
                PushTargetNameIfNeeded2();
            }
            else if (hudMode2 == 3)
            {
                WriteDockModeTo(_activeMat2, hudHalfFovX2, hudHalfFovY2);
            }
        }
    }

    // ============================================================
    // Material selection
    // ============================================================

    private void UpdateActiveMaterial(bool force)
    {
        Material desired = null;

        if (hudMode == 2) desired = orbitHudMat;
        else if (hudMode == 3) desired = dockHudMat;
        else desired = orbitHudMat;

        _activeMat = desired;

        if (_activeMat == null) return;
        if (hudRenderer == null) return;

        if (!force && _lastAssignedMat == _activeMat) return;

        if (hudRenderer.sharedMaterial != _activeMat)
            hudRenderer.sharedMaterial = _activeMat;

        _lastAssignedMat = _activeMat;

        _lastStaticMat = null;
        _lastTargetName = null;
    }

    private void UpdateActiveMaterial2(bool force)
    {
        Material desired = null;

        if (hudMode2 == 2) desired = orbitHudMat2;
        else if (hudMode2 == 3) desired = dockHudMat2;
        else desired = orbitHudMat2;

        _activeMat2 = desired;

        if (_activeMat2 == null) return;
        if (hudRenderer2 == null) return;

        if (!force && _lastAssignedMat2 == _activeMat2) return;

        if (hudRenderer2.sharedMaterial != _activeMat2)
            hudRenderer2.sharedMaterial = _activeMat2;

        _lastAssignedMat2 = _activeMat2;

        _lastStaticMat2 = null;
        _lastTargetName2 = null;
    }

    // ============================================================
    // Orbit mode writes
    // ============================================================

    private void WriteOrbitModeTo(Material targetMat, float halfFovX, float halfFovY)
    {
        if (targetMat == null) return;

        Vector3 prograde_B = fallbackPrograde_B;
        Vector3 radialOut_B = fallbackRadialOut_B;
        Vector3 normal_B = fallbackNormal_B;

        bool haveNav = (nav != null && nav.valid);

        if (haveNav)
        {
            Quaternion qBE = nav.qBE;
            Quaternion qEB = new Quaternion(-qBE.x, -qBE.y, -qBE.z, qBE.w);

            Vector3 that_E = nav.That_E;
            Vector3 rhat_E = nav.Rhat_E;
            Vector3 nhat_E = nav.Nhat_E;

            bool thatOk = that_E.sqrMagnitude > 1e-8f;
            bool rhatOk = rhat_E.sqrMagnitude > 1e-8f;
            bool nhatOk = nhat_E.sqrMagnitude > 1e-8f;

            if (thatOk)
            {
                prograde_B = qEB * that_E;
                if (prograde_B.sqrMagnitude > 1e-8f) prograde_B.Normalize();
                else thatOk = false;
            }

            if (rhatOk)
            {
                radialOut_B = qEB * rhat_E;
                if (radialOut_B.sqrMagnitude > 1e-8f) radialOut_B.Normalize();
                else rhatOk = false;
            }

            if (nhatOk)
            {
                normal_B = qEB * nhat_E;
                if (normal_B.sqrMagnitude > 1e-8f) normal_B.Normalize();
                else nhatOk = false;
            }

            if (!useFallbackIfInvalid)
            {
                if (!thatOk) prograde_B = Vector3.forward;
                if (!rhatOk) radialOut_B = Vector3.right;
                if (!nhatOk) normal_B = Vector3.up;
            }
        }
        else if (useFallbackIfInvalid)
        {
            if (fallbackPrograde_B.sqrMagnitude > 1e-8f) prograde_B = fallbackPrograde_B.normalized;
            else prograde_B = Vector3.forward;

            if (fallbackRadialOut_B.sqrMagnitude > 1e-8f) radialOut_B = fallbackRadialOut_B.normalized;
            else radialOut_B = Vector3.right;

            if (fallbackNormal_B.sqrMagnitude > 1e-8f) normal_B = fallbackNormal_B.normalized;
            else normal_B = Vector3.up;
        }

        targetMat.SetVector("_ProgradeDir_B", new Vector4(prograde_B.x, prograde_B.y, prograde_B.z, 0f));
        targetMat.SetVector("_RadialOutDir_B", new Vector4(radialOut_B.x, radialOut_B.y, radialOut_B.z, 0f));
        targetMat.SetVector("_NormalDir_B", new Vector4(normal_B.x, normal_B.y, normal_B.z, 0f));

        bool targetValid = false;
        Vector2 targetPosHUD = Vector2.zero;
        float targetRangeMeters = 0f;

        bool relVelValid = false;
        Vector2 relVelProgHUD = Vector2.zero;
        Vector2 relVelRetroHUD = Vector2.zero;
        float relSpeedMps = 0f;

        if (contacts != null && contacts.selValid)
        {
            Vector3 targetPos_B = new Vector3(
                (float)contacts.sel_drx_B,
                (float)contacts.sel_dry_B,
                (float)contacts.sel_drz_B
            );

            float targetPosSq = targetPos_B.sqrMagnitude;
            if (targetPosSq > 1e-8f)
            {
                targetValid = true;
                targetPosHUD = DirBToHudUV(targetPos_B, halfFovX, halfFovY);

                int sel = contacts.selectedStationIndex;
                if (sel >= 0 && contacts.range_m != null && sel < contacts.range_m.Length)
                    targetRangeMeters = (float)contacts.range_m[sel];
                else
                    targetRangeMeters = Mathf.Sqrt(targetPosSq);
            }

            Quaternion qBE_forRel = Quaternion.identity;
            if (nav != null && nav.valid)
                qBE_forRel = nav.qBE;

            Vector3 relVel_E = new Vector3(
                (float)contacts.sel_dvx_E,
                (float)contacts.sel_dvy_E,
                (float)contacts.sel_dvz_E
            );

            Vector3 relVel_B = -RotateEToBody(qBE_forRel, relVel_E);
            float relVelSq = relVel_B.sqrMagnitude;

            if (relVelSq > 1e-10f)
            {
                relVelValid = true;

                Vector3 relVelProg_B = relVel_B.normalized;
                Vector3 relVelRetro_B = -relVelProg_B;

                relVelProgHUD = DirBToHudUV(relVelProg_B, halfFovX, halfFovY);
                relVelRetroHUD = DirBToHudUV(relVelRetro_B, halfFovX, halfFovY);

                relSpeedMps = Mathf.Sqrt(relVelSq);
            }
        }

        targetMat.SetFloat("_TargetValid", targetValid ? 1f : 0f);
        targetMat.SetVector("_TargetPos_HUD", new Vector4(targetPosHUD.x, targetPosHUD.y, 0f, 0f));
        targetMat.SetFloat("_TargetRangeMeters", targetRangeMeters);

        targetMat.SetFloat("_TargetRelVelValid", relVelValid ? 1f : 0f);
        targetMat.SetVector("_TargetRelVelProg_HUD", new Vector4(relVelProgHUD.x, relVelProgHUD.y, 0f, 0f));
        targetMat.SetVector("_TargetRelVelRetro_HUD", new Vector4(relVelRetroHUD.x, relVelRetroHUD.y, 0f, 0f));
        targetMat.SetFloat("_TargetRelSpeedMps", relSpeedMps);

        UpdateDockPortMarker(Vector3.zero, Quaternion.identity, Vector3.forward, false);
        UpdateDockGates(Vector3.zero, Quaternion.identity, Vector3.forward, false);
    }

    // ============================================================
    // Dock mode writes
    // ============================================================

    private void WriteDockModeTo(Material targetMat, float halfFovX, float halfFovY)
    {
        if (targetMat == null) return;

        bool dockValid = false;
        float dockRangeMeters = 0f;
        float dockClosureMps = 0f;
        bool dockRelVelValid = false;
        Vector2 dockRelVelProgHUD = Vector2.zero;
        Vector2 dockRelVelRetroHUD = Vector2.zero;
        float dockRelSpeedMps = 0f;

        if (contacts != null && contacts.dockValid0)
        {
            Vector3 targetPortPos_B = new Vector3(
                (float)contacts.targetPort_px_B0,
                (float)contacts.targetPort_py_B0,
                (float)contacts.targetPort_pz_B0
            );

            Vector3 dockErr_B = new Vector3(
                (float)contacts.dockErr_px_B0,
                (float)contacts.dockErr_py_B0,
                (float)contacts.dockErr_pz_B0
            );

            Quaternion qTargetPortInB = contacts.qTargetPortInB0;
            Vector3 portForward_B = qTargetPortInB * Vector3.forward;

            dockValid = true;
            dockRangeMeters = dockErr_B.magnitude;

            Quaternion qBE_forRel = Quaternion.identity;
            if (nav != null && nav.valid)
                qBE_forRel = nav.qBE;

            Vector3 relVel_E = new Vector3(
                (float)contacts.sel_dvx_E,
                (float)contacts.sel_dvy_E,
                (float)contacts.sel_dvz_E
            );

            Vector3 relVel_B = -RotateEToBody(qBE_forRel, relVel_E);

            float relVelSq = relVel_B.sqrMagnitude;
            if (relVelSq > 1e-10f)
            {
                dockRelVelValid = true;

                Vector3 relVelProg_B = relVel_B.normalized;
                Vector3 relVelRetro_B = -relVelProg_B;

                dockRelVelProgHUD = DirBToHudUV(relVelProg_B, halfFovX, halfFovY);
                dockRelVelRetroHUD = DirBToHudUV(relVelRetro_B, halfFovX, halfFovY);

                dockRelSpeedMps = Mathf.Sqrt(relVelSq);
            }

            dockClosureMps = Vector3.Dot(relVel_B, -portForward_B);

            UpdateDockPortMarker(targetPortPos_B, qTargetPortInB, portForward_B, true);
            UpdateDockGates(targetPortPos_B, qTargetPortInB, portForward_B, true);
        }
        else
        {
            UpdateDockPortMarker(Vector3.zero, Quaternion.identity, Vector3.forward, false);
            UpdateDockGates(Vector3.zero, Quaternion.identity, Vector3.forward, false);
        }

        targetMat.SetFloat("_DockValid", dockValid ? 1f : 0f);
        targetMat.SetFloat("_DockRangeMeters", dockRangeMeters);
        targetMat.SetFloat("_DockClosureMps", dockClosureMps);

        targetMat.SetFloat("_DockRelVelValid", dockRelVelValid ? 1f : 0f);
        targetMat.SetVector("_DockRelVelProg_HUD", new Vector4(dockRelVelProgHUD.x, dockRelVelProgHUD.y, 0f, 0f));
        targetMat.SetVector("_DockRelVelRetro_HUD", new Vector4(dockRelVelRetroHUD.x, dockRelVelRetroHUD.y, 0f, 0f));
        targetMat.SetFloat("_DockRelSpeedMps", dockRelSpeedMps);
    }

    // ============================================================
    // Static material/font state
    // ============================================================

    private void PushStaticMaterialState(bool force)
    {
        if (_activeMat == null) return;

        bool matChanged =
            force ||
            _activeMat != _lastStaticMat ||
            fontData != _lastFontData ||
            !Mathf.Approximately(fontSdfEdge, _lastFontSdfEdge) ||
            !Mathf.Approximately(fontSdfSoftness, _lastFontSdfSoftness) ||
            !Mathf.Approximately(fontSignWidthScale, _lastFontSignWidthScale) ||
            !Mathf.Approximately(fontSignHeightScale, _lastFontSignHeightScale);

        if (!matChanged) return;

        _lastStaticMat = _activeMat;
        _lastFontData = fontData;
        _lastFontSdfEdge = fontSdfEdge;
        _lastFontSdfSoftness = fontSdfSoftness;
        _lastFontSignWidthScale = fontSignWidthScale;
        _lastFontSignHeightScale = fontSignHeightScale;

        if (fontData == null) return;

        PushFontDataToMaterial(_activeMat);

        _lastTargetName = null;
    }

    private void PushStaticMaterialState2(bool force)
    {
        if (_activeMat2 == null) return;

        bool matChanged =
            force ||
            _activeMat2 != _lastStaticMat2 ||
            fontData != _lastFontData2 ||
            !Mathf.Approximately(fontSdfEdge, _lastFontSdfEdge2) ||
            !Mathf.Approximately(fontSdfSoftness, _lastFontSdfSoftness2) ||
            !Mathf.Approximately(fontSignWidthScale, _lastFontSignWidthScale2) ||
            !Mathf.Approximately(fontSignHeightScale, _lastFontSignHeightScale2);

        if (!matChanged) return;

        _lastStaticMat2 = _activeMat2;
        _lastFontData2 = fontData;
        _lastFontSdfEdge2 = fontSdfEdge;
        _lastFontSdfSoftness2 = fontSdfSoftness;
        _lastFontSignWidthScale2 = fontSignWidthScale;
        _lastFontSignHeightScale2 = fontSignHeightScale;

        if (fontData == null) return;

        PushFontDataToMaterial(_activeMat2);

        _lastTargetName2 = null;
    }

    private void PushFontDataToMaterial(Material mat)
    {
        if (mat == null || fontData == null) return;

        mat.SetTexture("_FontAtlas", fontData.atlas);

        mat.SetFloat("_FontSdfEdge", fontSdfEdge);
        mat.SetFloat("_FontSdfSoftness", fontSdfSoftness);

        mat.SetVector("_FontUV_0", fontData.uv_0);
        mat.SetVector("_FontUV_1", fontData.uv_1);
        mat.SetVector("_FontUV_2", fontData.uv_2);
        mat.SetVector("_FontUV_3", fontData.uv_3);
        mat.SetVector("_FontUV_4", fontData.uv_4);
        mat.SetVector("_FontUV_5", fontData.uv_5);
        mat.SetVector("_FontUV_6", fontData.uv_6);
        mat.SetVector("_FontUV_7", fontData.uv_7);
        mat.SetVector("_FontUV_8", fontData.uv_8);
        mat.SetVector("_FontUV_9", fontData.uv_9);
        mat.SetVector("_FontUV_Minus", fontData.uv_minus);
        mat.SetVector("_FontUV_Plus", fontData.uv_plus);
        mat.SetVector("_FontUV_Dot", fontData.uv_dot);

        mat.SetFloat("_FontAspect_0", fontData.aspect_0);
        mat.SetFloat("_FontAspect_1", fontData.aspect_1);
        mat.SetFloat("_FontAspect_2", fontData.aspect_2);
        mat.SetFloat("_FontAspect_3", fontData.aspect_3);
        mat.SetFloat("_FontAspect_4", fontData.aspect_4);
        mat.SetFloat("_FontAspect_5", fontData.aspect_5);
        mat.SetFloat("_FontAspect_6", fontData.aspect_6);
        mat.SetFloat("_FontAspect_7", fontData.aspect_7);
        mat.SetFloat("_FontAspect_8", fontData.aspect_8);
        mat.SetFloat("_FontAspect_9", fontData.aspect_9);
        mat.SetFloat("_FontAspect_Minus", fontData.aspect_minus);
        mat.SetFloat("_FontAspect_Plus", fontData.aspect_plus);
        mat.SetFloat("_FontAspect_Dot", fontData.aspect_dot);

        mat.SetFloat("_FontSignWidthScale", fontSignWidthScale);
        mat.SetFloat("_FontSignHeightScale", fontSignHeightScale);

        mat.SetVector("_FontUV_A", fontData.uv_A);
        mat.SetVector("_FontUV_B", fontData.uv_B);
        mat.SetVector("_FontUV_C", fontData.uv_C);
        mat.SetVector("_FontUV_D", fontData.uv_D);
        mat.SetVector("_FontUV_E", fontData.uv_E);
        mat.SetVector("_FontUV_F", fontData.uv_F);
        mat.SetVector("_FontUV_G", fontData.uv_G);
        mat.SetVector("_FontUV_H", fontData.uv_H);
        mat.SetVector("_FontUV_I", fontData.uv_I);
        mat.SetVector("_FontUV_J", fontData.uv_J);
        mat.SetVector("_FontUV_K", fontData.uv_K);
        mat.SetVector("_FontUV_L", fontData.uv_L);
        mat.SetVector("_FontUV_M", fontData.uv_M);
        mat.SetVector("_FontUV_N", fontData.uv_N);
        mat.SetVector("_FontUV_O", fontData.uv_O);
        mat.SetVector("_FontUV_P", fontData.uv_P);
        mat.SetVector("_FontUV_Q", fontData.uv_Q);
        mat.SetVector("_FontUV_R", fontData.uv_R);
        mat.SetVector("_FontUV_S", fontData.uv_S);
        mat.SetVector("_FontUV_T", fontData.uv_T);
        mat.SetVector("_FontUV_U", fontData.uv_U);
        mat.SetVector("_FontUV_V", fontData.uv_V);
        mat.SetVector("_FontUV_W", fontData.uv_W);
        mat.SetVector("_FontUV_X", fontData.uv_X);
        mat.SetVector("_FontUV_Y", fontData.uv_Y);
        mat.SetVector("_FontUV_Z", fontData.uv_Z);

        mat.SetFloat("_FontAspect_A", fontData.aspect_A);
        mat.SetFloat("_FontAspect_B", fontData.aspect_B);
        mat.SetFloat("_FontAspect_C", fontData.aspect_C);
        mat.SetFloat("_FontAspect_D", fontData.aspect_D);
        mat.SetFloat("_FontAspect_E", fontData.aspect_E);
        mat.SetFloat("_FontAspect_F", fontData.aspect_F);
        mat.SetFloat("_FontAspect_G", fontData.aspect_G);
        mat.SetFloat("_FontAspect_H", fontData.aspect_H);
        mat.SetFloat("_FontAspect_I", fontData.aspect_I);
        mat.SetFloat("_FontAspect_J", fontData.aspect_J);
        mat.SetFloat("_FontAspect_K", fontData.aspect_K);
        mat.SetFloat("_FontAspect_L", fontData.aspect_L);
        mat.SetFloat("_FontAspect_M", fontData.aspect_M);
        mat.SetFloat("_FontAspect_N", fontData.aspect_N);
        mat.SetFloat("_FontAspect_O", fontData.aspect_O);
        mat.SetFloat("_FontAspect_P", fontData.aspect_P);
        mat.SetFloat("_FontAspect_Q", fontData.aspect_Q);
        mat.SetFloat("_FontAspect_R", fontData.aspect_R);
        mat.SetFloat("_FontAspect_S", fontData.aspect_S);
        mat.SetFloat("_FontAspect_T", fontData.aspect_T);
        mat.SetFloat("_FontAspect_U", fontData.aspect_U);
        mat.SetFloat("_FontAspect_V", fontData.aspect_V);
        mat.SetFloat("_FontAspect_W", fontData.aspect_W);
        mat.SetFloat("_FontAspect_X", fontData.aspect_X);
        mat.SetFloat("_FontAspect_Y", fontData.aspect_Y);
        mat.SetFloat("_FontAspect_Z", fontData.aspect_Z);
    }

    // ============================================================
    // Orbit target name
    // ============================================================

    private void PushTargetNameIfNeeded()
    {
        if (_activeMat == null) return;

        string safeName = SanitizeTargetName(targetName);

        if (_lastTargetName == safeName) return;
        _lastTargetName = safeName;

        int len = safeName.Length;
        _activeMat.SetFloat("_TargetNameLen", (float)len);

        _activeMat.SetFloat("_TargetNameC0", EncodeUpperIndexAt(safeName, 0));
        _activeMat.SetFloat("_TargetNameC1", EncodeUpperIndexAt(safeName, 1));
        _activeMat.SetFloat("_TargetNameC2", EncodeUpperIndexAt(safeName, 2));
        _activeMat.SetFloat("_TargetNameC3", EncodeUpperIndexAt(safeName, 3));
        _activeMat.SetFloat("_TargetNameC4", EncodeUpperIndexAt(safeName, 4));
    }

    private void PushTargetNameIfNeeded2()
    {
        if (_activeMat2 == null) return;

        string safeName = SanitizeTargetName(targetName);

        if (_lastTargetName2 == safeName) return;
        _lastTargetName2 = safeName;

        int len = safeName.Length;
        _activeMat2.SetFloat("_TargetNameLen", (float)len);

        _activeMat2.SetFloat("_TargetNameC0", EncodeUpperIndexAt(safeName, 0));
        _activeMat2.SetFloat("_TargetNameC1", EncodeUpperIndexAt(safeName, 1));
        _activeMat2.SetFloat("_TargetNameC2", EncodeUpperIndexAt(safeName, 2));
        _activeMat2.SetFloat("_TargetNameC3", EncodeUpperIndexAt(safeName, 3));
        _activeMat2.SetFloat("_TargetNameC4", EncodeUpperIndexAt(safeName, 4));
    }

    private static string SanitizeTargetName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        string up = raw.ToUpperInvariant();
        string filtered = "";

        int n = up.Length;
        if (n > 5) n = 5;

        for (int i = 0; i < n; i++)
        {
            char c = up[i];
            if (c >= 'A' && c <= 'Z')
                filtered += c;
        }

        return filtered;
    }

    private static float EncodeUpperIndexAt(string s, int i)
    {
        if (string.IsNullOrEmpty(s)) return -1f;
        if (i < 0 || i >= s.Length) return -1f;

        char c = s[i];
        if (c < 'A' || c > 'Z') return -1f;

        return (float)(c - 'A');
    }

    // ============================================================
    // Projection helpers
    // ============================================================

    private Vector2 DirBToHudUV(Vector3 dir_B, float halfFovX, float halfFovY)
    {
        if (dir_B.sqrMagnitude < 1e-8f) return Vector2.zero;

        dir_B.Normalize();

        float az = Mathf.Atan2(dir_B.x, dir_B.z);
        float el = Mathf.Atan2(dir_B.y, dir_B.z);

        Vector2 uvh;
        uvh.x = az / Mathf.Max(halfFovX, 1e-6f);
        uvh.y = el / Mathf.Max(halfFovY, 1e-6f);
        return uvh;
    }

    private static Vector3 RotateEToBody(Quaternion qBE, Vector3 vE)
    {
        Quaternion qEB = Quaternion.Inverse(qBE);
        return qEB * vE;
    }

    private void UpdateDockGates(Vector3 portPos_B, Quaternion portRot_B, Vector3 portForward_B, bool visible)
    {
        if (dockGateRoots == null) return;

        int gateCount = dockGateRoots.Length;
        int distCount = (dockGateDistancesMeters != null) ? dockGateDistancesMeters.Length : 0;
        int n = (gateCount < distCount) ? gateCount : distCount;

        for (int i = 0; i < gateCount; i++)
        {
            Transform gate = dockGateRoots[i];
            if (gate == null) continue;

            bool showThis = visible && (i < n);

            if (!showThis)
            {
                if (gate.gameObject.activeSelf)
                    gate.gameObject.SetActive(false);
                continue;
            }

            if (!gate.gameObject.activeSelf)
                gate.gameObject.SetActive(true);

            float d = dockGateDistancesMeters[i];
            Vector3 gatePos_B = portPos_B + portForward_B.normalized * (d + dockGateForwardOffset);

            gate.localPosition = gatePos_B;
            gate.localRotation = portRot_B;
        }
    }

    private void UpdateDockPortMarker(Vector3 portPos_B, Quaternion portRot_B, Vector3 portForward_B, bool visible)
    {
        if (dockPortMarkerRoot == null) return;

        if (!visible)
        {
            if (dockPortMarkerRoot.gameObject.activeSelf)
                dockPortMarkerRoot.gameObject.SetActive(false);
            return;
        }

        if (!dockPortMarkerRoot.gameObject.activeSelf)
            dockPortMarkerRoot.gameObject.SetActive(true);

        Vector3 markerPos_B = portPos_B + portForward_B.normalized * dockMarkerForwardOffset;
        dockPortMarkerRoot.localPosition = markerPos_B;

        if (!dockMarkerFaceCamera)
        {
            dockPortMarkerRoot.localRotation = portRot_B;
        }
        else
        {
            dockPortMarkerRoot.localRotation = portRot_B;
        }
    }

    // ============================================================
    // Knob UI hooks
    // ============================================================

    public void ApplyHudModeFromKnob()
    {
        float v = hudModeKnobValue;

        if (v < 22.5f) hudMode = 0;
        else if (v < 67.5f) hudMode = 1;
        else if (v < 112.5f) hudMode = 2;
        else hudMode = 3;
    }

    public void ApplyHudIntensityFromKnob()
    {
        float t = Mathf.InverseLerp(0f, 100f, hudIntensityKnobValue);
        _appliedHudIntensity = Mathf.Lerp(minHudIntensity, maxHudIntensity, t);
    }

    public void ApplyGlassTintFromKnob()
    {
        float t = Mathf.InverseLerp(0f, 100f, glassTintKnobValue);

        _appliedGlassAlpha = Mathf.Lerp(minGlassAlpha, maxGlassAlpha, t);
        _appliedGlassTint = Color.Lerp(minGlassTint, maxGlassTint, t);
    }

    public void ApplyHudMode2FromKnob()
    {
        float v = hudModeKnobValue2;

        if (v < 22.5f) hudMode2 = 0;
        else if (v < 67.5f) hudMode2 = 1;
        else if (v < 112.5f) hudMode2 = 2;
        else hudMode2 = 3;
    }

    public void ApplyHudIntensity2FromKnob()
    {
        float t = Mathf.InverseLerp(0f, 100f, hudIntensityKnobValue2);
        _appliedHudIntensity2 = Mathf.Lerp(minHudIntensity, maxHudIntensity, t);
    }

    public void ApplyGlassTint2FromKnob()
    {
        float t = Mathf.InverseLerp(0f, 100f, glassTintKnobValue2);

        _appliedGlassAlpha2 = Mathf.Lerp(minGlassAlpha, maxGlassAlpha, t);
        _appliedGlassTint2 = Color.Lerp(minGlassTint, maxGlassTint, t);
    }
}