using UdonSharp;
using UnityEngine;

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

    [Tooltip("Selected docking port on the selected station. -1 means none.")]
    public int selectedStationDockPortIndex = 0;

    [Tooltip("Selected craft docking port. -1 means none.")]
    public int selectedCraftDockPortIndex = 0;

    // --------------------------------------------------------------------
    // Docking targeting outputs (computed; targeting only, no latch logic)
    // Frames:
    // - All *_B values are in craft BODY frame (Unity world in your render model), relative to craft CG origin.
    // - qTargetPortInB: target station port frame expressed in craft BODY frame.
    // - qCraftPort_B:   craft port frame expressed in craft BODY frame.
    // --------------------------------------------------------------------
    [Header("Docking target (computed)")]
    [Tooltip("If true, compute docking targeting for slot1 too (usually unnecessary).")]
    public bool computeDockForSlot1 = false;

    // Slot 0 docking (selected station)
    public bool dockValid0 = false;
    public int dockStationPortIndex0 = -1;
    public int dockCraftPortIndex0 = -1;

    public double targetPort_px_B0, targetPort_py_B0, targetPort_pz_B0;
    public Quaternion qTargetPortInB0 = Quaternion.identity;

    public double craftPort_px_B0, craftPort_py_B0, craftPort_pz_B0;
    public Quaternion qCraftPort_B0 = Quaternion.identity;

    public double dockErr_px_B0, dockErr_py_B0, dockErr_pz_B0;
    public Quaternion qDockErr0 = Quaternion.identity; // inv(craftPort) * targetPort

    // Slot 1 docking (optional)
    public bool dockValid1 = false;
    public int dockStationPortIndex1 = -1;
    public int dockCraftPortIndex1 = -1;

    public double targetPort_px_B1, targetPort_py_B1, targetPort_pz_B1;
    public Quaternion qTargetPortInB1 = Quaternion.identity;

    public double craftPort_px_B1, craftPort_py_B1, craftPort_pz_B1;
    public Quaternion qCraftPort_B1 = Quaternion.identity;

    public double dockErr_px_B1, dockErr_py_B1, dockErr_pz_B1;
    public Quaternion qDockErr1 = Quaternion.identity;

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

        ClearDocking();
    }

    public void ClearDocking()
    {
        dockValid0 = false;
        dockStationPortIndex0 = -1;
        dockCraftPortIndex0 = -1;
        targetPort_px_B0 = targetPort_py_B0 = targetPort_pz_B0 = 0.0;
        craftPort_px_B0  = craftPort_py_B0  = craftPort_pz_B0  = 0.0;
        qTargetPortInB0 = Quaternion.identity;
        qCraftPort_B0 = Quaternion.identity;
        dockErr_px_B0 = dockErr_py_B0 = dockErr_pz_B0 = 0.0;
        qDockErr0 = Quaternion.identity;

        dockValid1 = false;
        dockStationPortIndex1 = -1;
        dockCraftPortIndex1 = -1;
        targetPort_px_B1 = targetPort_py_B1 = targetPort_pz_B1 = 0.0;
        craftPort_px_B1  = craftPort_py_B1  = craftPort_pz_B1  = 0.0;
        qTargetPortInB1 = Quaternion.identity;
        qCraftPort_B1 = Quaternion.identity;
        dockErr_px_B1 = dockErr_py_B1 = dockErr_pz_B1 = 0.0;
        qDockErr1 = Quaternion.identity;
    }
}