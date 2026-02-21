
using UdonSharp;
using UnityEngine;
using System;

public class ThrustModel : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public ConicState conic;
    public CraftStateModel craft;
    public CraftAttitudeState attitude;

    public CraftControlState control;
    public CraftConfig config;


    [Header("Behavior")]
    public double minThrottle = 1e-4;

    // Output acceleration in Unity-ECI (m/s^2)
    public double ax, ay, az;

    public bool IsThrusting()
    {
        if (control == null) return false;
        return (double)control.throttle01 > minThrottle;
    }

    public void Evaluate()
    {
        ax = ay = az = 0.0;

        if (bodies == null || conic == null || craft == null || control == null) return;

        double throttle = (double)control.throttle01;
        if (throttle <= minThrottle) return;


        double aMag = (config.maxThrustN * throttle) / craft.massKg;

        // Direction (Unity-ECI)
        double dx = 0, dy = 0, dz = 0;

        if (control.thrustMode == 2)
        {
            Vector3 f = control.targetForwardECI;
            dx = (double)f.x; dy = (double)f.y; dz = (double)f.z;
        }
        else if (control.thrustMode == 3)
        {
            // Body axis rotated by attitude quaternion (body -> ECI)
            if (attitude == null) return;

            Vector3 axisB = control.thrustAxisBody;
            if (axisB.sqrMagnitude < 1e-12f) axisB = Vector3.forward;

            Vector3 dir = attitude.qBE * axisB;   // Unity Quaternion rotates Vector3
            dx = (double)dir.x; dy = (double)dir.y; dz = (double)dir.z;
        }
        else
        {
            // Prograde/retrograde relative to current primary: v_rel = v_craft - v_primary
            byte pid = craft.primaryBodyId;

            double pvx, pvy, pvz;
            bodies.GetBodyVel(pid, out pvx, out pvy, out pvz);

            double vrx = craft.vx - pvx;
            double vry = craft.vy - pvy;
            double vrz = craft.vz - pvz;

            double vmag = Math.Sqrt(vrx*vrx + vry*vry + vrz*vrz);
            if (vmag < 1e-9) return;

            dx = vrx / vmag;
            dy = vry / vmag;
            dz = vrz / vmag;

            // Retrograde
            if (control.thrustMode == 1)
            {
                dx = -dx; dy = -dy; dz = -dz;
            }
        }

        // Normalize direction
        double dmag = Math.Sqrt(dx*dx + dy*dy + dz*dz);
        if (dmag < 1e-12) return;

        dx /= dmag; dy /= dmag; dz /= dmag;

        ax = aMag * dx;
        ay = aMag * dy;
        az = aMag * dz;
    }
}
