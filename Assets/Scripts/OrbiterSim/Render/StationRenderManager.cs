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
/// - GuidanceNavContactsState provides station pose in craft body frame:
///     dr_B        : station position relative to craft CG, expressed in craft body axes
///     qTargetInB  : station orientation expressed in craft body frame
///
/// This script is PRESENTATION ONLY:
/// - It does not compute relative transforms.
/// - It may optionally smooth the rendered relative pose.
/// - Raw contact truth remains in GuidanceNavContactsComputer / GuidanceNavContactsState.
/// </summary>
public class StationRenderManager : UdonSharpBehaviour
{
    [Header("Inputs")]
    public GuidanceNavContactsState contacts;
    public Transform craftCG;

    [Header("Station render roots (index matches contacts station list)")]
    public GameObject[] stationRenderRoots;

    [Header("Distance culling (meters)")]
    public double renderOnRangeMeters = 100000.0;
    public double renderOffRangeMeters = 150000.0;

    [Header("Pose")]
    [Tooltip("If true, apply station pose as world-space (recommended).")]
    public bool useWorldSpace = true;

    [Tooltip("Optional global model fix applied after qTargetInB (rare; prefer per-station prefab).")]
    public Quaternion globalModelFix = Quaternion.identity;

    [Header("Body presentation mapping")]
    [Tooltip("Match rendered body-frame convention used by the skybox/cockpit presentation.")]
    public bool flipPresentationX = true;


    [Header("Visual smoothing")]
    [Tooltip("If true, smooth rendered station relative position in craft/body space.")]
    public bool smoothRelativePosition = true;

    [Tooltip("Position smoothing time constant (seconds). Smaller = tighter, less lag.")]
    public float positionSmoothTimeSeconds = 0.10f;

    [Tooltip("If relative position jump exceeds this, snap instead of smoothing (meters). 0 disables snapping.")]
    public float positionSnapDistanceMeters = 500f;

    [Tooltip("If true, also smooth rendered station rotation.")]
    public bool smoothRelativeRotation = false;

    [Tooltip("Rotation smoothing gain in 1/seconds. Higher = tighter.")]
    public float rotationLerpRate = 12f;



    [Header("Debug")]
    public bool logSwitches = false;

    [Header("Read-only smoothing debug")]
    public Vector3 smoothedDrB;
    public Quaternion smoothedQTargetInB = Quaternion.identity;
    public float dbgPositionErrorMeters = 0f;

    // internal active station state
    private int _activeIndex = -1;
    private bool _active = false;

    // smoothing state for currently active rendered station
    private bool _poseInitialized = false;
    private Vector3 _smoothVelB = Vector3.zero;

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
                if (_activeIndex == contacts.fullStationIndex0 && contacts.fullValid0) ApplyPoseFromSlot0(_activeIndex);
                else if (_activeIndex == contacts.fullStationIndex1 && contacts.fullValid1) ApplyPoseFromSlot1(_activeIndex);
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

        // Disable all
        for (int i = 0; i < stationRenderRoots.Length; i++)
        {
            GameObject go = stationRenderRoots[i];
            if (go != null) go.SetActive(false);
        }

        // Reset smoothing whenever active rendered station changes
        _poseInitialized = false;
        _smoothVelB = Vector3.zero;
        dbgPositionErrorMeters = 0f;

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

        Vector3 drB;
        if (contacts.renderFullValid0)
            drB = new Vector3((float)contacts.render_drx_B0, (float)contacts.render_dry_B0, (float)contacts.render_drz_B0);
        else
            drB = new Vector3((float)contacts.drx_B0, (float)contacts.dry_B0, (float)contacts.drz_B0);

        Quaternion qTB = contacts.qTargetInB0 * globalModelFix;
        ApplyPose(root.transform, drB, qTB);
    }

    private void ApplyPoseFromSlot1(int stationIndex)
    {
        GameObject root = stationRenderRoots[stationIndex];
        if (root == null) return;

        Vector3 drB;
        if (contacts.renderFullValid1)
            drB = new Vector3((float)contacts.render_drx_B1, (float)contacts.render_dry_B1, (float)contacts.render_drz_B1);
        else
            drB = new Vector3((float)contacts.drx_B1, (float)contacts.dry_B1, (float)contacts.drz_B1);

        Quaternion qTB = contacts.qTargetInB1 * globalModelFix;
        ApplyPose(root.transform, drB, qTB);
    }

    private void ApplyPoseSmoothed(Transform stationRoot, Vector3 targetDrB, Quaternion targetQTargetInB)
    {
        float dt = Time.deltaTime;
        if (dt < 0f) dt = 0f;
        if (dt > 0.25f) dt = 0.25f;

        // Initialize on first valid frame after activation/switch
        if (!_poseInitialized)
        {
            smoothedDrB = targetDrB;
            smoothedQTargetInB = targetQTargetInB;
            _smoothVelB = Vector3.zero;
            _poseInitialized = true;

            dbgPositionErrorMeters = 0f;
            ApplyPose(stationRoot, smoothedDrB, smoothedQTargetInB);
            return;
        }

        // --- Position smoothing ---
        if (!smoothRelativePosition)
        {
            smoothedDrB = targetDrB;
            _smoothVelB = Vector3.zero;
            dbgPositionErrorMeters = 0f;
        }
        else
        {
            Vector3 err = targetDrB - smoothedDrB;
            float errMag = err.magnitude;
            dbgPositionErrorMeters = errMag;

            if (positionSnapDistanceMeters > 0f && errMag > positionSnapDistanceMeters)
            {
                smoothedDrB = targetDrB;
                _smoothVelB = Vector3.zero;
            }
            else
            {
                float smoothT = positionSmoothTimeSeconds;
                if (smoothT < 0.0001f) smoothT = 0.0001f;

                smoothedDrB = Vector3.SmoothDamp(
                    smoothedDrB,
                    targetDrB,
                    ref _smoothVelB,
                    smoothT,
                    Mathf.Infinity,
                    dt
                );
            }
        }

        // --- Rotation smoothing ---
        if (!smoothRelativeRotation)
        {
            smoothedQTargetInB = targetQTargetInB;
        }
        else
        {
            float rate = rotationLerpRate;
            if (rate < 0f) rate = 0f;

            float alpha = 1f - Mathf.Exp(-rate * dt);
            smoothedQTargetInB = Quaternion.Slerp(smoothedQTargetInB, targetQTargetInB, alpha);
        }

        ApplyPose(stationRoot, smoothedDrB, smoothedQTargetInB);
    }

    private void ApplyPose(Transform stationRoot, Vector3 drB, Quaternion qTargetInB)
    {
        Vector3 drRender = MapBodyVectorToRender(drB);
        Quaternion qRender = MapBodyRotationToRender(qTargetInB);

        // WORLD == craft BODY presentation frame, anchored at craftCG
        if (useWorldSpace)
        {
            stationRoot.position = craftCG.position + drRender;
            stationRoot.rotation = qRender;
        }
        else
        {
            stationRoot.localPosition = drRender;
            stationRoot.localRotation = qRender;
        }
    }

    private Vector3 MapBodyVectorToRender(Vector3 v)
    {
        if (!flipPresentationX) return v;
        return new Vector3(-v.x, v.y, v.z);
    }

    private Quaternion MapBodyRotationToRender(Quaternion qBody)
    {
        if (!flipPresentationX) return qBody;

        // Take body-frame basis vectors from the sim/body quaternion
        Vector3 x = qBody * Vector3.right;
        Vector3 y = qBody * Vector3.up;
        Vector3 z = qBody * Vector3.forward;

        // Apply the same body-presentation mapping used for positions
        x = MapBodyVectorToRender(x);
        y = MapBodyVectorToRender(y);
        z = MapBodyVectorToRender(z);

        // Rebuild a proper rotation from mapped basis
        x = SafeNormalize(x, Vector3.right);
        y = SafeNormalize(y, Vector3.up);
        z = SafeNormalize(z, Vector3.forward);

        // Re-orthonormalize
        z = SafeNormalize(z, Vector3.forward);
        x = SafeNormalize(Vector3.Cross(y, z), Vector3.right);
        y = SafeNormalize(Vector3.Cross(z, x), Vector3.up);

        return Quaternion.LookRotation(z, y);
    }

    private Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float m = v.magnitude;
        if (m < 1e-8f) return fallback;
        return v / m;
    }

}