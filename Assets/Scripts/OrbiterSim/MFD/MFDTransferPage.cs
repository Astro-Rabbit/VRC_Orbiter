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
    public OrbitAnalyzer src;
    public OrbitAnalyzer tgt;
    public OrbitAnalyzer tfr;

    // for calculating transfer orbit parameters
    public TransferConicFitter fitter;

    // for calculating position and velocity at burn time
    public ConicPropagator propagator;

    [Header("GC / Upload")]
    public GC_Core gc;

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
    public double meetOop1;
    public double meetTime2;
    public double meetX2;
    public double meetY2;
    public double meetActualX2;
    public double meetActualY2;
    public double meetDist2;
    public double meetOop2;

    [Header("Manual State")]
    [UdonSynced] public int stepSize = DEFAULT_STEP_SIZE;
    [UdonSynced] public double burnTime;
    [UdonSynced] public double burnDv;

    [Header("Auto Solver - Config")]
    public int coarseBurnSamples = 24;
    public int coarseDvSamples = 17;
    public int coarseArrivalSamples = 24;
    public int refineSamplesBurn = 7;
    public int refineSamplesDv = 7;
    public int refineSamplesArrival = 16;
    public int refinePasses = 2;
    public int solverJobsPerFrame = 2;

    public double autoLeadTimeSec = 60.0;
    public double autoMinTransferSec = 120.0;
    public double autoHardCapSec = 21600.0;      // 6 hr
    public double autoDvCapMps = 500.0;
    public double planeMismatchDegLimit = 10.0;
    public double acceptMissMeters = 500000.0;   // 500 km acceptance cap

    public double scoreDvWeight = 50.0;
    public double scoreTimeWeight = 0.01;

    [Header("Auto Solver - Status")]
    public bool autoBusy = false;
    public byte autoStatus = AUTO_IDLE;
    public bool autoShowSolution = false;

    [Header("Auto Candidate")]
    public bool autoValid = false;
    public double autoBurnTime;
    public double autoEncounterTime;
    public double autoBurnDv;
    public Vector3 autoDvE = Vector3.zero;
    public double autoDvMag;
    public double autoMissMeters;
    public double autoScore;

    // Internal active display/manual-or-auto selector
    private double activeBurnTime;
    private double activeBurnDv;

    private double calcBurnTime;
    private double stepRatio;
    private double srcLastEpochT0 = Double.NegativeInfinity;
    private double tgtLastEpochT0 = Double.NegativeInfinity;
    private bool burnChanged = true;

    private const int MAX_STEP_SIZE = 0;
    private const int MIN_STEP_SIZE = -10;
    private const int DEFAULT_STEP_SIZE = -5;

    // Auto status codes
    private const byte AUTO_IDLE = 0;
    private const byte AUTO_BUSY_COARSE = 1;
    private const byte AUTO_BUSY_REFINE = 2;
    private const byte AUTO_READY = 3;
    private const byte AUTO_FAIL_NO_TARGET = 10;
    private const byte AUTO_FAIL_PRIMARY_MISMATCH = 11;
    private const byte AUTO_FAIL_INVALID_SOURCE = 12;
    private const byte AUTO_FAIL_INVALID_TARGET = 13;
    private const byte AUTO_FAIL_PLANE_MISMATCH = 14;
    private const byte AUTO_FAIL_NO_SOLUTION = 15;
    private const byte AUTO_UPLOADED = 20;

    // Progressive solver phase
    private byte solverPhase = 0;
    private const byte PHASE_IDLE = 0;
    private const byte PHASE_COARSE = 1;
    private const byte PHASE_REFINE = 2;
    private const byte PHASE_FINALIZE = 3;

    // Search ranges
    private double solveNow;
    private double solveBurnStart;
    private double solveBurnEnd;
    private double solveArrivalCap;

    // Coarse loop indices
    private int coarseBurnIndex = 0;
    private int coarseDvIndex = 0;

    // Best coarse candidates (top 3)
    private const int TOP_COUNT = 3;
    private double[] topScore = new double[TOP_COUNT];
    private double[] topBurnTime = new double[TOP_COUNT];
    private double[] topEncounterTime = new double[TOP_COUNT];
    private double[] topBurnDv = new double[TOP_COUNT];
    private Vector3[] topDvE = new Vector3[TOP_COUNT];
    private double[] topMiss = new double[TOP_COUNT];

    // Refine state
    private int refineCandidateIndex = 0;
    private int refinePassIndex = 0;
    private int refineBurnIndex = 0;
    private int refineDvIndex = 0;

    private double refineCenterBurnTime;
    private double refineCenterBurnDv;
    private double refineWindowBurnTime;
    private double refineWindowDv;

    // Final best during solve
    private bool solveBestValid = false;
    private double solveBestScore;
    private double solveBestBurnTime;
    private double solveBestEncounterTime;
    private double solveBestBurnDv;
    private Vector3 solveBestDvE = Vector3.zero;
    private double solveBestMissMeters;

    void Start()
    {
        OnStepSizeChange();
        InitTopArrays();
    }

    void Update()
    {
        if (tgt == null || tgt.conic == null) {
            hasTarget = false;
            meets = false;
            if (autoBusy) {
                AbortAutoSolve(AUTO_FAIL_NO_TARGET);
            }
            return;
        }
        hasTarget = true;

        if (autoBusy) {
            StepAutoSolve();
        }

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

        if (autoShowSolution && autoValid) {
            activeBurnTime = autoBurnTime;
            activeBurnDv = autoBurnDv;
        } else {
            activeBurnTime = burnTime;
            activeBurnDv = burnDv;
        }

        calcBurnTime = Math.Max(activeBurnTime, clock.simTime);
        if (burnChanged || conicsUpdated || (calcBurnTime != activeBurnTime && activeBurnDv > 0.0)) {
            burnChanged = false;

            propagator.conic = src.conic;
            propagator.Evaluate(calcBurnTime);

            src.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out burnX, out burnY, out _);

            double dx = propagator.rel_vx;
            double dy = propagator.rel_vy;
            double dz = propagator.rel_vz;

            double mag = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (mag <= 1e-12) {
                dx = 1.0;
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
            fitter.vx = propagator.rel_vx + dx * activeBurnDv;
            fitter.vy = propagator.rel_vy + dy * activeBurnDv;
            fitter.vz = propagator.rel_vz + dz * activeBurnDv;

            fitter.Fit(src.conic.primaryBodyId, calcBurnTime);
            tfr.UpdateInfo();

            src.GetAligned(tfr, out tfrPx, out tfrPy);
            tfrArgpDiff = Math.Atan2(tfrPy, tfrPx);

            transferUpdated = true;
        }

        if ((transferUpdated || conicsUpdated) && tfr.e < 1.0 && tgt.e < 1.0) {
            CalculateIntersections();
        } else if (transferUpdated || conicsUpdated) {
            meets = false;
        }
    }

    public void CalculateIntersections()
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

        double phi = Math.Atan2(xDiff, yDiff);

        double ratio = pdiff / mag;
        if (ratio > 1.0 || ratio < -1.0) {
            meets = false;
            return;
        }
        meets = true;

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
        src.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        meetOop1 = z;
        x -= meetX1;
        y -= meetY1;
        meetDist1 = Math.Sqrt(x * x + y * y + z * z);

        tgt.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        meetActualX1 = x * tgtPx - y * tgtPy;
        meetActualY1 = y * tgtPx + x * tgtPy;

        propagator.Evaluate(meetTime2);
        src.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out x, out y, out z);
        meetOop2 = z;
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
                    BeginAutoSolve();
                    break;
                case 4:
                    UploadAutoNode();
                    break;
            }
        } else if (side == ButtonSide.Top) {
            autoShowSolution = false;

            double baseSpeed;
            switch (num) {
                case 0:
                    SetBurnTime(Math.Max(clock.simTime, burnTime) - stepRatio * src.t);
                    break;
                case 1:
                    SetBurnTime(Math.Max(clock.simTime, burnTime) + stepRatio * src.t);
                    break;
                case 2:
                    ResetState();
                    break;
                case 3:
                    baseSpeed = Math.Sqrt(bodies.GetMu(src.conic.primaryBodyId) / (4.0 * src.a));
                    SetBurnDV(burnDv - stepRatio * baseSpeed);
                    break;
                case 4:
                    baseSpeed = Math.Sqrt(bodies.GetMu(src.conic.primaryBodyId) / (4.0 * src.a));
                    SetBurnDV(burnDv + stepRatio * baseSpeed);
                    break;
            }
        }
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
        burnChanged = true;

        autoShowSolution = false;
        autoValid = false;
        autoBusy = false;
        autoStatus = AUTO_IDLE;
        solverPhase = PHASE_IDLE;
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
        double ratio = Math.Pow(10.0, stepSize / 3.0);

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
        burnChanged = true;
    }

    public void SetBurnDV(double newBurnDv)
    {
        if (!Networking.IsOwner(gameObject)) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        burnDv = newBurnDv;
        RequestSerialization();
        burnChanged = true;
    }

    // -------------------------------------------------------------------------
    // AUTO SOLVER
    // -------------------------------------------------------------------------

    public void BeginAutoSolve()
    {
        autoValid = false;
        autoShowSolution = false;
        meets = false;

        if (src == null || src.conic == null || !src.conic.valid) {
            AbortAutoSolve(AUTO_FAIL_INVALID_SOURCE);
            return;
        }
        if (tgt == null || tgt.conic == null || !tgt.conic.valid) {
            AbortAutoSolve(AUTO_FAIL_INVALID_TARGET);
            return;
        }
        if (src.conic.primaryBodyId != tgt.conic.primaryBodyId) {
            AbortAutoSolve(AUTO_FAIL_PRIMARY_MISMATCH);
            return;
        }
        if (src.e >= 1.0 || tgt.e >= 1.0 || src.a <= 0.0 || tgt.a <= 0.0) {
            AbortAutoSolve(AUTO_FAIL_INVALID_TARGET);
            return;
        }
        if (!CheckPlaneCompatibility()) {
            AbortAutoSolve(AUTO_FAIL_PLANE_MISMATCH);
            return;
        }

        solveNow = clock.simTime;

        double srcPeriod = src.t > 0.0 ? src.t : autoHardCapSec;
        double tgtPeriod = tgt.t > 0.0 ? tgt.t : autoHardCapSec;

        solveBurnStart = solveNow + autoLeadTimeSec;
        solveBurnEnd = solveNow + Math.Min(autoHardCapSec, Math.Min(2.0 * srcPeriod, 2.0 * tgtPeriod));
        solveArrivalCap = Math.Min(autoHardCapSec, 1.5 * Math.Max(srcPeriod, tgtPeriod));

        coarseBurnIndex = 0;
        coarseDvIndex = 0;

        refineCandidateIndex = 0;
        refinePassIndex = 0;
        refineBurnIndex = 0;
        refineDvIndex = 0;

        solveBestValid = false;
        solveBestScore = 0.0;
        solveBestBurnTime = 0.0;
        solveBestEncounterTime = 0.0;
        solveBestBurnDv = 0.0;
        solveBestDvE = Vector3.zero;
        solveBestMissMeters = 0.0;

        InitTopArrays();

        autoBusy = true;
        autoStatus = AUTO_BUSY_COARSE;
        solverPhase = PHASE_COARSE;
    }

    private void AbortAutoSolve(byte failCode)
    {
        autoBusy = false;
        autoValid = false;
        autoShowSolution = false;
        autoStatus = failCode;
        solverPhase = PHASE_IDLE;
    }

    private void StepAutoSolve()
    {
        int jobs = solverJobsPerFrame;
        if (jobs < 1) jobs = 1;

        while (jobs > 0 && autoBusy) {
            if (solverPhase == PHASE_COARSE) {
                if (!StepCoarseJob()) {
                    BeginRefinePhase();
                }
            } else if (solverPhase == PHASE_REFINE) {
                if (!StepRefineJob()) {
                    solverPhase = PHASE_FINALIZE;
                }
            } else if (solverPhase == PHASE_FINALIZE) {
                FinalizeAutoSolve();
            } else {
                autoBusy = false;
                solverPhase = PHASE_IDLE;
            }
            jobs--;
        }
    }

    private bool StepCoarseJob()
    {
        if (coarseBurnSamples < 2) coarseBurnSamples = 2;
        if (coarseDvSamples < 2) coarseDvSamples = 2;

        if (coarseBurnIndex >= coarseBurnSamples) {
            return false;
        }

        double burnFrac = (double)coarseBurnIndex / (double)(coarseBurnSamples - 1);
        double dvFrac = (double)coarseDvIndex / (double)(coarseDvSamples - 1);

        double tb = solveBurnStart + (solveBurnEnd - solveBurnStart) * burnFrac;
        double dv = -autoDvCapMps + (2.0 * autoDvCapMps) * dvFrac;

        EvaluateCandidate(tb, dv, coarseArrivalSamples, solveArrivalCap, out bool valid, out double score, out double encounterTime, out Vector3 dvE, out double missMeters);

        if (valid) {
            InsertTopCandidate(score, tb, encounterTime, dv, dvE, missMeters);

            if (!solveBestValid || score < solveBestScore) {
                solveBestValid = true;
                solveBestScore = score;
                solveBestBurnTime = tb;
                solveBestEncounterTime = encounterTime;
                solveBestBurnDv = dv;
                solveBestDvE = dvE;
                solveBestMissMeters = missMeters;
            }
        }

        coarseDvIndex++;
        if (coarseDvIndex >= coarseDvSamples) {
            coarseDvIndex = 0;
            coarseBurnIndex++;
        }

        return true;
    }

    private void BeginRefinePhase()
    {
        if (topScore[0] >= 1e299) {
            AbortAutoSolve(AUTO_FAIL_NO_SOLUTION);
            return;
        }

        refineCandidateIndex = 0;
        refinePassIndex = 0;
        refineBurnIndex = 0;
        refineDvIndex = 0;

        SetupRefineCandidateWindow();
        solverPhase = PHASE_REFINE;
        autoStatus = AUTO_BUSY_REFINE;
    }

    private void SetupRefineCandidateWindow()
    {
        refineCenterBurnTime = topBurnTime[refineCandidateIndex];
        refineCenterBurnDv = topBurnDv[refineCandidateIndex];

        double srcPeriod = src.t > 0.0 ? src.t : autoHardCapSec;
        double burnSpanBase = Math.Max(60.0, 0.25 * srcPeriod);
        double dvSpanBase = Math.Max(5.0, 0.25 * autoDvCapMps);

        double shrink = Math.Pow(0.35, refinePassIndex);
        refineWindowBurnTime = burnSpanBase * shrink;
        refineWindowDv = dvSpanBase * shrink;
    }

    private bool StepRefineJob()
    {
        if (refineCandidateIndex >= TOP_COUNT || topScore[refineCandidateIndex] >= 1e299) {
            return false;
        }

        double tb = SampleSymmetric(refineCenterBurnTime, refineWindowBurnTime, refineBurnSamplesIndex(), refineSamplesBurn);
        double dv = SampleSymmetric(refineCenterBurnDv, refineWindowDv, refineDvSamplesIndex(), refineSamplesDv);

        if (tb < solveBurnStart) tb = solveBurnStart;
        if (tb > solveBurnEnd) tb = solveBurnEnd;
        if (dv < -autoDvCapMps) dv = -autoDvCapMps;
        if (dv > autoDvCapMps) dv = autoDvCapMps;

        EvaluateCandidate(tb, dv, refineSamplesArrival, solveArrivalCap, out bool valid, out double score, out double encounterTime, out Vector3 dvE, out double missMeters);

        if (valid && (!solveBestValid || score < solveBestScore)) {
            solveBestValid = true;
            solveBestScore = score;
            solveBestBurnTime = tb;
            solveBestEncounterTime = encounterTime;
            solveBestBurnDv = dv;
            solveBestDvE = dvE;
            solveBestMissMeters = missMeters;
        }

        refineDvIndex++;
        if (refineDvIndex >= refineSamplesDv) {
            refineDvIndex = 0;
            refineBurnIndex++;
            if (refineBurnIndex >= refineSamplesBurn) {
                refineBurnIndex = 0;
                refinePassIndex++;
                if (refinePassIndex >= refinePasses) {
                    refinePassIndex = 0;
                    refineCandidateIndex++;
                    if (refineCandidateIndex >= TOP_COUNT || topScore[refineCandidateIndex] >= 1e299) {
                        return false;
                    }
                }
                SetupRefineCandidateWindow();
            }
        }

        return true;
    }

    private int refineBurnSamplesIndex()
    {
        return refineBurnIndex;
    }

    private int refineDvSamplesIndex()
    {
        return refineDvIndex;
    }

    private double SampleSymmetric(double center, double halfWidth, int index, int count)
    {
        if (count <= 1) return center;
        double u = (double)index / (double)(count - 1); // 0..1
        return center + (2.0 * u - 1.0) * halfWidth;
    }

    private void FinalizeAutoSolve()
    {
        autoBusy = false;
        solverPhase = PHASE_IDLE;

        if (!solveBestValid || solveBestMissMeters > acceptMissMeters) {
            autoValid = false;
            autoShowSolution = false;
            autoStatus = AUTO_FAIL_NO_SOLUTION;
            return;
        }

        autoValid = true;
        autoBurnTime = solveBestBurnTime;
        autoEncounterTime = solveBestEncounterTime;
        autoBurnDv = solveBestBurnDv;
        autoDvE = solveBestDvE;
        autoDvMag = autoDvE.magnitude;
        autoMissMeters = solveBestMissMeters;
        autoScore = solveBestScore;

        autoShowSolution = true;
        burnChanged = true;
        autoStatus = AUTO_READY;
    }

    private bool CheckPlaneCompatibility()
    {
        double sx, sy, sz;
        double tx, ty, tz;
        src.Normal(out sx, out sy, out sz);
        tgt.Normal(out tx, out ty, out tz);

        double dot = sx * tx + sy * ty + sz * tz;
        if (dot > 1.0) dot = 1.0;
        if (dot < -1.0) dot = -1.0;

        double angleDeg = Math.Acos(dot) * 180.0 / Math.PI;
        return angleDeg <= planeMismatchDegLimit;
    }

    private void EvaluateCandidate(
        double tb,
        double dvT,
        int arrivalSamples,
        double arrivalCapSec,
        out bool valid,
        out double score,
        out double encounterTime,
        out Vector3 dvE,
        out double missMeters)
    {
        valid = false;
        score = 0.0;
        encounterTime = 0.0;
        dvE = Vector3.zero;
        missMeters = 0.0;

        if (tb < clock.simTime) return;
        if (src.conic == null || tgt.conic == null) return;
        if (src.conic.primaryBodyId != tgt.conic.primaryBodyId) return;

        propagator.conic = src.conic;
        propagator.Evaluate(tb);

        double vx = propagator.rel_vx;
        double vy = propagator.rel_vy;
        double vz = propagator.rel_vz;
        double vmag = Math.Sqrt(vx * vx + vy * vy + vz * vz);
        if (vmag <= 1e-12) return;

        double tx = vx / vmag;
        double ty = vy / vmag;
        double tz = vz / vmag;

        fitter.rx = propagator.rel_rx;
        fitter.ry = propagator.rel_ry;
        fitter.rz = propagator.rel_rz;
        fitter.vx = vx + tx * dvT;
        fitter.vy = vy + ty * dvT;
        fitter.vz = vz + tz * dvT;

        fitter.Fit(src.conic.primaryBodyId, tb);
        tfr.UpdateInfo();

        if (!tfr.conic.valid) return;
        if (tfr.a <= 0.0 || tfr.e >= 1.0) return;

        double arrivalStart = tb + autoMinTransferSec;
        double srcPeriod = src.t > 0.0 ? src.t : autoHardCapSec;
        double tgtPeriod = tgt.t > 0.0 ? tgt.t : autoHardCapSec;
        double arrivalEnd = tb + Math.Min(arrivalCapSec, 1.5 * Math.Max(srcPeriod, tgtPeriod));

        if (arrivalEnd <= arrivalStart) return;
        if (arrivalSamples < 2) arrivalSamples = 2;

        bool found = false;
        double bestMiss = 0.0;
        double bestEncounterTime = 0.0;

        for (int i = 0; i < arrivalSamples; i++) {
            double frac = (double)i / (double)(arrivalSamples - 1);
            double ta = arrivalStart + (arrivalEnd - arrivalStart) * frac;

            // transfer position
            propagator.conic = tfr.conic;
            propagator.Evaluate(ta);
            double cx = propagator.rel_rx;
            double cy = propagator.rel_ry;
            double cz = propagator.rel_rz;

            // target position
            propagator.conic = tgt.conic;
            propagator.Evaluate(ta);
            double txp = propagator.rel_rx;
            double typ = propagator.rel_ry;
            double tzp = propagator.rel_rz;

            double dx = cx - txp;
            double dy = cy - typ;
            double dz = cz - tzp;
            double miss = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (!found || miss < bestMiss) {
                found = true;
                bestMiss = miss;
                bestEncounterTime = ta;
            }
        }

        if (!found) return;

        valid = true;
        encounterTime = bestEncounterTime;
        missMeters = bestMiss;
        dvE = new Vector3((float)(tx * dvT), (float)(ty * dvT), (float)(tz * dvT));
        score = bestMiss + scoreDvWeight * Math.Abs(dvT) + scoreTimeWeight * (bestEncounterTime - tb);
    }

    private void InitTopArrays()
    {
        for (int i = 0; i < TOP_COUNT; i++) {
            topScore[i] = 1e300;
            topBurnTime[i] = 0.0;
            topEncounterTime[i] = 0.0;
            topBurnDv[i] = 0.0;
            topDvE[i] = Vector3.zero;
            topMiss[i] = 0.0;
        }
    }

    private void InsertTopCandidate(double score, double burnTimeValue, double encounterTimeValue, double burnDvValue, Vector3 dvEValue, double missValue)
    {
        int insertIndex = -1;
        for (int i = 0; i < TOP_COUNT; i++) {
            if (score < topScore[i]) {
                insertIndex = i;
                break;
            }
        }
        if (insertIndex < 0) return;

        for (int i = TOP_COUNT - 1; i > insertIndex; i--) {
            topScore[i] = topScore[i - 1];
            topBurnTime[i] = topBurnTime[i - 1];
            topEncounterTime[i] = topEncounterTime[i - 1];
            topBurnDv[i] = topBurnDv[i - 1];
            topDvE[i] = topDvE[i - 1];
            topMiss[i] = topMiss[i - 1];
        }

        topScore[insertIndex] = score;
        topBurnTime[insertIndex] = burnTimeValue;
        topEncounterTime[insertIndex] = encounterTimeValue;
        topBurnDv[insertIndex] = burnDvValue;
        topDvE[insertIndex] = dvEValue;
        topMiss[insertIndex] = missValue;
    }

    public void UploadAutoNode()
    {
        if (!autoValid || gc == null) {
            return;
        }

        gc.API_Node_CreateAtTime(autoDvE, autoBurnTime);
        autoStatus = AUTO_UPLOADED;
    }

    // -------------------------------------------------------------------------
    // DISPLAY
    // -------------------------------------------------------------------------

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
        float scale = orbitSize / (float)src.a;

        display.DrawConic(Vector2.zero, scale * (float)bodies.GetRadius(src.conic.primaryBodyId), 0f, 0f, Color.white * 0.2f);
        display.DrawConic(Vector2.zero, (float)tfr.pe * scale, (float)tfrArgpDiff, (float)tfr.e, Color.gray);
        display.DrawConic(Vector2.zero, (float)src.pe * scale, 0f, (float)src.e, Color.green);
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
        Color planColor = autoShowSolution && autoValid ? Color.cyan : Color.green;

        display.DrawText(MFD.FormatPercent("STP", stepRatio), 2, 19, Color.green);
        display.DrawText(MFD.FormatNumber("BT", calcBurnTime - now), 3, 19, planColor);
        display.DrawText(MFD.FormatNumber("BDV", activeBurnDv), 4, 19, planColor);

        if (autoBusy) {
            display.DrawText("AUTO SEARCH", 6, 18, Color.yellow);
            if (autoStatus == AUTO_BUSY_COARSE) {
                display.DrawText("COARSE", 7, 21, Color.yellow);
            } else if (autoStatus == AUTO_BUSY_REFINE) {
                display.DrawText("REFINE", 7, 21, Color.yellow);
            }
        } else if (autoValid) {
            display.DrawText(MFD.FormatNumber("AMISS", autoMissMeters), 6, 14, Color.cyan);
            display.DrawText(MFD.FormatNumber("AT", autoEncounterTime - now), 7, 16, Color.cyan);
        } else {
            string s = GetAutoStatusText();
            if (s != "") {
                display.DrawText(s, 6, 24 - s.Length / 2, Color.yellow);
            }
        }

        if (meets) {
            // meet 1 = cyan
            display.DrawText(MFD.FormatNumber("T", meetTime1 - now), 2, 4, Color.cyan);
            display.DrawText(MFD.FormatNumber("DST", meetDist1), 3, 4, Color.cyan);
            //display.DrawText(MFD.FormatNumber("OOP", meetOop1), 4, 4, Color.cyan);

            // meet 2 = red
            display.DrawText(MFD.FormatNumber("T", meetTime2 - now), 2, 34, Color.red);
            display.DrawText(MFD.FormatNumber("DST", meetDist2), 3, 34, Color.red);
            //display.DrawText(MFD.FormatNumber("OOP", meetOop2), 4, 34, Color.red);
        }

        display.DrawText(" T- ", 0, 2, Color.white);
        display.DrawText(" T+ ", 0, 12, Color.white);
        display.DrawText("RSET", 0, 22, Color.white);
        display.DrawText(" V- ", 0, 32, Color.white);
        display.DrawText(" V+ ", 0, 42, Color.white);

        display.DrawText("STP-", MFD.TEXT_ROWS - 1, 2, Color.white);
        display.DrawText("STP+", MFD.TEXT_ROWS - 1, 12, Color.white);
        display.DrawText("AUTO", MFD.TEXT_ROWS - 1, 32, Color.white);
        display.DrawText("UPLD", MFD.TEXT_ROWS - 1, 42, Color.white);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    private string GetAutoStatusText()
    {
        switch (autoStatus) {
            case AUTO_IDLE: return "";
            case AUTO_READY: return "AUTO READY";
            case AUTO_UPLOADED: return "NODE UPLOADED";
            case AUTO_FAIL_NO_TARGET: return "NO TARGET";
            case AUTO_FAIL_PRIMARY_MISMATCH: return "PRIMARY MISMATCH";
            case AUTO_FAIL_INVALID_SOURCE: return "INVALID SOURCE";
            case AUTO_FAIL_INVALID_TARGET: return "INVALID TARGET";
            case AUTO_FAIL_PLANE_MISMATCH: return "PLANE MISMATCH";
            case AUTO_FAIL_NO_SOLUTION: return "NO SOLUTION";
        }
        return "";
    }

    public override void OnDeserialization()
    {
        OnStepSizeChange();
        burnChanged = true;
    }
}