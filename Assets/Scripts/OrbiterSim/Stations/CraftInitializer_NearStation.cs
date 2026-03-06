
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using System;

/// <summary>
/// CraftInitializer_NearStation
///
/// Purpose:
/// - Initialize the ACTIVE craft state (CraftStateModel + CraftConic) by spawning near a rails station.
/// - Uses a deterministic epoch T0 = clock.Now() and forces ephemeris + station state evaluation at T0.
/// - Offsets the craft from the station in the station's RTN frame (R/T/N) by a configurable amount.
/// - Writes craft SSB (heliocentric/SSB inertial solver frame) r/v.
/// - Sets craft.primaryBodyId to station primary.
/// - Calls ConicFitter.Fit(primaryId, T0) to make rails conic consistent with the initialized state.
///
/// Notes:
/// - This is rails-only initialization. It does NOT create a docking constraint.
/// - If you match station velocity and offset position, you will generally see relative drift over time (expected).
/// </summary>
public class CraftInitializer_NearStation : UdonSharpBehaviour
{
    // Axis selector (byte; no enums for Udon friendliness)
    public const byte AXIS_R = 0;
    public const byte AXIS_T = 1;
    public const byte AXIS_N = 2;

    [Header("Core refs")]
    public SimClock clock;
    public EphemerisSystem ephem;
    public BodyCatalog bodies;

    [Header("Station source (must be able to evaluate station at T0)")]
    public StationPropSystem stationProp;     // preferred: lets us force Evaluate(T0)
    public StationStateModel stationState;    // optional: used for reading after Evaluate

    [Header("Craft target")]
    public CraftStateModel craft;
    public ConicFitter conicFitter;           // required to make rails conic consistent
    public ConicState craftConic;             // optional: if you want to force-valid or set primary
    public CraftNetState netCore;             // optional: if you want this init to force rails mode

    [Header("Offset (RTN frame about station)")]
    public byte offsetAxis = AXIS_R;
    public double offsetMeters = 100.0;
    public double offsetSign = +1.0;          // +1 or -1

    [Header("Relative velocity (RTN)")]
    public bool matchStationVelocity = true;  // default true
    public double dV_R = 0.0;                 // m/s
    public double dV_T = 0.0;                 // m/s
    public double dV_N = 0.0;                 // m/s

    [Header("Safety / fallback")]
    public bool fallbackToNoInitIfInvalid = true;
    public bool log = true;

    public bool InitializeNow()
    {
        if (clock == null || ephem == null || bodies == null || craft == null || conicFitter == null)
        {
            if (log) Debug.Log("[CraftInitializer_NearStation] Missing core refs.");
            return false;
        }
        if (stationProp == null && stationState == null)
        {
            if (log) Debug.Log("[CraftInitializer_NearStation] Missing stationProp/stationState.");
            return false;
        }

        double T0 = clock.Now();

        // 1) Force ephemeris to T0 so BodyCatalog is coherent
        ephem.Evaluate(T0);

        // 2) Force station evaluation at T0 (preferred)
        if (stationProp != null) stationProp.Evaluate(T0);

        StationStateModel st = stationState != null ? stationState :
                               (stationProp != null ? stationProp.station : null);

        if (st == null || !st.valid)
        {
            if (log) Debug.Log("[CraftInitializer_NearStation] Station state invalid at init epoch.");
            return !fallbackToNoInitIfInvalid; // false if we require station
        }

        byte pid = st.primaryBodyId;

        // 3) Build RTN basis from station PRIMARY-relative rr/rv (solver inertial axes)
        // R = normalize(rr), N = normalize(rr x rv), T = N x R
        double Rhat_x, Rhat_y, Rhat_z;
        if (!TryNormalize(st.rrx, st.rry, st.rrz, out Rhat_x, out Rhat_y, out Rhat_z))
        {
            if (log) Debug.Log("[CraftInitializer_NearStation] Degenerate station rr for RTN.");
            return false;
        }

        double hx = st.rry * st.rvz - st.rrz * st.rvy;
        double hy = st.rrz * st.rvx - st.rrx * st.rvz;
        double hz = st.rrx * st.rvy - st.rry * st.rvx;

        double Nhat_x, Nhat_y, Nhat_z;
        if (!TryNormalize(hx, hy, hz, out Nhat_x, out Nhat_y, out Nhat_z))
        {
            if (log) Debug.Log("[CraftInitializer_NearStation] Degenerate station h = rr x rv for RTN.");
            return false;
        }

        // T = N x R
        double That_x = Nhat_y * Rhat_z - Nhat_z * Rhat_y;
        double That_y = Nhat_z * Rhat_x - Nhat_x * Rhat_z;
        double That_z = Nhat_x * Rhat_y - Nhat_y * Rhat_x;

        if (!TryNormalize(That_x, That_y, That_z, out That_x, out That_y, out That_z))
        {
            if (log) Debug.Log("[CraftInitializer_NearStation] Degenerate station T = N x R for RTN.");
            return false;
        }

        // 4) Position offset in RTN
        double s = offsetSign;
        double drx = 0, dry = 0, drz = 0;
        double mag = s * offsetMeters;

        if (offsetAxis == AXIS_R)
        {
            drx = mag * Rhat_x; dry = mag * Rhat_y; drz = mag * Rhat_z;
        }
        else if (offsetAxis == AXIS_T)
        {
            drx = mag * That_x; dry = mag * That_y; drz = mag * That_z;
        }
        else // AXIS_N
        {
            drx = mag * Nhat_x; dry = mag * Nhat_y; drz = mag * Nhat_z;
        }

        // Craft primary-relative r = station rr + dr
        double c_rrx = st.rrx + drx;
        double c_rry = st.rry + dry;
        double c_rrz = st.rrz + drz;

        // 5) Velocity: match station + optional RTN delta-V
        double c_rvx = st.rvx;
        double c_rvy = st.rvy;
        double c_rvz = st.rvz;

        if (!matchStationVelocity)
        {
            // If not matching, start with zero relative velocity in primary frame (rarely useful)
            c_rvx = 0.0; c_rvy = 0.0; c_rvz = 0.0;
        }

        // Apply RTN delta-V in inertial axes
        c_rvx += dV_R * Rhat_x + dV_T * That_x + dV_N * Nhat_x;
        c_rvy += dV_R * Rhat_y + dV_T * That_y + dV_N * Nhat_y;
        c_rvz += dV_R * Rhat_z + dV_T * That_z + dV_N * Nhat_z;

        // 6) Compose to SSB and write craft state
        bodies.FromPrimaryRelative(pid, c_rrx, c_rry, c_rrz, c_rvx, c_rvy, c_rvz, craft);
        craft.primaryBodyId = pid;

        // 7) Fit rails conic at T0 so craftProp rails will reproduce this state
        conicFitter.Fit(pid, T0);

        // Optional: force craft conic primary id (if you keep it separate)
        if (craftConic != null)
            craftConic.primaryBodyId = pid;

        // Optional: force rails mode immediately (owner only)
        if (netCore != null && Networking.IsOwner(netCore.gameObject))
        {
            netCore.SetMode(SimManager.MODE_RAILS, pid, true);
            netCore.ForcePublishCore();
        }

        if (log)
        {
            Debug.Log($"[CraftInitializer_NearStation] Init T0={T0:F2}s pid={pid} offset={offsetMeters:F1}m axis={offsetAxis} sign={offsetSign:+0.0;-0.0}");
        }

        return true;
    }

    private static bool TryNormalize(double x, double y, double z, out double ox, out double oy, out double oz)
    {
        double m2 = x * x + y * y + z * z;
        if (m2 < 1e-18)
        {
            ox = oy = oz = 0.0;
            return false;
        }
        double inv = 1.0 / Math.Sqrt(m2);
        ox = x * inv; oy = y * inv; oz = z * inv;
        return true;
    }
}