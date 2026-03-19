
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
    public OrbitAnalyzer src;
    public OrbitAnalyzer tgt;

    [Header("Display Data")]
    bool hasTarget = false;
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

    void Update()
    {
        if (tgt.conic == null) {
            hasTarget = false;
            return;
        }
        hasTarget = true;

        double _;
        if (src.conic.epochT0 != srcLastEpochT0 || tgt.conic.epochT0 != tgtLastEpochT0) {
            srcLastEpochT0 = src.conic.epochT0;
            tgtLastEpochT0 = tgt.conic.epochT0;

            double sx, sy, sz;
            src.Normal(out sx, out sy, out sz);
            double tx, ty, tz;
            tgt.Normal(out tx, out ty, out tz);

            double dot = sx*tx + sy*ty + sz*tz;
            inclination = Math.Acos(dot);

            double x, y;
            src.EclipticToPerifocal(tx, ty, tz, out x, out y, out _);
            double mag = Math.Sqrt(x*x + y*y);

            dx = x / mag;
            dy = y / mag;

            ascM = src.GetMeanAnomaly(Math.Atan2(dy, dx)) / (2*Math.PI);
            descM = src.GetMeanAnomaly(Math.Atan2(-dy, -dx)) / (2*Math.PI);
            double a = src.a;
            period = 2.0 * Math.PI * Math.Sqrt(a * a * a / bodies.GetMu(src.conic.primaryBodyId));
        }

        double m = (clock.simTime - src.conic.epochT0)/period + src.conic.M0Rad/(2*Math.PI);
        m %= 1;

        ascTime = period * ((ascM - m + 1) % 1);
        descTime = period * ((descM - m + 1) % 1);

        double rx, ry, rz;
        bodies.GetCraftToBodyVector(src.conic.primaryBodyId, craft, out rx, out ry, out rz);
        src.EclipticToPerifocal(rx, ry, rz, out px, out py, out _);
        double pmag = Math.Sqrt(px*px + py*py);
        px /= pmag;
        py /= pmag;
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
            display.DrawText(msg, 10, 24 - msg.Length/2, Color.green);

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
        display.DrawText(MFD.FormatNumber("ANT", ascTime), 2, 19, Color.green);
        display.DrawText(MFD.FormatNumber("DNT", descTime), 2, 34, Color.green);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}
