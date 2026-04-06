using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class AttitudeRateLimitConfigSync : UdonSharpBehaviour
{
    [Header("Wiring")]
    public AttitudeControllerPD controller;
    public SimManager simManager;

    [Header("Rate step config")]
    [Tooltip("Each editable step is this many deg/s.")]
    public float stepDegPerSec = 5f;

    [Tooltip("Max editable steps. Final max deg/s = maxSteps * stepDegPerSec.")]
    public int maxSteps = 36;

    [Header("Read-only mirrors")]
    public bool enableLimiter = true;
    public int limitSteps = 2;
    public bool restrictToSimOwner = false;

    [UdonSynced] private bool _enableLimiter = true;
    [UdonSynced] private byte _limitSteps = 2;
    [UdonSynced] private bool _restrictToSimOwner = false;
    [UdonSynced] private uint _rev = 0;

    private uint _appliedRev = 999999999u;

    void Start()
    {
        ApplyLocalMirrors();
        ApplyToController();
    }

    public override void OnDeserialization()
    {
        ApplyLocalMirrors();

        if (_appliedRev == _rev) return;
        _appliedRev = _rev;

        ApplyToController();
    }

    public void ForceApplyNow()
    {
        ApplyLocalMirrors();
        ApplyToController();
    }

    private void ApplyLocalMirrors()
    {
        enableLimiter = _enableLimiter;
        limitSteps = _limitSteps;
        restrictToSimOwner = _restrictToSimOwner;
    }

    private void ApplyToController()
    {
        if (controller == null) return;

        float rateRad = StepsToRad(limitSteps);

        controller.enableMaxAngularRate = enableLimiter;
        controller.maxRateX = rateRad;
        controller.maxRateY = rateRad;
        controller.maxRateZ = rateRad;
    }

    private float StepsToRad(int steps)
    {
        float deg = Mathf.Max(0, steps) * stepDegPerSec;
        return deg * Mathf.Deg2Rad;
    }

    private bool IsLocalSimOwner()
    {
        if (simManager == null) return false;
        return simManager.IsSimOwner();
    }

    private int ClampSteps(int v)
    {
        if (v < 0) return 0;
        if (v > maxSteps) return maxSteps;
        return v;
    }

    // ---------------------------------------------------------------------
    // Public reads
    // ---------------------------------------------------------------------

    public bool GetLimiterEnabled() { return enableLimiter; }
    public bool GetRestrictToSimOwner() { return restrictToSimOwner; }
    public float GetLimitDegPerSec() { return limitSteps * stepDegPerSec; }

    public bool CanLocalUserEdit()
    {
        if (!restrictToSimOwner) return true;
        return IsLocalSimOwner();
    }

    public bool CanLocalUserToggleLock()
    {
        return IsLocalSimOwner();
    }

    public bool IsLockedToOthers()
    {
        return restrictToSimOwner && !IsLocalSimOwner();
    }

    // ---------------------------------------------------------------------
    // Owner-side writes
    // ---------------------------------------------------------------------

    private void OwnerSetLimiterEnabled(bool enabled)
    {
        _enableLimiter = enabled;
        _rev++;

        ApplyLocalMirrors();
        ApplyToController();
        _appliedRev = _rev;

        RequestSerialization();
    }

    private void OwnerAdjustLimitSteps(int delta)
    {
        int next = ClampSteps((int)_limitSteps + delta);
        if (next == (int)_limitSteps) return;

        _limitSteps = (byte)next;
        _rev++;

        ApplyLocalMirrors();
        ApplyToController();
        _appliedRev = _rev;

        RequestSerialization();
    }

    private void OwnerSetRestrictToSimOwner(bool locked)
    {
        _restrictToSimOwner = locked;
        _rev++;

        ApplyLocalMirrors();
        _appliedRev = _rev;

        RequestSerialization();
    }

    // ---------------------------------------------------------------------
    // Network entry points
    // ---------------------------------------------------------------------

    [NetworkCallable]
    public void Net_RequestSetLimiterEnabled(bool enabled)
    {
        if (_restrictToSimOwner && !IsLocalSimOwner()) return;
        OwnerSetLimiterEnabled(enabled);
    }

    [NetworkCallable]
    public void Net_RequestAdjustLimitSteps(int delta)
    {
        if (_restrictToSimOwner && !IsLocalSimOwner()) return;
        OwnerAdjustLimitSteps(delta);
    }

    [NetworkCallable]
    public void Net_RequestSetRestrictToSimOwner(bool locked)
    {
        if (!IsLocalSimOwner()) return;
        OwnerSetRestrictToSimOwner(locked);
    }
}