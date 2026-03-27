using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDMenuPage : MFDPage
{
    [Header("Menu Entries")]
    public string[] pageNames; // ordered page list after MENU page

    [Header("Optional Logo")]
    public Texture2D logoTexture;
    
    [Tooltip("Logo placement in display UV space (xmin, ymin, xmax, ymax).")]
    public Vector4 logoRectUv = new Vector4(0.32f, 0.32f, 0.68f, 0.68f);

    [Tooltip("Tint applied to the logo image.")]
    public Color logoTint = Color.white;

    private const int BUTTONS_PER_SIDE = 5;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        int pageArrayIndex = -1;

        if (side == ButtonSide.Left) {
            if (num >= 0 && num < BUTTONS_PER_SIDE) {
                pageArrayIndex = num;
            }
        }
        else if (side == ButtonSide.Right) {
            if (num >= 0 && num < BUTTONS_PER_SIDE) {
                pageArrayIndex = BUTTONS_PER_SIDE + num;
            }
        }

        if (pageArrayIndex < 0) {
            return;
        }

        if (pageNames == null || pageArrayIndex >= pageNames.Length) {
            return;
        }

        display.SetPage((byte)(pageArrayIndex + 1));
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearGraphics();
        display.ClearText();

        DrawLogo(display);
        DrawBrandText(display); 

        DrawMenuLabels(display);
    }

    private void DrawLogo(MFD display)
    {
        if (logoTexture == null) {
            display.ClearImagePanel();
            return;
        }

        display.SetImagePanel(
            logoTexture,
            logoRectUv,
            new Vector4(0.004f, 0.004f, 0.996f, 0.996f),
            logoTint
        );
    }

    private void DrawBrandText(MFD display)
    {
        // Centered under logo for 48 columns:
        // "MFD System"                length 10 -> col 19
        // "A.S.P.E.R.B AVIONICS"      length 20 -> col 14
        // "V1.0"                      length 4  -> col 22

        display.DrawText("MFD System", 14, 19, Color.green);
        display.DrawText("A.S.P.E.R.B AVIONICS", 15, 14, Color.green);
        display.DrawText("V1.0", 16, 22, Color.green);
    }

    private void DrawMenuLabels(MFD display)
    {
        if (pageNames == null) {
            return;
        }

        int pageCount = pageNames.Length;
        if (pageCount > BUTTONS_PER_SIDE * 2) {
            pageCount = BUTTONS_PER_SIDE * 2;
        }

        for (int i = 0; i < pageCount; i++) {
            string name = pageNames[i];
            if (name == null) {
                name = "";
            }

            if (i < BUTTONS_PER_SIDE) {
                display.DrawVerticalText(name, i * 5, 0, Color.white);
            }
            else {
                int rightIndex = i - BUTTONS_PER_SIDE;
                display.DrawVerticalText(name, rightIndex * 5, MFD.TEXT_COLUMNS - 1, Color.white);
            }
        }
    }
}