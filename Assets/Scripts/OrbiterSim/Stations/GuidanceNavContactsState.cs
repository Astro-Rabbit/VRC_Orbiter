using UdonSharp;
using UnityEngine;

/// <summary>
/// GuidanceNavContactsState
/// Snapshot of station contacts relative to the ACTIVE craft for the current frame.
///
/// Always computed for ALL stations:
/// - valid[i]
/// - dr_E (station - craft) in solver inertial SSB
/// - range2, range
///
/// Full computed for up to 2 stations ("promoted slots"):
/// - fullStationIndex[s]
/// - dv_E
/// - dr_B (relative position expressed in craft BODY frame)
/// - qTargetInB (target body orientation expressed in craft BODY frame)
///
/// This is intentionally renderer/GC/orrery agnostic.
/// </summary>
public class GuidanceNavContactsState : UdonSharpBehaviour
{
    [Header("All contacts (arrays sized to stationCount)")]
    public int stationCount = 0;

    public bool[] valid;
    public double[] drx_E;
    public double[] dry_E;
    public double[] drz_E;

    public double[] range2_m2;
    public double[] range_m;

    [Header("Full detail (max 2 promoted stations)")]
    public int maxFull = 2;

    // Station indices for each full slot. -1 = none.
    public int fullStationIndex0 = -1;
    public int fullStationIndex1 = -1;

    public bool fullValid0 = false;
    public bool fullValid1 = false;

    // Full slot 0 (usually selected)
    public double dvx_E0, dvy_E0, dvz_E0;
    public double drx_B0, dry_B0, drz_B0;
    public Quaternion qTargetInB0 = Quaternion.identity;

    // Full slot 1 (usually nearest-other)
    public double dvx_E1, dvy_E1, dvz_E1;
    public double drx_B1, dry_B1, drz_B1;
    public Quaternion qTargetInB1 = Quaternion.identity;

    [Header("Selection (set by UI/GC/etc.)")]
    [Tooltip("Selected station index in the stations[] list. -1 means none.")]
    public int selectedStationIndex = -1;

    public void EnsureSize(int n)
    {
        if (n < 0) n = 0;

        if (valid != null && valid.Length == n &&
            drx_E != null && drx_E.Length == n &&
            range2_m2 != null && range2_m2.Length == n)
        {
            stationCount = n;
            return;
        }

        stationCount = n;
        valid = new bool[n];

        drx_E = new double[n];
        dry_E = new double[n];
        drz_E = new double[n];

        range2_m2 = new double[n];
        range_m = new double[n];
    }

    public void ClearFull()
    {
        fullStationIndex0 = -1;
        fullStationIndex1 = -1;
        fullValid0 = false;
        fullValid1 = false;

        dvx_E0 = dvy_E0 = dvz_E0 = 0.0;
        drx_B0 = dry_B0 = drz_B0 = 0.0;
        qTargetInB0 = Quaternion.identity;

        dvx_E1 = dvy_E1 = dvz_E1 = 0.0;
        drx_B1 = dry_B1 = drz_B1 = 0.0;
        qTargetInB1 = Quaternion.identity;
    }
}