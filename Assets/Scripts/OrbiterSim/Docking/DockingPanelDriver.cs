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
    // References
    // ---------------------------------------------------------------------
    [Header("Core refs")]
    public SimManager simManager;
    public DockingRuntimeState dock;
    public DockingComputer dockingComp;
    public DockingOpsController ops;
    public DockingOccupancyGate occupancyGate;

    [Header("Status text")]
    public TMP_Text stageText;

    [Header("Port status lamps")]
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

    private byte _lastPortState = 255;
    private byte _lastHatchState = 255;
    private byte _lastDockPhase = 255;
    private bool _lastDockActive = false;
    private bool _lastAllowDockingCapture = false;
    private bool _lastAllowStewart = false;

    // ---------------------------------------------------------------------
    // Unity
    // ---------------------------------------------------------------------
    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        RefreshIndicators(true);
    }

    void Update()
    {
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

    public void EVT_PortPressed()
    {
        RouteToOwner(nameof(Owner_PortPressed));
    }

    public void EVT_HatchPressed()
    {
        RouteToOwner(nameof(Owner_HatchPressed));
    }

    public void EVT_RetractPressed()
    {
        RouteToOwner(nameof(Owner_Retract));
    }

    public void EVT_UndockPressed()
    {
        RouteToOwner(nameof(Owner_Undock));
    }


    public void EVT_AirlockDoorPressed()
    {
        RouteToOwner(nameof(Owner_AirlockDoorPressed));
    }

    // ---------------------------------------------------------------------
    // Owner command handlers
    // ---------------------------------------------------------------------
    [NetworkCallable]
    public void Owner_PortPressed()
    {
        if (!HasAuthority()) return;
        if (ops == null) return;

        ops.CommandPortButton();
        RefreshIndicators(true);
    }

    [NetworkCallable]
    public void Owner_HatchPressed()
    {
        if (!HasAuthority()) return;
        if (ops == null) return;

        ops.CommandHatchButton();
        RefreshIndicators(true);
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

    [NetworkCallable]
    public void Owner_AirlockDoorPressed()
    {
        if (!HasAuthority()) return;
        if (ops == null) return;

        ops.CommandAirlockDoorButton();
        RefreshIndicators(true);
    }

    // ---------------------------------------------------------------------
    // UI/state helpers
    // ---------------------------------------------------------------------
    private void EnsureMPB()
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();
    }

    private byte GetPortState()
    {
        if (ops == null) return DockingOpsController.MECH_CLOSED;
        return ops.portState;
    }

    private byte GetHatchState()
    {
        if (ops == null) return DockingOpsController.MECH_CLOSED;
        return ops.hatchState;
    }

    private bool GetAllowDockingCapture()
    {
        return ops != null && ops.allowDockingCapture;
    }

    private bool GetAllowStewart()
    {
        return ops != null && ops.allowStewart;
    }

    private bool IsReadyToRetract()
    {
        if (dock == null) return false;
        if (ops == null) return false;

        if (!ops.IsPortOpen()) return false;
        if (!ops.IsHatchClosed()) return false;

        return dock.active && dock.phase == DockingRuntimeState.DOCK_SOFT;
    }

    private bool IsReadyToUndock()
    {
        if (dockingComp == null) return false;
        if (ops == null) return false;

        if (!dockingComp.CanUndock()) return false;
        if (!ops.IsHatchClosed()) return false;
        if (occupancyGate != null && occupancyGate.AnyPlayerOutsideCraft()) return false;

        return true;

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

        if (ops == null)
            return "OFF";

        switch (ops.portState)
        {
            case DockingOpsController.MECH_CLOSED:
                return "CLOSED";

            case DockingOpsController.MECH_OPENING:
            case DockingOpsController.MECH_CLOSING:
                return "MOVING";

            case DockingOpsController.MECH_OPEN:
                return "OPEN";
        }

        return "OFF";
    }

    private void RefreshIndicators(bool force)
    {
        byte dockPhase = (dock != null) ? dock.phase : DockingRuntimeState.DOCK_NONE;
        bool dockActive = (dock != null) && dock.active;

        byte portState = GetPortState();
        byte hatchState = GetHatchState();
        bool allowDockingCapture = GetAllowDockingCapture();
        bool allowStewart = GetAllowStewart();

        if (!force &&
            _lastPortState == portState &&
            _lastHatchState == hatchState &&
            _lastDockPhase == dockPhase &&
            _lastDockActive == dockActive &&
            _lastAllowDockingCapture == allowDockingCapture &&
            _lastAllowStewart == allowStewart)
        {
            return;
        }

        _lastPortState = portState;
        _lastHatchState = hatchState;
        _lastDockPhase = dockPhase;
        _lastDockActive = dockActive;
        _lastAllowDockingCapture = allowDockingCapture;
        _lastAllowStewart = allowStewart;

        // Port lamps
        SetLamp(lampCoverClosed, portState == DockingOpsController.MECH_CLOSED);
        SetLamp(lampCoverMoving,
            portState == DockingOpsController.MECH_OPENING ||
            portState == DockingOpsController.MECH_CLOSING);
        SetLamp(lampCoverOpen, portState == DockingOpsController.MECH_OPEN);

        // Action-ready lamps
        SetLamp(lampReadyToRetract, IsReadyToRetract());
        SetLamp(lampReadyToUndock, IsReadyToUndock());

        // Stage text
        if (stageText != null)
            stageText.text = ResolveStageText();
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