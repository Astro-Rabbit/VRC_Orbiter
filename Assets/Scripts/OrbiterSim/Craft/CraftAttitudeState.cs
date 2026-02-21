
using UdonSharp;
using UnityEngine;

public class CraftAttitudeState : UdonSharpBehaviour
{
    [Header("Attitude (body -> ECI)")]
    public Quaternion qBE = Quaternion.identity;

    [Header("Angular velocity in body frame (rad/s)")]
    public double wx;
    public double wy;
    public double wz;

    public void ResetState()
    {
        qBE = Quaternion.identity;
        wx = wy = wz = 0.0;
    }
}

