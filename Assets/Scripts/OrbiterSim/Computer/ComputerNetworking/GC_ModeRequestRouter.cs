using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.UdonNetworkCalling;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GC_ModeRequestRouter : UdonSharpBehaviour
{
    [Header("Authority")]
    public SimManager simManager;

    [Header("Targets")]
    public GC_Core gc;
    public GC_RuntimeNetState runtimeNet;
    public GC_RuntimeState runtime;
    public GC_ModeParams modeParams;
    [Header("Button lamps (Renderer using emissive material)")]
    public Renderer lampManual;
    public Renderer lampKillRot;
    public Renderer lampHoldCurrent;
    public Renderer lampHoldCurrentAndKillRot;

    public Renderer lampPrograde;
    public Renderer lampRetrograde;
    public Renderer lampRadialOut;
    public Renderer lampRadialIn;
    public Renderer lampNormal;
    public Renderer lampAntiNormal;

    public Renderer lampDockPointShipZAtPort;
    public Renderer lampDockAlignPorts;

    public Renderer lampPointAlongRelVel;
    public Renderer lampPointAgainstRelVel;

    public Renderer lampRelativeKillVel;
    public Renderer lampRelativeStopAssist;
    public Renderer lampRelativeToggleKillVel;

    [Header("Lamp colors")]
    public Color inactiveEmission = Color.red * 1.5f;
    public Color activeEmission = Color.green * 1.5f;

    [Header("Shader property")]
    public string emissionColorProperty = "_EmissionColor";

    private MaterialPropertyBlock _mpb;

    private byte _lastProgramId = 255;
    private byte _lastModeId = 255;
    private byte _lastTranslateModeId = 255;

    private bool HasAuthority()
    {
        bool goOwner = Networking.IsOwner(gameObject);
        bool simAuth = (simManager == null) ? true : simManager.IsSimOwner();
        return goOwner && simAuth;
    }

    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        RefreshLamps(true);
    }

    void Update()
    {
        RefreshLamps(false);
    }

    private void AfterStateChange()
    {
        // if (runtimeNet != null)
        //     runtimeNet.ForcePublish();

        RefreshLamps(true);
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

    private void RefreshLamps(bool force)
    {
        if (runtime == null) return;

        byte prog = runtime.activeProgramId;
        byte mode = runtime.activeModeId;
        byte xlat = runtime.activeTranslateModeId;

        if (!force && prog == _lastProgramId && mode == _lastModeId && xlat == _lastTranslateModeId)
            return;

        _lastProgramId = prog;
        _lastModeId = mode;
        _lastTranslateModeId = xlat;

        // Clear all to inactive first
        SetLamp(lampManual, false);
        SetLamp(lampKillRot, false);
        SetLamp(lampHoldCurrent, false);
        SetLamp(lampHoldCurrentAndKillRot, false);

        SetLamp(lampPrograde, false);
        SetLamp(lampRetrograde, false);
        SetLamp(lampRadialOut, false);
        SetLamp(lampRadialIn, false);
        SetLamp(lampNormal, false);
        SetLamp(lampAntiNormal, false);

        SetLamp(lampDockPointShipZAtPort, false);
        SetLamp(lampDockAlignPorts, false);

        SetLamp(lampPointAlongRelVel, false);
        SetLamp(lampPointAgainstRelVel, false);

        SetLamp(lampRelativeKillVel, false);
        SetLamp(lampRelativeStopAssist, false);
        SetLamp(lampRelativeToggleKillVel, false);

        // Attitude program lamps
        switch (prog)
        {
            case GC_RuntimeState.PROG_MANUAL:
                SetLamp(lampManual, true);
                break;

            case GC_RuntimeState.PROG_KILL_ROT:
                SetLamp(lampKillRot, true);
                break;

            case GC_RuntimeState.PROG_HOLD_ATT:
                SetLamp(lampHoldCurrent, true);
                SetLamp(lampHoldCurrentAndKillRot, true); // both map to hold-quat style
                break;

            case GC_RuntimeState.PROG_HOLD_PROGRADE:
                SetLamp(lampPrograde, true);
                break;

            case GC_RuntimeState.PROG_HOLD_RETRO:
                SetLamp(lampRetrograde, true);
                break;

            case GC_RuntimeState.PROG_HOLD_RAD_OUT:
                SetLamp(lampRadialOut, true);
                break;

            case GC_RuntimeState.PROG_HOLD_RAD_IN:
                SetLamp(lampRadialIn, true);
                break;

            case GC_RuntimeState.PROG_HOLD_NORMAL:
                SetLamp(lampNormal, true);
                break;

            case GC_RuntimeState.PROG_HOLD_ANTINORM:
                SetLamp(lampAntiNormal, true);
                break;

            case GC_RuntimeState.PROG_RELVEL_PRO:
                SetLamp(lampPointAlongRelVel, true);
                break;

            case GC_RuntimeState.PROG_RELVEL_RETRO:
                SetLamp(lampPointAgainstRelVel, true);
                break;
        }

        // Docking modes use activeModeId directly
        if (mode == GC_RuntimeState.MODE_DOCK_POINT_SHIPZ_TO_PORT)
            SetLamp(lampDockPointShipZAtPort, true);

        if (mode == GC_RuntimeState.MODE_DOCK_ALIGN_PORTS)
            SetLamp(lampDockAlignPorts, true);

        // Translation assist uses activeTranslateModeId
        if (xlat == GC_RuntimeState.XLAT_KILL_RELVEL)
        {
            SetLamp(lampRelativeKillVel, true);
            SetLamp(lampRelativeToggleKillVel, true);
        }
        else if (xlat == GC_RuntimeState.XLAT_MANUAL)
        {
            SetLamp(lampRelativeStopAssist, true);
        }
    }

    private void SetLamp(Renderer r, bool active)
    {
        if (r == null) return;

        r.GetPropertyBlock(_mpb);
        _mpb.SetColor(emissionColorProperty, active ? activeEmission : inactiveEmission);
        r.SetPropertyBlock(_mpb);
    }

    // -----------------------------------------------------------------
    // Button-facing request methods
    // -----------------------------------------------------------------

    public void RequestSetManual()                 { RouteToOwner(nameof(Owner_SetManual)); }
    public void RequestKillRot()                  { RouteToOwner(nameof(Owner_KillRot)); }
    public void RequestHoldCurrent()              { RouteToOwner(nameof(Owner_HoldCurrent)); }
    public void RequestHoldCurrentAndKillRot()    { RouteToOwner(nameof(Owner_HoldCurrentAndKillRot)); }

    public void RequestHoldPrograde()             { RouteToOwner(nameof(Owner_HoldPrograde)); }
    public void RequestHoldRetrograde()           { RouteToOwner(nameof(Owner_HoldRetrograde)); }
    public void RequestHoldRadialOut()            { RouteToOwner(nameof(Owner_HoldRadialOut)); }
    public void RequestHoldRadialIn()             { RouteToOwner(nameof(Owner_HoldRadialIn)); }
    public void RequestHoldNormal()               { RouteToOwner(nameof(Owner_HoldNormal)); }
    public void RequestHoldAntiNormal()           { RouteToOwner(nameof(Owner_HoldAntiNormal)); }

    public void RequestDockPointShipZAtPort()     { RouteToOwner(nameof(Owner_DockPointShipZAtPort)); }
    public void RequestDockAlignPorts()           { RouteToOwner(nameof(Owner_DockAlignPorts)); }

    public void RequestPointAlongRelVel()         { RouteToOwner(nameof(Owner_PointAlongRelVel)); }
    public void RequestPointAgainstRelVel()       { RouteToOwner(nameof(Owner_PointAgainstRelVel)); }

    public void RequestRelativeKillVel()          { RouteToOwner(nameof(Owner_RelativeKillVel)); }
    public void RequestRelativeStopAssist()       { RouteToOwner(nameof(Owner_RelativeStopAssist)); }
    public void RequestRelativeToggleKillVel()    { RouteToOwner(nameof(Owner_RelativeToggleKillVel)); }

    // -----------------------------------------------------------------
    // Owner-only receivers
    // -----------------------------------------------------------------
    
    [NetworkCallable]
    public void Owner_SetManual()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_SetModeManual();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_KillRot()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_KillRot();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_HoldCurrent()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_HoldCurrent();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_HoldCurrentAndKillRot()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_HoldCurrentAndKillRot();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_HoldPrograde()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldPrograde();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_HoldRetrograde()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldRetrograde();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_HoldRadialOut()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldRadialOut();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_HoldRadialIn()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldRadialIn();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_HoldNormal()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldNormal();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_HoldAntiNormal()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldAntiNormal();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_DockPointShipZAtPort()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Dock_PointShipZAtTargetPort();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_DockAlignPorts()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Dock_AlignPorts();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_PointAlongRelVel()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_PointAlongRelVel(gc.defaultBodyAxisToPoint);
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_PointAgainstRelVel()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_PointAgainstRelVel(gc.defaultBodyAxisToPoint);
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_RelativeKillVel()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Relative_KillVel_SelectedStation();
        AfterStateChange();
    }
    
    [NetworkCallable]
    public void Owner_RelativeStopAssist()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Relative_StopTranslationAssist();
        AfterStateChange();
    }

    [NetworkCallable]
    public void Owner_RelativeToggleKillVel()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Relative_ToggleKillVel();
        AfterStateChange();
    }
}