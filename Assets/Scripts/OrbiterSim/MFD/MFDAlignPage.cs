using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDAlignPage : MFDPage
{
    [Header("References")]
    public BodyCatalog bodies;
    public SimClock clock;
    public CraftStateModel craft;
    public GuidanceNavCoreState nav;
    public OrbitAnalyzer tgt;
    public GC_Core gc;
    public RendezvousTutorial tutorial;

    [Header("Display Data")]
    public bool hasTarget = false;
    public double ascTime;
    public double descTime;
    public double currentTime;
    public double inclination;
    public double dx;
    public double dy;
    public double px;
    public double py;
    public Vector3 anBurn;
    public Vector3 dnBurn;

    private double ascM;
    private double descM;
    private double period;

    void Update()
    {
        if (nav == null || !nav.valid || tgt == null || tgt.conic == null) {
            hasTarget = false;
            return;
        }
        hasTarget = true;

        // -----------------------------------------------------------------
        // Source orbit geometry from nav
        // -----------------------------------------------------------------
        Vector3 sW = nav.h_E.normalized; // source orbit normal
        Vector3 sP;

        if (nav.eVec_E.sqrMagnitude > 1e-12f) {
            sP = nav.eVec_E.normalized;  // periapsis direction
        } else {
            // circular fallback: use current radius direction as periapsis-like X axis
            Vector3 rNow = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);
            if (rNow.sqrMagnitude > 1e-12f) sP = rNow.normalized;
            else sP = Vector3.right;
        }

        Vector3 sQ = Vector3.Cross(sW, sP).normalized;

        // -----------------------------------------------------------------
        // Target orbit normal (still from target analyzer)
        // -----------------------------------------------------------------
        double tx, ty, tz;
        tgt.Normal(out tx, out ty, out tz);
        Vector3 tN = new Vector3((float)tx, (float)ty, (float)tz).normalized;

        // inclination between orbit normals
        double dot = Vector3.Dot(sW, tN);
        dot = Math.Max(-1.0, Math.Min(1.0, dot));
        inclination = Math.Acos(dot);

        // -----------------------------------------------------------------
        // Port of old:
        // src.EclipticToPerifocal(targetNormal)
        // -----------------------------------------------------------------
        double x = Vector3.Dot(tN, sP);
        double y = Vector3.Dot(tN, sQ);
        double z = Vector3.Dot(tN, sW);

        double mag = Math.Sqrt(x * x + y * y);
        if (mag > 1e-12) {
            dx = y / mag;
            dy = -x / mag;
        } else {
            dx = 1.0;
            dy = 0.0;
        }

        double ascTheta = Math.Atan2(dy, dx);
        double descTheta = Math.Atan2(-dy, -dx);

        ascM = GetMeanAnomalyFromTheta(ascTheta) / (2.0 * Math.PI);
        descM = GetMeanAnomalyFromTheta(descTheta) / (2.0 * Math.PI);

        // -----------------------------------------------------------------
        // Source orbit scalars from nav
        // -----------------------------------------------------------------
        double mu = nav.muPrimary;
        double a = nav.a;
        double e = nav.e;
        double p = nav.p;      // semi-latus rectum directly from nav
        double h = nav.hMag;   // |h| directly from nav

        if (e < 1.0 && a > 0.0 && mu > 0.0) {
            period = 2.0 * Math.PI * Math.Sqrt(a * a * a / mu);
        } else {
            period = 0.0;
        }

        // node radii / speeds
        double anR = p / (1.0 + e * dx);
        double dnR = p / (1.0 - e * dx);

        double anV = (anR > 1e-12) ? (h / anR) : 0.0;
        double dnV = (dnR > 1e-12) ? (h / dnR) : 0.0;

        // -----------------------------------------------------------------
        // Port of old burn construction:
        // burn built in source perifocal frame, then rotated back to inertial
        // -----------------------------------------------------------------
        double burnNormal = dx * y - dy * x;
        double burnBack = 1.0 - z;

        Vector3 burnDirE = PerifocalToEcliptic(
            (float)(burnBack * dy),
            (float)(-burnBack * dx),
            (float)(-burnNormal),
            sP, sQ, sW
        );

        anBurn = (float)anV * burnDirE;
        dnBurn = (float)-dnV * burnDirE;

        // -----------------------------------------------------------------
        // Current mean anomaly from current nav anomaly
        // -----------------------------------------------------------------
        double m = 0.0;
        if (e < 1.0) {
            m = (GetMeanAnomalyFromTheta(nav.nuRad) / (2.0 * Math.PI));
            m %= 1.0;
            if (m < 0.0) m += 1.0;
        }

        if (e < 1.0 && period > 0.0) {
            ascTime = period * ((ascM - m + 1.0) % 1.0);
            descTime = period * ((descM - m + 1.0) % 1.0);
        } else {
            ascTime = -1.0;
            descTime = -1.0;
        }

        // Use nav mission time directly
        currentTime = nav.t;

        // -----------------------------------------------------------------
        // Current craft position in source perifocal frame (for display)
        // -----------------------------------------------------------------
        Vector3 rCur = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);
        px = Vector3.Dot(rCur, sP);
        py = Vector3.Dot(rCur, sQ);

        double pmag = Math.Sqrt(px * px + py * py);
        if (pmag > 1e-12) {
            px /= pmag;
            py /= pmag;
        } else {
            px = 1.0;
            py = 0.0;
        }
    }

    private double GetMeanAnomalyFromTheta(double theta)
    {
        double e = nav.e;

        double s = Math.Sin(0.5 * theta);
        double c = Math.Cos(0.5 * theta);

        double a = Math.Sqrt(Math.Max(0.0, 1.0 - e)) * s;
        double b = Math.Sqrt(1.0 + e) * c;

        double E = 2.0 * Math.Atan2(a, b);
        return Wrap2Pi(E - e * Math.Sin(E));
    }

    private Vector3 PerifocalToEcliptic(float ix, float iy, float iz, Vector3 pHat, Vector3 qHat, Vector3 wHat)
    {
        // Preserve magnitude exactly like old OrbitAnalyzer.PerifocalToEcliptic
        return ix * pHat + iy * qHat + iz * wHat;
    }

    private static double Wrap2Pi(double a)
    {
        double twoPi = 2.0 * Math.PI;
        a %= twoPi;
        if (a < 0.0) a += twoPi;
        return a;
    }

    public void UploadBurn(bool ascending)
    {
        double burnTime = (ascending ? ascTime : descTime) + currentTime;
        gc.API_RequestCreateNode_Time(ascending ? anBurn : dnBurn, burnTime);

        // TODO: come up with a less ugly way of detecting this in the tutorial
        tutorial.OnAlignNodeCreate(burnTime);
    }

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
        } else if (side == ButtonSide.Top) {
            if (num == 1) {
                UploadBurn(true);
            } else if (num == 3) {
                UploadBurn(false);
            }
        }
    }

    public override void DrawDisplay(MFD display)
    {
        if (!hasTarget) {
            display.ClearGraphics();
            display.ClearText();

            string msg = "NO TARGET SELECTED";
            display.DrawText(msg, 10, 24 - msg.Length / 2, Color.green);

            display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
            return;
        }

        const float orbitSize = 0.7f;

        display.ClearGraphics();
        display.DrawLine(Vector2.zero, orbitSize * new Vector2((float)dy, -(float)dx), Color.white);
        display.DrawLine(Vector2.zero, orbitSize * new Vector2((float)(-dy), (float)(dx)), Color.white * 0.2f);
        display.DrawLine(Vector2.zero, orbitSize * new Vector2((float)py, -(float)px), Color.green);
        display.DrawConic(Vector2.zero, orbitSize, 0f, 0f, Color.green);

        display.ClearText();
        display.DrawText(MFD.FormatAngle("Inc", inclination), 2, 4, Color.green);
        display.DrawText(MFD.FormatNumber("ANT", ascTime), 2, 19, Color.green);
        display.DrawText(MFD.FormatNumber("DNT", descTime), 2, 34, Color.green);

        display.DrawText("PBAN", 0, 12, Color.white);
        display.DrawText("PBDN ", 0, 32, Color.white);
        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}