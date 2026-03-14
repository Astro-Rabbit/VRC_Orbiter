using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MFDTransferPage : MFDPage
{
    [Header("References")]
    public CraftStateModel craft;
    public BodyCatalog bodies;
    public SimClock clock;
    public OrbitAnalyzer src;
    public OrbitAnalyzer tgt;
    public OrbitAnalyzer tfr;

    // for calculating transfer orbit parameters
    public TransferConicFitter fitter;

    // for calculating position and velocity at burn time
    public ConicPropagator propagator;

    [Header("Display Data")]
    public double tgtPx;
    public double tgtPy;
    public double tfrPx;
    public double tfrPy;
    public double tgtArgpDiff;
    public double tfrArgpDiff;
    public double burnX;
    public double burnY;
    public double srcX;
    public double srcY;
    public bool meets = false;
    public double meetTime1;
    public double meetX1;
    public double meetY1;
    public double meetActualX1;
    public double meetActualY1;
    public double meetDist1;
    public double meetOop1;
    public double meetTime2;
    public double meetX2;
    public double meetY2;
    public double meetActualX2;
    public double meetActualY2;
    public double meetDist2;
    public double meetOop2;

    [Header("State")]
    [UdonSynced] public int stepSize = DEFAULT_STEP_SIZE;
    [UdonSynced] public double burnTime;
    [UdonSynced] public double burnDv;

    private double calcBurnTime;
    private double stepRatio;
    private double srcLastEpochT0 = Double.NegativeInfinity;
    private double tgtLastEpochT0 = Double.NegativeInfinity;
    private bool burnChanged = true;

    private const int MAX_STEP_SIZE = 0;
    private const int MIN_STEP_SIZE = -10;
    private const int DEFAULT_STEP_SIZE = -5;

    void Start()
    {
        OnStepSizeChange();
    }

    void Update()
    {
        bool conicsUpdated = false;
        bool transferUpdated = false;

        double x, y, z, _;
        bodies.GetCraftToBodyVector(src.conic.primaryBodyId, craft, out x, out y, out z);
        src.EclipticToPerifocal(x, y, z, out srcX, out srcY, out _);

        if (srcLastEpochT0 != src.conic.epochT0 || tgtLastEpochT0 != tgt.conic.epochT0 || burnChanged) {
            srcLastEpochT0 = src.conic.epochT0;
            tgtLastEpochT0 = tgt.conic.epochT0;

            src.GetAligned(tgt, out tgtPx, out tgtPy);
            tgtArgpDiff = Math.Atan2(tgtPy, tgtPx);

            conicsUpdated = true;
        }

        calcBurnTime = Math.Max(burnTime, clock.simTime);
        if (burnChanged || (calcBurnTime != burnTime && burnDv > 0)) {
            burnChanged = false;

            propagator.conic = src.conic;
            propagator.Evaluate(calcBurnTime);

            src.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out burnX, out burnY, out _);

            // prograde direction unit vector
            double dx = propagator.rel_vx;
            double dy = propagator.rel_vy;
            double dz = propagator.rel_vz;

            double mag = Math.Sqrt(dx*dx + dy*dy + dz*dz);
            if (mag == 0) {
                // Shouldn't normally happen, but avoid blowing up in case it does
                dx = 1;
                dy = 0;
                dz = 0;
            } else {
                dx /= mag;
                dy /= mag;
                dz /= mag;
            }

            fitter.rx = propagator.rel_rx;
            fitter.ry = propagator.rel_ry;
            fitter.rz = propagator.rel_rz;
            fitter.vx = propagator.rel_vx + dx*burnDv;
            fitter.vy = propagator.rel_vy + dy*burnDv;
            fitter.vz = propagator.rel_vz + dz*burnDv;

            fitter.Fit(src.conic.primaryBodyId, calcBurnTime);
            tfr.UpdateInfo();

            src.GetAligned(tfr, out tfrPx, out tfrPy);
            tfrArgpDiff = Math.Atan2(tfrPy, tfrPx);

            transferUpdated = true;
        }

        if ((transferUpdated || conicsUpdated) && tfr.e < 1 && tgt.e < 1) {
            CalculateIntersections();
        }
    }

    public void CalculateIntersections()
    {
        double p1 = tfr.pe * (1 + tfr.e);
        double p2 = tgt.pe * (1 + tgt.e);

        double pdiff = p1 - p2;
        double b1 = tfr.e * p2;
        double b2 = tgt.e * p1;

        double argpDiff = tgtArgpDiff - tfrArgpDiff;
        double xDiff = b2*Math.Cos(argpDiff) - b1;
        double yDiff = b2*Math.Sin(argpDiff);

        double mag = Math.Sqrt(xDiff*xDiff + yDiff*yDiff);
        double phi = Math.Atan2(xDiff, yDiff);

        double ratio = pdiff / mag;
        if (ratio > 1 || ratio < -1) {
            meets = false;
            return;
        }
        meets = true;

        double thetaDiff = Math.Asin(ratio);
        double theta1 = phi + thetaDiff;
        double theta2 = phi + Math.PI - thetaDiff;

        double r1 = p1 / (1 + tfr.e*Math.Cos(theta1));
        double r2 = p1 / (1 + tfr.e*Math.Cos(theta2));

        meetX1 = r1 * Math.Cos(theta1 + tfrArgpDiff);
        meetY1 = r1 * Math.Sin(theta1 + tfrArgpDiff);
        meetX2 = r2 * Math.Cos(theta2 + tfrArgpDiff);
        meetY2 = r2 * Math.Sin(theta2 + tfrArgpDiff);

        meetTime1 = tfr.GetTime(theta1);
        meetTime2 = tfr.GetTime(theta2);

        double x, y, z;

        propagator.conic = tgt.conic;
        propagator.Evaluate(meetTime1);
        src.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        x -= meetX1;
        y -= meetY1;
        meetDist1 = Math.Sqrt(x*x + y*y + z*z);

        tgt.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        meetActualX1 = x*tgtPx - y*tgtPy;
        meetActualY1 = y*tgtPx + x*tgtPy;

        propagator.Evaluate(meetTime2);
        src.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        x -= meetX2;
        y -= meetY2;
        meetDist2 = Math.Sqrt(x*x + y*y + z*z);

        tgt.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        meetActualX2 = x*tgtPx - y*tgtPy;
        meetActualY2 = y*tgtPx + x*tgtPy;
    }

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom) {
            switch (num) {
                case 0:
                SetStepSize(Math.Max(MIN_STEP_SIZE, stepSize - 1));
                break;
                case 1:
                SetStepSize(Math.Min(MAX_STEP_SIZE, stepSize + 1));
                break;
                case 2:
                display.SetPage((byte)MFDPageID.Menu);
                break;
                case 3:
                break;
                case 4:
                break;
            }
        } else if (side == ButtonSide.Top) {
            double baseSpeed;
            switch (num) {
                case 0:
                SetBurnTime(calcBurnTime - stepRatio*src.t);
                break;
                case 1:
                SetBurnTime(calcBurnTime + stepRatio*src.t);
                break;
                case 2:
                ResetState();
                break;
                case 3:
                baseSpeed = Math.Sqrt(bodies.GetMu(src.conic.primaryBodyId) / (4 * src.a));
                SetBurnDV(burnDv - stepRatio*baseSpeed);
                break;
                case 4:
                baseSpeed = Math.Sqrt(bodies.GetMu(src.conic.primaryBodyId) / (4 * src.a));
                SetBurnDV(burnDv + stepRatio*baseSpeed);
                break;
            }
        }
    }

    [NetworkCallable]
    public void ResetState()
    {
        if (Networking.IsOwner(gameObject)) {
            stepSize = DEFAULT_STEP_SIZE;
            burnTime = clock.simTime;
            burnDv = 0;
            RequestSerialization();
            OnStepSizeChange();
            burnChanged = true;
        } else {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "ResetState");
        }
    }

    [NetworkCallable]
    public void SetStepSize(int newStepSize)
    {
        if (Networking.IsOwner(gameObject)) {
            stepSize = newStepSize;
            RequestSerialization();
            OnStepSizeChange();
        } else {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "SetStepSize", newStepSize);
        }
    }

    private void OnStepSizeChange()
    {
        double ratio = Math.Pow(10, stepSize / 3);

        int type = -stepSize % 3;
        if (type == 1) {
            ratio /= 2;
        } else if (type == 2) {
            ratio /= 5;
        }

        stepRatio = ratio;
    }

    [NetworkCallable]
    public void SetBurnTime(double newBurnTime)
    {
        if (Networking.IsOwner(gameObject)) {
            burnTime = newBurnTime;
            RequestSerialization();
            burnChanged = true;
        } else {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "SetBurnTime", newBurnTime);
        }
    }

    [NetworkCallable]
    public void SetBurnDV(double newBurnDv)
    {
        if (Networking.IsOwner(gameObject)) {
            burnDv = newBurnDv;
            RequestSerialization();
            burnChanged = true;
        } else {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "SetBurnDv", newBurnDv);
        }
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearGraphics();
        const float orbitSize = 0.4f;
        float scale = orbitSize / (float)src.a;

        Vector2 tfrPePos = scale * (float)tfr.pe * new Vector2((float)tfrPy, -(float)tfrPx);
        display.DrawConic(tfrPePos, (float)tfr.pe * scale, (float)tfrArgpDiff, (float)tfr.e, Color.gray);

        Vector2 pePos = new Vector2(0f, -scale * (float)src.pe);
        display.DrawConic(pePos, (float)src.pe * scale, 0f, (float)src.e, Color.green);

        Vector2 tgtPePos = scale * (float)tgt.pe * new Vector2((float)tgtPy, -(float)tgtPx);
        display.DrawConic(tgtPePos, (float)tgt.pe * scale, (float)tgtArgpDiff, (float)tgt.e, Color.yellow);

        display.DrawLine(Vector2.zero, scale * new Vector2((float)burnY, -(float)burnX), Color.gray);
        display.DrawLine(Vector2.zero, scale * new Vector2((float)srcY, -(float)srcX), Color.green);
        if (meets) {
            display.DrawLine(Vector2.zero, scale * new Vector2((float)meetY1, -(float)meetX1), Color.blue);
            display.DrawLine(Vector2.zero, scale * new Vector2((float)meetY2, -(float)meetX2), Color.red);
            display.DrawLine(Vector2.zero, scale * new Vector2((float)meetActualY1, -(float)meetActualX1), Color.blue * 0.3f);
            display.DrawLine(Vector2.zero, scale * new Vector2((float)meetActualY2, -(float)meetActualX2), Color.red * 0.15f);
        }

        display.ClearText();

        double now = clock.simTime;
        display.DrawText(MFD.FormatPercent("STP", stepRatio), 2, 19, Color.green);
        display.DrawText(MFD.FormatNumber("BT", calcBurnTime - now), 3, 19, Color.green);
        display.DrawText(MFD.FormatNumber("BDV", burnDv), 4, 19, Color.green);

        if (meets) {
            display.DrawText(MFD.FormatNumber("T", meetTime1 - now), 2, 4, Color.red);
            display.DrawText(MFD.FormatNumber("DST", burnDv), 3, 4, Color.red);
            //display.DrawText(MFD.FormatNumber("OOP", burnDv), 4, 4, Color.red);

            display.DrawText(MFD.FormatNumber("T", meetTime1 - now), 2, 34, Color.blue);
            display.DrawText(MFD.FormatNumber("DST", burnDv), 3, 34, Color.blue);
            //display.DrawText(MFD.FormatNumber("OOP", burnDv), 4, 34, Color.blue);
        }

        display.DrawText(" T- ", 0, 2, Color.white);
        display.DrawText(" T+ ", 0, 12, Color.white);
        display.DrawText("RSET", 0, 22, Color.white);
        display.DrawText(" V- ", 0, 32, Color.white);
        display.DrawText(" V+ ", 0, 42, Color.white);

        display.DrawText("STP-", MFD.TEXT_ROWS - 1, 2, Color.white);
        display.DrawText("STP+", MFD.TEXT_ROWS - 1, 12, Color.white);
        //display.DrawText("CALC", MFD.TEXT_ROWS - 1, 32, Color.white);
        //display.DrawText("PLAN", MFD.TEXT_ROWS - 1, 42, Color.white);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    public override void OnDeserialization()
    {
        OnStepSizeChange();
        burnChanged = true;
    }
}