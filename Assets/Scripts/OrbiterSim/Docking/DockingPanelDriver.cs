using UdonSharp;
using UnityEngine;
using TMPro;

using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.UdonNetworkCalling;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DockingPanelDriver : UdonSharpBehaviour
{
    // ---------------------------------------------------------------------
    // Placeholder cover states
    // ---------------------------------------------------------------------
    public const byte COVER_CLOSED = 0;
    public const byte COVER_MOVING = 1;
    public const byte COVER_OPEN   = 2;

    // ---------------------------------------------------------------------
    // References
    // ---------------------------------------------------------------------
    [Header("Core refs")]
    public SimManager simManager;
    public DockingRuntimeState dock;
    public DockingComputer dockingComp;
    public StewartPlatformController stewart;

    [Header("UI input registers")]
    [Tooltip("0 = docking disabled, 1 = docking enabled")]
    public byte enableDockingSwitchState = 0;

    [Header("Placeholder systems / interlocks")]
    [Tooltip("Placeholder until real docking cover system exists.")]
    public byte portCoverState = COVER_CLOSED;

    [Tooltip("Placeholder until real hatch system exists.")]
    public bool hatchClosed = true;

    [Tooltip("Placeholder until real bay pressure system exists.")]
    public bool bayDepressurized = true;

    [Header("Status text")]
    public TMP_Text stageText;

    [Header("Cover status lamps")]
    public Renderer lampCoverClosed;
    public Renderer lampCoverMoving;
    public Renderer lampCoverOpen;

    [Header("Action readiness lamps")]
    public Renderer lampReadyToRetract;
    public Renderer lampReadyToUndock;

    [Header("Lamp colors")]
    public Color inactiveEmission = Color.red * 1.5f;
    public Color activeEmission = Color.green * 1.5f;

    [Header("Shader property")]
    public string emissionColorProperty = "_EmissionColor";

    // ---------------------------------------------------------------------
    // Cached state
    // ---------------------------------------------------------------------
    private MaterialPropertyBlock _mpb;

    private byte _lastEnableDockingSwitchState = 255;
    private byte _lastPortCoverState = 255;
    private byte _lastDockPhase = 255;
    private bool _lastDockActive = false;
    private bool _lastHatchClosed = false;
    private bool _lastBayDepressurized = false;

    // ---------------------------------------------------------------------
    // Unity
    // ---------------------------------------------------------------------
    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        ApplyDockingPrepState(true);
        RefreshIndicators(true);
    }

    void Update()
    {
        ApplyDockingPrepState(false);
        RefreshIndicators(false);
    }

    // ---------------------------------------------------------------------
    // UI entry points
    // ---------------------------------------------------------------------

    private bool HasAuthority()
    {
        if (simManager != null) return simManager.IsSimOwner();
        return Networking.IsOwner(gameObject);
    }

    private void RouteToOwner(string ownerEventName)
    {
        if (HasAuthority())
        {
            SendCustomEvent(ownerEventName);
            return;
        }

        SendCustomNetworkEvent(NetworkEventTarget.Owner, ownerEventName);
    }


    public void EVT_EnableDockingChanged()
    {
        ApplyDockingPrepState(true);
        RefreshIndicators(true);
    }

    public void EVT_RetractPressed()
    {
        RouteToOwner(nameof(Owner_Retract));
    }

    public void EVT_UndockPressed()
    {
        RouteToOwner(nameof(Owner_Undock));
    }

    // ---------------------------------------------------------------------
    // Core behavior
    // ---------------------------------------------------------------------

    private void ApplyDockingPrepState(bool force)
    {
        bool dockingEnabled = (enableDockingSwitchState != 0);

        if (!force && dockingEnabled == (simManager != null && simManager.dockingAllowed))
        {
            // still continue to Stewart logic below
        }

        if (simManager != null)
            simManager.dockingAllowed = dockingEnabled;

        // Placeholder cover behavior.
        // Later replace this with real cover controller commands.
        if (dockingEnabled)
        {
            if (portCoverState == COVER_CLOSED)
                CommandOpenCoverPlaceholder();
        }
        else
        {
            // Don't auto-close if actively docked.
            bool activelyDocked = (dock != null && dock.active);
            if (!activelyDocked && portCoverState == COVER_OPEN)
                CommandCloseCoverPlaceholder();
        }

        // Stewart only enabled once cover is fully open and docking is enabled.
        if (stewart != null)
            stewart.platformEnabled = dockingEnabled && (portCoverState == COVER_OPEN);
    }

    private void RefreshIndicators(bool force)
    {
        byte dockPhase = (dock != null) ? dock.phase : DockingRuntimeState.DOCK_NONE;
        bool dockActive = (dock != null) && dock.active;

        if (!force &&
            _lastEnableDockingSwitchState == enableDockingSwitchState &&
            _lastPortCoverState == portCoverState &&
            _lastDockPhase == dockPhase &&
            _lastDockActive == dockActive &&
            _lastHatchClosed == hatchClosed &&
            _lastBayDepressurized == bayDepressurized)
        {
            return;
        }

        _lastEnableDockingSwitchState = enableDockingSwitchState;
        _lastPortCoverState = portCoverState;
        _lastDockPhase = dockPhase;
        _lastDockActive = dockActive;
        _lastHatchClosed = hatchClosed;
        _lastBayDepressurized = bayDepressurized;

        // Cover lamps
        SetLamp(lampCoverClosed, portCoverState == COVER_CLOSED);
        SetLamp(lampCoverMoving, portCoverState == COVER_MOVING);
        SetLamp(lampCoverOpen,   portCoverState == COVER_OPEN);

        // Action-ready lamps
        SetLamp(lampReadyToRetract, IsReadyToRetract());
        SetLamp(lampReadyToUndock,  IsReadyToUndock());

        // Stage text
        if (stageText != null)
            stageText.text = ResolveStageText();
    }


    [NetworkCallable]
    public void Owner_Retract()
    {
        if (!HasAuthority()) return;
        if (dock == null) return;
        if (!IsReadyToRetract()) return;

        dock.CommandRetract();
        RefreshIndicators(true);
    }

    [NetworkCallable]
    public void Owner_Undock()
    {
        if (!HasAuthority()) return;
        if (dockingComp == null) return;
        if (!IsReadyToUndock()) return;

        dockingComp.CommandUndock();
        RefreshIndicators(true);
    }

    // ---------------------------------------------------------------------
    // State helpers
    // ---------------------------------------------------------------------

    private void EnsureMPB()
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();
    }

    private bool IsReadyToRetract()
    {
        if (dock == null) return false;
        if (enableDockingSwitchState == 0) return false;
        if (portCoverState != COVER_OPEN) return false;

        return dock.active && dock.phase == DockingRuntimeState.DOCK_SOFT;
    }

    private bool IsReadyToUndock()
    {
        if (dockingComp == null) return false;
        if (!dockingComp.CanUndock()) return false;
        if (!hatchClosed) return false;
        if (!bayDepressurized) return false;

        return true;
    }

    private string ResolveStageText()
    {
        if (dock != null && dock.active)
        {
            switch (dock.phase)
            {
                case DockingRuntimeState.DOCK_SOFT:
                    return "SOFTLOCK";

                case DockingRuntimeState.DOCK_RETRACT:
                    return "RETRACT";

                case DockingRuntimeState.DOCK_HARD:
                    return "HARDLOCK";
            }
        }

        switch (portCoverState)
        {
            case COVER_CLOSED: return "CLOSED";
            case COVER_MOVING: return "MOVING";
            case COVER_OPEN:   return "OPEN";
        }

        return "OPEN";
    }

    // ---------------------------------------------------------------------
    // Placeholder cover control
    // ---------------------------------------------------------------------

    public void CommandOpenCoverPlaceholder()
    {
        // Later: talk to a real cover controller.
        portCoverState = COVER_OPEN;
    }

    public void CommandCloseCoverPlaceholder()
    {
        // Later: talk to a real cover controller.
        portCoverState = COVER_CLOSED;
    }

    public void SetCoverMovingPlaceholder()
    {
        portCoverState = COVER_MOVING;
    }

    // ---------------------------------------------------------------------
    // Lamp helper
    // ---------------------------------------------------------------------

    private void SetLamp(Renderer r, bool active)
    {
        if (r == null) return;

        EnsureMPB();

        r.GetPropertyBlock(_mpb);
        _mpb.SetColor(emissionColorProperty, active ? activeEmission : inactiveEmission);
        r.SetPropertyBlock(_mpb);
    }
}