using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// StationStateReadout
/// Debug/telemetry readout for a StationStateModel (no rendering).
///
/// - Displays primary-relative and SSB state
/// - Computes basic derived values (rr magnitude, speed, altitude)
/// - Optional throttled Debug.Log printing
/// </summary>
public class StationStateReadout : UdonSharpBehaviour
{
    [Header("References")]
    public StationStateModel station;
    public BodyCatalog bodies; // optional (for radius/altitude); can be null

    [Header("Update")]
    [Tooltip("If true, updates the fields every Update().")]
    public bool updateContinuously = true;

    [Tooltip("If > 0, only update every N seconds (reduces inspector churn).")]
    public float updateEverySeconds = 0.10f;

    [Header("Logging")]
    public bool logToConsole = false;

    [Tooltip("If logging, print at most every N seconds.")]
    public float logEverySeconds = 1.0f;

    // Internal timers
    private float _tUpdate = 0f;
    private float _tLog = 0f;

    // --------------------
    // Inspector outputs
    // --------------------
    [Header("Validity")]
    public bool valid;
    public byte primaryBodyId;

    [Header("Primary-relative (rr) meters")]
    public double rrx, rry, rrz;

    [Header("Primary-relative (rv) m/s")]
    public double rvx, rvy, rvz;

    [Header("SSB inertial (r) meters")]
    public double rx, ry, rz;

    [Header("SSB inertial (v) m/s")]
    public double vx, vy, vz;

    [Header("Attitude (body -> inertial)")]
    public Quaternion q_B2E;

    [Header("Derived (primary-relative)")]
    public double rMag_m;
    public double vMag_mps;
    public double altitude_m;  // above primary radius if available
    public double radius_m;    // primary radius used (0 if unknown)

    void Update()
    {
        if (!updateContinuously) return;

        float dt = Time.deltaTime;
        _tUpdate += dt;
        _tLog += dt;

        float updPeriod = Mathf.Max(0.0f, updateEverySeconds);
        if (updPeriod > 0.0f && _tUpdate < updPeriod) return;
        _tUpdate = 0f;

        RefreshReadout();

        if (logToConsole)
        {
            float logPeriod = Mathf.Max(0.1f, logEverySeconds);
            if (_tLog >= logPeriod)
            {
                _tLog = 0f;
                Debug.Log(BuildLogLine());
            }
        }
    }

    [ContextMenu("Refresh Now")]
    public void RefreshNow()
    {
        RefreshReadout();
        if (logToConsole) Debug.Log(BuildLogLine());
    }

    private void RefreshReadout()
    {
        if (station == null)
        {
            valid = false;
            return;
        }

        valid = station.valid;
        primaryBodyId = station.primaryBodyId;

        rrx = station.rrx; rry = station.rry; rrz = station.rrz;
        rvx = station.rvx; rvy = station.rvy; rvz = station.rvz;

        rx = station.rx; ry = station.ry; rz = station.rz;
        vx = station.vx; vy = station.vy; vz = station.vz;

        q_B2E = station.q_B2E;

        // Derived magnitudes (primary-relative)
        rMag_m = Math.Sqrt(rrx * rrx + rry * rry + rrz * rrz);
        vMag_mps = Math.Sqrt(rvx * rvx + rvy * rvy + rvz * rvz);

        radius_m = 0.0;
        altitude_m = 0.0;

        if (bodies != null)
        {
            radius_m = bodies.GetRadius(primaryBodyId);
            if (radius_m > 0.0)
                altitude_m = rMag_m - radius_m;
        }
    }

    private string BuildLogLine()
    {
        if (station == null) return "[StationReadout] station=null";

        // Keep log compact and easy to sanity-check
        return
            $"[StationReadout] valid={station.valid} pid={station.primaryBodyId} " +
            $"| rr=({station.rrx:F1},{station.rry:F1},{station.rrz:F1}) m " +
            $"| |rr|={rMag_m:F1} m alt={altitude_m:F1} m " +
            $"| rv=({station.rvx:F3},{station.rvy:F3},{station.rvz:F3}) m/s | |rv|={vMag_mps:F3} m/s " +
            $"| rSSB=({station.rx:F1},{station.ry:F1},{station.rz:F1}) m";
    }
}