using UdonSharp;
using UnityEngine;

public class MFDPenAdjustPage : MFDPage
{
    //
    public PenUpdater penUpdater;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        // Any button on the bottom row (or specifically B3) to finish
        if (side == ButtonSide.Bottom && num == 2)
        {
            // Lock both pens to be safe
            penUpdater.ExitLeft();
            penUpdater.ExitRight();
            display.SetPage((byte)MFDPageID.Settings);
        }
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearText();
        display.ClearGraphics();

        string msg = "ADJUST PEN BY GRABBING PICKUP";
        display.DrawText(msg, MFD.TEXT_ROWS / 2, (MFD.TEXT_COLUMNS - msg.Length) / 2, Color.yellow);

        display.DrawText("DONE", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.green);
    }
}