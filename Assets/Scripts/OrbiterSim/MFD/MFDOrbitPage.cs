using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDOrbitPage : MFDPage
{
    [Header("References")]
    public BodyCatalog bodies;
    public ConicState conic;
    public ConicPropagator conicPropagator;
    public MFDPage menuPage;

    [Header("Display Data")]
    public double eccentricity;
    public double a;
    public double period;
    public double apoapsis;
    public double periapsis;
    public double raan;
    public double inclination;
    public double argp;

    private double lastEpochT0 = Double.NegativeInfinity;
    private string[] suffixes =  new[] {"", "k", "M", "G"};

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage(menuPage);
        }
    }

    public void Update()
    {
        if (conic.epochT0 != lastEpochT0) {
            lastEpochT0 = conic.epochT0;

            a = conic.aMeters;
            eccentricity = conic.e;

            if (eccentricity < 1.0 && a > 0.0) {
                double mu = bodies.GetMu(conic.primaryBodyId);
                period = 2.0 * Math.PI * Math.Sqrt(a * a * a / mu);
                apoapsis = a * (1.0 + eccentricity);
            }

            periapsis = a * (1 - eccentricity);

            raan = conic.raanRad;
            inclination = conic.iRad;
            argp = conic.argpRad;
        }

        for (int i = 0; i < activeDisplayCount; i++) {
            DrawDisplay(activeDisplays[i]);
        }
    }

    private string FormatNumber(string title, double num)
    {
        int i;
        for (i = 0; i < 4; i++) {
            if (num < 10.0) {
                break;
            }

            num /= 1000.0;
        }

        // FIXME: Is there a better way to fall back?
        if (i == 4) {
            return "";
        }

        return title.PadRight(4) + num.ToString(i == 0 ? "0.0000" : "0.000") + suffixes[i];
    }

    private string FormatAngle(string title, double angle)
    {
        return title.PadRight(4) + (Math.PI / 180.0 * angle).ToString("0.0").PadLeft(5) + "°";
    }

    public override void DrawDisplay(MFD display)
    {
        const float orbitSize = 0.75f;

        display.ClearGraphics();
        float focusY = -orbitSize * (1f - (float)(periapsis / a));
        display.DrawConic(new Vector2(0f, focusY), (float)(periapsis * orbitSize / a), 0f, (float)eccentricity, Color.green);

        const int leftMargin = 2;
        const int topMargin = 2;

        display.ClearText();
        if (eccentricity < 1.0 && a > 0.0) {
            display.DrawText(FormatNumber("T", period), topMargin + 0, leftMargin, Color.white);

            display.DrawText(FormatNumber("ApR", period), topMargin + 1, leftMargin, Color.white);
        }
        display.DrawText(FormatNumber("PeR", periapsis), topMargin + 2, leftMargin, Color.white);
        display.DrawText(FormatNumber("Ecc", eccentricity), topMargin + 3, leftMargin, Color.white);
        display.DrawText(FormatAngle("LAN", raan), topMargin + 4, leftMargin, Color.white);
        display.DrawText(FormatAngle("Inc", inclination), topMargin + 5, leftMargin, Color.white);
        display.DrawText(FormatAngle("AgP", argp), topMargin + 6, leftMargin, Color.white);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}
