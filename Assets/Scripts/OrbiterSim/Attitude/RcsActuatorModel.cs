
using UdonSharp;
using UnityEngine;

public class RcsActuatorModel : UdonSharpBehaviour
{
    [Header("Max torque (Nm)")]
    public double maxTx = 500.0;
    public double maxTy = 500.0;
    public double maxTz = 500.0;

    [Header("Deadband (Nm)")]
    public double deadband = 0.0;

    public void ComputeTorque(double cmdTx, double cmdTy, double cmdTz,
                              out double tx, out double ty, out double tz)
    {
        tx = ApplyDeadband(Clamp(cmdTx, -maxTx, maxTx));
        ty = ApplyDeadband(Clamp(cmdTy, -maxTy, maxTy));
        tz = ApplyDeadband(Clamp(cmdTz, -maxTz, maxTz));
    }

    private double ApplyDeadband(double x)
    {
        if (deadband <= 0.0) return x;
        if (x > -deadband && x < deadband) return 0.0;
        return x;
    }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }
}

