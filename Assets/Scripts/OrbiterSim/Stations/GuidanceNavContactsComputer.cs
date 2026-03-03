using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// GuidanceNavContactsComputer
/// Computes per-frame station contact information relative to craft.
///
/// Design:
/// - Pass A (all stations): dr_E + range2 (+ optional range)
/// - Pass B (full detail): up to 2 stations:
///     slot0 = selected station (if valid)
///     slot1 = nearest other station within fullDetailRangeMeters (optional)
///
/// Call this AFTER:
/// - stations have been evaluated for this frame (rails @ Tview)
/// - craft state + attitude have been finalized for this frame
/// </summary>
public class GuidanceNavContactsComputer : UdonSharpBehaviour
{
    [Header("Inputs")]
    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;
    public StationStateModel[] stations;

    [Header("Output")]
    public GuidanceNavContactsState contacts;

    [Header("Policy")]
    public bool computeRange = true;

    [Tooltip("Only consider non-selected stations within this distance for slot1 full-detail.")]
    public double fullDetailRangeMeters = 500000.0; // 500 km default

    [Tooltip("If true, slot1 will always pick nearest-other even if far (ignores fullDetailRangeMeters).")]
    public bool allowSlot1BeyondRange = false;

    [Header("Debug")]
    public bool logMissing = false;

    public void Evaluate()
    {
        if (craft == null || craftAtt == null || contacts == null || stations == null)
        {
            if (logMissing) Debug.Log("[GuidanceNavContactsComputer] Missing references.");
            return;
        }

        int n = stations.Length;
        contacts.EnsureSize(n);
        contacts.ClearFull();

        // Craft SSB inertial
        double crx = craft.rx, cry = craft.ry, crz = craft.rz;
        double cvx = craft.vx, cvy = craft.vy, cvz = craft.vz;

        // Pass A: dr_E + range2 for all stations
        for (int i = 0; i < n; i++)
        {
            StationStateModel st = stations[i];
            bool ok = (st != null && st.valid);

            contacts.valid[i] = ok;

            if (!ok)
            {
                contacts.drx_E[i] = contacts.dry_E[i] = contacts.drz_E[i] = 0.0;
                contacts.range2_m2[i] = double.PositiveInfinity;
                contacts.range_m[i] = double.PositiveInfinity;
                continue;
            }

            double drx = st.rx - crx;
            double dry = st.ry - cry;
            double drz = st.rz - crz;

            contacts.drx_E[i] = drx;
            contacts.dry_E[i] = dry;
            contacts.drz_E[i] = drz;

            double r2 = drx * drx + dry * dry + drz * drz;
            contacts.range2_m2[i] = r2;

            if (computeRange)
                contacts.range_m[i] = Math.Sqrt(r2);
            else
                contacts.range_m[i] = 0.0;
        }

        // Determine slot0 (selected)
        int sel = contacts.selectedStationIndex;
        if (sel >= 0 && sel < n && contacts.valid[sel])
        {
            FillFullSlot0(sel);
        }

        // Determine slot1 (nearest other)
        int idx1 = FindNearestOther(sel);
        if (idx1 >= 0)
        {
            FillFullSlot1(idx1);
        }
    }

    private int FindNearestOther(int selectedIndex)
    {
        int n = stations.Length;
        double bestR2 = double.PositiveInfinity;
        int bestIdx = -1;

        double maxR2 = fullDetailRangeMeters * fullDetailRangeMeters;

        for (int i = 0; i < n; i++)
        {
            if (!contacts.valid[i]) continue;
            if (i == selectedIndex) continue;

            double r2 = contacts.range2_m2[i];
            if (!allowSlot1BeyondRange && r2 > maxR2) continue;

            if (r2 < bestR2)
            {
                bestR2 = r2;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    private void FillFullSlot0(int stationIndex)
    {
        StationStateModel st = stations[stationIndex];

        contacts.fullStationIndex0 = stationIndex;
        contacts.fullValid0 = true;

        // dv_E
        contacts.dvx_E0 = st.vx - craft.vx;
        contacts.dvy_E0 = st.vy - craft.vy;
        contacts.dvz_E0 = st.vz - craft.vz;

        // dr_B (craft body frame)
        double drxE = contacts.drx_E[stationIndex];
        double dryE = contacts.dry_E[stationIndex];
        double drzE = contacts.drz_E[stationIndex];
        RotateEToBody(craftAtt.qBE, drxE, dryE, drzE, out contacts.drx_B0, out contacts.dry_B0, out contacts.drz_B0);

        // qTargetInB = inv(qCraftBE) * qTargetBE
        Quaternion qC = craftAtt.qBE;
        Quaternion qT = st.q_B2E;
        contacts.qTargetInB0 = Quaternion.Inverse(qC) * qT;
    }

    private void FillFullSlot1(int stationIndex)
    {
        StationStateModel st = stations[stationIndex];

        contacts.fullStationIndex1 = stationIndex;
        contacts.fullValid1 = true;

        contacts.dvx_E1 = st.vx - craft.vx;
        contacts.dvy_E1 = st.vy - craft.vy;
        contacts.dvz_E1 = st.vz - craft.vz;

        double drxE = contacts.drx_E[stationIndex];
        double dryE = contacts.dry_E[stationIndex];
        double drzE = contacts.drz_E[stationIndex];
        RotateEToBody(craftAtt.qBE, drxE, dryE, drzE, out contacts.drx_B1, out contacts.dry_B1, out contacts.drz_B1);

        Quaternion qC = craftAtt.qBE;
        Quaternion qT = st.q_B2E;
        contacts.qTargetInB1 = Quaternion.Inverse(qC) * qT;
    }

    // Convert an inertial E-frame vector into craft body frame using qBE (body->E).
    private static void RotateEToBody(Quaternion qBE, double vxE, double vyE, double vzE,
                                     out double vxB, out double vyB, out double vzB)
    {
        Quaternion qEB = Quaternion.Inverse(qBE);
        Vector3 vB = qEB * new Vector3((float)vxE, (float)vyE, (float)vzE);
        vxB = (double)vB.x;
        vyB = (double)vB.y;
        vzB = (double)vB.z;
    }
}