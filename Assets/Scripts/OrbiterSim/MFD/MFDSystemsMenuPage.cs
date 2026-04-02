using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDSystemsMenuPage : MFDPage
{
    [Header("Optional image")]
    public Texture2D logoTexture;
    public Vector4 logoRectUv = new Vector4(0.34f, 0.20f, 0.66f, 0.52f);
    public Color logoTint = Color.white;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Left)
        {
            if (num == 0)
            {
                display.SetPage((byte)MFDPageID.SystemsForces);
                return;
            }

            if (num == 1)
            {
                display.SetPage((byte)MFDPageID.SystemsCrew);
                return;
            }

            if (num == 2)
            {
                display.SetPage((byte)MFDPageID.SystemsPdLimits);
                return;
            }
        }

        if (side == ButtonSide.Bottom && num == 2)
        {
            display.SetPage((byte)MFDPageID.Menu);
        }
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearGraphics();
        display.ClearText();

        if (logoTexture != null)
        {
            display.SetImagePanel(
                logoTexture,
                logoRectUv,
                new Vector4(0.004f, 0.004f, 0.996f, 0.996f),
                logoTint
            );
        }
        else
        {
            display.ClearImagePanel();
        }

        display.DrawText("SYSTEMS", 1, 19, Color.green);
        display.DrawText("SUBSYSTEM MENU", 2, 16, Color.green);

        display.DrawVerticalText("FRC", 0, 0, Color.white);
        display.DrawVerticalText("CREW",   5, 0, Color.white);
        display.DrawVerticalText("LIM", 10, 0, Color.white);

        display.DrawText("L1  FORCES / GC / FUEL", 8, 13, Color.green);
        display.DrawText("L2  CREW / DOCK MANIFEST", 10, 13, Color.green);
        display.DrawText("L3  PD RATE LIMITS", 12, 13, Color.green);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}