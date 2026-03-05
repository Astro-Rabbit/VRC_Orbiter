using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class TestingWarpUiController : UdonSharpBehaviour
{
    [Header("References")]
    public SimClock clock;
    [Tooltip("Optional: used to check integrated/rails policy.")]
    public SimManager simManager;

    [Header("Warp Step")]
    [Tooltip("Multiplicative factor per button press (e.g., 5 or 10).")]
    public double stepFactor = 10.0;

    [Header("Limits")]
    [Tooltip("Minimum warp. Per your request, keep this at 1.")]
    public double minWarp = 1.0;

    [Tooltip("Maximum warp allowed via UI.")]
    public double maxWarp = 10000.0;

    [Header("Behavior")]
    [Tooltip("If true, block warp changes while in integrated mode (except Reset->1).")]
    public bool blockWhileIntegrated = true;

    [Tooltip("If true, print debug logs when warp changes or is blocked.")]
    public bool debugLogs = true;

    // ----------------------------
    // Unity UI Button hooks
    // ----------------------------

    public void WarpIncrease()
    {
        if (!Validate()) return;

        if (IsIntegratedMode() && blockWhileIntegrated)
        {
            // Integrated must be x1 by your SimManager policy; don't let UI fight it.
            ForceWarp1WithLog("[Warp] Increase blocked in INTEGRATED; forcing x1.");
            return;
        }

        double cur = GetCurrentWarp();
        double next = cur * Mathf.Max(1f, (float)stepFactor);
        next = ClampWarp(next);

        ApplyWarp(next, "Increase");
    }

    public void WarpDecrease()
    {
        if (!Validate()) return;

        // Decreasing is always safe; but still respect integrated policy if you want it strict.
        if (IsIntegratedMode() && blockWhileIntegrated)
        {
            ForceWarp1WithLog("[Warp] Decrease blocked in INTEGRATED; forcing x1.");
            return;
        }

        double cur = GetCurrentWarp();
        double sf = System.Math.Max(1.0, stepFactor);
        double next = cur / sf;
        next = ClampWarp(next);

        ApplyWarp(next, "Decrease");
    }

    public void WarpReset()
    {
        if (!Validate()) return;

        ApplyWarp(1.0, "Reset");
    }

    // ----------------------------
    // Internals
    // ----------------------------

    private bool Validate()
    {
        if (clock == null)
        {
            if (debugLogs) Debug.LogWarning("[Warp] No SimClock reference.");
            return false;
        }

        if (!CanControlWarp())
        {
            if (debugLogs) Debug.LogWarning("[Warp] You do not own the SimClock; cannot change warp.");
            return false;
        }

        // Enforce the user's constraint always
        if (minWarp < 1.0) minWarp = 1.0;
        if (maxWarp < minWarp) maxWarp = minWarp;

        if (stepFactor < 1.0001) stepFactor = 2.0; // sane default if misconfigured

        return true;
    }

    private bool CanControlWarp()
    {
        // If network time is enabled, only the owner of the SimClock object should change warp.
        if (clock.useNetworkTime)
            return Networking.IsOwner(clock.gameObject);

        // In offline/local stepping testing, allow local control.
        return true;
    }

    private bool IsIntegratedMode()
    {
        // If no simManager or no netCore wired, assume not integrated.
        if (simManager == null || simManager.netCore == null) return false;

        return simManager.netCore.mode == SimManager.MODE_INTEGRATED;
    }

    private double GetCurrentWarp()
    {
        // Prefer clock.timeScale (public), but ensure it's sane.
        double cur = clock.timeScale;
        if (cur < 0.0) cur = 0.0;
        return cur;
    }

    private double ClampWarp(double w)
    {
        if (w < minWarp) w = minWarp;
        if (w > maxWarp) w = maxWarp;
        return w;
    }

    private void ApplyWarp(double newWarp, string reason)
    {
        newWarp = ClampWarp(newWarp);

        // If integrated, your SimManager will clamp anyway — but we can be explicit.
        if (IsIntegratedMode() && newWarp != 1.0)
        {
            ForceWarp1WithLog("[Warp] Warp != x1 requested in INTEGRATED; forcing x1.");
            return;
        }

        double before = GetCurrentWarp();
        if (System.Math.Abs(before - newWarp) < 1e-9) return;

        clock.SetTimeScale(newWarp);

        if (debugLogs) Debug.Log($"[Warp] {reason}: x{before:0.###} -> x{newWarp:0.###}");
    }

    private void ForceWarp1WithLog(string msg)
    {
        if (debugLogs) Debug.Log(msg);
        if (System.Math.Abs(GetCurrentWarp() - 1.0) > 1e-9)
            clock.SetTimeScale(1.0);
    }
}