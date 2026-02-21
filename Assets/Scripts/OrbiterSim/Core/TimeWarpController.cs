using UdonSharp;
using UnityEngine;

public class TimeWarpController : UdonSharpBehaviour
{
    [Header("User requested warp")]
    public double requestedTimeScale = 1.0;
    public SimManager Manager;

    [Header("Hard limits")]
    public double maxWarpAnalytic = 100000.0;     // only used in analytic mode
    public double maxWarpFixedStep = 500.0;       // practical cap if you’re stepping
    public double maxWarpWhileThrusting = 4.0;   // when thrusting / burns
    public double maxWarpNearBody = 500.0;         // if altitude low

    [Header("Near-body gating")]
    public double nearBodyAltMeters = 20000.0;   // within 200 km altitude clamp warp
    public double earthRadiusM = 6371000.0;
    public double moonRadiusM = 1737400.0;

    [Header("Cooldown (optional)")]
    public bool useCooldown = true;
    public double cooldownSeconds = 2.0;
    [HideInInspector] public double cooldownUntilSimTime = -1.0;

    /// <summary>
    /// Call this when something critical happens (SOI boundary, primary switch, etc.)
    /// to force warp down briefly.
    /// </summary>
    public void TriggerCooldown(double simTimeNow)
    {
        if (!useCooldown) return;
        cooldownUntilSimTime = simTimeNow + cooldownSeconds;
    }

    public double GetAllowedTimeScale(
        double simTimeNow,
        bool isThrusting,
        byte primaryBodyId,
        double craftToPrimaryDistanceMeters
    )
    {
        double allowed = requestedTimeScale;

        // Cooldown forces warp to 1x
        if (useCooldown && simTimeNow < cooldownUntilSimTime)
        {
            // Manager.timeScale = 1;
            return 1.0;
        }
            

        // Thrusting clamp
        if (isThrusting && allowed > maxWarpWhileThrusting)
            allowed = maxWarpWhileThrusting;

        // Near-body clamp (based on altitude)
        double bodyR = (primaryBodyId == 1) ? earthRadiusM : moonRadiusM;
        double alt = craftToPrimaryDistanceMeters - bodyR;
        if (alt < nearBodyAltMeters && allowed > maxWarpNearBody)
            allowed = maxWarpNearBody;

        // Global clamp to keep things sane
        if (allowed > maxWarpAnalytic) allowed = maxWarpAnalytic;
        if (allowed < 0.0) allowed = 0.0;
        
        return allowed;
    }
}
