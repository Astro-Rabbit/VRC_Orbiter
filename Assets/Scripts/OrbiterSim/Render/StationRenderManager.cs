using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// StationRenderManager
///
/// Renders at most ONE station GameObject at a time in the craft BODY/WORLD frame.
///
/// Assumptions (matches your SkyBoxDriver contract):
/// - Unity WORLD axes == craft BODY axes (craft mesh does not rotate).
/// - Sky/planets are shader-driven using craftAtt.qBE.
/// - contacts snapshot provides station pose in craft body frame:
///     dr_B  (meters)  : station position relative to craft CG, expressed in craft body axes
///     qTargetInB      : station orientation expressed in craft body frame
///
/// This script does NOT compute relative transforms; it only consumes GuidanceNavContactsState.
/// </summary>
public class StationRenderManager : UdonSharpBehaviour
{
    [Header("Inputs")]
    public GuidanceNavContactsState contacts;
    public Transform craftCG;

    [Header("Station render roots (index matches contacts station list)")]
    public GameObject[] stationRenderRoots;

    [Header("Distance culling (meters)")]
    public double renderOnRangeMeters = 100000.0;   // 200 km example
    public double renderOffRangeMeters = 150000.0;  // hysteresis; must be >= on

    [Header("Pose")]
    [Tooltip("If true, apply station pose as world-space (recommended).")]
    public bool useWorldSpace = true;

    [Tooltip("Optional global model fix applied after qTargetInB (rare; prefer per-station prefab).")]
    public Quaternion globalModelFix = Quaternion.identity;

    [Header("Debug")]
    public bool logSwitches = false;

    // internal
    private int _activeIndex = -1;
    private bool _active = false;

    public void Tick()
    {
        if (contacts == null || craftCG == null || stationRenderRoots == null) return;
        int n = stationRenderRoots.Length;
        if (n == 0) return;

        // Choose candidate station to render (prefer selected slot0, then nearest slot1)
        int idx = -1;
        int slot = -1;

        if (contacts.fullValid0)
        {
            int i0 = contacts.fullStationIndex0;
            if (i0 >= 0 && i0 < n)
            {
                double r2 = SafeRange2(i0);
                if (ShouldRender(r2))
                {
                    idx = i0;
                    slot = 0;
                }
            }
        }

        if (idx < 0 && contacts.fullValid1)
        {
            int i1 = contacts.fullStationIndex1;
            if (i1 >= 0 && i1 < n)
            {
                double r2 = SafeRange2(i1);
                if (ShouldRender(r2))
                {
                    idx = i1;
                    slot = 1;
                }
            }
        }

        // Apply active state transitions with hysteresis
        if (_active)
        {
            // If currently active, check OFF condition using off range
            if (_activeIndex >= 0 && _activeIndex < n)
            {
                double r2 = SafeRange2(_activeIndex);
                double off2 = renderOffRangeMeters * renderOffRangeMeters;
                if (r2 > off2 || idx < 0)
                {
                    SetActiveStation(-1);
                }
            }
            else
            {
                SetActiveStation(-1);
            }
        }
        else
        {
            // If inactive, check ON condition
            if (idx >= 0)
                SetActiveStation(idx);
        }

        // If active, update pose from the appropriate full slot
        if (_active && _activeIndex >= 0 && _activeIndex < n)
        {
            if (slot == 0 && _activeIndex == contacts.fullStationIndex0 && contacts.fullValid0)
                ApplyPoseFromSlot0(_activeIndex);
            else if (slot == 1 && _activeIndex == contacts.fullStationIndex1 && contacts.fullValid1)
                ApplyPoseFromSlot1(_activeIndex);
            else
            {
                // Slot mismatch (e.g., selection changed). Re-resolve quickly:
                // Prefer whichever slot matches active index.
                if (_activeIndex == contacts.fullStationIndex0 && contacts.fullValid0) ApplyPoseFromSlot0(_activeIndex);
                else if (_activeIndex == contacts.fullStationIndex1 && contacts.fullValid1) ApplyPoseFromSlot1(_activeIndex);
                // else we lack full data; hide to avoid wrong pose.
                else SetActiveStation(-1);
            }
        }
    }

    private double SafeRange2(int stationIndex)
    {
        if (contacts.range2_m2 == null || stationIndex < 0 || stationIndex >= contacts.range2_m2.Length)
            return double.PositiveInfinity;
        return contacts.range2_m2[stationIndex];
    }

    private bool ShouldRender(double range2)
    {
        double on2 = renderOnRangeMeters * renderOnRangeMeters;
        return range2 <= on2;
    }

    private void SetActiveStation(int newIndex)
    {
        if (newIndex == _activeIndex && ((_active && newIndex >= 0) || (!_active && newIndex < 0)))
            return;

        // Disable all (cheap given small N; you can optimize later)
        for (int i = 0; i < stationRenderRoots.Length; i++)
        {
            GameObject go = stationRenderRoots[i];
            if (go != null) go.SetActive(false);
        }

        if (newIndex >= 0 && newIndex < stationRenderRoots.Length && stationRenderRoots[newIndex] != null)
        {
            stationRenderRoots[newIndex].SetActive(true);
            _activeIndex = newIndex;
            _active = true;
            if (logSwitches) Debug.Log($"[StationRenderManager] Active station = {newIndex}");
        }
        else
        {
            _activeIndex = -1;
            _active = false;
            if (logSwitches) Debug.Log("[StationRenderManager] No active station");
        }
    }

    private void ApplyPoseFromSlot0(int stationIndex)
    {
        GameObject root = stationRenderRoots[stationIndex];
        if (root == null) return;

        Vector3 drB = new Vector3((float)contacts.drx_B0, (float)contacts.dry_B0, (float)contacts.drz_B0);
        Quaternion qTB = contacts.qTargetInB0 * globalModelFix;

        ApplyPose(root.transform, drB, qTB);
    }

    private void ApplyPoseFromSlot1(int stationIndex)
    {
        GameObject root = stationRenderRoots[stationIndex];
        if (root == null) return;

        Vector3 drB = new Vector3((float)contacts.drx_B1, (float)contacts.dry_B1, (float)contacts.drz_B1);
        Quaternion qTB = contacts.qTargetInB1 * globalModelFix;

        ApplyPose(root.transform, drB, qTB);
    }

    private void ApplyPose(Transform stationRoot, Vector3 drB, Quaternion qTargetInB)
    {
        // WORLD == craft BODY, anchored at craftCG
        if (useWorldSpace)
        {
            stationRoot.position = craftCG.position + drB;
            stationRoot.rotation = qTargetInB;
        }
        else
        {
            // If you parent station roots under craftCG, you can set local pose instead.
            stationRoot.localPosition = drB;
            stationRoot.localRotation = qTargetInB;
        }
    }
}