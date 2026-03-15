using UdonSharp;
using UnityEngine;
using System;

public class MFDDockPage : MFDPage
{
    [Header("References")]
    public GC_UiButtonRouter router;
    public StewartPlatformController stewart;//Cant find the thing talking to the platform so for now i'll just ref it here. 

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Left)
        {
            if (num == 0) router.point_port();
            if (num == 1) router.Align_port();
            if (num == 2) router.kill_motion();
        }
        else if (side == ButtonSide.Right)
        {
            if (num == 0) router.Undock();
            if (num == 1)
            {
                router.Retract();
                stewart.platformEnabled = false;
            }
            if (num == 2) stewart.platformEnabled = true;
        }
        else if (side == ButtonSide.Bottom && num == 2)
        {
            display.SetPage((byte)MFDPageID.Menu);
        }
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearText();
        display.ClearGraphics();

        byte activeMode = router.runtime.activeProgramId;

        // --- Left Side: Autopilots ---
        display.DrawText("PNT PORT", 2, 2, activeMode == 10 ? Color.green : Color.white); // Check GC_RuntimeState for correct ID
        display.DrawText("ALN PORT", 6, 2, activeMode == 11 ? Color.green : Color.white);
        display.DrawText("KILL REL", 10, 2, activeMode == 12 ? Color.green : Color.white);

        // --- Right Side: Hardware ---
        display.DrawText("UNDOCK", 2, MFD.TEXT_COLUMNS - 8, Color.white);
        display.DrawText("RETRCT", 6, MFD.TEXT_COLUMNS - 8, Color.white);
        display.DrawText("EXTENT", 10, MFD.TEXT_COLUMNS - 8, Color.white);

        // --- Center: Data Readout ---
        int centerCol = 15;
        // Note: Assuming GC_Core/Nav provides these values. Adjust variable names if needed.
       // double range = router.gc.nav.rel_r_mag;
        //double relVel = router.gc.nav.rel_v_mag;

        display.DrawText("--- DOCKING DATA ---", 4, centerCol, Color.cyan);
        //display.DrawText(FormatNumber("RNG:", range) + "m", 6, centerCol, Color.white);
        //display.DrawText(FormatNumber("RELV:", relVel) + "m/s", 8, centerCol, Color.white);

        // Target Name (Truncated to fit)
        string targetName = "TARGET: STATION Alpha"; // Placeholder or pull from router.Dock
        display.DrawText(targetName, 12, centerCol, Color.yellow);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);

        if (stewart.platformEnabled)
        {
            display.DrawText("ON", 7, MFD.TEXT_COLUMNS - 12, Color.green);
        }
        else
        {
            display.DrawText("OFF", 7, MFD.TEXT_COLUMNS - 12, Color.red);
        }
    }

    private string FormatNumber(string title, double num)
    {
        return title.PadRight(6) + num.ToString("F2").PadLeft(8);
    }
}