using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class OrreryController : UdonSharpBehaviour
{
    // ---------------------------------------------------------------------
    // Focus modes
    // ---------------------------------------------------------------------
    public const byte FOCUS_SUN   = 0;
    public const byte FOCUS_EARTH = 1;
    public const byte FOCUS_MOON  = 2;
    public const byte FOCUS_CRAFT = 3;

    // ---------------------------------------------------------------------
    // Scale modes
    // ---------------------------------------------------------------------
    public const byte SCALEMODE_AUTO         = 0;
    public const byte SCALEMODE_SYSTEM_FIT   = 1;
    public const byte SCALEMODE_FOCUSED_BODY = 2;
    public const byte SCALEMODE_CRAFT_LOCAL  = 3;

    // ---------------------------------------------------------------------
    // Orientation modes
    // ---------------------------------------------------------------------
    public const byte ORIENTMODE_AUTO         = 0;
    public const byte ORIENTMODE_HELIOCENTRIC = 1;
    public const byte ORIENTMODE_BODY         = 2;
    public const byte ORIENTMODE_CRAFT        = 3;

    // ---------------------------------------------------------------------
    // References
    // ---------------------------------------------------------------------
    [Header("State Sources")]
    public GuidanceNavCoreState nav;
    public BodyCatalog bodies;

    [Header("Frame Conversion")]
    public SimUnityFrameBridge frameBridge;

    [Header("Optional fallback craft refs")]
    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;

    [Header("Visual Transforms")]
    public Transform sunTf;
    public Transform earthTf;
    public Transform moonTf;
    public Transform craftTf;

    [Header("SOI Visual Transforms")]
    public Transform earthSoiTf;
    public Transform moonSoiTf;

    [Header("Show / Hide")]
    public bool showSun = true;
    public bool showEarth = true;
    public bool showMoon = true;
    public bool showCraft = true;
    public bool showEarthSOI = true;
    public bool showMoonSOI = true;
    [Header("Body Materials (optional runtime lighting drive)")]
    public Renderer earthRenderer;
    public Renderer moonRenderer;
    private Renderer[] _craftRenderers;
    private bool _craftRenderersCached = false;
    private MaterialPropertyBlock _earthMPB;
    private MaterialPropertyBlock _moonMPB;


    [Header("SOI Display")]
    public bool soiUsePhysicalScale = true;
    public float earthSoiScaleMultiplier = 1.0f;
    public float moonSoiScaleMultiplier = 1.0f;
    public float earthSoiMinDisplayDiameter = 0.0f;
    public float moonSoiMinDisplayDiameter = 0.0f;

    [Header("SOI Materials")]
    public Renderer earthSoiRenderer;
    public Renderer moonSoiRenderer;

    [Header("Volume Clipping")]
    public Renderer stencilVolumeRenderer;
    public Renderer orbitRibbonRenderer;
    public bool deriveClipRadiusFromRendererBounds = true;
    public float fallbackClipRadiusWorld = 0.30f;
    public bool updateBodyMaterialLighting = true;

    [Header("Body Mesh Alignment")]
    [Tooltip("Extra local mesh alignment in BODY FIXED space before sim->Unity conversion.")]
    public Vector3 earthMeshAlignmentEulerDeg = new Vector3(90f, 0f, 0f);
    public Vector3 moonMeshAlignmentEulerDeg = new Vector3(90f, 0f, 0f);

    // ---------------------------------------------------------------------
    // Body display scaling
    // ---------------------------------------------------------------------
    [Header("Body Physical Radii (m)")]
    public double sunRadiusMeters = 696340000.0;

    [Header("Body Display Multipliers")]
    public float sunDisplayScaleMultiplier = 1.0f;
    public float earthDisplayScaleMultiplier = 1.0f;
    public float moonDisplayScaleMultiplier = 1.0f;

    [Header("Body Minimum Display Diameters (Unity units)")]
    public float sunMinDisplayDiameter = 0.00f;
    public float earthMinDisplayDiameter = 0.00f;
    public float moonMinDisplayDiameter = 0.00f;

    // ---------------------------------------------------------------------
    // Craft display
    // ---------------------------------------------------------------------
    [Header("Craft Display")]
    public float craftDisplayDiameterWhenFocused = 0.028f;
    public float craftBodyModeReferenceDiameter = 0.018f;
    public double craftBodyModeReferenceRangeMeters = 10000.0;
    public float craftBodyModeShrinkExponent = 1.0f;
    public float craftBodyModeMinDiameter = 0.006f;
    public float craftBodyModeMaxDiameter = 0.03f;

    [Header("Craft Physical Scaling")]
    public double craftPhysicalReferenceSizeMeters = 50.0;
    public float craftFocusTargetDisplaySizeUnity = 0.40f;

    [Header("Craft Mesh Mirror")]
    [Tooltip("Optional pure visual mirror on craft mesh after correct frame conversion. Leave off unless the craft model itself is mirrored.")]
    public bool mirrorCraftVisualX = false;
    public bool mirrorCraftVisualY = false;
    public bool mirrorCraftVisualZ = false;

    [Header("Craft Focus Manual Zoom Limits")]
    public float craftFocusManualZoomMinDecades = -2.0f;
    public float craftFocusManualZoomMaxDecades = 1.0f;

    // ---------------------------------------------------------------------
    // Focus
    // ---------------------------------------------------------------------
    [Header("Focus")]
    public byte focusMode = FOCUS_EARTH;

    // ---------------------------------------------------------------------
    // Scale controls
    // ---------------------------------------------------------------------
    [Header("Scale")]
    public float hologramRadiusUnity = 0.30f;

    [Range(0.1f, 1.0f)]
    public float autoScaleFill = 0.82f;

    public byte scaleMode = SCALEMODE_AUTO;
    public float manualZoomDecades = 0.0f;
    public double minSystemFitRangeMeters = 10.0;

    [Header("Focused Body Scale")]
    [Range(0.05f, 0.95f)]
    public float focusedBodyTargetDiameterFraction = 0.45f;
    public bool focusedBodyKeepCraftInView = true;

    [Range(0.1f, 1.0f)]
    public float focusedBodyCraftFitFraction = 0.92f;

    [Header("Craft Focus Zoom")]
    public double craftFocusDefaultRangeMeters = 5000.0;
    public bool craftFocusUseAltitudeForDefault = true;
    public double craftFocusAltitudeRangeMultiplier = 6.0;
    public double craftFocusMinDefaultRangeMeters = 1000.0;
    public double craftFocusMaxDefaultRangeMeters = 1000000.0;

    // ---------------------------------------------------------------------
    // Orientation controls
    // ---------------------------------------------------------------------
    [Header("Orientation")]
    public byte orientationMode = ORIENTMODE_AUTO;

    [Header("Craft Frame Alignment")]
    [Tooltip("Extra rotation in craft/body frame before sim->Unity conversion.")]
    public Vector3 craftFrameAlignmentEulerDeg = Vector3.zero;

    // ---------------------------------------------------------------------
    // Smoothing
    // ---------------------------------------------------------------------
    [Header("Smoothing")]
    public float focusFollowSpeed = 6.0f;
    public float scaleFollowSpeed = 6.0f;
    public float rotationFollowSpeed = 6.0f;

    [Header("Focus Transition")]
    public bool smoothFocusTransitions = true;
    public double focusSettleDistanceMeters = 1000.0;
    public double focusSettleRelative = 1e-6;

    private bool _focusTransitionActive = false;
    private byte _prevFocusModeForTransition = 255;

    [Header("Presentation Offset")]
    public Vector3 localOffset = Vector3.zero;

    [Header("Ticking")]
    public bool useInternalLateUpdate = false;

    // ---------------------------------------------------------------------
    // Internal smoothed state (SIM FRAME)
    // ---------------------------------------------------------------------
    private double _focusX_smoothed;
    private double _focusY_smoothed;
    private double _focusZ_smoothed;

    // orrery-local frame -> sim inertial frame
    private Quaternion _frameQ_E_smoothed = Quaternion.identity;

    // Unity meters per sim meter
    private float _sceneScaleSmoothed = 1.0f;
    private bool _initialized = false;

    // ---------------------------------------------------------------------
    void Start()
    {
        ApplyInitialVisualScales();
        ForceInitializeNow();
    }

    void LateUpdate()
    {
        if (!useInternalLateUpdate) return;
        TickOrrery();
    }

    private void CacheCraftRenderers()
    {
        if (_craftRenderersCached) return;

        if (craftTf != null)
            _craftRenderers = craftTf.GetComponentsInChildren<Renderer>(true);

        _craftRenderersCached = true;
    }

    // ---------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------
    public void API_FocusSun()   { focusMode = FOCUS_SUN; }
    public void API_FocusEarth() { focusMode = FOCUS_EARTH; }
    public void API_FocusMoon()  { focusMode = FOCUS_MOON; }
    public void API_FocusCraft() { focusMode = FOCUS_CRAFT; }

    public void API_ZoomIn(float stepDecades)  { manualZoomDecades += stepDecades; }
    public void API_ZoomOut(float stepDecades) { manualZoomDecades -= stepDecades; }
    public void API_ResetZoom()                { manualZoomDecades = 0.0f; }

    public void API_SetScaleModeAuto()         { scaleMode = SCALEMODE_AUTO; }
    public void API_SetScaleModeSystemFit()    { scaleMode = SCALEMODE_SYSTEM_FIT; }
    public void API_SetScaleModeFocusedBody()  { scaleMode = SCALEMODE_FOCUSED_BODY; }
    public void API_SetScaleModeCraftLocal()   { scaleMode = SCALEMODE_CRAFT_LOCAL; }

    public void API_SetOrientationAuto()         { orientationMode = ORIENTMODE_AUTO; }
    public void API_SetOrientationHeliocentric() { orientationMode = ORIENTMODE_HELIOCENTRIC; }
    public void API_SetOrientationBody()         { orientationMode = ORIENTMODE_BODY; }
    public void API_SetOrientationCraft()        { orientationMode = ORIENTMODE_CRAFT; }

    public void ForceInitializeNow()
    {
        double fx, fy, fz;
        Quaternion frameQ;
        float scaleTarget;

        ComputeTargets(out fx, out fy, out fz, out frameQ, out scaleTarget);

        _focusX_smoothed = fx;
        _focusY_smoothed = fy;
        _focusZ_smoothed = fz;
        _frameQ_E_smoothed = frameQ;
        _sceneScaleSmoothed = (scaleTarget > 0.0f) ? scaleTarget : 1.0f;
        _initialized = true;
        _prevFocusModeForTransition = focusMode;
        _focusTransitionActive = false;
    }

    // ---------------------------------------------------------------------
    // Main tick
    // ---------------------------------------------------------------------
    public void TickOrrery()
    {
        if (bodies == null) return;
        if (frameBridge == null) return;
        if (nav == null && craft == null) return;

        double fxTarget, fyTarget, fzTarget;
        Quaternion frameQTarget;
        float scaleTarget;

        ComputeTargets(out fxTarget, out fyTarget, out fzTarget, out frameQTarget, out scaleTarget);

        if (!_initialized)
        {
            _focusX_smoothed = fxTarget;
            _focusY_smoothed = fyTarget;
            _focusZ_smoothed = fzTarget;
            _frameQ_E_smoothed = frameQTarget;
            _sceneScaleSmoothed = (scaleTarget > 0.0f) ? scaleTarget : 1.0f;
            _initialized = true;
        }

        float dt = Time.deltaTime;
        if (dt < 0.0f) dt = 0.0f;
        if (dt > 0.2f) dt = 0.2f;

        float aRot = 1.0f - Mathf.Exp(-rotationFollowSpeed * dt);
        float aScl = 1.0f - Mathf.Exp(-scaleFollowSpeed * dt);

        bool focusChanged = FocusTargetChanged(focusMode);

        if (focusChanged)
        {
            _focusTransitionActive = smoothFocusTransitions;
            _prevFocusModeForTransition = focusMode;

            if (!smoothFocusTransitions)
            {
                _focusX_smoothed = fxTarget;
                _focusY_smoothed = fyTarget;
                _focusZ_smoothed = fzTarget;
            }
        }

        if (_focusTransitionActive)
        {
            float aPos = 1.0f - Mathf.Exp(-focusFollowSpeed * dt);

            _focusX_smoothed = LerpD(_focusX_smoothed, fxTarget, aPos);
            _focusY_smoothed = LerpD(_focusY_smoothed, fyTarget, aPos);
            _focusZ_smoothed = LerpD(_focusZ_smoothed, fzTarget, aPos);

            double err = FocusErrorMeters(fxTarget, fyTarget, fzTarget);
            double settle = ComputeFocusSettleThreshold(fxTarget, fyTarget, fzTarget);

            if (err <= settle)
            {
                _focusX_smoothed = fxTarget;
                _focusY_smoothed = fyTarget;
                _focusZ_smoothed = fzTarget;
                _focusTransitionActive = false;
            }
        }
        else
        {
            _focusX_smoothed = fxTarget;
            _focusY_smoothed = fyTarget;
            _focusZ_smoothed = fzTarget;
        }

        _frameQ_E_smoothed = Quaternion.Slerp(_frameQ_E_smoothed, frameQTarget, aRot);
        _sceneScaleSmoothed = Mathf.Lerp(_sceneScaleSmoothed, scaleTarget, aScl);

        ApplyBodiesAndCraft();
        UpdateBodyMaterials();
        UpdateClipVolumeParams();
    }

    // ---------------------------------------------------------------------
    // Target computation
    // ---------------------------------------------------------------------
    private void ComputeTargets(out double fxTarget, out double fyTarget, out double fzTarget, out Quaternion frameQTarget, out float scaleTarget)
    {
        GetFocusPositionE(out fxTarget, out fyTarget, out fzTarget);
        frameQTarget = GetResolvedFrameQ_E();
        scaleTarget = ComputeSceneScale(fxTarget, fyTarget, fzTarget);

        if (scaleTarget <= 0.0f)
            scaleTarget = 1.0f;
    }

    private void GetFocusPositionE(out double x, out double y, out double z)
    {
        if (focusMode == FOCUS_SUN)
        {
            GetBodyPosED(bodies.sunId, out x, out y, out z);
            return;
        }

        if (focusMode == FOCUS_EARTH)
        {
            GetBodyPosED(bodies.earthId, out x, out y, out z);
            return;
        }

        if (focusMode == FOCUS_MOON)
        {
            GetBodyPosED(bodies.moonId, out x, out y, out z);
            return;
        }

        GetCraftPosED(out x, out y, out z);
    }

    /// <summary>
    /// Returns qFrame_E:
    /// orrery-local frame -> sim inertial frame
    /// </summary>
    private Quaternion GetResolvedFrameQ_E()
    {
        byte mode = ResolveOrientationMode();

        if (mode == ORIENTMODE_HELIOCENTRIC)
            return Quaternion.identity;

        if (mode == ORIENTMODE_BODY)
            return GetOrientationReferenceBodyQ_E();

        if (mode == ORIENTMODE_CRAFT)
            return GetCraftFrameQ_E();

        return Quaternion.identity;
    }

    private byte ResolveOrientationMode()
    {
        if (orientationMode == ORIENTMODE_HELIOCENTRIC) return ORIENTMODE_HELIOCENTRIC;
        if (orientationMode == ORIENTMODE_BODY)         return ORIENTMODE_BODY;
        if (orientationMode == ORIENTMODE_CRAFT)        return ORIENTMODE_CRAFT;

        if (focusMode == FOCUS_SUN)   return ORIENTMODE_HELIOCENTRIC;
        if (focusMode == FOCUS_EARTH) return ORIENTMODE_BODY;
        if (focusMode == FOCUS_MOON)  return ORIENTMODE_BODY;
        return ORIENTMODE_CRAFT;
    }

    private Quaternion GetOrientationReferenceBodyQ_E()
    {
        if (focusMode == FOCUS_EARTH)
            return GetBodyQ_BodyToE(bodies.earthId);

        if (focusMode == FOCUS_MOON)
            return GetBodyQ_BodyToE(bodies.moonId);

        if (nav != null && nav.valid)
            return GetBodyQ_BodyToE(nav.primaryId);

        return Quaternion.identity;
    }

    private Quaternion GetCraftFrameQ_E()
    {
        Quaternion qBE = GetCraftQ_BE();

        Vector3 upE = qBE * Vector3.up;
        Vector3 fwdE = qBE * Vector3.forward;

        if (upE.sqrMagnitude < 1e-10f) upE = Vector3.up;
        if (fwdE.sqrMagnitude < 1e-10f) fwdE = Vector3.forward;

        upE.Normalize();
        fwdE.Normalize();

        Vector3 rightE = Vector3.Cross(upE, fwdE);
        if (rightE.sqrMagnitude < 1e-10f)
            rightE = qBE * Vector3.right;

        rightE.Normalize();
        fwdE = Vector3.Cross(rightE, upE).normalized;

        Quaternion qCraftFrame = Quaternion.LookRotation(fwdE, upE);
        Quaternion qAlign = Quaternion.Euler(craftFrameAlignmentEulerDeg);

        return qCraftFrame * qAlign;
    }

    private float ComputeSceneScale(double focusX, double focusY, double focusZ)
    {
        byte resolvedScaleMode = ResolveScaleMode();

        float baseScale;
        if (resolvedScaleMode == SCALEMODE_SYSTEM_FIT)
            baseScale = ComputeSystemFitScale(focusX, focusY, focusZ);
        else if (resolvedScaleMode == SCALEMODE_FOCUSED_BODY)
            baseScale = ComputeFocusedBodyScale();
        else if (resolvedScaleMode == SCALEMODE_CRAFT_LOCAL)
            baseScale = ComputeCraftFocusScale();
        else
            baseScale = ComputeSystemFitScale(focusX, focusY, focusZ);

        float zoomDecades = GetManualZoomDecadesForCurrentFocus();
        float manualMul = Mathf.Pow(10.0f, zoomDecades);
        return baseScale * manualMul;
    }

    private byte ResolveScaleMode()
    {
        if (scaleMode == SCALEMODE_SYSTEM_FIT)   return SCALEMODE_SYSTEM_FIT;
        if (scaleMode == SCALEMODE_FOCUSED_BODY) return SCALEMODE_FOCUSED_BODY;
        if (scaleMode == SCALEMODE_CRAFT_LOCAL)  return SCALEMODE_CRAFT_LOCAL;

        if (focusMode == FOCUS_SUN)   return SCALEMODE_SYSTEM_FIT;
        if (focusMode == FOCUS_EARTH) return SCALEMODE_FOCUSED_BODY;
        if (focusMode == FOCUS_MOON)  return SCALEMODE_FOCUSED_BODY;
        return SCALEMODE_CRAFT_LOCAL;
    }

    private float ComputeSystemFitScale(double focusX, double focusY, double focusZ)
    {
        double maxDist = minSystemFitRangeMeters;

        double sx, sy, sz;
        double ex, ey, ez;
        double mx, my, mz;
        double cx, cy, cz;

        GetBodyPosED(bodies.sunId, out sx, out sy, out sz);
        GetBodyPosED(bodies.earthId, out ex, out ey, out ez);
        GetBodyPosED(bodies.moonId, out mx, out my, out mz);
        GetCraftPosED(out cx, out cy, out cz);

        if (focusMode == FOCUS_SUN)
        {
            maxDist = Max4(
                DistMeters(focusX, focusY, focusZ, ex, ey, ez),
                DistMeters(focusX, focusY, focusZ, mx, my, mz),
                DistMeters(focusX, focusY, focusZ, cx, cy, cz),
                maxDist
            );
        }
        else if (focusMode == FOCUS_EARTH)
        {
            maxDist = Max4(
                DistMeters(focusX, focusY, focusZ, mx, my, mz),
                DistMeters(focusX, focusY, focusZ, cx, cy, cz),
                DistMeters(focusX, focusY, focusZ, sx, sy, sz),
                maxDist
            );
        }
        else if (focusMode == FOCUS_MOON)
        {
            maxDist = Max4(
                DistMeters(focusX, focusY, focusZ, ex, ey, ez),
                DistMeters(focusX, focusY, focusZ, cx, cy, cz),
                DistMeters(focusX, focusY, focusZ, sx, sy, sz),
                maxDist
            );
        }
        else
        {
            maxDist = Max4(
                DistMeters(focusX, focusY, focusZ, ex, ey, ez),
                DistMeters(focusX, focusY, focusZ, mx, my, mz),
                DistMeters(focusX, focusY, focusZ, sx, sy, sz),
                maxDist
            );
        }

        if (maxDist < minSystemFitRangeMeters)
            maxDist = minSystemFitRangeMeters;

        float workRadius = hologramRadiusUnity * autoScaleFill;
        return (float)(workRadius / maxDist);
    }

    private float ComputeFocusedBodyScale()
    {
        byte bodyId = ResolveFocusedBodyScaleBodyId();
        double bodyRadius = GetBodyRadiusMeters(bodyId);

        if (bodyRadius <= 0.0)
            return ComputeSystemFitScale(_focusX_smoothed, _focusY_smoothed, _focusZ_smoothed);

        float hologramDiameter = 2.0f * hologramRadiusUnity;
        float targetBodyDiameter = hologramDiameter * focusedBodyTargetDiameterFraction;

        float scaleFromBody = (float)(targetBodyDiameter / (2.0 * bodyRadius));

        if (focusedBodyKeepCraftInView && nav != null && nav.valid && nav.primaryId == bodyId)
        {
            double craftRangeFromBody = nav.rMag;
            if (craftRangeFromBody > 1.0)
            {
                float workRadius = hologramRadiusUnity * autoScaleFill * focusedBodyCraftFitFraction;
                float scaleToFitCraft = (float)(workRadius / craftRangeFromBody);

                if (scaleToFitCraft < scaleFromBody)
                    scaleFromBody = scaleToFitCraft;
            }
        }

        return scaleFromBody;
    }

    private float ComputeCraftFocusScale()
    {
        double refSize = craftPhysicalReferenceSizeMeters;
        if (refSize <= 1e-6)
            refSize = 1.0;

        float targetSize = craftFocusTargetDisplaySizeUnity;
        if (targetSize < 0.001f)
            targetSize = 0.001f;

        return targetSize / (float)refSize;
    }

    private byte ResolveFocusedBodyScaleBodyId()
    {
        if (focusMode == FOCUS_EARTH) return bodies.earthId;
        if (focusMode == FOCUS_MOON)  return bodies.moonId;

        if (nav != null && nav.valid)
            return nav.primaryId;

        return bodies.earthId;
    }

    // ---------------------------------------------------------------------
    // Placement
    // ---------------------------------------------------------------------
    private void ApplyBodiesAndCraft()
    {
        Quaternion qFrameE = _frameQ_E_smoothed;
        Quaternion qE_toFrame = Quaternion.Inverse(qFrameE);

        ApplyDynamicBodyScales();
        ApplySOIVisuals(qE_toFrame);

        if (sunTf != null)
        {
            sunTf.gameObject.SetActive(showSun);
            if (showSun)
            {
                double x, y, z;
                GetBodyPosED(bodies.sunId, out x, out y, out z);
                sunTf.localPosition = ComputeDisplayPosition(x, y, z, qE_toFrame);
                sunTf.localRotation = Quaternion.identity;
            }
        }

        if (earthTf != null)
        {
            earthTf.gameObject.SetActive(showEarth);
            if (showEarth)
            {
                double x, y, z;
                GetBodyPosED(bodies.earthId, out x, out y, out z);
                earthTf.localPosition = ComputeDisplayPosition(x, y, z, qE_toFrame);

                Quaternion qBodyE = GetBodyQ_BodyToE(bodies.earthId);
                Quaternion qBodyRel = qE_toFrame * qBodyE;
                Quaternion qMeshAlign = Quaternion.Euler(earthMeshAlignmentEulerDeg);

                // Apply mesh alignment in SIM/body space, then convert basis once
                earthTf.localRotation = frameBridge.SimRotationToUnityRotation(qBodyRel * qMeshAlign);
            }
        }

        if (moonTf != null)
        {
            moonTf.gameObject.SetActive(showMoon);
            if (showMoon)
            {
                double x, y, z;
                GetBodyPosED(bodies.moonId, out x, out y, out z);
                moonTf.localPosition = ComputeDisplayPosition(x, y, z, qE_toFrame);

                Quaternion qBodyE = GetBodyQ_BodyToE(bodies.moonId);
                Quaternion qBodyRel = qE_toFrame * qBodyE;
                Quaternion qMeshAlign = Quaternion.Euler(moonMeshAlignmentEulerDeg);

                moonTf.localRotation = frameBridge.SimRotationToUnityRotation(qBodyRel * qMeshAlign);
            }
        }

        if (craftTf != null)
        {
            craftTf.gameObject.SetActive(showCraft);
            if (showCraft)
            {
                double x, y, z;
                GetCraftPosED(out x, out y, out z);
                craftTf.localPosition = ComputeDisplayPosition(x, y, z, qE_toFrame);

                Quaternion qCraftE = GetCraftQ_BE();
                Quaternion qCraftRel = qE_toFrame * qCraftE;

                craftTf.localRotation = frameBridge.SimRotationToUnityRotation(qCraftRel);
                craftTf.localScale = GetCraftDisplayScale();
            }
        }
    }

    private void ApplyDynamicBodyScales()
    {
        if (sunTf != null)
        {
            float d = GetDisplayedBodyDiameterUnity(sunRadiusMeters, sunDisplayScaleMultiplier, sunMinDisplayDiameter);
            sunTf.localScale = Vector3.one * d;
        }

        if (earthTf != null)
        {
            float d = GetDisplayedBodyDiameterUnity(bodies.earthRadiusM, earthDisplayScaleMultiplier, earthMinDisplayDiameter);
            earthTf.localScale = Vector3.one * d;
        }

        if (moonTf != null)
        {
            float d = GetDisplayedBodyDiameterUnity(bodies.moonRadiusM, moonDisplayScaleMultiplier, moonMinDisplayDiameter);
            moonTf.localScale = Vector3.one * d;
        }
    }

    private float GetDisplayedBodyDiameterUnity(double radiusMeters, float multiplier, float minDiameter)
    {
        float d = (float)(2.0 * radiusMeters * _sceneScaleSmoothed) * multiplier;
        if (d < minDiameter) d = minDiameter;
        return d;
    }

    private float ComputeCraftDisplayDiameterUnity()
    {
        if (focusMode == FOCUS_CRAFT)
        {
            float d = (float)(craftPhysicalReferenceSizeMeters * _sceneScaleSmoothed);
            if (d < 0.001f) d = 0.001f;
            return d;
        }

        float workRadius = hologramRadiusUnity * autoScaleFill;
        double representedRangeMeters = workRadius / Mathf.Max(_sceneScaleSmoothed, 1e-12f);

        float dBody = craftBodyModeReferenceDiameter;

        if (craftBodyModeReferenceRangeMeters > 0.0 && representedRangeMeters > 0.0)
        {
            double ratio = craftBodyModeReferenceRangeMeters / representedRangeMeters;
            dBody *= Mathf.Pow((float)ratio, craftBodyModeShrinkExponent);
        }

        if (dBody < craftBodyModeMinDiameter) dBody = craftBodyModeMinDiameter;
        if (dBody > craftBodyModeMaxDiameter) dBody = craftBodyModeMaxDiameter;

        return dBody;
    }

    private Vector3 GetCraftDisplayScale()
    {
        float s = ComputeCraftDisplayDiameterUnity();

        float sx = s;
        float sy = s;
        float sz = s;

        if (mirrorCraftVisualX) sx = -sx;
        if (mirrorCraftVisualY) sy = -sy;
        if (mirrorCraftVisualZ) sz = -sz;

        return new Vector3(sx, sy, sz);
    }

    private Vector3 ComputeDisplayPosition(double objX, double objY, double objZ, Quaternion qE_toFrame)
    {
        // stay in sim basis until after relative subtraction and frame rotation
        double dx = objX - _focusX_smoothed;
        double dy = objY - _focusY_smoothed;
        double dz = objZ - _focusZ_smoothed;

        Vector3 dFrameSim = qE_toFrame * new Vector3((float)dx, (float)dy, (float)dz);
        Vector3 presented = frameBridge.SimDirectionToUnityVec3(dFrameSim * _sceneScaleSmoothed);

        return presented + localOffset;
    }

    // ---------------------------------------------------------------------
    // Source reads
    // ---------------------------------------------------------------------
    private void GetCraftPosED(out double x, out double y, out double z)
    {
        if (nav != null)
        {
            x = nav.rC_x;
            y = nav.rC_y;
            z = nav.rC_z;
            return;
        }

        if (craft != null)
        {
            x = craft.rx;
            y = craft.ry;
            z = craft.rz;
            return;
        }

        x = y = z = 0.0;
    }

    private Quaternion GetCraftQ_BE()
    {
        if (nav != null)
            return nav.qBE;

        if (craftAtt != null)
            return craftAtt.qBE;

        return Quaternion.identity;
    }

    private float GetManualZoomDecadesForCurrentFocus()
    {
        if (focusMode == FOCUS_CRAFT)
            return Mathf.Clamp(manualZoomDecades, craftFocusManualZoomMinDecades, craftFocusManualZoomMaxDecades);

        return manualZoomDecades;
    }

    private void GetBodyPosED(byte bodyId, out double x, out double y, out double z)
    {
        bodies.GetBodyPos(bodyId, out x, out y, out z);
    }

    private Quaternion GetBodyQ_BodyToE(byte bodyId)
    {
        if (nav != null && nav.primaryId == bodyId)
            return nav.qPF2E;

        return bodies.GetBodyFixedToInertial(bodyId);
    }

    private double GetBodyRadiusMeters(byte bodyId)
    {
        if (bodyId == bodies.sunId) return sunRadiusMeters;
        return bodies.GetRadius(bodyId);
    }

    // ---------------------------------------------------------------------
    // Initial visual setup
    // ---------------------------------------------------------------------
    private void ApplyInitialVisualScales()
    {
        if (sunTf != null)   sunTf.localScale = Vector3.one * Mathf.Max(sunMinDisplayDiameter, 0.001f);
        if (earthTf != null) earthTf.localScale = Vector3.one * Mathf.Max(earthMinDisplayDiameter, 0.001f);
        if (moonTf != null)  moonTf.localScale = Vector3.one * Mathf.Max(moonMinDisplayDiameter, 0.001f);
        if (craftTf != null) craftTf.localScale = Vector3.one * craftDisplayDiameterWhenFocused;
    }

    // ---------------------------------------------------------------------
    // Body lighting / material drive
    // ---------------------------------------------------------------------
    private void UpdateBodyMaterials()
    {
        if (!updateBodyMaterialLighting || bodies == null || frameBridge == null)
            return;

        double sx, sy, sz;
        bodies.GetBodyPos(bodies.sunId, out sx, out sy, out sz);

        if (earthRenderer != null)
        {
            if (_earthMPB == null) _earthMPB = new MaterialPropertyBlock();

            double ex, ey, ez;
            bodies.GetBodyPos(bodies.earthId, out ex, out ey, out ez);

            Vector3 sunDirWorld = MapWorldDirectionEToOrreryWorld(
                sx - ex,
                sy - ey,
                sz - ez
            );

            earthRenderer.GetPropertyBlock(_earthMPB);
            _earthMPB.SetVector("_SunDirWorld", new Vector4(sunDirWorld.x, sunDirWorld.y, sunDirWorld.z, 0f));
            earthRenderer.SetPropertyBlock(_earthMPB);
        }

        if (moonRenderer != null)
        {
            if (_moonMPB == null) _moonMPB = new MaterialPropertyBlock();

            double mx, my, mz;
            bodies.GetBodyPos(bodies.moonId, out mx, out my, out mz);

            Vector3 sunDirWorld = MapWorldDirectionEToOrreryWorld(
                sx - mx,
                sy - my,
                sz - mz
            );

            moonRenderer.GetPropertyBlock(_moonMPB);
            _moonMPB.SetVector("_SunDirWorld", new Vector4(sunDirWorld.x, sunDirWorld.y, sunDirWorld.z, 0f));
            moonRenderer.SetPropertyBlock(_moonMPB);
        }
    }

    // ---------------------------------------------------------------------
    // Public mapping helpers
    // ---------------------------------------------------------------------
    public Vector3 MapWorldPointEToOrreryLocal(double worldX, double worldY, double worldZ)
    {
        double dx = worldX - _focusX_smoothed;
        double dy = worldY - _focusY_smoothed;
        double dz = worldZ - _focusZ_smoothed;

        Quaternion qE_toFrame = Quaternion.Inverse(_frameQ_E_smoothed);
        Vector3 dFrameSim = qE_toFrame * new Vector3((float)dx, (float)dy, (float)dz);

        Vector3 presented = frameBridge.SimDirectionToUnityVec3(dFrameSim * _sceneScaleSmoothed);
        return presented + localOffset;
    }

    public float GetCurrentSceneScale()
    {
        return _sceneScaleSmoothed;
    }

    public Vector3 MapWorldDirectionEToOrreryLocal(double dirX, double dirY, double dirZ)
    {
        Quaternion qE_toFrame = Quaternion.Inverse(_frameQ_E_smoothed);
        Vector3 dFrameSim = qE_toFrame * new Vector3((float)dirX, (float)dirY, (float)dirZ);
        Vector3 presented = frameBridge.SimDirectionToUnityVec3(dFrameSim);
        return presented.normalized;
    }

    public Vector3 MapWorldDirectionEToOrreryWorld(double dirX, double dirY, double dirZ)
    {
        Vector3 localDir = MapWorldDirectionEToOrreryLocal(dirX, dirY, dirZ);
        return transform.TransformDirection(localDir).normalized;
    }

    // ---------------------------------------------------------------------
    // Clip volume
    // ---------------------------------------------------------------------
    private void UpdateClipVolumeParams()
    {
        Vector3 centerWorld;
        float radiusWorld;
        GetClipSphereWorld(out centerWorld, out radiusWorld);

        ApplyClipParamsToRenderer(earthRenderer, centerWorld, radiusWorld);
        ApplyClipParamsToRenderer(moonRenderer, centerWorld, radiusWorld);
        ApplyClipParamsToRenderer(orbitRibbonRenderer, centerWorld, radiusWorld);
        ApplyClipParamsToRenderer(earthSoiRenderer, centerWorld, radiusWorld);
        ApplyClipParamsToRenderer(moonSoiRenderer, centerWorld, radiusWorld);
        if (craftTf != null)
        {
            CacheCraftRenderers();

            if (_craftRenderers != null)
            {
                int n = _craftRenderers.Length;
                for (int i = 0; i < n; i++)
                {
                    ApplyClipParamsToRenderer(_craftRenderers[i], centerWorld, radiusWorld);
                }
            }
        }
    }

    private void GetClipSphereWorld(out Vector3 centerWorld, out float radiusWorld)
    {
        centerWorld = transform.position;
        radiusWorld = Mathf.Max(0.001f, fallbackClipRadiusWorld);

        if (stencilVolumeRenderer != null)
        {
            Bounds b = stencilVolumeRenderer.bounds;
            centerWorld = b.center;

            if (deriveClipRadiusFromRendererBounds)
            {
                radiusWorld = 0.5f * Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                radiusWorld = Mathf.Max(radiusWorld, 0.001f);
            }
        }
    }

    private void ApplyClipParamsToRenderer(Renderer r, Vector3 centerWorld, float radiusWorld)
    {
        if (r == null) return;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetVector("_ClipCenterWorld", new Vector4(centerWorld.x, centerWorld.y, centerWorld.z, 0f));
        mpb.SetFloat("_ClipRadiusWorld", radiusWorld);
        r.SetPropertyBlock(mpb);
    }

    private void ApplySOIVisuals(Quaternion qE_toFrame)
    {
        if (earthSoiTf != null)
        {
            earthSoiTf.gameObject.SetActive(showEarthSOI);
            if (showEarthSOI)
            {
                double x, y, z;
                GetBodyPosED(bodies.earthId, out x, out y, out z);
                earthSoiTf.localPosition = ComputeDisplayPosition(x, y, z, qE_toFrame);
                earthSoiTf.localRotation = Quaternion.identity;

                double r = bodies.GetSOIRadius(bodies.earthId);
                float d = GetDisplayedSOIDiameterUnity(r, earthSoiScaleMultiplier, earthSoiMinDisplayDiameter);
                earthSoiTf.localScale = Vector3.one * d;
            }
        }

        if (moonSoiTf != null)
        {
            moonSoiTf.gameObject.SetActive(showMoonSOI);
            if (showMoonSOI)
            {
                double x, y, z;
                GetBodyPosED(bodies.moonId, out x, out y, out z);
                moonSoiTf.localPosition = ComputeDisplayPosition(x, y, z, qE_toFrame);
                moonSoiTf.localRotation = Quaternion.identity;

                double r = bodies.GetSOIRadius(bodies.moonId);
                float d = GetDisplayedSOIDiameterUnity(r, moonSoiScaleMultiplier, moonSoiMinDisplayDiameter);
                moonSoiTf.localScale = Vector3.one * d;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Utilities
    // ---------------------------------------------------------------------
    private bool FocusTargetChanged(byte currentFocusMode)
    {
        return currentFocusMode != _prevFocusModeForTransition;
    }

    private double ComputeFocusSettleThreshold(double tx, double ty, double tz)
    {
        double mag = System.Math.Sqrt(tx * tx + ty * ty + tz * tz);
        double rel = mag * focusSettleRelative;
        return System.Math.Max(focusSettleDistanceMeters, rel);
    }

    private double FocusErrorMeters(double tx, double ty, double tz)
    {
        double dx = tx - _focusX_smoothed;
        double dy = ty - _focusY_smoothed;
        double dz = tz - _focusZ_smoothed;
        return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public void GetCurrentClipSphereWorld(out Vector3 centerWorld, out float radiusWorld)
    {
        GetClipSphereWorld(out centerWorld, out radiusWorld);
    }

    private static double DistMeters(double ax, double ay, double az, double bx, double by, double bz)
    {
        double dx = ax - bx;
        double dy = ay - by;
        double dz = az - bz;
        return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static double Max4(double a, double b, double c, double d)
    {
        double m = a;
        if (b > m) m = b;
        if (c > m) m = c;
        if (d > m) m = d;
        return m;
    }

    private static double LerpD(double a, double b, float t)
    {
        return a + (b - a) * (double)t;
    }


    private float GetDisplayedSOIDiameterUnity(double soiRadiusMeters, float multiplier, float minDiameter)
    {
        float d = (float)(2.0 * soiRadiusMeters * _sceneScaleSmoothed) * multiplier;
        if (d < minDiameter) d = minDiameter;
        return d;
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hologramRadiusUnity < 0.01f) hologramRadiusUnity = 0.01f;
        if (autoScaleFill < 0.05f) autoScaleFill = 0.05f;

        if (focusedBodyTargetDiameterFraction < 0.01f) focusedBodyTargetDiameterFraction = 0.01f;
        if (focusedBodyTargetDiameterFraction > 0.99f) focusedBodyTargetDiameterFraction = 0.99f;

        if (focusedBodyCraftFitFraction < 0.05f) focusedBodyCraftFitFraction = 0.05f;
        if (focusedBodyCraftFitFraction > 1.0f) focusedBodyCraftFitFraction = 1.0f;

        if (sunDisplayScaleMultiplier < 0.0f) sunDisplayScaleMultiplier = 0.0f;
        if (earthDisplayScaleMultiplier < 0.0f) earthDisplayScaleMultiplier = 0.0f;
        if (moonDisplayScaleMultiplier < 0.0f) moonDisplayScaleMultiplier = 0.0f;

        if (craftDisplayDiameterWhenFocused < 0.001f) craftDisplayDiameterWhenFocused = 0.001f;
        if (craftBodyModeReferenceDiameter < 0.001f) craftBodyModeReferenceDiameter = 0.001f;
        if (craftBodyModeMinDiameter < 0.001f) craftBodyModeMinDiameter = 0.001f;
        if (craftBodyModeMaxDiameter < craftBodyModeMinDiameter) craftBodyModeMaxDiameter = craftBodyModeMinDiameter;
        if (craftBodyModeReferenceRangeMeters < 1.0) craftBodyModeReferenceRangeMeters = 1.0;
        if (craftBodyModeShrinkExponent < 0.0f) craftBodyModeShrinkExponent = 0.0f;

        if (craftFocusDefaultRangeMeters < 1.0) craftFocusDefaultRangeMeters = 1.0;
        if (craftFocusMinDefaultRangeMeters < 1.0) craftFocusMinDefaultRangeMeters = 1.0;
        if (craftFocusMaxDefaultRangeMeters < craftFocusMinDefaultRangeMeters) craftFocusMaxDefaultRangeMeters = craftFocusMinDefaultRangeMeters;
        if (craftFocusAltitudeRangeMultiplier < 0.0) craftFocusAltitudeRangeMultiplier = 0.0;

        ApplyInitialVisualScales();
    }
#endif
}