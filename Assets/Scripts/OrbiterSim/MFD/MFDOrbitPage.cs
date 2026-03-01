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

    public float posX;
    public float posY;

    private Quaternion bodyToPerifocal;

    private double lastEpochT0 = Double.NegativeInfinity;
    private string[] suffixes =  new[] {"", "k", "M", "G"};


    private int leftMargin;
    private int topMargin;
    private Color infoColor;
    private MFD currentDisplay;

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

            const double RAD2DEG = 180 / Math.PI;
            bodyToPerifocal = Quaternion.Euler(0, 0, (float)(-argp * RAD2DEG))
                * Quaternion.Euler((float)(-inclination * RAD2DEG), 0, 0)
                * Quaternion.Euler(0, 0, (float)(-raan * RAD2DEG));
        }

        Vector3 bodyPos = new Vector3((float)conicPropagator.rel_rx, (float)conicPropagator.rel_ry, (float)conicPropagator.rel_rz);
        Vector3 perifocalPos = bodyToPerifocal * bodyPos;
        posX = perifocalPos.y;
        posY = -perifocalPos.x;
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
        return title.PadRight(4) + (180.0 / Math.PI * angle).ToString("0.0").PadLeft(5) + "°";
    }

    void DrawInfo(int line, string info) 
    {
        currentDisplay.DrawText(info, topMargin + line, leftMargin, infoColor);
    }

    public override void DrawDisplay(MFD display)
    {
        const float orbitSize = 0.5f;

        display.ClearGraphics();
        float scale = orbitSize / (float)a;
        float focusY = (float)periapsis * scale - orbitSize;
        Vector2 center = new Vector2(0f, focusY);
        display.DrawConic(center, (float)(periapsis * scale), 0f, (float)eccentricity, Color.green);
        display.DrawLine(center, center + scale * new Vector2(posX, posY), Color.green);

        // Some nested function support would feel pretty sweet right around now
        currentDisplay = display;
        leftMargin = 2;
        topMargin = 2;
        infoColor = Color.green;

        display.ClearText();
        if (eccentricity < 1.0 && a > 0.0) {
            DrawInfo(0, FormatNumber("T", period));
            DrawInfo(1, FormatNumber("ApR", apoapsis));
        }
        DrawInfo(2, FormatNumber("PeR", periapsis));
        DrawInfo(3, FormatNumber("Ecc", eccentricity));
        DrawInfo(4, FormatAngle("LAN", raan));
        DrawInfo(5, FormatAngle("Inc", inclination));
        DrawInfo(6, FormatAngle("AgP", argp));

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}
