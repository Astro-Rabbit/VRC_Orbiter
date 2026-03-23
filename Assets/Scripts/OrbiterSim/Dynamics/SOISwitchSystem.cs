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
    public bool Evaluate(out byte newPrimaryId, out double triggerDistance, out double triggerSOI)
    {
        newPrimaryId = 0;
        triggerDistance = 0.0;
        triggerSOI = 0.0;

        if (!enableSwitching) return false;
        if (bodies == null || craft == null) return false;

        byte sunId   = bodies.sunId;
        byte earthId = bodies.earthId;
        byte moonId  = bodies.moonId;

        byte currentPrimary = craft.primaryBodyId;

        double dEarth = bodies.GetCraftDistanceToBody(earthId, craft);
        double dMoon  = bodies.GetCraftDistanceToBody(moonId, craft);

        double rEarthSOI = bodies.GetSOIRadius(earthId);
        double rMoonSOI  = bodies.GetSOIRadius(moonId);

        double earthEnter = rEarthSOI - hysteresisM;
        double earthExit  = rEarthSOI + hysteresisM;

        double moonEnter = rMoonSOI - hysteresisM;
        double moonExit  = rMoonSOI + hysteresisM;

        // ---------------------------------------------------------
        // Priority 1: enter Moon if close enough and not already Moon
        // This should win over Earth because Moon SOI is nested inside Earth SOI.
        // ---------------------------------------------------------
        if (currentPrimary != moonId && rMoonSOI > 0.0 && dMoon < moonEnter)
        {
            newPrimaryId = moonId;
            triggerDistance = dMoon;
            triggerSOI = rMoonSOI;

            if (log) Debug.Log($"[SOI] Request -> Moon dMoon={dMoon:F0} enter={moonEnter:F0}");
            return true;
        }

        // ---------------------------------------------------------
        // If currently Moon, check Moon exit first.
        // On exit from Moon, go to Earth.
        // ---------------------------------------------------------
        if (currentPrimary == moonId)
        {
            if (rMoonSOI > 0.0 && dMoon > moonExit)
            {
                newPrimaryId = earthId;
                triggerDistance = dMoon;
                triggerSOI = rMoonSOI;

                if (log) Debug.Log($"[SOI] Request Moon->Earth dMoon={dMoon:F0} exit={moonExit:F0}");
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------
        // Sun/Earth switching
        // ---------------------------------------------------------
        if (currentPrimary == sunId)
        {
            if (rEarthSOI > 0.0 && dEarth < earthEnter)
            {
                newPrimaryId = earthId;
                triggerDistance = dEarth;
                triggerSOI = rEarthSOI;

                if (log) Debug.Log($"[SOI] Request Sun->Earth dEarth={dEarth:F0} enter={earthEnter:F0}");
                return true;
            }

            return false;
        }

        if (currentPrimary == earthId)
        {
            // Moon entry already handled above.
            if (rEarthSOI > 0.0 && dEarth > earthExit)
            {
                newPrimaryId = sunId;
                triggerDistance = dEarth;
                triggerSOI = rEarthSOI;

                if (log) Debug.Log($"[SOI] Request Earth->Sun dEarth={dEarth:F0} exit={earthExit:F0}");
                return true;
            }

            return false;
        }

        return false;
    }
}