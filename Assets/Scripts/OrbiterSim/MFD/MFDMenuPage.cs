using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDMenuPage : MFDPage
{
    public string[] pageNames; // ordered page list after MENU page

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

        // Page IDs are assumed to follow MENU in array order:
        // pageNames[0] -> page ID 1
        // pageNames[1] -> page ID 2
        // ...
        display.SetPage((byte)(pageArrayIndex + 1));
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearText();

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
                // Left side
                display.DrawVerticalText(name, i * 5, 0, Color.white);
            }
            else {
                // Right side
                int rightIndex = i - BUTTONS_PER_SIDE;
                display.DrawVerticalText(name, rightIndex * 5, MFD.TEXT_COLUMNS - 1, Color.white);
            }
        }
    }
}