using UdonSharp;
using UnityEngine;

public class MFDSettingsPage : MFDPage
{
    public PenUpdater penUpdater;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Left)
        {
            if (num == 0) // L1: Pen L
            {
                penUpdater.EnterLeft();
                display.SetPage((byte)MFDPageID.PenAdjust);
            }
            else if (num == 1) // L2: Pen R
            {
                penUpdater.EnterRight();
                display.SetPage((byte)MFDPageID.PenAdjust);
            }
            else if (num == 2) // L3: Toggle Filter
            {
                penUpdater.toggleFilter();
            }
        }
        else if (side == ButtonSide.Bottom && num == 2) // B3: Back
        {
            display.SetPage((byte)MFDPageID.Menu);
        }
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearText();
        display.ClearGraphics();

        // Button Labels
        display.DrawText("PEN L", 2, 2, Color.white);
        display.DrawText("PEN R", 6, 2, Color.white);

        display.DrawText("FILTER: ", 10, 2, Color.white);
        if (penUpdater.useFilter)
            display.DrawText("ON", 10, 10, Color.green);
        else
            display.DrawText("OFF", 10, 10, Color.red);

        display.DrawText("BACK", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}