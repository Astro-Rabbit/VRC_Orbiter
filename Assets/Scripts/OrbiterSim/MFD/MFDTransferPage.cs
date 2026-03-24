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

    [Header("Auto / Lambert")]
    public GC_Core gc;
    public int coarseBurnSamples = 16;
    public int coarseEncounterSamples = 20;
    public int refineTopCount = 2;
    public int refinePasses = 2;
    public int refineBurnSamples = 7;
    public int refineEncounterSamples = 7;
    public int coarseSolvesPerFrame = 4;
    public int refineSolvesPerFrame = 2;

    public double burnLeadTimeSec = 30.0;
    public double minTimeOfFlightSec = 120.0;
    public double burnSearchMaxSec = 21600.0;      // 6 hr
    public double encounterSearchMaxSec = 21600.0; // 6 hr
    public double dvCapMps = 1000.0;

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

    [Header("State")]
    [UdonSynced] public int stepSize = DEFAULT_STEP_SIZE;
    [UdonSynced] public double burnTime;
    [UdonSynced] public double burnDv;

    [Header("Auto State (read-only)")]
    public byte autoStatus = AUTO_IDLE;
    public bool autoValid = false;
    public double autoBurnTime = 0.0;
    public double autoEncounterTime = 0.0;
    public Vector3 autoDvE = Vector3.zero;
    public float autoDvMag = 0.0f;

    private double calcBurnTime;
    private double stepRatio;
    private double srcLastEpochT0 = Double.NegativeInfinity;
    private double tgtLastEpochT0 = Double.NegativeInfinity;
    private bool burnChanged = true;

    private const int MAX_STEP_SIZE = 0;
    private const int MIN_STEP_SIZE = -10;
    private const int DEFAULT_STEP_SIZE = -5;

    private const byte AUTO_IDLE = 0;
    private const byte AUTO_COARSE = 1;
    private const byte AUTO_REFINE = 2;
    private const byte AUTO_READY = 3;
    private const byte AUTO_PLAN = 4;
    private const byte AUTO_NONE = 5;
    private const byte AUTO_ERR = 6;

    private const byte PHASE_IDLE = 0;
    private const byte PHASE_COARSE = 1;
    private const byte PHASE_REFINE = 2;
    private const byte PHASE_FINALIZE = 3;

    private byte solverPhase = PHASE_IDLE;
    private bool solverBusy = false;

    private double solveNow = 0.0;
    private double solveMu = 0.0;
    private byte solvePrimaryBodyId = 0;
    private double solveBurnStart = 0.0;
    private double solveBurnEnd = 0.0;
    private double solveMaxEncounterDt = 0.0;
    private double solveSrcEpoch = Double.NegativeInfinity;
    private double solveTgtEpoch = Double.NegativeInfinity;

    private int coarseBurnIndex = 0;
    private int coarseEncounterIndex = 0;

    private int refineCandidateIndex = 0;
    private int refinePassIndex = 0;
    private int refineBurnIndex = 0;
    private int refineEncounterIndex = 0;
    private double refineCenterBurn = 0.0;
    private double refineCenterEncounter = 0.0;
    private double refineBurnHalfWindow = 0.0;
    private double refineEncounterHalfWindow = 0.0;

    private bool bestValid = false;
    private double bestScore = 0.0;
    private double bestBurnTime = 0.0;
    private double bestEncounterTime = 0.0;
    private Vector3 bestDvE = Vector3.zero;
    private float bestDvMag = 0.0f;

    private const int TOP_MAX = 2;
    private double[] topScore = new double[TOP_MAX];
    private double[] topBurnTime = new double[TOP_MAX];
    private double[] topEncounterTime = new double[TOP_MAX];
    private Vector3[] topDvE = new Vector3[TOP_MAX];
    private float[] topDvMag = new float[TOP_MAX];

    void Start()
    {
        OnStepSizeChange();
        ClearTopCandidates();
    }

    void Update()
    {
        if (tgt == null || tgt.conic == null) {
            hasTarget = false;
            meets = false;
            if (solverBusy) AbortAutoSolve(AUTO_NONE);
            return;
        }
        hasTarget = true;

        if (solverBusy) {
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

        calcBurnTime = Math.Max(burnTime, clock.simTime);
        if (burnChanged || conicsUpdated || (calcBurnTime != burnTime && burnDv > 0.0)) {
            burnChanged = false;

            propagator.conic = src.conic;
            propagator.Evaluate(calcBurnTime);

            src.EclipticToPerifocal(propagator.rel_rx, propagator.rel_ry, propagator.rel_rz, out burnX, out burnY, out _);

            double dx = propagator.rel_vx;
            double dy = propagator.rel_vy;
            double dz = propagator.rel_vz;
            double mag = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (mag > 1e-12) {
                dx /= mag;
                dy /= mag;
                dz /= mag;
            } else {
                dx = 1.0;
                dy = 0.0;
                dz = 0.0;
            }

            fitter.rx = propagator.rel_rx;
            fitter.ry = propagator.rel_ry;
            fitter.rz = propagator.rel_rz;
            fitter.vx = propagator.rel_vx + dx * burnDv;
            fitter.vy = propagator.rel_vy + dy * burnDv;
            fitter.vz = propagator.rel_vz + dz * burnDv;

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

        propagator.conic = tgt.conic;

        propagator.Evaluate(meetTime1);
        double x, y, z;
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
                    PlanAutoNode();
                    break;
            }
        } else if (side == ButtonSide.Top) {
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
                    SetBurnDV(burnDv - stepRatio * Math.Sqrt(bodies.GetMu(src.conic.primaryBodyId) / (4.0 * src.a)));
                    break;
                case 4:
                    SetBurnDV(burnDv + stepRatio * Math.Sqrt(bodies.GetMu(src.conic.primaryBodyId) / (4.0 * src.a)));
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

    // ============================================================
    // AUTO SOLVER
    // ============================================================

    public void BeginAutoSolve()
    {
        if (src == null || src.conic == null || !src.conic.valid) {
            AbortAutoSolve(AUTO_ERR);
            return;
        }
        if (tgt == null || tgt.conic == null || !tgt.conic.valid) {
            AbortAutoSolve(AUTO_NONE);
            return;
        }
        if (src.conic.primaryBodyId != tgt.conic.primaryBodyId) {
            AbortAutoSolve(AUTO_ERR);
            return;
        }

        double srcPeriod = (src.t > 0.0) ? src.t : burnSearchMaxSec;
        double tgtPeriod = (tgt.t > 0.0) ? tgt.t : burnSearchMaxSec;
        double maxPeriod = Math.Max(srcPeriod, tgtPeriod);

        solveNow = clock.simTime;
        solveMu = bodies.GetMu(src.conic.primaryBodyId);
        solvePrimaryBodyId = src.conic.primaryBodyId;
        solveBurnStart = solveNow + burnLeadTimeSec;
        solveBurnEnd = solveNow + Math.Min(burnSearchMaxSec, 2.0 * maxPeriod);
        solveMaxEncounterDt = Math.Min(encounterSearchMaxSec, 2.0 * maxPeriod);

        solveSrcEpoch = src.conic.epochT0;
        solveTgtEpoch = tgt.conic.epochT0;

        coarseBurnIndex = 0;
        coarseEncounterIndex = 0;
        refineCandidateIndex = 0;
        refinePassIndex = 0;
        refineBurnIndex = 0;
        refineEncounterIndex = 0;

        bestValid = false;
        bestScore = 0.0;
        bestBurnTime = 0.0;
        bestEncounterTime = 0.0;
        bestDvE = Vector3.zero;
        bestDvMag = 0.0f;

        autoValid = false;
        autoBurnTime = 0.0;
        autoEncounterTime = 0.0;
        autoDvE = Vector3.zero;
        autoDvMag = 0.0f;

        ClearTopCandidates();

        solverBusy = true;
        solverPhase = PHASE_COARSE;
        autoStatus = AUTO_COARSE;
    }

    private void AbortAutoSolve(byte failStatus)
    {
        solverBusy = false;
        solverPhase = PHASE_IDLE;
        autoValid = false;
        autoStatus = failStatus;
    }

    private void StepAutoSolve()
    {
        if (src == null || tgt == null || src.conic == null || tgt.conic == null) {
            AbortAutoSolve(AUTO_ERR);
            return;
        }

        if (src.conic.epochT0 != solveSrcEpoch || tgt.conic.epochT0 != solveTgtEpoch) {
            AbortAutoSolve(AUTO_ERR);
            return;
        }

        if (solverPhase == PHASE_COARSE) {
            int n = coarseSolvesPerFrame;
            if (n < 1) n = 1;

            while (n > 0 && solverPhase == PHASE_COARSE) {
                if (!StepCoarseSolve()) {
                    BeginRefinePhase();
                    break;
                }
                n--;
            }
        } else if (solverPhase == PHASE_REFINE) {
            int n = refineSolvesPerFrame;
            if (n < 1) n = 1;

            while (n > 0 && solverPhase == PHASE_REFINE) {
                if (!StepRefineSolve()) {
                    solverPhase = PHASE_FINALIZE;
                    break;
                }
                n--;
            }
        }

        if (solverPhase == PHASE_FINALIZE) {
            FinalizeAutoSolve();
        }
    }

    private bool StepCoarseSolve()
    {
        if (coarseBurnSamples < 2) coarseBurnSamples = 2;
        if (coarseEncounterSamples < 2) coarseEncounterSamples = 2;

        if (coarseBurnIndex >= coarseBurnSamples) {
            return false;
        }

        double tb = LerpSolveBurn(coarseBurnIndex, coarseBurnSamples);
        double ta = LerpSolveEncounter(tb, coarseEncounterIndex, coarseEncounterSamples);

        EvaluateLambertCandidate(tb, ta, out bool valid, out float dvMag, out Vector3 dvE, out double score);

        if (valid) {
            InsertTopCandidate(score, tb, ta, dvE, dvMag);

            if (!bestValid || score < bestScore) {
                bestValid = true;
                bestScore = score;
                bestBurnTime = tb;
                bestEncounterTime = ta;
                bestDvE = dvE;
                bestDvMag = dvMag;
            }
        }

        coarseEncounterIndex++;
        if (coarseEncounterIndex >= coarseEncounterSamples) {
            coarseEncounterIndex = 0;
            coarseBurnIndex++;
        }

        return true;
    }

    private void BeginRefinePhase()
    {
        if (!HasTopCandidate()) {
            AbortAutoSolve(AUTO_NONE);
            return;
        }

        refineCandidateIndex = 0;
        refinePassIndex = 0;
        refineBurnIndex = 0;
        refineEncounterIndex = 0;
        SetupRefineWindow();

        solverPhase = PHASE_REFINE;
        autoStatus = AUTO_REFINE;
    }

    private bool StepRefineSolve()
    {
        if (refineCandidateIndex >= refineTopCount || refineCandidateIndex >= TOP_MAX) {
            return false;
        }
        if (topScore[refineCandidateIndex] >= 1e299) {
            return false;
        }

        double tb = SampleCentered(refineCenterBurn, refineBurnHalfWindow, refineBurnIndex, refineBurnSamples);
        double ta = SampleCentered(refineCenterEncounter, refineEncounterHalfWindow, refineEncounterIndex, refineEncounterSamples);

        if (tb < solveBurnStart) tb = solveBurnStart;
        if (tb > solveBurnEnd) tb = solveBurnEnd;

        double minEncounter = tb + minTimeOfFlightSec;
        double maxEncounter = tb + solveMaxEncounterDt;
        if (ta < minEncounter) ta = minEncounter;
        if (ta > maxEncounter) ta = maxEncounter;

        EvaluateLambertCandidate(tb, ta, out bool valid, out float dvMag, out Vector3 dvE, out double score);

        if (valid && (!bestValid || score < bestScore)) {
            bestValid = true;
            bestScore = score;
            bestBurnTime = tb;
            bestEncounterTime = ta;
            bestDvE = dvE;
            bestDvMag = dvMag;
        }

        refineEncounterIndex++;
        if (refineEncounterIndex >= refineEncounterSamples) {
            refineEncounterIndex = 0;
            refineBurnIndex++;

            if (refineBurnIndex >= refineBurnSamples) {
                refineBurnIndex = 0;
                refinePassIndex++;

                if (refinePassIndex >= refinePasses) {
                    refinePassIndex = 0;
                    refineCandidateIndex++;
                    if (refineCandidateIndex >= refineTopCount || refineCandidateIndex >= TOP_MAX || topScore[refineCandidateIndex] >= 1e299) {
                        return false;
                    }
                }

                SetupRefineWindow();
            }
        }

        return true;
    }

    private void FinalizeAutoSolve()
    {
        solverBusy = false;
        solverPhase = PHASE_IDLE;

        if (!bestValid) {
            autoValid = false;
            autoStatus = AUTO_NONE;
            return;
        }

        autoValid = true;
        autoBurnTime = bestBurnTime;
        autoEncounterTime = bestEncounterTime;
        autoDvE = bestDvE;
        autoDvMag = bestDvMag;
        autoStatus = AUTO_READY;
    }

    private void SetupRefineWindow()
    {
        refineCenterBurn = topBurnTime[refineCandidateIndex];
        refineCenterEncounter = topEncounterTime[refineCandidateIndex];

        double burnSpan = Math.Max(60.0, (solveBurnEnd - solveBurnStart) / Math.Max(2.0, (double)(coarseBurnSamples - 1)));
        double encSpan = Math.Max(60.0, solveMaxEncounterDt / Math.Max(2.0, (double)(coarseEncounterSamples - 1)));

        double shrink = Math.Pow(0.35, refinePassIndex + 1);
        refineBurnHalfWindow = burnSpan * shrink;
        refineEncounterHalfWindow = encSpan * shrink;
    }

    private void EvaluateLambertCandidate(double tb, double ta, out bool valid, out float dvMag, out Vector3 dvE, out double score)
    {
        valid = false;
        dvMag = 0.0f;
        dvE = Vector3.zero;
        score = 0.0;

        if (tb < solveBurnStart) return;
        if (tb > solveBurnEnd) return;
        if (ta <= tb + minTimeOfFlightSec) return;
        if (ta > tb + solveMaxEncounterDt) return;
        if (solveMu <= 0.0) return;

        propagator.conic = src.conic;
        propagator.Evaluate(tb);

        double r1x = propagator.rel_rx;
        double r1y = propagator.rel_ry;
        double r1z = propagator.rel_rz;
        double vCurX = propagator.rel_vx;
        double vCurY = propagator.rel_vy;
        double vCurZ = propagator.rel_vz;

        propagator.conic = tgt.conic;
        propagator.Evaluate(ta);

        double r2x = propagator.rel_rx;
        double r2y = propagator.rel_ry;
        double r2z = propagator.rel_rz;

        double vReqX, vReqY, vReqZ;
        if (!SolveLambertShortElliptic(r1x, r1y, r1z, r2x, r2y, r2z, ta - tb, solveMu, out vReqX, out vReqY, out vReqZ)) {
            return;
        }

        double dvx = vReqX - vCurX;
        double dvy = vReqY - vCurY;
        double dvz = vReqZ - vCurZ;
        double dv = Math.Sqrt(dvx * dvx + dvy * dvy + dvz * dvz);

        if (dv > dvCapMps) return;

        dvE = new Vector3((float)dvx, (float)dvy, (float)dvz);
        dvMag = (float)dv;

        // Score: prioritize lower DV, with a small preference for shorter TOF.
        score = dv + 0.002 * (ta - tb);
        valid = true;
    }

    private bool SolveLambertShortElliptic(
        double r1x, double r1y, double r1z,
        double r2x, double r2y, double r2z,
        double dt,
        double mu,
        out double v1x, out double v1y, out double v1z)
    {
        v1x = 0.0;
        v1y = 0.0;
        v1z = 0.0;

        if (dt <= 0.0 || mu <= 0.0) return false;

        double r1 = Math.Sqrt(r1x * r1x + r1y * r1y + r1z * r1z);
        double r2 = Math.Sqrt(r2x * r2x + r2y * r2y + r2z * r2z);
        if (r1 <= 0.0 || r2 <= 0.0) return false;

        double dot = r1x * r2x + r1y * r2y + r1z * r2z;
        double cosDtheta = dot / (r1 * r2);
        if (cosDtheta > 1.0) cosDtheta = 1.0;
        if (cosDtheta < -1.0) cosDtheta = -1.0;

        double dtheta = Math.Acos(cosDtheta);
        if (dtheta <= 1e-8 || Math.Abs(Math.PI - dtheta) <= 1e-8) return false;

        double sinDtheta = Math.Sin(dtheta);
        double denom = 1.0 - cosDtheta;
        if (Math.Abs(denom) <= 1e-12) return false;

        double A = sinDtheta * Math.Sqrt(r1 * r2 / denom);
        if (Math.Abs(A) <= 1e-12) return false;

        double f0, y0;
        if (!LambertTimeResidual(0.0, r1, r2, A, dt, mu, out f0, out y0)) return false;
        if (f0 > 0.0) return false; // desired dt below parabolic minimum

        double zLow = 0.0;
        double fLow = f0;

        double zHigh = 4.0;
        double fHigh = 0.0;
        bool bracketed = false;

        for (int i = 0; i < 32; i++) {
            double fTry, yTry;
            if (LambertTimeResidual(zHigh, r1, r2, A, dt, mu, out fTry, out yTry)) {
                if (fTry >= 0.0) {
                    fHigh = fTry;
                    bracketed = true;
                    break;
                }
            }
            zHigh *= 2.0;
        }

        if (!bracketed) return false;

        double zMid = 0.0;
        double fMid = 0.0;
        double y = 0.0;

        for (int iter = 0; iter < 48; iter++) {
            zMid = 0.5 * (zLow + zHigh);

            if (!LambertTimeResidual(zMid, r1, r2, A, dt, mu, out fMid, out y)) {
                zLow = zMid;
                continue;
            }

            if (Math.Abs(fMid) < 1e-6) {
                break;
            }

            if (fMid > 0.0) {
                zHigh = zMid;
                fHigh = fMid;
            } else {
                zLow = zMid;
                fLow = fMid;
            }
        }

        if (y <= 0.0) return false;

        double Cz = StumpffC(zMid);
        if (Cz <= 0.0) return false;

        double g = A * Math.Sqrt(y / mu);
        if (Math.Abs(g) <= 1e-9) return false;

        double f = 1.0 - y / r1;

        v1x = (r2x - f * r1x) / g;
        v1y = (r2y - f * r1y) / g;
        v1z = (r2z - f * r1z) / g;
        return true;
    }

    private bool LambertTimeResidual(double z, double r1, double r2, double A, double dt, double mu, out double residual, out double y)
    {
        residual = 0.0;
        y = 0.0;

        double C = StumpffC(z);
        double S = StumpffS(z);

        if (C <= 1e-14) {
            return false;
        }

        y = r1 + r2 + A * ((z * S - 1.0) / Math.Sqrt(C));
        if (y <= 0.0) {
            return false;
        }

        double x = Math.Sqrt(y / C);
        double tof = (x * x * x * S + A * Math.Sqrt(y)) / Math.Sqrt(mu);
        residual = tof - dt;
        return true;
    }

    private double StumpffC(double z)
    {
        if (Math.Abs(z) < 1e-8) {
            return 0.5;
        }

        if (z > 0.0) {
            double s = Math.Sqrt(z);
            return (1.0 - Math.Cos(s)) / z;
        }

        double sh = Math.Sqrt(-z);
        return (Math.Cosh(sh) - 1.0) / (-z);
    }

    private double StumpffS(double z)
    {
        if (Math.Abs(z) < 1e-8) {
            return 1.0 / 6.0;
        }

        if (z > 0.0) {
            double s = Math.Sqrt(z);
            return (s - Math.Sin(s)) / (s * s * s);
        }

        double sh = Math.Sqrt(-z);
        return (Math.Sinh(sh) - sh) / (sh * sh * sh);
    }

    private void PlanAutoNode()
    {
        if (!autoValid || gc == null) return;
        int idx = gc.API_Node_CreateAtTime(autoDvE, autoBurnTime);
        if (idx >= 0) {
            autoStatus = AUTO_PLAN;
        } else {
            autoStatus = AUTO_ERR;
        }
    }

    private double LerpSolveBurn(int index, int count)
    {
        if (count <= 1) return solveBurnStart;
        double u = (double)index / (double)(count - 1);
        return solveBurnStart + (solveBurnEnd - solveBurnStart) * u;
    }

    private double LerpSolveEncounter(double tb, int index, int count)
    {
        double start = tb + minTimeOfFlightSec;
        double end = tb + solveMaxEncounterDt;
        if (count <= 1) return start;
        double u = (double)index / (double)(count - 1);
        return start + (end - start) * u;
    }

    private double SampleCentered(double center, double halfWindow, int index, int count)
    {
        if (count <= 1) return center;
        double u = (double)index / (double)(count - 1);
        return center + (2.0 * u - 1.0) * halfWindow;
    }

    private void ClearTopCandidates()
    {
        for (int i = 0; i < TOP_MAX; i++) {
            topScore[i] = 1e300;
            topBurnTime[i] = 0.0;
            topEncounterTime[i] = 0.0;
            topDvE[i] = Vector3.zero;
            topDvMag[i] = 0.0f;
        }
    }

    private bool HasTopCandidate()
    {
        return topScore[0] < 1e299;
    }

    private void InsertTopCandidate(double score, double tb, double ta, Vector3 dvE, float dvMag)
    {
        int idx = -1;
        for (int i = 0; i < TOP_MAX; i++) {
            if (score < topScore[i]) {
                idx = i;
                break;
            }
        }
        if (idx < 0) return;

        for (int i = TOP_MAX - 1; i > idx; i--) {
            topScore[i] = topScore[i - 1];
            topBurnTime[i] = topBurnTime[i - 1];
            topEncounterTime[i] = topEncounterTime[i - 1];
            topDvE[i] = topDvE[i - 1];
            topDvMag[i] = topDvMag[i - 1];
        }

        topScore[idx] = score;
        topBurnTime[idx] = tb;
        topEncounterTime[idx] = ta;
        topDvE[idx] = dvE;
        topDvMag[idx] = dvMag;
    }

    // ============================================================
    // DISPLAY
    // ============================================================

    public override void DrawDisplay(MFD display)
    {
        display.ClearGraphics();

        if (!hasTarget) {
            display.ClearText();
            string msg = "NO TARGET SELECTED";
            display.DrawText(msg, 10, 24 - msg.Length / 2, Color.green);

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
            return;
        }

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
        display.DrawText(MFD.FormatPercent("STP", stepRatio), 2, 19, Color.green);
        display.DrawText(MFD.FormatNumber("BT", calcBurnTime - now), 3, 19, Color.green);
        display.DrawText(MFD.FormatNumber("BDV", burnDv), 4, 19, Color.green);

        if (meets) {
            display.DrawText(MFD.FormatNumber("T", meetTime1 - now), 2, 4, Color.cyan);
            display.DrawText(MFD.FormatNumber("DST", meetDist1), 3, 4, Color.cyan);

            display.DrawText(MFD.FormatNumber("T", meetTime2 - now), 2, 34, Color.red);
            display.DrawText(MFD.FormatNumber("DST", meetDist2), 3, 34, Color.red);
        }

        // Auto block: bottom right
        display.DrawText("AUTO", 6, 39, Color.white);
        display.DrawText(GetAutoStatusText(), 7, 39, GetAutoStatusColor());

        if (autoValid || solverBusy) {
            display.DrawText(FormatAdvLine(), 8, 35, GetAutoStatusColor());
        } else {
            display.DrawText("DV  ----", 8, 35, Color.gray);
        }

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

    private string GetAutoStatusText()
    {
        switch (autoStatus) {
            case AUTO_IDLE: return "IDLE";
            case AUTO_COARSE: return "COAR";
            case AUTO_REFINE: return "REFN";
            case AUTO_READY: return "RDY ";
            case AUTO_PLAN: return "PLAN";
            case AUTO_NONE: return "NONE";
            case AUTO_ERR: return "ERR ";
        }
        return "ERR ";
    }

    private Color GetAutoStatusColor()
    {
        switch (autoStatus) {
            case AUTO_READY:
            case AUTO_PLAN:
                return Color.green;
            case AUTO_COARSE:
            case AUTO_REFINE:
                return Color.yellow;
            case AUTO_NONE:
                return Color.gray;
            case AUTO_ERR:
                return Color.red;
        }
        return Color.white;
    }

    private string FormatAdvLine()
    {
        float dv = 0.0f;
        if (solverBusy && bestValid) {
            dv = bestDvMag;
        } else if (autoValid) {
            dv = autoDvMag;
        }

        if (dv <= 0.0f) return "DV  ----";
        if (dv < 100.0f) return "DV  " + dv.ToString("F1");
        if (dv < 1000.0f) return "DV  " + dv.ToString("F0");
        return "DV   >1k";
    }

    public override void OnDeserialization()
    {
        OnStepSizeChange();
        burnChanged = true;
    }
}