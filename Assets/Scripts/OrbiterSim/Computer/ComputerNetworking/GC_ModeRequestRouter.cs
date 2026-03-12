using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GC_ModeRequestRouter : UdonSharpBehaviour
{
    [Header("Authority")]
    public SimManager simManager;

    [Header("Targets")]
    public GC_Core gc;
    public GC_RuntimeNetState runtimeNet;

    private bool HasAuthority()
    {
        bool goOwner = Networking.IsOwner(gameObject);
        bool simAuth = (simManager == null) ? true : simManager.IsSimOwner();
        return goOwner && simAuth;
    }

    private void AfterStateChange()
    {
        if (runtimeNet != null)
            runtimeNet.ForcePublish();
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

    public void Owner_SetManual()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_SetModeManual();
        AfterStateChange();
    }

    public void Owner_KillRot()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_KillRot();
        AfterStateChange();
    }

    public void Owner_HoldCurrent()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_HoldCurrent();
        AfterStateChange();
    }

    public void Owner_HoldCurrentAndKillRot()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_HoldCurrentAndKillRot();
        AfterStateChange();
    }

    public void Owner_HoldPrograde()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldPrograde();
        AfterStateChange();
    }

    public void Owner_HoldRetrograde()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldRetrograde();
        AfterStateChange();
    }

    public void Owner_HoldRadialOut()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldRadialOut();
        AfterStateChange();
    }

    public void Owner_HoldRadialIn()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldRadialIn();
        AfterStateChange();
    }

    public void Owner_HoldNormal()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldNormal();
        AfterStateChange();
    }

    public void Owner_HoldAntiNormal()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_HoldAntiNormal();
        AfterStateChange();
    }

    public void Owner_DockPointShipZAtPort()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Dock_PointShipZAtTargetPort();
        AfterStateChange();
    }

    public void Owner_DockAlignPorts()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Dock_AlignPorts();
        AfterStateChange();
    }

    public void Owner_PointAlongRelVel()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_PointAlongRelVel(gc.defaultBodyAxisToPoint);
        AfterStateChange();
    }

    public void Owner_PointAgainstRelVel()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Attitude_PointAgainstRelVel(gc.defaultBodyAxisToPoint);
        AfterStateChange();
    }

    public void Owner_RelativeKillVel()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Relative_KillVel_SelectedStation();
        AfterStateChange();
    }

    public void Owner_RelativeStopAssist()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Relative_StopTranslationAssist();
        AfterStateChange();
    }

    public void Owner_RelativeToggleKillVel()
    {
        if (!HasAuthority() || gc == null) return;
        gc.API_Relative_ToggleKillVel();
        AfterStateChange();
    }
}