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
    public BodyCatalog bodies;
    public SimClock clock;
    public OrbitAnalyzer src;
    public OrbitAnalyzer tgt;
    public OrbitAnalyzer tfr;

    // for calculating transfer orbit parameters
    public TransferConicFitter fitter;

    // for calculating position and velocity at burn time
    public ConicPropagator transferPropagator;

    [Header("Display Data")]
    public double tgtPx;
    public double tgtPy;
    public double tfrPx;
    public double tfrPy;
    public double tgtArgpDiff;
    public double tfrArgpDiff;

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
        if (srcLastEpochT0 != src.conic.epochT0 || tgtLastEpochT0 != tgt.conic.epochT0 || burnChanged) {
            srcLastEpochT0 = src.conic.epochT0;
            tgtLastEpochT0 = tgt.conic.epochT0;

            src.GetAligned(tgt, out tgtPx, out tgtPy);
            tgtArgpDiff = Math.Atan2(tgtPy, tgtPx);
        }

        calcBurnTime = Math.Max(burnTime, clock.simTime);
        if (burnChanged || (calcBurnTime != burnTime && burnDv > 0)) {
            burnChanged = false;

            transferPropagator.conic = src.conic;
            transferPropagator.Evaluate(calcBurnTime);

            // prograde direction unit vector
            double dx = transferPropagator.rel_vx;
            double dy = transferPropagator.rel_vy;
            double dz = transferPropagator.rel_vz;

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

            fitter.rx = transferPropagator.rel_rx;
            fitter.ry = transferPropagator.rel_ry;
            fitter.rz = transferPropagator.rel_rz;
            fitter.vx = transferPropagator.rel_vx + dx*burnDv;
            fitter.vy = transferPropagator.rel_vy + dy*burnDv;
            fitter.vz = transferPropagator.rel_vz + dz*burnDv;

            fitter.Fit(src.conic.primaryBodyId, calcBurnTime);
            tfr.UpdateInfo();

            src.GetAligned(tfr, out tfrPx, out tfrPy);
            tfrArgpDiff = Math.Atan2(tfrPy, tfrPx);
        }
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
                SetBurnTime(burnTime - stepRatio*src.t);
                break;
                case 1:
                SetBurnTime(burnTime + stepRatio*src.t);
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
        const float orbitSize = 0.5f;

        display.ClearGraphics();
        float scale = orbitSize / (float)src.a;

        Vector2 tfrPePos = scale * (float)tfr.pe * new Vector2((float)tfrPy, -(float)tfrPx);
        display.DrawConic(tfrPePos, (float)tfr.pe * scale, (float)tfrArgpDiff, (float)tfr.e, Color.gray);

        Vector2 pePos = new Vector2(0f, -scale * (float)src.pe);
        display.DrawConic(pePos, (float)src.pe * scale, 0f, (float)src.e, Color.green);

        Vector2 tgtPePos = scale * (float)tgt.pe * new Vector2((float)tgtPy, -(float)tgtPx);
        display.DrawConic(tgtPePos, (float)tgt.pe * scale, (float)tgtArgpDiff, (float)tgt.e, Color.yellow);

        display.ClearText();

        display.DrawText(" T- ", 0, 2, Color.white);
        display.DrawText(" T+ ", 0, 12, Color.white);
        display.DrawText("RSET", 0, 22, Color.white);
        display.DrawText(" V- ", 0, 32, Color.white);
        display.DrawText(" V+ ", 0, 42, Color.white);

        display.DrawText("STP-", MFD.TEXT_ROWS - 1, 2, Color.white);
        display.DrawText("STP+", MFD.TEXT_ROWS - 1, 12, Color.white);
        display.DrawText("CALC", MFD.TEXT_ROWS - 1, 32, Color.white);
        display.DrawText("PLAN", MFD.TEXT_ROWS - 1, 42, Color.white);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    public override void OnDeserialization()
    {
        OnStepSizeChange();
        burnChanged = true;
    }
}