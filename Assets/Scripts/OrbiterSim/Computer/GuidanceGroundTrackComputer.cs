using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// GuidanceGroundTrackComputer
///
/// Builds a future ground-track prediction for the craft over its current primary body.
///
/// V1 scope:
/// - Current primary only
/// - Uses current osculating conic from nav
/// - Uses EphemerisSystem for future body state/orientation sampling
/// - Spherical body lat/lon/alt
/// - Slow refresh cadence by policy
/// - No SOI crossing handling
/// - No terrain intersection
/// - No rendering / map projection responsibilities
///
/// Important:
/// - This is a derived-product computer, not part of the primary simulation step.
/// - It should update at low cadence or on-demand.
/// </summary>
public class GuidanceGroundTrackComputer : UdonSharpBehaviour
{
    [Header("Inputs")]
    public GuidanceNavCoreState nav;
    public EphemerisSystem ephemeris;
    public BodyCatalog bodies;

    [Header("Output")]
    public GC_GroundTrackState track;

    [Header("Auto update")]
    public bool autoUpdate = true;

    [Tooltip("Refresh interval while coasting / conic-dominated.")]
    public float coastUpdateIntervalSec = 60f;

    [Tooltip("Refresh interval while actively integrated / rapidly changing.")]
    public float activeUpdateIntervalSec = 3f;

    [Tooltip("If true, recompute immediately when marked dirty and cooldown permits.")]
    public bool recomputeWhenDirty = true;

    [Tooltip("Minimum real-time spacing between forced recomputes.")]
    public float minForcedRecomputeGapSec = 0.25f;

    [Header("Prediction sampling")]
    [Tooltip("Prediction horizon in mission seconds.")]
    public double horizonSec = 5400.0;

    [Tooltip("Uniform sample spacing in mission seconds.")]
    public double sampleStepSec = 30.0;

    [Tooltip("Maximum sample count written to the output state.")]
    public int maxSamples = 256;

    [Header("Policy")]
    [Tooltip("Only compute track for Earth and Moon for now.")]
    public bool currentPrimaryOnly = true;

    [Tooltip("If true, suppress output when nav is invalid.")]
    public bool requireValidNav = true;

    [Tooltip("Maximum allowed eccentricity for V1 propagation. Hyperbolic is allowed if propagation helper succeeds.")]
    public bool allowHyperbolic = true;

    [Header("Runtime")]
    public bool dirty = true;
    public float lastRecomputeRealtime = -9999f;
    public double lastSolvedMissionTime = -1.0;
    public byte lastSolvedBodyId = 255;

    void Start()
    {
        if (track != null)
        {
            track.maxSamples = maxSamples;
            track.EnsureSize();
            track.Clear();
        }

        dirty = true;
    }

    void Update()
    {
        if (!autoUpdate) return;
        EvaluateIfDue();
    }

    public void MarkDirty()
    {
        dirty = true;
    }

    public void ForceRecompute()
    {
        dirty = true;
        EvaluateTrack();
    }

    public void EvaluateIfDue()
    {
        if (track == null || nav == null) return;

        float nowRT = Time.time;
        float dtRT = nowRT - lastRecomputeRealtime;

        float interval = ChooseUpdateIntervalSec();

        bool dueByTime = (dtRT >= interval);
        bool dueByDirty = dirty && recomputeWhenDirty && (dtRT >= minForcedRecomputeGapSec);

        if (!dueByTime && !dueByDirty) return;

        EvaluateTrack();
    }

    private float ChooseUpdateIntervalSec()
    {
        // V1 heuristic:
        // If nav dt is very small or zero, we cannot really infer sim mode here robustly.
        // Default to coast interval unless caller marks dirty frequently.
        if (nav == null) return coastUpdateIntervalSec;

        // A simple policy hook for later:
        // use faster updates when active primary-relative speed is large and altitude is low,
        // but for now keep it simple and stable.
        return coastUpdateIntervalSec;
    }

    public void EvaluateTrack()
    {
        if (track == null) return;

        track.maxSamples = maxSamples;
        track.EnsureSize();
        track.Clear();

        if (nav == null)
        {
            track.SetStatusInvalid("Missing nav");
            return;
        }

        if (ephemeris == null)
        {
            track.SetStatusInvalid("Missing ephemeris");
            return;
        }

        if (bodies == null)
        {
            track.SetStatusInvalid("Missing body catalog");
            return;
        }

        if (requireValidNav && !nav.valid)
        {
            track.SetStatusInvalid("Nav invalid");
            return;
        }

        byte bodyId = nav.primaryId;

        if (currentPrimaryOnly)
        {
            // V1 body support is whatever ephemeris/body catalog currently supports meaningfully.
            // Here we treat zero radius bodies as unsupported for ground-track use.
            double bodyRadius = bodies.GetRadius(bodyId);
            if (bodyRadius <= 0.0)
            {
                track.SetStatusInvalid("Primary body has no supported radius");
                return;
            }
        }

        if (sampleStepSec <= 0.0)
        {
            track.SetStatusInvalid("sampleStepSec must be > 0");
            return;
        }

        if (horizonSec < 0.0)
        {
            track.SetStatusInvalid("horizonSec must be >= 0");
            return;
        }

        int desiredSamples = 1 + (int)Math.Floor(horizonSec / sampleStepSec);
        if (desiredSamples < 1) desiredSamples = 1;
        if (desiredSamples > maxSamples) desiredSamples = maxSamples;

        double bodyRadiusMeters = bodies.GetRadius(bodyId);
        if (bodyRadiusMeters <= 0.0)
        {
            track.SetStatusInvalid("Body radius invalid");
            return;
        }

        // Conic propagation input from nav's inertial-fit elements.
        double a = nav.a;
        double e = nav.e;
        double inc = nav.iInertialRad;
        double raan = nav.raanInertialRad;
        double argp = nav.argpInertialRad;
        double nu0 = nav.nuRad;
        double mu = nav.muPrimary;
        double t0 = nav.t;

        if (mu <= 0.0)
        {
            track.SetStatusInvalid("Primary mu invalid");
            return;
        }

        if (!allowHyperbolic && e > 1.0)
        {
            track.SetStatusInvalid("Hyperbolic track disabled");
            return;
        }

        track.valid = true;
        track.bodyId = bodyId;
        track.sourceTimeSec = nav.t;
        track.sourceJD = nav.jd;
        track.horizonSec = horizonSec;
        track.sampleStepSec = sampleStepSec;
        track.usedCurrentPrimaryOnly = currentPrimaryOnly;
        track.lastStatus = "OK";

        int written = 0;

        for (int i = 0; i < desiredSamples; i++)
        {
            double tSample = t0 + sampleStepSec * (double)i;

            double rel_rx, rel_ry, rel_rz;
            double rel_vx, rel_vy, rel_vz;

            bool propOk = OrbitHelpers.TryPropagateConicStateFromElements(
                a, e, inc, raan, argp, nu0,
                t0, tSample, mu,
                12, 1e-6,
                out rel_rx, out rel_ry, out rel_rz,
                out rel_vx, out rel_vy, out rel_vz);

            if (!propOk)
            {
                // Keep samples already written; terminate early.
                break;
            }

            double body_rx, body_ry, body_rz;
            double body_vx, body_vy, body_vz;
            double body_ox, body_oy, body_oz;
            Quaternion qPF2E;

            ephemeris.SampleBodyStateAtTime(
                bodyId, tSample,
                out body_rx, out body_ry, out body_rz,
                out body_vx, out body_vy, out body_vz,
                out body_ox, out body_oy, out body_oz,
                out qPF2E);

            // rel_r is already body-centered inertial in solver frame.
            // Convert body-centered inertial -> body-fixed.
            Quaternion qE2PF = Quaternion.Inverse(qPF2E);
            Vector3 rPF = qE2PF * new Vector3((float)rel_rx, (float)rel_ry, (float)rel_rz);

            double x = rPF.x;
            double y = rPF.y;
            double z = rPF.z;

            double r2 = x * x + y * y + z * z;
            if (r2 <= 1e-12)
                break;

            double rMag = Math.Sqrt(r2);
            double alt = rMag - bodyRadiusMeters;

            double latRad = Math.Asin(Clamp(z / rMag, -1.0, 1.0));
            double lonRad = Math.Atan2(y, x);

            track.tSec[written] = tSample;
            track.latDeg[written] = latRad * 180.0 / Math.PI;
            track.lonDeg[written] = lonRad * 180.0 / Math.PI;
            track.altMeters[written] = alt;

            Vector3 u = (rPF.sqrMagnitude > 1e-12f) ? rPF.normalized : Vector3.zero;
            track.bodyFixedUnit[written] = u;

            written++;
        }

        track.sampleCount = written;
        track.valid = (written > 0);

        if (!track.valid)
            track.lastStatus = "No valid propagated samples";

        lastRecomputeRealtime = Time.time;
        lastSolvedMissionTime = nav.t;
        lastSolvedBodyId = bodyId;
        dirty = false;
    }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }
}