
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
    public ConicPropagator conicPropagator;
    public ConicState srcConic;
    public ConicState tgtConic;

    [Header("Display Data")]
    double ascTime;
    double descTime;
    double inclination;
    double dx;
    double dy;
    double px;
    double py;


    private double srcLastEpochT0 = Double.NegativeInfinity;
    private double tgtLastEpochT0 = Double.NegativeInfinity;

    private double ascM;
    private double descM;
    private double period;

    private string[] suffixes =  new[] {"", "k", "M", "G"};

    void Start()
    {
    }

    void Update()
    {
        if (srcConic.epochT0 != srcLastEpochT0 || tgtConic.epochT0 != tgtLastEpochT0) {
            srcLastEpochT0 = srcConic.epochT0;
            tgtLastEpochT0 = tgtConic.epochT0;

            double sx, sy, sz;
            OrbitNormal(srcConic.raanRad, srcConic.iRad, out sx, out sy, out sz);
            double tx, ty, tz;
            OrbitNormal(tgtConic.raanRad, tgtConic.iRad, out tx, out ty, out tz);

            double dot = sx*tx + sy*ty + sz*tz;
            inclination = Math.Acos(dot);

            double x, y;
            ProjectDir(srcConic.raanRad, srcConic.iRad, srcConic.argpRad, tx, ty, tz, out x, out y);
            double mag = Math.Sqrt(x*x + y*y);

            dx = -y / mag;
            dy = x / mag;

            ascM = MeanAnomaly(dx, dy, srcConic.e) / Math.PI;
            descM = MeanAnomaly(-dx, -dy, srcConic.e) / Math.PI;
            double a = srcConic.aMeters;
            period = 2.0 * Math.PI * Math.Sqrt(a * a * a / bodies.GetMu(srcConic.primaryBodyId));
        }

        double m = (clock.simTime - srcConic.epochT0) / period;

        ascTime = period * ((m - ascM + 1) % 1);
        descTime = period * ((m - descM + 1) % 1);

        double rx = conicPropagator.rel_rx;
        double ry = conicPropagator.rel_ry;
        double rz = conicPropagator.rel_rz;
        ProjectDir(srcConic.raanRad, srcConic.iRad, srcConic.argpRad, rx, ry, rz, out px, out py);
        double pmag = Math.Sqrt(px*px + py*py);
        px /= pmag;
        py /= pmag;
    }

    private static void OrbitNormal(double raan, double i, out double x, out double y, out double z)
    {
        double cr = Math.Cos(raan);
        double sr = Math.Sin(raan);
        double ci = Math.Cos(i);
        double si = Math.Sin(i);

        x = sr * si;
        y = -cr * si;
        z = ci;
    }

    private static void ProjectDir(double raan, double i, double argp, double ix, double iy, double iz, out double ox, out double oy)
    {
        double cr = Math.Cos(raan);
        double sr = Math.Sin(raan);
        double ci = Math.Cos(i);
        double si = Math.Sin(i);
        double ca = Math.Cos(argp);
        double sa = Math.Cos(argp);

        double m00 =  cr * ca - sr * sa * ci;
        double m01 = -cr * sa - sr * ca * ci;

        double m10 =  sr * ca + cr * sa * ci;
        double m11 = -sr * sa + cr * ca * ci;

        double m20 =  sa * si;
        double m21 =  ca * si;

        ox = m00 * ix + m10 * iy + m20 * iz;
        oy = m01 * ix + m11 * iy + m21 * iz;
    }

    private static double MeanAnomaly(double c, double s, double e)
    {
        double num = Math.Sqrt(1 - e*e) * s;
        double eccAnom = Math.Atan2(num, e + c);

        return eccAnom + e * num / (1 + e * c);
    }

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
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
        return title.PadRight(4) + (180.0 / Math.PI * angle).ToString("0.0").PadLeft(5) + "°";
    }

    public override void DrawDisplay(MFD display)
    {
        const float orbitSize = 0.7f;

        display.ClearGraphics();
        display.DrawLine(Vector2.zero, orbitSize * new Vector2((float)dx, (float)dy), Color.green);
        display.DrawLine(Vector2.zero, orbitSize * new Vector2((float)-dx, (float)-dy), Color.yellow);
        display.DrawLine(Vector2.zero, orbitSize * new Vector2((float)px, (float)py), Color.gray);
        display.DrawConic(Vector2.zero, orbitSize, 0f, 0f, Color.green);

        display.ClearText();
        display.DrawText(FormatAngle("Inc", inclination), 2, 4, Color.green);
        display.DrawText(FormatNumber("ANT", ascTime), 2, 19, Color.green);
        display.DrawText(FormatNumber("DNT", descTime), 2, 34, Color.green);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}
