using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDAlignPage : MFDPage
{
    [Header("References")]
    public GuidanceNavCoreState nav;
    public OrbitAnalyzer tgt;

    [Header("Display Data")]
    public bool hasTarget = false;
    public double ascTime;
    public double descTime;
    public double inclination;
    public double dx;
    public double dy;
    public double px;
    public double py;

    // Source perifocal-like basis in E
    private Vector3 srcP_E = Vector3.right;
    private Vector3 srcQ_E = Vector3.up;
    private Vector3 srcN_E = Vector3.forward;

    private const double E_TOL = 1e-6;
    private const double N_TOL = 1e-9;
    private const double H_TOL = 1e-9;

    void Update()
    {
        if (nav == null || !nav.valid || tgt == null || tgt.conic == null) {
            hasTarget = false;
            return;
        }

        // This page is really only meaningful for a closed source orbit.
        if (!(nav.e < 1.0) || !(nav.a > 0.0) || !(nav.muPrimary > 0.0)) {
            hasTarget = false;
            return;
        }

        hasTarget = true;

        if (!BuildSourceBasisFromNav()) {
            hasTarget = false;
            return;
        }

        double tx, ty, tz;
        tgt.Normal(out tx, out ty, out tz);

        Vector3 tgtN_E = new Vector3((float)tx, (float)ty, (float)tz);
        float tgtNmag = tgtN_E.magnitude;
        if (tgtNmag <= 1e-9f) {
            hasTarget = false;
            return;
        }
        tgtN_E /= tgtNmag;

        // Relative inclination
        double dot = Clamp(Vector3.Dot(srcN_E, tgtN_E), -1.0, 1.0);
        inclination = Math.Acos(dot);

        // If planes are nearly identical or anti-parallel, node line is ill-defined.
        Vector3 nodeLine_E = Vector3.Cross(srcN_E, tgtN_E);
        float nodeMag = nodeLine_E.magnitude;
        if (nodeMag <= 1e-9f) {
            dx = 1.0;
            dy = 0.0;
            ascTime = 0.0;
            descTime = 0.0;

            // Current craft marker in source plane
            ProjectCraftIntoSourcePlane();
            return;
        }
        nodeLine_E /= nodeMag;

        // Pick ascending node of source orbit relative to target plane.
        Vector3 ascNodeHat_E = ChooseAscendingNode(nodeLine_E, tgtN_E);
        Vector3 descNodeHat_E = -ascNodeHat_E;

        // Project ascending node into source plane coordinates for drawing.
        double ascX = Vector3.Dot(ascNodeHat_E, srcP_E);
        double ascY = Vector3.Dot(ascNodeHat_E, srcQ_E);
        double ascMag = Math.Sqrt(ascX * ascX + ascY * ascY);
        if (ascMag > 1e-12) {
            dx = ascX / ascMag;
            dy = ascY / ascMag;
        } else {
            dx = 1.0;
            dy = 0.0;
        }

        // Current craft position in source plane.
        ProjectCraftIntoSourcePlane();

        // True anomalies of node directions in source basis.
        double ascNu = Wrap2Pi(Math.Atan2(dy, dx));
        double descNu = Wrap2Pi(ascNu + Math.PI);

        // Use live nav true anomaly and OrbitHelpers for timing.
        bool ascOk = OrbitHelpers.TryTimeToTrueAnomaly(
            nav.a,
            nav.e,
            nav.muPrimary,
            nav.nuRad,
            ascNu,
            E_TOL,
            out ascTime
        );

        bool descOk = OrbitHelpers.TryTimeToTrueAnomaly(
            nav.a,
            nav.e,
            nav.muPrimary,
            nav.nuRad,
            descNu,
            E_TOL,
            out descTime
        );

        if (!ascOk) ascTime = 0.0;
        if (!descOk) descTime = 0.0;
    }

    private bool BuildSourceBasisFromNav()
    {
        Vector3 h = nav.h_E;
        float hMag = h.magnitude;
        if (hMag <= 1e-9f) return false;

        srcN_E = h / hMag;

        Vector3 pCand = nav.eVec_E;
        if (pCand.magnitude <= 1e-6f) {
            pCand = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);
        }

        float pMag = pCand.magnitude;
        if (pMag <= 1e-9f) return false;

        srcP_E = pCand / pMag;

        Vector3 q = Vector3.Cross(srcN_E, srcP_E);
        float qMag = q.magnitude;
        if (qMag <= 1e-9f) return false;
        srcQ_E = q / qMag;

        // Re-orthogonalize P in case circular fallback used a slightly noisy radial vector.
        srcP_E = Vector3.Cross(srcQ_E, srcN_E).normalized;

        return true;
    }

    private Vector3 ChooseAscendingNode(Vector3 nodeLine_E, Vector3 tgtN_E)
    {
        Vector3 r1 = nodeLine_E.normalized;
        Vector3 t1 = Vector3.Cross(srcN_E, r1).normalized;

        // Ascending relative to target plane means local source velocity has positive component
        // toward +target normal.
        if (Vector3.Dot(t1, tgtN_E) >= 0f) {
            return r1;
        }

        return -r1;
    }

    private void ProjectCraftIntoSourcePlane()
    {
        Vector3 r_E = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);

        double x = Vector3.Dot(r_E, srcP_E);
        double y = Vector3.Dot(r_E, srcQ_E);

        double mag = Math.Sqrt(x * x + y * y);
        if (mag > 1e-12) {
            px = x / mag;
            py = y / mag;
        } else {
            px = 1.0;
            py = 0.0;
        }
    }

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
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
        display.DrawLine(Vector2.zero, orbitSize * new Vector2(-(float)dy, (float)dx), Color.white * 0.2f);
        display.DrawLine(Vector2.zero, orbitSize * new Vector2((float)py, -(float)px), Color.green);
        display.DrawConic(Vector2.zero, orbitSize, 0f, 0f, Color.green);

        display.ClearText();
        display.DrawText(MFD.FormatAngle("Inc", inclination), 2, 4, Color.green);
        display.DrawText(MFD.FormatNumber("ANT", ascTime), 2, 19, Color.green);
        display.DrawText(MFD.FormatNumber("DNT", descTime), 2, 34, Color.green);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }

    private static double Wrap2Pi(double a)
    {
        double twoPi = 2.0 * Math.PI;
        a %= twoPi;
        if (a < 0.0) a += twoPi;
        return a;
    }
}