using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDMenuPage : MFDPage
{
    public MFDPage[] pages;
    public string[] pageNames; // Left side only for now

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Left) {
            if (num < pages.Length && pages[num] != null) {
                display.SetPage((byte)(num+1));
            }
        }
    }

    public override void DrawDisplay(MFD display)
    {
        int pageCount = Math.Min(pages.Length, pageNames.Length);
        for (int i = 0; i < pageCount; i++) {
            string name = pageNames[i];
            int len = name.Length;

            for (int j = 0; j < len && j < 5; j++) {
                display.DrawText(name[j].ToString(), (i + 1) * MFD.TEXT_ROWS / (5*2) - 2 + j, 0, Color.white);
            }
        }
    }
}
