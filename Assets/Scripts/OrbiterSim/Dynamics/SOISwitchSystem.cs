using UdonSharp;
using UnityEngine;
using System;

public class SOISwitchSystem : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public CraftStateModel craft;

    [Header("Behavior")]
    public bool enableSwitching = true;

    [Tooltip("Hysteresis (meters) to prevent rapid toggling near boundary.")]
    public double hysteresisM = 20000.0;

    [Header("Debug")]
    public bool log = false;

    // returns true if a switch is requested this tick
    public bool Evaluate(out byte newPrimaryId, out double distToMoon, out double rSOI)
    {
        newPrimaryId = 0;
        distToMoon = 0.0;
        rSOI = 0.0;

        if (!enableSwitching) return false;
        if (bodies == null || craft == null) return false;

        byte earthId = bodies.earthId;
        byte moonId  = bodies.moonId;

        // Moon position (heliocentric inertial)
        double mx, my, mz;
        bodies.GetBodyPos(moonId, out mx, out my, out mz);

        // Craft distance to Moon
        double dx = craft.rx - mx;
        double dy = craft.ry - my;
        double dz = craft.rz - mz;
        distToMoon = Math.Sqrt(dx*dx + dy*dy + dz*dz);

        rSOI = bodies.GetSOIRadius(moonId);
        if (rSOI <= 0.0) return false;

        double enter = rSOI - hysteresisM;
        double exit  = rSOI + hysteresisM;

        byte currentPrimary = craft.primaryBodyId;

        if (currentPrimary != moonId && distToMoon < enter)
        {
            newPrimaryId = moonId;
            if (log) Debug.Log($"[SOI] Request Earth->Moon dMoon={distToMoon:F0} enter={enter:F0}");
            return true;
        }

        if (currentPrimary == moonId && distToMoon > exit)
        {
            newPrimaryId = earthId;
            if (log) Debug.Log($"[SOI] Request Moon->Earth dMoon={distToMoon:F0} exit={exit:F0}");
            return true;
        }

        return false;
    }
}
