using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MFDTransferPage : MFDPage
{
    [Header("References")]
    public CraftStateModel craft;
    public BodyCatalog bodies;
    public SimClock clock;
    public GC_Core gc;
    public GuidanceNavCoreState nav;
    public OrbitAnalyzer src;
    public OrbitAnalyzer tgt;
    public OrbitAnalyzer tfr;
    public TransferSolver solver;

    // for calculating transfer orbit parameters
    public TransferConicFitter fitter;

    // for calculating position and velocity at burn time
    public ConicPropagator propagator;

    public RendezvousTutorial tutorial;

    [Header("Display Data")]
    public bool hasTarget = false;
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
    public double meetTime2;
    public double meetX2;
    public double meetY2;
    public double meetActualX2;
    public double meetActualY2;
    public double meetDist2;

    [Header("State")]
    [UdonSynced] public int stepSize = DEFAULT_STEP_SIZE;
    [UdonSynced] public double burnTime;
    [UdonSynced] public double burnDv;

    private double calcBurnTime;
    private double stepRatio;

    private const int MAX_STEP_SIZE = 0;
    private const int MIN_STEP_SIZE = -10;
    private const int DEFAULT_STEP_SIZE = -5;

    void Start()
    {
        OnStepSizeChange();
    }

    void Update()
    {
        if (nav == null || !nav.valid || src == null || src.conic == null || tgt == null || tgt.conic == null) {
            hasTarget = false;
            meets = false;

            if (solver != null && solver.solverBusy) {
                solver.AbortAutoSolve(TransferSolver.AUTO_NONE);
            }

            return;
        }
        hasTarget = true;

        Vector3 sP, sQ, sW;
        BuildSourcePerifocalBasis(out sP, out sQ, out sW);

        double tmpZ;

        // current source position from nav
        EclipticToSourcePerifocal(
            nav.r_x, nav.r_y, nav.r_z,
            sP, sQ, sW,
            out srcX, out srcY, out tmpZ
        );

        // target alignment in source frame
        GetAlignedToSource(tgt, sP, sQ, out tgtPx, out tgtPy);
        tgtArgpDiff = Math.Atan2(tgtPy, tgtPx);

        calcBurnTime = Math.Max(burnTime, clock.simTime);

        // preserve original propagation path
        propagator.conic = src.conic;
        propagator.Evaluate(calcBurnTime);

        EclipticToSourcePerifocal(
            propagator.rel_rx, propagator.rel_ry, propagator.rel_rz,
            sP, sQ, sW,
            out burnX, out burnY, out tmpZ
        );

        double dx = propagator.rel_vx;
        double dy = propagator.rel_vy;
        double dz = propagator.rel_vz;

        double mag = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (mag == 0.0) {
            dx = 0.0;
            dy = 0.0;
            dz = 0.0;
        } else {
            dx /= mag;
            dy /= mag;
            dz /= mag;
        }

        fitter.rx = propagator.rel_rx;
        fitter.ry = propagator.rel_ry;
        fitter.rz = propagator.rel_rz;
        fitter.vx = propagator.rel_vx + dx * burnDv;
        fitter.vy = propagator.rel_vy + dy * burnDv;
        fitter.vz = propagator.rel_vz + dz * burnDv;

        fitter.Fit(src.conic.primaryBodyId, calcBurnTime);
        tfr.UpdateInfo();

        // transfer alignment in source frame
        GetAlignedToSource(tfr, sP, sQ, out tfrPx, out tfrPy);
        tfrArgpDiff = Math.Atan2(tfrPy, tfrPx);

        if (tfr.e < 1.0 && tgt.e < 1.0) {
            CalculateIntersections(sP, sQ, sW);
        } else {
            meets = false;
        }
    }

    void FixedUpdate()
    {
        if (solver.solverBusy) {
            solver.StepAutoSolve();
        }
    }

    private void BuildSourcePerifocalBasis(out Vector3 pHat, out Vector3 qHat, out Vector3 wHat)
    {
        // Build the source perifocal basis from the inertial orbital elements in nav.
        // This matches the conic orientation the transfer page expects much better than
        // using a normalized eVec/current-r fallback basis.

        double O = nav.raanInertialRad;
        double I = nav.iInertialRad;
        double W = nav.argpInertialRad;

        double cO = Math.Cos(O);
        double sO = Math.Sin(O);
        double cI = Math.Cos(I);
        double sI = Math.Sin(I);
        double cW = Math.Cos(W);
        double sW = Math.Sin(W);

        // Standard PQW -> inertial basis
        pHat = new Vector3(
            (float)(cO * cW - sO * sW * cI),
            (float)(sO * cW + cO * sW * cI),
            (float)(sW * sI)
        );

        qHat = new Vector3(
            (float)(-cO * sW - sO * cW * cI),
            (float)(-sO * sW + cO * cW * cI),
            (float)(cW * sI)
        );

        wHat = new Vector3(
            (float)(sO * sI),
            (float)(-cO * sI),
            (float)(cI)
        );

        // keep the basis orthonormal in float land
        pHat.Normalize();
        qHat = Vector3.Cross(wHat, pHat).normalized;
        wHat = Vector3.Cross(pHat, qHat).normalized;

        // circular fallback: argp is not meaningful, so lock P to current radius in-plane
        if (nav.e < 1e-6) {
            Vector3 rNow = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);
            Vector3 rInPlane = rNow - Vector3.Dot(rNow, wHat) * wHat;

            if (rInPlane.sqrMagnitude > 1e-12f) {
                pHat = rInPlane.normalized;
                qHat = Vector3.Cross(wHat, pHat).normalized;
            }
        }
    }

    private void EclipticToSourcePerifocal(
        double ex, double ey, double ez,
        Vector3 pHat, Vector3 qHat, Vector3 wHat,
        out double px, out double py, out double pz
    )
    {
        Vector3 v = new Vector3((float)ex, (float)ey, (float)ez);
        px = Vector3.Dot(v, pHat);
        py = Vector3.Dot(v, qHat);
        pz = Vector3.Dot(v, wHat);
    }

    private void GetAlignedToSource(OrbitAnalyzer other, Vector3 sP, Vector3 sQ, out double ax, out double ay)
    {
        double nx, ny, nz;
        other.Normal(out nx, out ny, out nz);

        Vector3 n = new Vector3((float)nx, (float)ny, (float)nz).normalized;

        double x = Vector3.Dot(n, sP);
        double y = Vector3.Dot(n, sQ);

        double mag = Math.Sqrt(x * x + y * y);
        if (mag > 1e-12) {
            ax = y / mag;
            ay = -x / mag;
        } else {
            ax = 1.0;
            ay = 0.0;
        }
    }

    public void CalculateIntersections(Vector3 sP, Vector3 sQ, Vector3 sW)
    {
        double p1 = tfr.pe * (1.0 + tfr.e);
        double p2 = tgt.pe * (1.0 + tgt.e);

        double pdiff = p1 - p2;
        double b1 = tfr.e * p2;
        double b2 = tgt.e * p1;

        double argpDiff = tgtArgpDiff - tfrArgpDiff;
        double xDiff = b2 * Math.Cos(argpDiff) - b1;
        double yDiff = b2 * Math.Sin(argpDiff);

        double mag = Math.Sqrt(xDiff * xDiff + yDiff * yDiff);
        if (mag <= 1e-12) {
            meets = false;
            return;
        }

        double ratio = pdiff / mag;
        if (ratio > 1.0 || ratio < -1.0) {
            meets = false;
            return;
        }
        meets = true;

        double phi = Math.Atan2(xDiff, yDiff);

        double thetaDiff = Math.Asin(ratio);
        double theta1 = phi + thetaDiff;
        double theta2 = phi + Math.PI - thetaDiff;

        double r1 = p1 / (1.0 + tfr.e * Math.Cos(theta1));
        double r2 = p1 / (1.0 + tfr.e * Math.Cos(theta2));

        meetX1 = r1 * Math.Cos(theta1 + tfrArgpDiff);
        meetY1 = r1 * Math.Sin(theta1 + tfrArgpDiff);
        meetX2 = r2 * Math.Cos(theta2 + tfrArgpDiff);
        meetY2 = r2 * Math.Sin(theta2 + tfrArgpDiff);

        meetTime1 = tfr.GetTime(theta1);
        meetTime2 = tfr.GetTime(theta2);

        double x, y, z;

        propagator.conic = tgt.conic;
        propagator.Evaluate(meetTime1);
        EclipticToSourcePerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, sP, sQ, sW, out x, out y, out z);
        x -= meetX1;
        y -= meetY1;
        meetDist1 = Math.Sqrt(x * x + y * y + z * z);

        tgt.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        meetActualX1 = x * tgtPx - y * tgtPy;
        meetActualY1 = y * tgtPx + x * tgtPy;

        propagator.Evaluate(meetTime2);
        EclipticToSourcePerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, sP, sQ, sW, out x, out y, out z);
        x -= meetX2;
        y -= meetY2;
        meetDist2 = Math.Sqrt(x * x + y * y + z * z);

        tgt.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        meetActualX2 = x * tgtPx - y * tgtPy;
        meetActualY2 = y * tgtPx + x * tgtPy;
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
                    solver.BeginAutoSolve();
                    break;
                case 4:
                    UploadAutoNode();
                    break;
            }
        } else if (side == ButtonSide.Top) {
            double baseSpeed;
            switch (num) {
                case 0:
                    SetBurnTime(calcBurnTime - stepRatio * GetSourcePeriod());
                    break;
                case 1:
                    SetBurnTime(calcBurnTime + stepRatio * GetSourcePeriod());
                    break;
                case 2:
                    ResetState();
                    break;
                case 3:
                    baseSpeed = GetSourceBaseSpeed();
                    SetBurnDV(burnDv - stepRatio * baseSpeed);
                    break;
                case 4:
                    baseSpeed = GetSourceBaseSpeed();
                    SetBurnDV(burnDv + stepRatio * baseSpeed);
                    break;
            }
        }
    }

    private double GetSourcePeriod()
    {
        if (nav == null || !nav.valid) return 0.0;
        if (nav.e >= 1.0 || nav.a <= 0.0 || nav.muPrimary <= 0.0) return 0.0;

        return 2.0 * Math.PI * Math.Sqrt(nav.a * nav.a * nav.a / nav.muPrimary);
    }

    private double GetSourceBaseSpeed()
    {
        if (nav == null || !nav.valid) return 0.0;
        if (nav.a <= 0.0 || nav.muPrimary <= 0.0) return 0.0;

        return Math.Sqrt(nav.muPrimary / (4.0 * nav.a));
    }

    public void ResetState()
    {
        if (!Networking.IsOwner(gameObject)) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        stepSize = DEFAULT_STEP_SIZE;
        burnTime = clock.simTime;
        burnDv = 0.0;
        RequestSerialization();

        OnStepSizeChange();
    }

    public void SetStepSize(int newStepSize)
    {
        if (!Networking.IsOwner(gameObject)) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        stepSize = newStepSize;
        RequestSerialization();
        OnStepSizeChange();
    }

    private void OnStepSizeChange()
    {
        double ratio = Math.Pow(10.0, stepSize / 3);

        int type = -stepSize % 3;
        if (type == 1) {
            ratio /= 2.0;
        } else if (type == 2) {
            ratio /= 5.0;
        }

        stepRatio = ratio;
    }

    public void SetBurnTime(double newBurnTime)
    {
        if (!Networking.IsOwner(gameObject)) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        burnTime = newBurnTime;
        RequestSerialization();
    }

    public void SetBurnDV(double newBurnDv)
    {
        if (!Networking.IsOwner(gameObject)) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        burnDv = newBurnDv;
        RequestSerialization();
    }

    private void UploadAutoNode()
    {
        if (!solver.autoValid) return;
        if (!Networking.IsOwner(gc.gameObject)) {
            return;
        }

        int idx = gc.API_RequestCreateNode_Time(
            solver.autoDvE,
            solver.autoBurnTime
        );

        if (idx >= 0) {
            solver.autoStatus = TransferSolver.AUTO_PLAN;
        } else {
            solver.autoStatus = TransferSolver.AUTO_ERR;
        }

        tutorial.OnTransferNodeCreate(solver.autoBurnTime, solver.autoEncounterTime);
    }

    public override void DrawDisplay(MFD display)
    {
        if (!hasTarget) {
            display.ClearGraphics();
            display.ClearText();

            string msg = "NO TARGET SELECTED";
            display.DrawText(msg, 10, 24 - msg.Length / 2, Color.green);

            display.DrawText("STP-", MFD.TEXT_ROWS - 1, 2, Color.white);
            display.DrawText("STP+", MFD.TEXT_ROWS - 1, 12, Color.white);
            display.DrawText("AUTO", MFD.TEXT_ROWS - 1, 32, Color.white);
            display.DrawText("UPLD", MFD.TEXT_ROWS - 1, 42, Color.white);
            display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
            return;
        }

        display.ClearGraphics();
        const float orbitSize = 0.4f;

        float scale = 0.0f;
        if (Math.Abs((float)nav.a) > 1e-6f) {
            scale = orbitSize / (float)nav.a;
        }

        double srcPe = nav.p / (1.0 + nav.e);

        display.DrawConic(Vector2.zero, scale * (float)bodies.GetRadius(nav.primaryId), 0f, 0f, Color.white * 0.2f);
        display.DrawConic(Vector2.zero, (float)tfr.pe * scale, (float)tfrArgpDiff, (float)tfr.e, Color.gray);
        display.DrawConic(Vector2.zero, (float)srcPe * scale, 0f, (float)nav.e, Color.green);
        display.DrawConic(Vector2.zero, (float)tgt.pe * scale, (float)tgtArgpDiff, (float)tgt.e, Color.yellow);

        display.DrawLine(Vector2.zero, scale * new Vector2((float)burnY, -(float)burnX), Color.gray);
        display.DrawLine(Vector2.zero, scale * new Vector2((float)srcY, -(float)srcX), Color.green);

        if (meets) {
            display.DrawLine(Vector2.zero, scale * new Vector2((float)meetY1, -(float)meetX1), Color.cyan);
            display.DrawLine(Vector2.zero, scale * new Vector2((float)meetY2, -(float)meetX2), Color.red);
            display.DrawLine(Vector2.zero, scale * new Vector2((float)meetActualY1, -(float)meetActualX1), Color.cyan * 0.2f);
            display.DrawLine(Vector2.zero, scale * new Vector2((float)meetActualY2, -(float)meetActualX2), Color.red * 0.2f);
        }

        display.ClearText();

        double now = clock.simTime;
        Color planColor = Color.green;

        display.DrawText(MFD.FormatPercent("STP", stepRatio), 2, 19, Color.green);
        display.DrawText(MFD.FormatNumber("BT", calcBurnTime - now), 3, 19, planColor);
        display.DrawText(MFD.FormatNumber("BDV", burnDv), 4, 19, planColor);

        if (solver.autoStatus != TransferSolver.AUTO_IDLE && solver.autoStatus != TransferSolver.AUTO_IDLE) {
            display.DrawText("AUTO", 6, 39, Color.white);
            display.DrawText(GetAutoStatusText(), 7, 39, GetAutoStatusColor());
        }

        if (solver.autoValid || solver.solverBusy) {
            display.DrawText(FormatAdvLine(), 8, 35, GetAutoStatusColor());
        }

        if (meets) {
            display.DrawText(MFD.FormatNumber("T", meetTime1 - now), 2, 4, Color.cyan);
            display.DrawText(MFD.FormatNumber("DST", meetDist1), 3, 4, Color.cyan);

            display.DrawText(MFD.FormatNumber("T", meetTime2 - now), 2, 34, Color.red);
            display.DrawText(MFD.FormatNumber("DST", meetDist2), 3, 34, Color.red);
        }

        display.DrawText(" T- ", 0, 2, Color.white);
        display.DrawText(" T+ ", 0, 12, Color.white);
        display.DrawText("RSET", 0, 22, Color.white);
        display.DrawText(" V- ", 0, 32, Color.white);
        display.DrawText(" V+ ", 0, 42, Color.white);

        display.DrawText("STP-", MFD.TEXT_ROWS - 1, 2, Color.white);
        display.DrawText("STP+", MFD.TEXT_ROWS - 1, 12, Color.white);
        display.DrawText("CALC", MFD.TEXT_ROWS - 1, 32, Color.white);
        display.DrawText("UPLD", MFD.TEXT_ROWS - 1, 42, Color.white);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    private string GetAutoStatusText()
    {
        switch (solver.autoStatus) {
            case TransferSolver.AUTO_IDLE: return "IDLE";
            case TransferSolver.AUTO_COARSE: return "COAR";
            case TransferSolver.AUTO_REFINE: return "REFN";
            case TransferSolver.AUTO_READY: return "RDY ";
            case TransferSolver.AUTO_PLAN: return "PLAN";
            case TransferSolver.AUTO_NONE: return "NONE";
            case TransferSolver.AUTO_ERR: return "ERR ";
        }
        return "ERR ";
    }

    private Color GetAutoStatusColor()
    {
        switch (solver.autoStatus) {
            case TransferSolver.AUTO_READY:
            case TransferSolver.AUTO_PLAN:
                return Color.green;
            case TransferSolver.AUTO_COARSE:
            case TransferSolver.AUTO_REFINE:
                return Color.yellow;
            case TransferSolver.AUTO_NONE:
                return Color.gray;
            case TransferSolver.AUTO_ERR:
                return Color.red;
        }
        return Color.white;
    }

    private string FormatAdvLine()
    {
        float dv = 0.0f;
        if (solver.solverBusy && solver.bestValid) {
            dv = solver.bestDvMag;
        } else if (solver.autoValid) {
            dv = solver.autoDvMag;
        }

        if (dv <= 0.0f) return "DV  ----";
        if (dv < 100.0f) return "DV  " + dv.ToString("F1");
        if (dv < 1000.0f) return "DV  " + dv.ToString("F0");
        return "DV   >1k";
    }

    public override void OnDeserialization()
    {
        OnStepSizeChange();
    }
}