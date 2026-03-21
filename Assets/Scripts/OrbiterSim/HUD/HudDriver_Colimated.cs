using UdonSharp;
using UnityEngine;

public class HudDriver_Colimated : UdonSharpBehaviour
{
    [Header("Pilot HUD Renderer / Materials")]
    public Renderer hudRenderer;
    public Material orbitHudMat;
    public Material dockHudMat;

    [Header("Copilot HUD Renderer / Materials")]
    public Renderer hudRenderer2;
    public Material orbitHudMat2;
    public Material dockHudMat2;

    [Header("References")]
    public GuidanceNavCoreState nav;
    public GuidanceNavContactsState contacts;

    [Header("Pilot HUD config")]
    [Tooltip("0=OFF, 1=GROUND, 2=ORBIT, 3=APPROACH, 4=DOCK")]
    public byte hudMode = 2;
    [Tooltip("Angular half-width of HUD in body-frame radians.")]
    public float hudHalfFovX = 0.25f;
    [Tooltip("Angular half-height of HUD in body-frame radians.")]
    public float hudHalfFovY = 0.18f;

    [Header("Copilot HUD config")]
    [Tooltip("0=OFF, 1=GROUND, 2=ORBIT, 3=APPROACH, 4=DOCK")]
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

    [Header("Body presentation mapping")]
    [Tooltip("Match the rendered body-frame convention used by skybox / station rendering.")]
    public bool flipPresentationX = true;

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

    [Header("Pilot Panel Control Inputs")]
    [Tooltip("Direct mode value for pilot HUD selection.")]
    public float hudModeKnobValue = 2f;
    [Tooltip("Raw knob value for pilot HUD intensity control.")]
    public float hudIntensityKnobValue = 100f;
    [Tooltip("Raw knob value for pilot glass tint/dim control.")]
    public float glassTintKnobValue = 0f;

    [Header("Copilot Panel Control Inputs")]
    [Tooltip("Direct mode value for copilot HUD selection.")]
    public float hudModeKnobValue2 = 2f;
    [Tooltip("Raw knob value for copilot HUD intensity control.")]
    public float hudIntensityKnobValue2 = 100f;
    [Tooltip("Raw knob value for copilot glass tint/dim control.")]
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

    // Pilot runtime
    private Material _pilotActiveMat;
    private Material _pilotLastAssignedMat;
    private MaterialPropertyBlock _pilotBlock;
    private bool _pilotStaticDirty = true;
    private string _pilotLastTargetName;

    // Copilot runtime
    private Material _copilotActiveMat;
    private Material _copilotLastAssignedMat;
    private MaterialPropertyBlock _copilotBlock;
    private bool _copilotStaticDirty = true;
    private string _copilotLastTargetName;

    // Cached knob-applied values
    private float _appliedHudIntensity = 1.0f;
    private float _appliedGlassAlpha = 0.08f;
    private Color _appliedGlassTint = new Color(0.15f, 0.35f, 0.18f, 1.0f);

    private float _appliedHudIntensity2 = 1.0f;
    private float _appliedGlassAlpha2 = 0.08f;
    private Color _appliedGlassTint2 = new Color(0.15f, 0.35f, 0.18f, 1.0f);

    private void Start()
    {
        if (_pilotBlock == null) _pilotBlock = new MaterialPropertyBlock();
        if (_copilotBlock == null) _copilotBlock = new MaterialPropertyBlock();

        ApplyHudModeFromKnob();
        ApplyHudIntensityFromKnob();
        ApplyGlassTintFromKnob();

        ApplyHudMode2FromKnob();
        ApplyHudIntensity2FromKnob();
        ApplyGlassTint2FromKnob();

        UpdatePilotMaterial(true);
        UpdateCopilotMaterial(true);

        _pilotStaticDirty = true;
        _copilotStaticDirty = true;
    }

    private void OnEnable()
    {
        if (_pilotBlock == null) _pilotBlock = new MaterialPropertyBlock();
        if (_copilotBlock == null) _copilotBlock = new MaterialPropertyBlock();

        UpdatePilotMaterial(true);
        UpdateCopilotMaterial(true);

        _pilotStaticDirty = true;
        _copilotStaticDirty = true;
    }

    private void Update()
    {
        UpdatePilotMaterial(false);
        UpdateCopilotMaterial(false);

        ApplyPilotHud();
        ApplyCopilotHud();
    }

    // ============================================================
    // Pilot / copilot material selection
    // ============================================================

    private void UpdatePilotMaterial(bool force)
    {
        Material desired = (hudMode == 4) ? dockHudMat : orbitHudMat;
        _pilotActiveMat = desired;

        if (_pilotActiveMat == null || hudRenderer == null) return;
        if (!force && _pilotLastAssignedMat == _pilotActiveMat) return;

        if (hudRenderer.sharedMaterial != _pilotActiveMat)
            hudRenderer.sharedMaterial = _pilotActiveMat;

        _pilotLastAssignedMat = _pilotActiveMat;
        _pilotStaticDirty = true;
        _pilotLastTargetName = null;
    }

    private void UpdateCopilotMaterial(bool force)
    {
        Material desired = (hudMode2 == 4) ? dockHudMat2 : orbitHudMat2;
        _copilotActiveMat = desired;

        if (_copilotActiveMat == null || hudRenderer2 == null) return;
        if (!force && _copilotLastAssignedMat == _copilotActiveMat) return;

        if (hudRenderer2.sharedMaterial != _copilotActiveMat)
            hudRenderer2.sharedMaterial = _copilotActiveMat;

        _copilotLastAssignedMat = _copilotActiveMat;
        _copilotStaticDirty = true;
        _copilotLastTargetName = null;
    }

    // ============================================================
    // Main apply paths
    // ============================================================

    private void ApplyPilotHud()
    {
        if (hudRenderer == null || _pilotActiveMat == null) return;
        if (_pilotBlock == null) _pilotBlock = new MaterialPropertyBlock();

        if (_pilotStaticDirty)
        {
            PushStaticBlockState(_pilotBlock);
            _pilotStaticDirty = false;
        }

        WriteCommonHudState(
            _pilotBlock,
            hudMode,
            hudHalfFovX,
            hudHalfFovY,
            _appliedHudIntensity,
            _appliedGlassAlpha,
            _appliedGlassTint
        );

        if (hudMode == 2)
        {
            WriteNavBaseTo(_pilotBlock);
            WriteOrbitCueTo(_pilotBlock, hudHalfFovX, hudHalfFovY);
            ClearDockWorldAids();
        }
        else if (hudMode == 3)
        {
            WriteNavBaseTo(_pilotBlock);
            WriteApproachCueTo(_pilotBlock, hudHalfFovX, hudHalfFovY);
            PushTargetNameToBlockIfNeeded(_pilotBlock, ref _pilotLastTargetName, targetName);
            ClearOrbitReadoutsIfNoApproachOverride(_pilotBlock);
            ClearDockWorldAids();
        }
        else if (hudMode == 4)
        {
            WriteDockModeToBlock(_pilotBlock, hudHalfFovX, hudHalfFovY);
            UpdateDockWorldAidsFromContacts();
        }
        else
        {
            ClearAllNavApproachOrbitDockOverlayState(_pilotBlock);
            ClearDockWorldAids();
        }

        hudRenderer.SetPropertyBlock(_pilotBlock);
    }

    private void ApplyCopilotHud()
    {
        if (hudRenderer2 == null || _copilotActiveMat2Missing()) return;
        if (_copilotBlock == null) _copilotBlock = new MaterialPropertyBlock();

        if (_copilotStaticDirty)
        {
            PushStaticBlockState(_copilotBlock);
            _copilotStaticDirty = false;
        }

        WriteCommonHudState(
            _copilotBlock,
            hudMode2,
            hudHalfFovX2,
            hudHalfFovY2,
            _appliedHudIntensity2,
            _appliedGlassAlpha2,
            _appliedGlassTint2
        );

        if (hudMode2 == 2)
        {
            WriteNavBaseTo(_copilotBlock);
            WriteOrbitCueTo(_copilotBlock, hudHalfFovX2, hudHalfFovY2);
        }
        else if (hudMode2 == 3)
        {
            WriteNavBaseTo(_copilotBlock);
            WriteApproachCueTo(_copilotBlock, hudHalfFovX2, hudHalfFovY2);
            PushTargetNameToBlockIfNeeded(_copilotBlock, ref _copilotLastTargetName, targetName);
            ClearOrbitReadoutsIfNoApproachOverride(_copilotBlock);
        }
        else if (hudMode2 == 4)
        {
            WriteDockModeToBlock(_copilotBlock, hudHalfFovX2, hudHalfFovY2);
        }
        else
        {
            ClearAllNavApproachOrbitDockOverlayState(_copilotBlock);
        }

        hudRenderer2.SetPropertyBlock(_copilotBlock);
    }

    private bool _copilotActiveMat2Missing()
    {
        return _copilotActiveMat == null;
    }

    // ============================================================
    // Common HUD block writes
    // ============================================================

    private void WriteCommonHudState(
        MaterialPropertyBlock block,
        byte mode,
        float halfFovX,
        float halfFovY,
        float hudIntensity,
        float glassAlpha,
        Color glassTint)
    {
        block.SetFloat("_HudMode", (float)mode);
        block.SetFloat("_HudHalfFovX", halfFovX);
        block.SetFloat("_HudHalfFovY", halfFovY);
        block.SetFloat("_HudIntensity", hudIntensity);
        block.SetFloat("_GlassAlpha", glassAlpha);
        block.SetColor("_GlassTint", glassTint);
    }

    private void PushStaticBlockState(MaterialPropertyBlock block)
    {
        if (block == null) return;
        if (fontData == null) return;

        block.SetTexture("_FontAtlas", fontData.atlas);

        block.SetFloat("_FontSdfEdge", fontSdfEdge);
        block.SetFloat("_FontSdfSoftness", fontSdfSoftness);

        block.SetVector("_FontUV_0", fontData.uv_0);
        block.SetVector("_FontUV_1", fontData.uv_1);
        block.SetVector("_FontUV_2", fontData.uv_2);
        block.SetVector("_FontUV_3", fontData.uv_3);
        block.SetVector("_FontUV_4", fontData.uv_4);
        block.SetVector("_FontUV_5", fontData.uv_5);
        block.SetVector("_FontUV_6", fontData.uv_6);
        block.SetVector("_FontUV_7", fontData.uv_7);
        block.SetVector("_FontUV_8", fontData.uv_8);
        block.SetVector("_FontUV_9", fontData.uv_9);
        block.SetVector("_FontUV_Minus", fontData.uv_minus);
        block.SetVector("_FontUV_Plus", fontData.uv_plus);
        block.SetVector("_FontUV_Dot", fontData.uv_dot);

        block.SetFloat("_FontAspect_0", fontData.aspect_0);
        block.SetFloat("_FontAspect_1", fontData.aspect_1);
        block.SetFloat("_FontAspect_2", fontData.aspect_2);
        block.SetFloat("_FontAspect_3", fontData.aspect_3);
        block.SetFloat("_FontAspect_4", fontData.aspect_4);
        block.SetFloat("_FontAspect_5", fontData.aspect_5);
        block.SetFloat("_FontAspect_6", fontData.aspect_6);
        block.SetFloat("_FontAspect_7", fontData.aspect_7);
        block.SetFloat("_FontAspect_8", fontData.aspect_8);
        block.SetFloat("_FontAspect_9", fontData.aspect_9);
        block.SetFloat("_FontAspect_Minus", fontData.aspect_minus);
        block.SetFloat("_FontAspect_Plus", fontData.aspect_plus);
        block.SetFloat("_FontAspect_Dot", fontData.aspect_dot);

        block.SetFloat("_FontSignWidthScale", fontSignWidthScale);
        block.SetFloat("_FontSignHeightScale", fontSignHeightScale);

        block.SetVector("_FontUV_A", fontData.uv_A);
        block.SetVector("_FontUV_B", fontData.uv_B);
        block.SetVector("_FontUV_C", fontData.uv_C);
        block.SetVector("_FontUV_D", fontData.uv_D);
        block.SetVector("_FontUV_E", fontData.uv_E);
        block.SetVector("_FontUV_F", fontData.uv_F);
        block.SetVector("_FontUV_G", fontData.uv_G);
        block.SetVector("_FontUV_H", fontData.uv_H);
        block.SetVector("_FontUV_I", fontData.uv_I);
        block.SetVector("_FontUV_J", fontData.uv_J);
        block.SetVector("_FontUV_K", fontData.uv_K);
        block.SetVector("_FontUV_L", fontData.uv_L);
        block.SetVector("_FontUV_M", fontData.uv_M);
        block.SetVector("_FontUV_N", fontData.uv_N);
        block.SetVector("_FontUV_O", fontData.uv_O);
        block.SetVector("_FontUV_P", fontData.uv_P);
        block.SetVector("_FontUV_Q", fontData.uv_Q);
        block.SetVector("_FontUV_R", fontData.uv_R);
        block.SetVector("_FontUV_S", fontData.uv_S);
        block.SetVector("_FontUV_T", fontData.uv_T);
        block.SetVector("_FontUV_U", fontData.uv_U);
        block.SetVector("_FontUV_V", fontData.uv_V);
        block.SetVector("_FontUV_W", fontData.uv_W);
        block.SetVector("_FontUV_X", fontData.uv_X);
        block.SetVector("_FontUV_Y", fontData.uv_Y);
        block.SetVector("_FontUV_Z", fontData.uv_Z);

        block.SetFloat("_FontAspect_A", fontData.aspect_A);
        block.SetFloat("_FontAspect_B", fontData.aspect_B);
        block.SetFloat("_FontAspect_C", fontData.aspect_C);
        block.SetFloat("_FontAspect_D", fontData.aspect_D);
        block.SetFloat("_FontAspect_E", fontData.aspect_E);
        block.SetFloat("_FontAspect_F", fontData.aspect_F);
        block.SetFloat("_FontAspect_G", fontData.aspect_G);
        block.SetFloat("_FontAspect_H", fontData.aspect_H);
        block.SetFloat("_FontAspect_I", fontData.aspect_I);
        block.SetFloat("_FontAspect_J", fontData.aspect_J);
        block.SetFloat("_FontAspect_K", fontData.aspect_K);
        block.SetFloat("_FontAspect_L", fontData.aspect_L);
        block.SetFloat("_FontAspect_M", fontData.aspect_M);
        block.SetFloat("_FontAspect_N", fontData.aspect_N);
        block.SetFloat("_FontAspect_O", fontData.aspect_O);
        block.SetFloat("_FontAspect_P", fontData.aspect_P);
        block.SetFloat("_FontAspect_Q", fontData.aspect_Q);
        block.SetFloat("_FontAspect_R", fontData.aspect_R);
        block.SetFloat("_FontAspect_S", fontData.aspect_S);
        block.SetFloat("_FontAspect_T", fontData.aspect_T);
        block.SetFloat("_FontAspect_U", fontData.aspect_U);
        block.SetFloat("_FontAspect_V", fontData.aspect_V);
        block.SetFloat("_FontAspect_W", fontData.aspect_W);
        block.SetFloat("_FontAspect_X", fontData.aspect_X);
        block.SetFloat("_FontAspect_Y", fontData.aspect_Y);
        block.SetFloat("_FontAspect_Z", fontData.aspect_Z);
    }

    // ============================================================
    // Nav / orbit / approach writes
    // ============================================================

    private void WriteNavBaseTo(MaterialPropertyBlock block)
    {
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

        prograde_B = MapBodyVectorToRender(prograde_B);
        radialOut_B = MapBodyVectorToRender(radialOut_B);
        normal_B = MapBodyVectorToRender(normal_B);

        block.SetVector("_ProgradeDir_B", new Vector4(prograde_B.x, prograde_B.y, prograde_B.z, 0f));
        block.SetVector("_RadialOutDir_B", new Vector4(radialOut_B.x, radialOut_B.y, radialOut_B.z, 0f));
        block.SetVector("_NormalDir_B", new Vector4(normal_B.x, normal_B.y, normal_B.z, 0f));
    }

    private void WriteOrbitCueTo(MaterialPropertyBlock block, float halfFovX, float halfFovY)
    {
        bool nodeValid = false;
        Vector2 nodePosHUD = Vector2.zero;
        float nodeDVmag = 0f;
        float nodeRemainingDV = 0f;

        if (nav != null && nav.valid && nav.selectedNodeVectorValid)
        {
            Vector3 nodeDir_E = nav.selectedNodeDir_E;
            if (nodeDir_E.sqrMagnitude > 1e-8f)
            {
                Quaternion qBE = nav.qBE;
                Vector3 nodeDir_B = RotateEToBody(qBE, nodeDir_E);
                nodeDir_B = MapBodyVectorToRender(nodeDir_B);

                if (nodeDir_B.sqrMagnitude > 1e-8f)
                {
                    nodeDir_B.Normalize();
                    nodeValid = true;
                    nodePosHUD = DirBToHudUV(nodeDir_B, halfFovX, halfFovY);
                    nodeDVmag = nav.selectedNodeDVmag_mps;
                    nodeRemainingDV = nav.selectedNodeRemainingDV_mps;
                }
            }
        }

        block.SetFloat("_NodeValid", nodeValid ? 1f : 0f);
        block.SetVector("_NodePos_HUD", new Vector4(nodePosHUD.x, nodePosHUD.y, 0f, 0f));
        block.SetFloat("_NodeDVmag_mps", nodeDVmag);
        block.SetFloat("_NodeRemainingDV_mps", nodeRemainingDV);

        block.SetFloat("_TargetValid", 0f);
        block.SetVector("_TargetPos_HUD", Vector4.zero);
        block.SetFloat("_TargetRangeMeters", 0f);
        block.SetFloat("_TargetRelVelValid", 0f);
        block.SetVector("_TargetRelVelProg_HUD", Vector4.zero);
        block.SetVector("_TargetRelVelRetro_HUD", Vector4.zero);
        block.SetFloat("_TargetRelSpeedMps", 0f);

        ClearTargetNameInBlock(block);

        float rdvValue;
        float rdvUnitCode;
        if (nodeValid) rdvValue = EncodeSpeedDisplay((double)nodeRemainingDV, out rdvUnitCode);
        else rdvValue = EncodeSpeedDisplay(0.0, out rdvUnitCode);

        bool apoValid = false;
        float apoValue = 0f;
        float apoUnitCode = 0f;

        bool perValid = false;
        float perValue = 0f;
        float perUnitCode = 0f;

        if (nav != null && nav.valid && nav.radiusPrimary > 0.0)
        {
            double e = nav.e;
            double p = nav.p;

            if (p > 0.0 && e > -1.0)
            {
                double rp = p / (1.0 + e);
                double periAlt = rp - nav.radiusPrimary;
                perValid = true;
                perValue = EncodeDistanceDisplay(periAlt, out perUnitCode);
            }

            if (p > 0.0 && e >= 0.0 && e < 1.0)
            {
                double ra = p / (1.0 - e);
                double apoAlt = ra - nav.radiusPrimary;
                apoValid = true;
                apoValue = EncodeDistanceDisplay(apoAlt, out apoUnitCode);
            }
        }

        block.SetFloat("_OrbitRDV_Value", rdvValue);
        block.SetFloat("_OrbitRDV_UnitCode", rdvUnitCode);

        block.SetFloat("_OrbitAPO_Valid", apoValid ? 1f : 0f);
        block.SetFloat("_OrbitAPO_Value", apoValue);
        block.SetFloat("_OrbitAPO_UnitCode", apoUnitCode);

        block.SetFloat("_OrbitPER_Valid", perValid ? 1f : 0f);
        block.SetFloat("_OrbitPER_Value", perValue);
        block.SetFloat("_OrbitPER_UnitCode", perUnitCode);
    }

    private void WriteApproachCueTo(MaterialPropertyBlock block, float halfFovX, float halfFovY)
    {
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
            targetPos_B = MapBodyVectorToRender(targetPos_B);

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
            relVel_B = MapBodyVectorToRender(relVel_B);

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

        block.SetFloat("_TargetValid", targetValid ? 1f : 0f);
        block.SetVector("_TargetPos_HUD", new Vector4(targetPosHUD.x, targetPosHUD.y, 0f, 0f));
        block.SetFloat("_TargetRangeMeters", targetRangeMeters);

        block.SetFloat("_TargetRelVelValid", relVelValid ? 1f : 0f);
        block.SetVector("_TargetRelVelProg_HUD", new Vector4(relVelProgHUD.x, relVelProgHUD.y, 0f, 0f));
        block.SetVector("_TargetRelVelRetro_HUD", new Vector4(relVelRetroHUD.x, relVelRetroHUD.y, 0f, 0f));
        block.SetFloat("_TargetRelSpeedMps", relSpeedMps);

        block.SetFloat("_NodeValid", 0f);
        block.SetVector("_NodePos_HUD", Vector4.zero);
        block.SetFloat("_NodeDVmag_mps", 0f);
        block.SetFloat("_NodeRemainingDV_mps", 0f);

        block.SetFloat("_OrbitRDV_Value", 0f);
        block.SetFloat("_OrbitRDV_UnitCode", 0f);
        block.SetFloat("_OrbitAPO_Valid", 0f);
        block.SetFloat("_OrbitAPO_Value", 0f);
        block.SetFloat("_OrbitAPO_UnitCode", 0f);
        block.SetFloat("_OrbitPER_Valid", 0f);
        block.SetFloat("_OrbitPER_Value", 0f);
        block.SetFloat("_OrbitPER_UnitCode", 0f);
    }

    private void WriteDockModeToBlock(MaterialPropertyBlock block, float halfFovX, float halfFovY)
    {
        bool dockValid = false;
        float dockRangeMeters = 0f;
        float dockClosureMps = 0f;
        bool dockRelVelValid = false;
        Vector2 dockRelVelProgHUD = Vector2.zero;
        Vector2 dockRelVelRetroHUD = Vector2.zero;
        float dockRelSpeedMps = 0f;

        if (contacts != null && contacts.dockValid0)
        {
            Vector3 dockErr_B = new Vector3(
                (float)contacts.dockErr_px_B0,
                (float)contacts.dockErr_py_B0,
                (float)contacts.dockErr_pz_B0
            );

            Quaternion qTargetPortInB = contacts.qTargetPortInB0;
            Vector3 portForward_B = qTargetPortInB * Vector3.forward;

            Vector3 dockErr_Render = MapBodyVectorToRender(dockErr_B);
            Vector3 portForward_Render = MapBodyVectorToRender(portForward_B);

            dockValid = true;
            dockRangeMeters = dockErr_Render.magnitude;

            Quaternion qBE_forRel = Quaternion.identity;
            if (nav != null && nav.valid)
                qBE_forRel = nav.qBE;

            Vector3 relVel_E = new Vector3(
                (float)contacts.sel_dvx_E,
                (float)contacts.sel_dvy_E,
                (float)contacts.sel_dvz_E
            );

            Vector3 relVel_B = -RotateEToBody(qBE_forRel, relVel_E);
            relVel_B = MapBodyVectorToRender(relVel_B);

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

            dockClosureMps = Vector3.Dot(relVel_B, -portForward_Render);
        }

        block.SetFloat("_DockValid", dockValid ? 1f : 0f);
        block.SetFloat("_DockRangeMeters", dockRangeMeters);
        block.SetFloat("_DockClosureMps", dockClosureMps);
        block.SetFloat("_DockRelVelValid", dockRelVelValid ? 1f : 0f);
        block.SetVector("_DockRelVelProg_HUD", new Vector4(dockRelVelProgHUD.x, dockRelVelProgHUD.y, 0f, 0f));
        block.SetVector("_DockRelVelRetro_HUD", new Vector4(dockRelVelRetroHUD.x, dockRelVelRetroHUD.y, 0f, 0f));
        block.SetFloat("_DockRelSpeedMps", dockRelSpeedMps);

        block.SetFloat("_NodeValid", 0f);
        block.SetVector("_NodePos_HUD", Vector4.zero);
        block.SetFloat("_NodeDVmag_mps", 0f);
        block.SetFloat("_NodeRemainingDV_mps", 0f);

        block.SetFloat("_TargetValid", 0f);
        block.SetVector("_TargetPos_HUD", Vector4.zero);
        block.SetFloat("_TargetRangeMeters", 0f);

        block.SetFloat("_TargetRelVelValid", 0f);
        block.SetVector("_TargetRelVelProg_HUD", Vector4.zero);
        block.SetVector("_TargetRelVelRetro_HUD", Vector4.zero);
        block.SetFloat("_TargetRelSpeedMps", 0f);

        ClearTargetNameInBlock(block);

        block.SetFloat("_OrbitRDV_Value", 0f);
        block.SetFloat("_OrbitRDV_UnitCode", 0f);
        block.SetFloat("_OrbitAPO_Valid", 0f);
        block.SetFloat("_OrbitAPO_Value", 0f);
        block.SetFloat("_OrbitAPO_UnitCode", 0f);
        block.SetFloat("_OrbitPER_Valid", 0f);
        block.SetFloat("_OrbitPER_Value", 0f);
        block.SetFloat("_OrbitPER_UnitCode", 0f);
    }

    // ============================================================
    // Dock world aids - pilot only
    // ============================================================

    private void UpdateDockWorldAidsFromContacts()
    {
        if (contacts != null && contacts.dockValid0)
        {
            Vector3 targetPortPos_B = new Vector3(
                (float)contacts.targetPort_px_B0,
                (float)contacts.targetPort_py_B0,
                (float)contacts.targetPort_pz_B0
            );

            Quaternion qTargetPortInB = contacts.qTargetPortInB0;
            Vector3 portForward_B = qTargetPortInB * Vector3.forward;

            UpdateDockPortMarker(targetPortPos_B, qTargetPortInB, portForward_B, true);
            UpdateDockGates(targetPortPos_B, qTargetPortInB, portForward_B, true);
        }
        else
        {
            ClearDockWorldAids();
        }
    }

    private void ClearDockWorldAids()
    {
        UpdateDockPortMarker(Vector3.zero, Quaternion.identity, Vector3.forward, false);
        UpdateDockGates(Vector3.zero, Quaternion.identity, Vector3.forward, false);
    }

    // ============================================================
    // Utility: clear / target name / formatting
    // ============================================================

    private void ClearAllNavApproachOrbitDockOverlayState(MaterialPropertyBlock block)
    {
        block.SetFloat("_NodeValid", 0f);
        block.SetVector("_NodePos_HUD", Vector4.zero);
        block.SetFloat("_NodeDVmag_mps", 0f);
        block.SetFloat("_NodeRemainingDV_mps", 0f);

        block.SetFloat("_TargetValid", 0f);
        block.SetVector("_TargetPos_HUD", Vector4.zero);
        block.SetFloat("_TargetRangeMeters", 0f);

        block.SetFloat("_TargetRelVelValid", 0f);
        block.SetVector("_TargetRelVelProg_HUD", Vector4.zero);
        block.SetVector("_TargetRelVelRetro_HUD", Vector4.zero);
        block.SetFloat("_TargetRelSpeedMps", 0f);

        block.SetFloat("_DockValid", 0f);
        block.SetFloat("_DockRangeMeters", 0f);
        block.SetFloat("_DockClosureMps", 0f);
        block.SetFloat("_DockRelVelValid", 0f);
        block.SetVector("_DockRelVelProg_HUD", Vector4.zero);
        block.SetVector("_DockRelVelRetro_HUD", Vector4.zero);
        block.SetFloat("_DockRelSpeedMps", 0f);

        block.SetFloat("_OrbitRDV_Value", 0f);
        block.SetFloat("_OrbitRDV_UnitCode", 0f);
        block.SetFloat("_OrbitAPO_Valid", 0f);
        block.SetFloat("_OrbitAPO_Value", 0f);
        block.SetFloat("_OrbitAPO_UnitCode", 0f);
        block.SetFloat("_OrbitPER_Valid", 0f);
        block.SetFloat("_OrbitPER_Value", 0f);
        block.SetFloat("_OrbitPER_UnitCode", 0f);

        ClearTargetNameInBlock(block);
    }

    private void ClearTargetNameInBlock(MaterialPropertyBlock block)
    {
        block.SetFloat("_TargetNameLen", 0f);
        block.SetFloat("_TargetNameC0", -1f);
        block.SetFloat("_TargetNameC1", -1f);
        block.SetFloat("_TargetNameC2", -1f);
        block.SetFloat("_TargetNameC3", -1f);
        block.SetFloat("_TargetNameC4", -1f);
    }

    private void PushTargetNameToBlockIfNeeded(MaterialPropertyBlock block, ref string lastName, string rawName)
    {
        string safeName = SanitizeTargetName(rawName);
        lastName = safeName;

        int len = safeName.Length;
        block.SetFloat("_TargetNameLen", (float)len);
        block.SetFloat("_TargetNameC0", EncodeUpperIndexAt(safeName, 0));
        block.SetFloat("_TargetNameC1", EncodeUpperIndexAt(safeName, 1));
        block.SetFloat("_TargetNameC2", EncodeUpperIndexAt(safeName, 2));
        block.SetFloat("_TargetNameC3", EncodeUpperIndexAt(safeName, 3));
        block.SetFloat("_TargetNameC4", EncodeUpperIndexAt(safeName, 4));
    }

    private void ClearOrbitReadoutsIfNoApproachOverride(MaterialPropertyBlock block)
    {
        block.SetFloat("_OrbitRDV_Value", 0f);
        block.SetFloat("_OrbitRDV_UnitCode", 0f);
        block.SetFloat("_OrbitAPO_Valid", 0f);
        block.SetFloat("_OrbitAPO_Value", 0f);
        block.SetFloat("_OrbitAPO_UnitCode", 0f);
        block.SetFloat("_OrbitPER_Valid", 0f);
        block.SetFloat("_OrbitPER_Value", 0f);
        block.SetFloat("_OrbitPER_UnitCode", 0f);
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

    // ============================================================
    // Body presentation mapping
    // ============================================================

    private Vector3 MapBodyVectorToRender(Vector3 v)
    {
        if (!flipPresentationX) return v;
        return new Vector3(-v.x, v.y, v.z);
    }

    private Quaternion MapBodyRotationToRender(Quaternion qBody)
    {
        if (!flipPresentationX) return qBody;

        Vector3 x = qBody * Vector3.right;
        Vector3 y = qBody * Vector3.up;
        Vector3 z = qBody * Vector3.forward;

        x = MapBodyVectorToRender(x);
        y = MapBodyVectorToRender(y);
        z = MapBodyVectorToRender(z);

        x = SafeNormalize(x, Vector3.right);
        y = SafeNormalize(y, Vector3.up);
        z = SafeNormalize(z, Vector3.forward);

        x = SafeNormalize(Vector3.Cross(y, z), Vector3.right);
        y = SafeNormalize(Vector3.Cross(z, x), Vector3.up);

        return Quaternion.LookRotation(z, y);
    }

    private Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float m = v.magnitude;
        if (m < 1e-8f) return fallback;
        return v / m;
    }

    // ============================================================
    // Dock world aids
    // ============================================================

    private void UpdateDockGates(Vector3 portPos_B, Quaternion portRot_B, Vector3 portForward_B, bool visible)
    {
        if (dockGateRoots == null) return;

        int gateCount = dockGateRoots.Length;
        int distCount = (dockGateDistancesMeters != null) ? dockGateDistancesMeters.Length : 0;
        int n = (gateCount < distCount) ? gateCount : distCount;

        Vector3 portPos_Render = MapBodyVectorToRender(portPos_B);
        Quaternion portRot_Render = MapBodyRotationToRender(portRot_B);
        Vector3 portForward_Render = MapBodyVectorToRender(portForward_B).normalized;

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
            Vector3 gatePos_B = portPos_Render + portForward_Render * (d + dockGateForwardOffset);

            gate.localPosition = gatePos_B;
            gate.localRotation = portRot_Render;
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

        Vector3 portPos_Render = MapBodyVectorToRender(portPos_B);
        Quaternion portRot_Render = MapBodyRotationToRender(portRot_B);
        Vector3 portForward_Render = MapBodyVectorToRender(portForward_B).normalized;

        Vector3 markerPos_B = portPos_Render + portForward_Render * dockMarkerForwardOffset;
        dockPortMarkerRoot.localPosition = markerPos_B;
        dockPortMarkerRoot.localRotation = portRot_Render;
    }

    // ============================================================
    // Display formatting helpers
    // ============================================================

    private static float EncodeDistanceDisplay(double meters, out float unitCode)
    {
        double absM = System.Math.Abs(meters);

        if (absM >= 1.0e6)
        {
            unitCode = 2f; // Mm
            return (float)(meters / 1.0e6);
        }

        if (absM >= 1.0e3)
        {
            unitCode = 1f; // km
            return (float)(meters / 1.0e3);
        }

        unitCode = 0f; // m
        return (float)meters;
    }

    private static float EncodeSpeedDisplay(double metersPerSecond, out float unitCode)
    {
        double absV = System.Math.Abs(metersPerSecond);

        if (absV >= 1.0e3)
        {
            unitCode = 1f; // km/s scale
            return (float)(metersPerSecond / 1.0e3);
        }

        unitCode = 0f; // m/s scale
        return (float)metersPerSecond;
    }

    // ============================================================
    // Panel hooks
    // ============================================================

    public void ApplyHudModeFromKnob()
    {
        int m = Mathf.RoundToInt(hudModeKnobValue);
        if (m < 0) m = 0;
        if (m > 4) m = 4;
        hudMode = (byte)m;
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
        int m = Mathf.RoundToInt(hudModeKnobValue2);
        if (m < 0) m = 0;
        if (m > 4) m = 4;
        hudMode2 = (byte)m;
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