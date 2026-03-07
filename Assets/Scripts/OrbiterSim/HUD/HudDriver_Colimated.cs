using UdonSharp;
using UnityEngine;

public class HudDriver_Colimated : UdonSharpBehaviour
{
    [Header("Renderer / Materials")]
    public Renderer hudRenderer;
    public Material orbitHudMat;
    public Material dockHudMat;

    [Header("References")]
    public GuidanceNavCoreState nav;
    public GuidanceNavContactsState contacts;

    [Header("HUD config")]
    [Tooltip("0=OFF, 1=GROUND, 2=ORBIT, 3=DOCK")]
    public byte hudMode = 2;

    [Tooltip("Angular half-width of HUD in body-frame radians.")]
    public float hudHalfFovX = 0.25f;

    [Tooltip("Angular half-height of HUD in body-frame radians.")]
    public float hudHalfFovY = 0.18f;

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

    // Active material cache
    private Material _activeMat;
    private Material _lastAssignedMat;

    // Static material cache
    private Material _lastStaticMat;
    private HudFontData _lastFontData;
    private float _lastFontSdfEdge;
    private float _lastFontSdfSoftness;
    private float _lastFontSignWidthScale;
    private float _lastFontSignHeightScale;

    private string _lastTargetName;

    private void Start()
    {
        UpdateActiveMaterial(true);
        PushStaticMaterialState(true);
    }

    private void OnEnable()
    {
        UpdateActiveMaterial(true);
        PushStaticMaterialState(true);
    }

    private void Update()
    {
        UpdateActiveMaterial(false);
        if (_activeMat == null) return;

        PushStaticMaterialState(false);

        // Always push basic HUD controls
        _activeMat.SetFloat("_HudMode", (float)hudMode);
        _activeMat.SetFloat("_HudHalfFovX", hudHalfFovX);
        _activeMat.SetFloat("_HudHalfFovY", hudHalfFovY);

        if (hudMode == 2)
        {
            WriteOrbitMode();
            PushTargetNameIfNeeded();
        }
        else if (hudMode == 3)
        {
            WriteDockMode();
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

        Material[] mats = hudRenderer.sharedMaterials;
        if (mats == null || mats.Length <= 2) return;

        if (mats[1] != _activeMat)
        {
            mats[1] = _activeMat;
            hudRenderer.sharedMaterials = mats;
        }

        _lastAssignedMat = _activeMat;

        // Force static repush when material changes
        _lastStaticMat = null;
        _lastTargetName = null;
    }

    // ============================================================
    // Orbit mode writes
    // ============================================================

    private void WriteOrbitMode()
    {
        if (_activeMat == null) return;

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

        _activeMat.SetVector("_ProgradeDir_B", new Vector4(prograde_B.x, prograde_B.y, prograde_B.z, 0f));
        _activeMat.SetVector("_RadialOutDir_B", new Vector4(radialOut_B.x, radialOut_B.y, radialOut_B.z, 0f));
        _activeMat.SetVector("_NormalDir_B", new Vector4(normal_B.x, normal_B.y, normal_B.z, 0f));

        // Mode-agnostic selected target overlay for orbit shader
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
                targetPosHUD = DirBToHudUV(targetPos_B);

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

                relVelProgHUD = DirBToHudUV(relVelProg_B);
                relVelRetroHUD = DirBToHudUV(relVelRetro_B);

                relSpeedMps = Mathf.Sqrt(relVelSq);
            }
        }

        _activeMat.SetFloat("_TargetValid", targetValid ? 1f : 0f);
        _activeMat.SetVector("_TargetPos_HUD", new Vector4(targetPosHUD.x, targetPosHUD.y, 0f, 0f));
        _activeMat.SetFloat("_TargetRangeMeters", targetRangeMeters);

        _activeMat.SetFloat("_TargetRelVelValid", relVelValid ? 1f : 0f);
        _activeMat.SetVector("_TargetRelVelProg_HUD", new Vector4(relVelProgHUD.x, relVelProgHUD.y, 0f, 0f));
        _activeMat.SetVector("_TargetRelVelRetro_HUD", new Vector4(relVelRetroHUD.x, relVelRetroHUD.y, 0f, 0f));
        _activeMat.SetFloat("_TargetRelSpeedMps", relSpeedMps);
        UpdateDockPortMarker(Vector3.zero, Quaternion.identity, Vector3.forward, false);
        UpdateDockGates(Vector3.zero, Quaternion.identity, Vector3.forward, false);
    }

    // ============================================================
    // Dock mode writes
    // ============================================================

    private void WriteDockMode()
    {
        if (_activeMat == null) return;

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

            Vector3 portForward_B = qTargetPortInB * Vector3.forward; // +Z outward

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

                dockRelVelProgHUD = DirBToHudUV(relVelProg_B);
                dockRelVelRetroHUD = DirBToHudUV(relVelRetro_B);

                dockRelSpeedMps = Mathf.Sqrt(relVelSq);
            }
            // Positive closing toward the port
            dockClosureMps = Vector3.Dot(relVel_B, -portForward_B);

            UpdateDockPortMarker(targetPortPos_B, qTargetPortInB, portForward_B, true);
            UpdateDockGates(targetPortPos_B, qTargetPortInB, portForward_B, true);
        }
        else
        {
            UpdateDockPortMarker(Vector3.zero, Quaternion.identity, Vector3.forward, false);
            UpdateDockGates(Vector3.zero, Quaternion.identity, Vector3.forward, false);
        }

        _activeMat.SetFloat("_DockValid", dockValid ? 1f : 0f);
        _activeMat.SetFloat("_DockRangeMeters", dockRangeMeters);
        _activeMat.SetFloat("_DockClosureMps", dockClosureMps);

        _activeMat.SetFloat("_DockRelVelValid", dockRelVelValid ? 1f : 0f);
        _activeMat.SetVector("_DockRelVelProg_HUD", new Vector4(dockRelVelProgHUD.x, dockRelVelProgHUD.y, 0f, 0f));
        _activeMat.SetVector("_DockRelVelRetro_HUD", new Vector4(dockRelVelRetroHUD.x, dockRelVelRetroHUD.y, 0f, 0f));
        _activeMat.SetFloat("_DockRelSpeedMps", dockRelSpeedMps);

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

        _activeMat.SetTexture("_FontAtlas", fontData.atlas);

        _activeMat.SetFloat("_FontSdfEdge", fontSdfEdge);
        _activeMat.SetFloat("_FontSdfSoftness", fontSdfSoftness);

        // Digits / symbols
        _activeMat.SetVector("_FontUV_0", fontData.uv_0);
        _activeMat.SetVector("_FontUV_1", fontData.uv_1);
        _activeMat.SetVector("_FontUV_2", fontData.uv_2);
        _activeMat.SetVector("_FontUV_3", fontData.uv_3);
        _activeMat.SetVector("_FontUV_4", fontData.uv_4);
        _activeMat.SetVector("_FontUV_5", fontData.uv_5);
        _activeMat.SetVector("_FontUV_6", fontData.uv_6);
        _activeMat.SetVector("_FontUV_7", fontData.uv_7);
        _activeMat.SetVector("_FontUV_8", fontData.uv_8);
        _activeMat.SetVector("_FontUV_9", fontData.uv_9);
        _activeMat.SetVector("_FontUV_Minus", fontData.uv_minus);
        _activeMat.SetVector("_FontUV_Plus", fontData.uv_plus);
        _activeMat.SetVector("_FontUV_Dot", fontData.uv_dot);

        _activeMat.SetFloat("_FontAspect_0", fontData.aspect_0);
        _activeMat.SetFloat("_FontAspect_1", fontData.aspect_1);
        _activeMat.SetFloat("_FontAspect_2", fontData.aspect_2);
        _activeMat.SetFloat("_FontAspect_3", fontData.aspect_3);
        _activeMat.SetFloat("_FontAspect_4", fontData.aspect_4);
        _activeMat.SetFloat("_FontAspect_5", fontData.aspect_5);
        _activeMat.SetFloat("_FontAspect_6", fontData.aspect_6);
        _activeMat.SetFloat("_FontAspect_7", fontData.aspect_7);
        _activeMat.SetFloat("_FontAspect_8", fontData.aspect_8);
        _activeMat.SetFloat("_FontAspect_9", fontData.aspect_9);
        _activeMat.SetFloat("_FontAspect_Minus", fontData.aspect_minus);
        _activeMat.SetFloat("_FontAspect_Plus", fontData.aspect_plus);
        _activeMat.SetFloat("_FontAspect_Dot", fontData.aspect_dot);

        _activeMat.SetFloat("_FontSignWidthScale", fontSignWidthScale);
        _activeMat.SetFloat("_FontSignHeightScale", fontSignHeightScale);

        // Uppercase UV
        _activeMat.SetVector("_FontUV_A", fontData.uv_A);
        _activeMat.SetVector("_FontUV_B", fontData.uv_B);
        _activeMat.SetVector("_FontUV_C", fontData.uv_C);
        _activeMat.SetVector("_FontUV_D", fontData.uv_D);
        _activeMat.SetVector("_FontUV_E", fontData.uv_E);
        _activeMat.SetVector("_FontUV_F", fontData.uv_F);
        _activeMat.SetVector("_FontUV_G", fontData.uv_G);
        _activeMat.SetVector("_FontUV_H", fontData.uv_H);
        _activeMat.SetVector("_FontUV_I", fontData.uv_I);
        _activeMat.SetVector("_FontUV_J", fontData.uv_J);
        _activeMat.SetVector("_FontUV_K", fontData.uv_K);
        _activeMat.SetVector("_FontUV_L", fontData.uv_L);
        _activeMat.SetVector("_FontUV_M", fontData.uv_M);
        _activeMat.SetVector("_FontUV_N", fontData.uv_N);
        _activeMat.SetVector("_FontUV_O", fontData.uv_O);
        _activeMat.SetVector("_FontUV_P", fontData.uv_P);
        _activeMat.SetVector("_FontUV_Q", fontData.uv_Q);
        _activeMat.SetVector("_FontUV_R", fontData.uv_R);
        _activeMat.SetVector("_FontUV_S", fontData.uv_S);
        _activeMat.SetVector("_FontUV_T", fontData.uv_T);
        _activeMat.SetVector("_FontUV_U", fontData.uv_U);
        _activeMat.SetVector("_FontUV_V", fontData.uv_V);
        _activeMat.SetVector("_FontUV_W", fontData.uv_W);
        _activeMat.SetVector("_FontUV_X", fontData.uv_X);
        _activeMat.SetVector("_FontUV_Y", fontData.uv_Y);
        _activeMat.SetVector("_FontUV_Z", fontData.uv_Z);

        // Uppercase aspect
        _activeMat.SetFloat("_FontAspect_A", fontData.aspect_A);
        _activeMat.SetFloat("_FontAspect_B", fontData.aspect_B);
        _activeMat.SetFloat("_FontAspect_C", fontData.aspect_C);
        _activeMat.SetFloat("_FontAspect_D", fontData.aspect_D);
        _activeMat.SetFloat("_FontAspect_E", fontData.aspect_E);
        _activeMat.SetFloat("_FontAspect_F", fontData.aspect_F);
        _activeMat.SetFloat("_FontAspect_G", fontData.aspect_G);
        _activeMat.SetFloat("_FontAspect_H", fontData.aspect_H);
        _activeMat.SetFloat("_FontAspect_I", fontData.aspect_I);
        _activeMat.SetFloat("_FontAspect_J", fontData.aspect_J);
        _activeMat.SetFloat("_FontAspect_K", fontData.aspect_K);
        _activeMat.SetFloat("_FontAspect_L", fontData.aspect_L);
        _activeMat.SetFloat("_FontAspect_M", fontData.aspect_M);
        _activeMat.SetFloat("_FontAspect_N", fontData.aspect_N);
        _activeMat.SetFloat("_FontAspect_O", fontData.aspect_O);
        _activeMat.SetFloat("_FontAspect_P", fontData.aspect_P);
        _activeMat.SetFloat("_FontAspect_Q", fontData.aspect_Q);
        _activeMat.SetFloat("_FontAspect_R", fontData.aspect_R);
        _activeMat.SetFloat("_FontAspect_S", fontData.aspect_S);
        _activeMat.SetFloat("_FontAspect_T", fontData.aspect_T);
        _activeMat.SetFloat("_FontAspect_U", fontData.aspect_U);
        _activeMat.SetFloat("_FontAspect_V", fontData.aspect_V);
        _activeMat.SetFloat("_FontAspect_W", fontData.aspect_W);
        _activeMat.SetFloat("_FontAspect_X", fontData.aspect_X);
        _activeMat.SetFloat("_FontAspect_Y", fontData.aspect_Y);
        _activeMat.SetFloat("_FontAspect_Z", fontData.aspect_Z);

        _lastTargetName = null;
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

    private Vector2 DirBToHudUV(Vector3 dir_B)
    {
        if (dir_B.sqrMagnitude < 1e-8f) return Vector2.zero;

        dir_B.Normalize();

        float az = Mathf.Atan2(dir_B.x, dir_B.z);
        float el = Mathf.Atan2(dir_B.y, dir_B.z);

        Vector2 uvh;
        uvh.x = az / Mathf.Max(hudHalfFovX, 1e-6f);
        uvh.y = el / Mathf.Max(hudHalfFovY, 1e-6f);
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
            // Optional later mode: face the viewer while staying anchored.
            // For now, just use true port orientation.
            dockPortMarkerRoot.localRotation = portRot_B;
        }
    }

}