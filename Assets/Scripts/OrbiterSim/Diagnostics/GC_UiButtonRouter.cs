using UdonSharp;
using UnityEngine;

/// <summary>
/// GC_UiButtonRouter
/// Super-basic Unity UI hook to call GC_Core APIs via Button OnClick().
/// Assumes GC_Core exposes no-argument convenience APIs (HoldPrograde, etc.).
/// </summary>
public class GC_UiButtonRouter : UdonSharpBehaviour
{
    [Header("Target")]
    public GC_Core gc;

    // ---- Mode selection ----
    public void Btn_ModeManual()
    {
        if (gc == null) return;
        gc.API_SetModeManual();
    }

    public void Btn_KillRot()
    {
        if (gc == null) return;
        // Use your no-arg wrapper if you added it; otherwise call the existing API_Attitude_KillRot().
        gc.API_Attitude_KillRot();
    }

    // ---- RTN holds (no args) ----
    public void Btn_HoldPrograde()
    {
        if (gc == null) return;
        gc.API_HoldPrograde();
    }

    public void Btn_HoldRetrograde()
    {
        if (gc == null) return;
        gc.API_HoldRetrograde();
    }

    public void Btn_HoldRadialOut()
    {
        if (gc == null) return;
        gc.API_HoldRadialOut();
    }

    public void Btn_HoldRadialIn()
    {
        if (gc == null) return;
        gc.API_HoldRadialIn();
    }

    public void Btn_HoldNormal()
    {
        if (gc == null) return;
        gc.API_HoldNormal();
    }

    public void Btn_HoldAntiNormal()
    {
        if (gc == null) return;
        gc.API_HoldAntiNormal();
    }


}