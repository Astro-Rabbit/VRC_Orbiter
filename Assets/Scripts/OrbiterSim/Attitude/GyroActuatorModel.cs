using UdonSharp;
using UnityEngine;

public class GyroActuatorModel : UdonSharpBehaviour
{
    [Header("Max torque (Nm)")]
    public double maxTx = 200.0;
    public double maxTy = 200.0;
    public double maxTz = 200.0;

    public void ComputeTorque(double cmdTx, double cmdTy, double cmdTz,
                              out double tx, out double ty, out double tz)
    {
        tx = Clamp(cmdTx, -maxTx, maxTx);
        ty = Clamp(cmdTy, -maxTy, maxTy);
        tz = Clamp(cmdTz, -maxTz, maxTz);
    }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }
}
