using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDOrbitPage : MFDPage
{
    [Header("References")]
    public GuidanceNavCoreState nav;

    [Header("Display Data")]
    public double eccentricity;
    public double a;
    public double p;
    public double period;
    public double apoapsis;
    public double periapsis;
    public double raan;
    public double inclination;
    public double argp;

    public float posX;
    public float posY;

    private Quaternion bodyToPerifocal;

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
        if (nav == null || !nav.valid) {
            return;
        }

        // -------------------------
        // Pull live conic data from GC nav
        // -------------------------
        a = nav.a;
        p = nav.p;
        eccentricity = nav.e;

        raan = nav.raanRad;
        inclination = nav.iRad;
        argp = nav.argpRad;

        // Reset values every frame so nothing stale survives regime changes.
        period = 0.0;
        apoapsis = 0.0;
        periapsis = 0.0;

        // Prefer p-based radius formulas when available.
        // They are well behaved and align with the GC's fitted conic data.
        if (p > 0.0) {
            periapsis = p / (1.0 + eccentricity);

            if (eccentricity < 1.0) {
                apoapsis = p / (1.0 - eccentricity);
            }
        }
        else {
            // Fallback in case p is unavailable/invalid for some reason.
            periapsis = a * (1.0 - eccentricity);

            if (eccentricity < 1.0) {
                apoapsis = a * (1.0 + eccentricity);
            }
        }

        if (eccentricity < 1.0 && a > 0.0 && nav.muPrimary > 0.0) {
            period = 2.0 * Math.PI * Math.Sqrt(a * a * a / nav.muPrimary);
        }

        // -------------------------
        // Build body->perifocal transform
        // Same convention as old page so the look stays the same.
        // -------------------------
        const double RAD2DEG = 180.0 / Math.PI;
        bodyToPerifocal =
            Quaternion.Euler(0f, 0f, (float)(-argp * RAD2DEG)) *
            Quaternion.Euler((float)(-inclination * RAD2DEG), 0f, 0f) *
            Quaternion.Euler(0f, 0f, (float)(-raan * RAD2DEG));

        // -------------------------
        // Current craft position in perifocal plane
        // nav.r_* is craft relative to primary in the inertial/body-reference
        // convention already used by GC nav.
        // -------------------------
        Vector3 bodyPos = new Vector3(
            (float)nav.r_x,
            (float)nav.r_y,
            (float)nav.r_z
        );

        Vector3 perifocalPos = bodyToPerifocal * bodyPos;
        posX = perifocalPos.y;
        posY = -perifocalPos.x;
    }

    void DrawInfo(int line, string info)
    {
        currentDisplay.DrawText(info, topMargin + line, leftMargin, infoColor);
    }

    public override void DrawDisplay(MFD display)
    {
        const float orbitSize = 0.5f;

        display.ClearGraphics();

        if (nav != null && nav.valid) {
            bool canDrawEllipse = (eccentricity < 1.0 && periapsis > 0.0 && apoapsis > 0.0);

            if (canDrawEllipse) {
                float scale = orbitSize / (float)apoapsis;
                Vector2 center = new Vector2(0f, -orbitSize + (float)periapsis * scale);

                display.DrawConic(
                    center,
                    scale * (float)nav.radiusPrimary,
                    0f,
                    0f,
                    Color.white * 0.2f
                );

                display.DrawConic(
                    center,
                    (float)periapsis * scale,
                    0f,
                    (float)eccentricity,
                    Color.green
                );

                display.DrawLine(center, center + scale * new Vector2(posX, posY), Color.green);
            }
        }

        currentDisplay = display;
        leftMargin = 2;
        topMargin = 2;
        infoColor = Color.green;

        display.ClearText();

        if (nav != null && nav.valid) {
            if (eccentricity < 1.0 && a > 0.0) {
                DrawInfo(0, MFD.FormatNumber("T", period));
                DrawInfo(1, MFD.FormatNumber("ApR", apoapsis));
            }

            DrawInfo(2, MFD.FormatNumber("PeR", periapsis));
            DrawInfo(3, MFD.FormatNumber("Ecc", eccentricity));
            DrawInfo(4, MFD.FormatAngle("LAN", raan));
            DrawInfo(5, MFD.FormatAngle("Inc", inclination));
            DrawInfo(6, MFD.FormatAngle("AgP", argp));
        }

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}