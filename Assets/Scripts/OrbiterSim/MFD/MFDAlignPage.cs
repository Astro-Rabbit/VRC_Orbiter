
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
        if (tgt.conic == null) {
            hasTarget = false;
            return;
        }
        hasTarget = true;


        double sx, sy, sz;
        src.Normal(out sx, out sy, out sz);
        double tx, ty, tz;
        tgt.Normal(out tx, out ty, out tz);

        double dot = sx*tx + sy*ty + sz*tz;
        inclination = Math.Acos(dot);

        double x, y, z;
        src.EclipticToPerifocal(tx, ty, tz, out x, out y, out z);

        double mag = Math.Sqrt(x*x + y*y);
        dx = y / mag;
        dy = -x / mag;

        ascM = src.GetMeanAnomaly(Math.Atan2(dy, dx)) / (2*Math.PI);
        descM = src.GetMeanAnomaly(Math.Atan2(-dy, -dx)) / (2*Math.PI);

        double mu = bodies.GetMu(src.conic.primaryBodyId);
        double a = src.a;
        period = 2.0 * Math.PI * Math.Sqrt(a * a * a / mu);

        double p = src.pe * (1.0 - src.e);
        double h = Math.Sqrt(mu * p);
        double anR = p / (1.0 + src.e * dx);
        double anV = h / anR;
        double dnR = p / (1.0 + src.e * -dx);
        double dnV = h / dnR;

        double burnNormal = dx*y - dy*x;
        double burnBack = 1.0 - z;
        double burnX, burnY, burnZ;
        src.PerifocalToEcliptic(burnBack * dy, -burnBack * dx, -burnNormal, out burnX, out burnY, out burnZ);
        anBurn = (float)anV * new Vector3((float)burnX, (float)burnY, (float)burnZ);
        dnBurn = (float)-dnV * new Vector3((float)burnX, (float)burnY, (float)burnZ);

        double m = (clock.simTime - src.conic.epochT0)/period + src.conic.M0Rad/(2*Math.PI);
        m %= 1;

        ascTime = period * ((ascM - m + 1) % 1);
        descTime = period * ((descM - m + 1) % 1);

        // Not sure if this is necessary, but just to be sure burn uploading is correctly time synced
        currentTime = clock.simTime;

        double rx, ry, rz, _;
        bodies.GetCraftToBodyVector(src.conic.primaryBodyId, craft, out rx, out ry, out rz);
        src.EclipticToPerifocal(rx, ry, rz, out px, out py, out _);
        double pmag = Math.Sqrt(px*px + py*py);
        px /= pmag;
        py /= pmag;
    }

    public void UploadBurn(bool ascending)
    {
        double burnTime = (ascending ? ascTime : descTime) + currentTime;
        gc.API_Node_CreateAtTime(ascending ? anBurn : dnBurn, burnTime);

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
        display.DrawText(MFD.FormatAngle("Inc", inclination), 2, 4, Color.green);
        display.DrawText(MFD.FormatNumber("ANT", ascTime), 2, 19, Color.green);
        display.DrawText(MFD.FormatNumber("DNT", descTime), 2, 34, Color.green);

        display.DrawText("PBAN", 0, 12, Color.white);
        display.DrawText("PBDN ", 0, 32, Color.white);
        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}
