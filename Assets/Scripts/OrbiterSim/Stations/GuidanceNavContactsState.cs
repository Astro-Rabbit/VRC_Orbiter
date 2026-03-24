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

    [Header("Render-only smoothed full-slot pose (presentation only)")]
    public bool renderFullValid0 = false;
    public bool renderFullValid1 = false;

    public double render_drx_B0, render_dry_B0, render_drz_B0;
    public double render_drx_B1, render_dry_B1, render_drz_B1;

    [Header("Selection (set by UI/GC/etc.)")]
    [Tooltip("Selected station index in the stations[] list. -1 means none.")]
    public int selectedStationIndex = -1;

    [Tooltip("Selected docking port on the selected station. -1 means none.")]
    public int selectedStationDockPortIndex = -1;

    [Tooltip("Selected craft docking port. -1 means none.")]
    public int selectedCraftDockPortIndex = 0;

    [Header("Selected station root relative (computed)")]
    public bool selValid = false;
    public double sel_drx_E, sel_dry_E, sel_drz_E;
    public double sel_drx_B, sel_dry_B, sel_drz_B;
    public double sel_dvx_E, sel_dvy_E, sel_dvz_E;

    [Header("Docking target (computed)")]
    [Tooltip("If true, compute docking targeting for slot1 too (usually unnecessary).")]
    public bool computeDockForSlot1 = false;

    // Slot 0 docking (selected station)
    public bool dockValid0 = false;
    public int dockStationPortIndex0 = -1;
    public int dockCraftPortIndex0 = -1;

    public double targetPort_px_B0, targetPort_py_B0, targetPort_pz_B0;
    public Quaternion qTargetPortInB0 = Quaternion.identity;
    public Quaternion qTargetPort_E0 = Quaternion.identity;

    public double craftPort_px_B0, craftPort_py_B0, craftPort_pz_B0;
    public Quaternion qCraftPort_B0 = Quaternion.identity;

    public double dockErr_px_B0, dockErr_py_B0, dockErr_pz_B0;
    public Quaternion qDockErr0 = Quaternion.identity;

    // Slot 1 docking (optional)
    public bool dockValid1 = false;
    public int dockStationPortIndex1 = -1;
    public int dockCraftPortIndex1 = -1;

    public double targetPort_px_B1, targetPort_py_B1, targetPort_pz_B1;
    public Quaternion qTargetPortInB1 = Quaternion.identity;
    public Quaternion qTargetPort_E1 = Quaternion.identity;
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

        renderFullValid0 = false;
        renderFullValid1 = false;
        render_drx_B0 = render_dry_B0 = render_drz_B0 = 0.0;
        render_drx_B1 = render_dry_B1 = render_drz_B1 = 0.0;

        ClearDocking();
    }

    public void SetSelection(int stationIndex, int stationDockPortIndex)
    {
        selectedStationIndex = stationIndex;
        selectedStationDockPortIndex = stationDockPortIndex;
        selectedCraftDockPortIndex = 0;
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

        qTargetPort_E0 = Quaternion.identity;
        qTargetPort_E1 = Quaternion.identity;
    }
}