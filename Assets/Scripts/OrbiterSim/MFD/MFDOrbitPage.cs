using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDOrbitPage : MFDPage
{
    [Header("References")]
    public BodyCatalog bodies;
    public GuidanceNavCoreState nav;

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

    private int leftMargin;
    private int topMargin;
    private Color infoColor;
    private MFD currentDisplay;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
        }
    }

    public void Update()
    {
        if (nav == null || !nav.valid) return;

        a = nav.a;
        eccentricity = nav.e;

        double mu = nav.muPrimary;

        // periapsis valid for any conic if p/e are valid
        periapsis = nav.p / (1.0 + eccentricity);

        if (eccentricity < 1.0 && a > 0.0 && mu > 0.0) {
            period = 2.0 * Math.PI * Math.Sqrt(a * a * a / mu);
            apoapsis = nav.p / (1.0 - eccentricity);
        } else {
            period = 0.0;
            apoapsis = -1.0;
        }

        raan = nav.raanRad;
        inclination = nav.iRad;
        argp = nav.argpRad;

        // Build perifocal basis directly from nav in E frame
        Vector3 pHat;
        if (nav.eVec_E.sqrMagnitude > 1e-12f) {
            pHat = nav.eVec_E.normalized;
        } else {
            Vector3 rNow = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);
            if (rNow.sqrMagnitude > 1e-12f) pHat = rNow.normalized;
            else pHat = Vector3.right;
        }

        Vector3 wHat = nav.h_E.normalized;
        Vector3 qHat = Vector3.Cross(wHat, pHat).normalized;

        Vector3 bodyPos = new Vector3(
            (float)nav.r_x,
            (float)nav.r_y,
            (float)nav.r_z
        );

        double pfX = Vector3.Dot(bodyPos, pHat);
        double pfY = Vector3.Dot(bodyPos, qHat);

        // MFD display convention matches your old pages:
        // screen x = perifocal y
        // screen y = -perifocal x
        posX = (float)pfY;
        posY = (float)(-pfX);
    }

    void DrawInfo(int line, string info)
    {
        currentDisplay.DrawText(info, topMargin + line, leftMargin, infoColor);
    }

    public override void DrawDisplay(MFD display)
    {
        const float orbitSize = 0.5f;

        display.ClearGraphics();

        float scale = orbitSize / Mathf.Max(1f, Mathf.Abs((float)a));

        // Keep the focus at origin. Do NOT offset by periapsis.
        Vector2 focus = Vector2.zero;

        display.DrawConic(focus, scale * (float)bodies.GetRadius(nav.primaryId), 0f, 0f, Color.white * 0.2f);
        display.DrawConic(focus, (float)periapsis * scale, 0f, (float)eccentricity, Color.green);
        display.DrawLine(focus, focus + scale * new Vector2(posX, posY), Color.green);

        currentDisplay = display;
        leftMargin = 2;
        topMargin = 2;
        infoColor = Color.green;

        display.ClearText();
        if (eccentricity < 1.0 && a > 0.0) {
            DrawInfo(0, MFD.FormatNumber("T", period));
            DrawInfo(1, MFD.FormatNumber("ApR", apoapsis));
        } else {
            DrawInfo(0, "HYPERBOLIC");
        }
        DrawInfo(2, MFD.FormatNumber("PeR", periapsis));
        DrawInfo(3, MFD.FormatNumber("Ecc", eccentricity));
        DrawInfo(4, MFD.FormatAngle("LAN", raan));
        DrawInfo(5, MFD.FormatAngle("Inc", inclination));
        DrawInfo(6, MFD.FormatAngle("AgP", argp));

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}