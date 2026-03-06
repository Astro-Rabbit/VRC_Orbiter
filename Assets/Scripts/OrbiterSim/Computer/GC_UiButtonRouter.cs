using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GC_UiButtonRouter
/// Super-basic Unity UI hook to call GC_Core APIs via Button OnClick().
/// Assumes GC_Core exposes no-argument convenience APIs (HoldPrograde, etc.).
/// </summary>
public class GC_UiButtonRouter : UdonSharpBehaviour
{
    [Header("Target")]
    public GC_Core gc;
    public GC_RuntimeState runtime;
    public DockingComputer Dock;
    public DockingRuntimeState DockState;

    [Header("Test Settings")]
    public float deltaV_mps = 50f;      // test burn magnitude
    public float leadTimeSec = 60f;     // schedule node this many seconds in future


    [Header("Buttons (background Images)")]
    public Image manualBtn;

    public Image holdAttBtn;
    public Image killRotBtn;
    public Image progradeBtn;
    public Image retroBtn;
    public Image radOutBtn;
    public Image radInBtn;
    public Image normalBtn;
    public Image antiNormBtn;

    [Header("Colors")]
    public Color offColor = new Color(0.2f,0.2f,0.2f,1f);
    public Color onColor  = new Color(0.2f,0.8f,1f,1f);


    void Update()
    {
        if (runtime == null) return;

        byte id = runtime.activeProgramId;

        // Reset everything
        SetAllOff();

        // Turn on correct one
        switch (id)
        {
            case GC_RuntimeState.PROG_MANUAL:
                manualBtn.color = onColor; break;

            case GC_RuntimeState.PROG_HOLD_ATT:
                holdAttBtn.color = onColor; break;


            case GC_RuntimeState.PROG_KILL_ROT:
                killRotBtn.color = onColor; break;

            case GC_RuntimeState.PROG_HOLD_PROGRADE:
                progradeBtn.color = onColor; break;

            case GC_RuntimeState.PROG_HOLD_RETRO:
                retroBtn.color = onColor; break;

            case GC_RuntimeState.PROG_HOLD_RAD_OUT:
                radOutBtn.color = onColor; break;

            case GC_RuntimeState.PROG_HOLD_RAD_IN:
                radInBtn.color = onColor; break;

            case GC_RuntimeState.PROG_HOLD_NORMAL:
                normalBtn.color = onColor; break;

            case GC_RuntimeState.PROG_HOLD_ANTINORM:
                antiNormBtn.color = onColor; break;

        }
    }


    void SetAllOff()
    {
        manualBtn.color   = offColor;

        holdAttBtn.color  = offColor;
        killRotBtn.color  = offColor;
        progradeBtn.color = offColor;
        retroBtn.color    = offColor;
        radOutBtn.color   = offColor;
        radInBtn.color    = offColor;
        normalBtn.color   = offColor;
        antiNormBtn.color = offColor;
    }

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


    public void Btn_HoldAttitude()
    {
        if (gc == null) return;
        gc.API_Attitude_HoldCurrent();
    }


    public void point_port()
    {
        gc.API_Dock_PointShipZAtTargetPort();
    }

    public void Align_port()
    {
        gc.API_Dock_AlignPorts();
    }


    public void kill_motion()
    {
        gc.API_Relative_KillVel_SelectedStation();
    }

    public void API_TestNode()
    {
        if (gc == null || gc.nav == null) return;

        // Execution time = now + lead
        double tExec = gc.nav.t + (double)leadTimeSec;

        // Use current prograde as burn direction
        Vector3 progradeE = gc.nav.That_E;

        Vector3 dvE = progradeE * deltaV_mps;

        int idx = gc.API_Node_CreateAtTime(dvE, tExec);

        if (idx >= 0)
        {
            Debug.Log("[GC_TestNodeButton] Created test node index " + idx);
        }
        else
        {
            Debug.LogWarning("[GC_TestNodeButton] Failed to create node (no free slot).");
        }
    }

    public void Undock()
    {
        Dock.CommandUndock();
    }

    public void Retract()
    {
        DockState.CommandRetract();
    }
}