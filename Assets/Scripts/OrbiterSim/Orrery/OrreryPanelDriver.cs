using UdonSharp;
using UnityEngine;

/// <summary>
/// OrreryPanelDriver
///
/// Bridges physical panel controls (switches / knobs / buttons) to the orrery.
/// This is intentionally a UI-facing state machine, not the orrery itself.
///
/// Control model:
/// - MODE switch: BODY / CRAFT / TARGET
/// - TARGET knob: discrete target selection
/// - ORIENT knob: discrete orientation override
/// - ZOOM knob: relative delta control, not absolute persistent zoom
/// - ORBITS / MARKERS / TARGETS switches: layer enables
/// - RESET button: reset manual zoom and re-baseline zoom knob
/// </summary>
public class OrreryPanelDriver : UdonSharpBehaviour
{
    // ---------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------
    public const int VIEW_BODY   = 0;
    public const int VIEW_CRAFT  = 1;
    public const int VIEW_TARGET = 2;

    public const int TARGET_SUN   = 0;
    public const int TARGET_EARTH = 1;
    public const int TARGET_MOON  = 2;
    public const int TARGET_CRAFT = 3;

    public const int ORIENT_AUTO         = 0;
    public const int ORIENT_HELIOCENTRIC = 1;
    public const int ORIENT_BODY         = 2;
    public const int ORIENT_CRAFT        = 3;

    // ---------------------------------------------------------------------
    // References
    // ---------------------------------------------------------------------
    [Header("References")]
    public OrreryController orrery;
    public OrreryCraftOrbitRibbon orbitRibbon;
    public OrreryCraftDirectionMarkers directionMarkers;

    // ---------------------------------------------------------------------
    // Input registers (written by MFDSwitch / MFDKnob)
    // ---------------------------------------------------------------------
    [Header("Panel Input Registers")]
    [Tooltip("3-way mode switch. 0=BODY, 1=CRAFT, 2=TARGET")]
    public int modeSwitchState = VIEW_BODY;

    [Tooltip("2-way switch. 0=OFF, 1=ON")]
    public int orbitsSwitchState = 1;

    [Tooltip("2-way switch. 0=OFF, 1=ON")]
    public int markersSwitchState = 1;

    [Tooltip("2-way switch. 0=OFF, 1=ON. Reserved for future target/station layer control.")]
    public int targetsSwitchState = 1;

    [Tooltip("Relative zoom knob input value.")]
    public float zoomKnobValue = 0f;

    [Tooltip("Discrete target knob input value.")]
    public float targetKnobValue = TARGET_EARTH;

    [Tooltip("Discrete orientation override knob input value.")]
    public float orientationKnobValue = ORIENT_AUTO;

    // ---------------------------------------------------------------------
    // Settings
    // ---------------------------------------------------------------------
    [Header("Zoom Settings")]
    [Tooltip("Manual zoom decades applied per 1.0 knob unit of zoom delta.")]
    public float zoomDecadesPerKnobUnit = 0.12f;

    [Tooltip("Clamp UI-driven manual zoom to this min/max range.")]
    public float manualZoomMinDecades = -4.0f;

    public float manualZoomMaxDecades = 4.0f;

    [Header("Startup")]
    public bool applyStateOnStart = true;

    // ---------------------------------------------------------------------
    // Debug / resolved state
    // ---------------------------------------------------------------------
    [Header("Resolved State (read-only)")]
    public int selectedTargetId = TARGET_EARTH;
    public int orientationOverrideId = ORIENT_AUTO;

    public bool showOrbits = true;
    public bool showMarkers = true;
    public bool showTargets = true; // reserved for future targets/stations layer

    // ---------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------
    private bool _initialized = false;
    private float _lastConsumedZoomKnobValue = 0f;

    // ---------------------------------------------------------------------
    // Unity
    // ---------------------------------------------------------------------
    void Start()
    {
        selectedTargetId = ClampTargetId(Mathf.RoundToInt(targetKnobValue));
        orientationOverrideId = ClampOrientationId(Mathf.RoundToInt(orientationKnobValue));

        _lastConsumedZoomKnobValue = zoomKnobValue;
        _initialized = true;

        if (applyStateOnStart)
            ApplyAllState(true);
    }

    // ---------------------------------------------------------------------
    // Event entry points for UI controls
    // ---------------------------------------------------------------------

    public void EVT_ModeChanged()
    {
        modeSwitchState = ClampViewMode(modeSwitchState);

        // Per your requirement:
        // changing mode resets manual zoom and also resets the knob zero reference.
        if (orrery != null)
            orrery.manualZoomDecades = 0f;

        ResetZoomKnobBaseline();
        ApplyAllState(false);
    }

    public void EVT_ZoomKnobChanged()
    {
        if (!_initialized) return;
        ConsumeZoomKnobDelta();
    }

    public void EVT_TargetChanged()
    {
        selectedTargetId = ClampTargetId(Mathf.RoundToInt(targetKnobValue));
        ApplyResolvedFocusAndOrientation();
    }

    public void EVT_OrientationChanged()
    {
        orientationOverrideId = ClampOrientationId(Mathf.RoundToInt(orientationKnobValue));
        ApplyResolvedFocusAndOrientation();
    }

    public void EVT_OrbitsChanged()
    {
        ApplyLayerState();
    }

    public void EVT_MarkersChanged()
    {
        ApplyLayerState();
    }

    public void EVT_TargetsChanged()
    {
        ApplyLayerState();
    }

    public void EVT_ResetView()
    {
        if (orrery != null)
            orrery.manualZoomDecades = 0f;

        ResetZoomKnobBaseline();
        ApplyResolvedFocusAndOrientation();
        ApplyLayerState();
    }

    // ---------------------------------------------------------------------
    // Core application
    // ---------------------------------------------------------------------

    private void ApplyAllState(bool resetZoomBaseline)
    {
        if (resetZoomBaseline)
            ResetZoomKnobBaseline();

        ApplyResolvedFocusAndOrientation();
        ApplyLayerState();
    }

    private void ApplyResolvedFocusAndOrientation()
    {
        if (orrery == null) return;

        ApplyResolvedFocus();
        ApplyResolvedOrientation();
    }

    private void ApplyResolvedFocus()
    {
        if (orrery == null) return;

        int view = ClampViewMode(modeSwitchState);

        switch (view)
        {
            default:
            case VIEW_BODY:
                ApplyBodyFocus();
                break;

            case VIEW_CRAFT:
                orrery.API_FocusCraft();
                break;

            case VIEW_TARGET:
                ApplySelectedTargetFocus();
                break;
        }
    }

    private void ApplyBodyFocus()
    {
        if (orrery == null) return;

        // BODY mode uses current primary when available.
        // Falls back to Earth if nav is unavailable or primary is unsupported.
        byte primaryId = 255;

        if (orrery.nav != null && orrery.nav.valid)
            primaryId = orrery.nav.primaryId;

        if (orrery.bodies != null)
        {
            if (primaryId == orrery.bodies.sunId)
            {
                orrery.API_FocusSun();
                return;
            }

            if (primaryId == orrery.bodies.earthId)
            {
                orrery.API_FocusEarth();
                return;
            }

            if (primaryId == orrery.bodies.moonId)
            {
                orrery.API_FocusMoon();
                return;
            }
        }

        orrery.API_FocusEarth();
    }

    private void ApplySelectedTargetFocus()
    {
        if (orrery == null) return;

        switch (selectedTargetId)
        {
            default:
            case TARGET_EARTH:
                orrery.API_FocusEarth();
                return;

            case TARGET_SUN:
                orrery.API_FocusSun();
                return;

            case TARGET_MOON:
                orrery.API_FocusMoon();
                return;

            case TARGET_CRAFT:
                orrery.API_FocusCraft();
                return;
        }
    }

    private void ApplyResolvedOrientation()
    {
        if (orrery == null) return;

        switch (orientationOverrideId)
        {
            default:
            case ORIENT_AUTO:
                orrery.API_SetOrientationAuto();
                break;

            case ORIENT_HELIOCENTRIC:
                orrery.API_SetOrientationHeliocentric();
                break;

            case ORIENT_BODY:
                orrery.API_SetOrientationBody();
                break;

            case ORIENT_CRAFT:
                orrery.API_SetOrientationCraft();
                break;
        }
    }

    private void ApplyLayerState()
    {
        showOrbits = (orbitsSwitchState != 0);
        showMarkers = (markersSwitchState != 0);
        showTargets = (targetsSwitchState != 0);

        if (orbitRibbon != null)
            orbitRibbon.displayEnabled = showOrbits;

        if (directionMarkers != null)
            directionMarkers.displayEnabled = showMarkers;

        // showTargets is reserved for future target/station layer logic.
        // Keeping it as explicit state now avoids redesign later.
    }

    // ---------------------------------------------------------------------
    // Zoom knob handling
    // ---------------------------------------------------------------------

    private void ConsumeZoomKnobDelta()
    {
        if (orrery == null) return;

        float deltaUnits = zoomKnobValue - _lastConsumedZoomKnobValue;
        if (Mathf.Abs(deltaUnits) < 1e-6f) return;

        float newZoom = orrery.manualZoomDecades + (deltaUnits * zoomDecadesPerKnobUnit);
        newZoom = Mathf.Clamp(newZoom, manualZoomMinDecades, manualZoomMaxDecades);
        orrery.manualZoomDecades = newZoom;

        // Re-baseline after consuming, so the knob acts like an incremental control.
        _lastConsumedZoomKnobValue = zoomKnobValue;
    }

    private void ResetZoomKnobBaseline()
    {
        _lastConsumedZoomKnobValue = zoomKnobValue;
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private int ClampViewMode(int v)
    {
        if (v < VIEW_BODY) return VIEW_BODY;
        if (v > VIEW_TARGET) return VIEW_TARGET;
        return v;
    }

    private int ClampTargetId(int v)
    {
        if (v < TARGET_SUN) return TARGET_SUN;
        if (v > TARGET_CRAFT) return TARGET_CRAFT;
        return v;
    }

    private int ClampOrientationId(int v)
    {
        if (v < ORIENT_AUTO) return ORIENT_AUTO;
        if (v > ORIENT_CRAFT) return ORIENT_CRAFT;
        return v;
    }
}