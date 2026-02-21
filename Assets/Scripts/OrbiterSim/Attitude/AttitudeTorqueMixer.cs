using UdonSharp;
using UnityEngine;

public class AttitudeTorqueMixer : UdonSharpBehaviour
{
    [Header("References")]
    public CraftControlState control; // provides mode/weights
    public GyroActuatorModel gyro;
    public RcsActuatorModel rcs;

    [Header("Output torque (Nm, body frame)")]
    public double tx, ty, tz;

    // control.actuatorMode suggested values:
    // 0=Auto, 1=GyroOnly, 2=RcsOnly, 3=Mix
    public void Evaluate(double cmdTx, double cmdTy, double cmdTz)
    {
        tx = ty = tz = 0.0;

        byte mode = 0;
        double wG = 1.0, wR = 0.0;

        if (control != null)
        {
            mode = control.actuatorMode;
            wG = control.gyroWeight;
            wR = control.rcsWeight;
        }

        double gTx=0,gTy=0,gTz=0;
        double rTx=0,rTy=0,rTz=0;

        if (mode == 1) // GyroOnly
        {
            if (gyro != null) gyro.ComputeTorque(cmdTx, cmdTy, cmdTz, out tx, out ty, out tz);
            return;
        }

        if (mode == 2) // RcsOnly
        {
            if (rcs != null) rcs.ComputeTorque(cmdTx, cmdTy, cmdTz, out tx, out ty, out tz);
            return;
        }

        if (mode == 3) // Mix weights
        {
            if (gyro != null) gyro.ComputeTorque(cmdTx, cmdTy, cmdTz, out gTx, out gTy, out gTz);
            if (rcs  != null) rcs.ComputeTorque (cmdTx, cmdTy, cmdTz, out rTx, out rTy, out rTz);

            tx = wG * gTx + wR * rTx;
            ty = wG * gTy + wR * rTy;
            tz = wG * gTz + wR * rTz;
            return;
        }

        // Auto: gyro first, spill residual into RCS
        if (gyro != null) gyro.ComputeTorque(cmdTx, cmdTy, cmdTz, out gTx, out gTy, out gTz);

        double resTx = cmdTx - gTx;
        double resTy = cmdTy - gTy;
        double resTz = cmdTz - gTz;

        if (rcs != null) rcs.ComputeTorque(resTx, resTy, resTz, out rTx, out rTy, out rTz);

        tx = gTx + rTx;
        ty = gTy + rTy;
        tz = gTz + rTz;
    }
}
