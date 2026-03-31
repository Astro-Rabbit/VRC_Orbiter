using UdonSharp;
using System;
using UnityEngine;

public class TransferSolver : UdonSharpBehaviour 
{
    [Header("References")]
    public ConicPropagator propagator;
    public BodyCatalog bodies;
    public SimClock clock;
    public OrbitAnalyzer src;
    public OrbitAnalyzer tgt;
    public OrbitAnalyzer tfr;

    [Header("Auto / Lambert")]
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
    public double dvCapMps = 11000.0;

    [Header("Auto State (read-only)")]
    public byte autoStatus = AUTO_IDLE;
    public bool autoValid = false;
    public double autoBurnTime = 0.0;
    public double autoEncounterTime = 0.0;
    public Vector3 autoDvE = Vector3.zero;
    public float autoDvMag = 0.0f;

    public const byte AUTO_IDLE = 0;
    public const byte AUTO_COARSE = 1;
    public const byte AUTO_REFINE = 2;
    public const byte AUTO_READY = 3;
    public const byte AUTO_PLAN = 4;
    public const byte AUTO_NONE = 5;
    public const byte AUTO_ERR = 6;

    public const byte PHASE_IDLE = 0;
    public const byte PHASE_COARSE = 1;
    public const byte PHASE_REFINE = 2;
    public const byte PHASE_FINALIZE = 3;

    public byte solverPhase = PHASE_IDLE;
    public  bool solverBusy = false;

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

    public bool bestValid = false;
    public double bestScore = 0.0;
    public double bestBurnTime = 0.0;
    public double bestEncounterTime = 0.0;
    public Vector3 bestDvE = Vector3.zero;
    public float bestDvMag = 0.0f;

    private const int TOP_MAX = 2;
    private double[] topScore = new double[TOP_MAX];
    private double[] topBurnTime = new double[TOP_MAX];
    private double[] topEncounterTime = new double[TOP_MAX];
    private Vector3[] topDvE = new Vector3[TOP_MAX];
    private float[] topDvMag = new float[TOP_MAX];

    void Start()
    {
        ClearTopCandidates();
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

    public void AbortAutoSolve(byte failStatus)
    {
        solverBusy = false;
        solverPhase = PHASE_IDLE;
        autoValid = false;
        autoStatus = failStatus;
    }

    public void StepAutoSolve()
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


}