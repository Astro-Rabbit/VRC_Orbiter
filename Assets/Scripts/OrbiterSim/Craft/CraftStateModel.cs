using UdonSharp;
using UnityEngine;

public class CraftStateModel : UdonSharpBehaviour
{
    [Header("Canonical craft state (ECI)")]
    public double rx, ry, rz;   // meters
    public double vx, vy, vz;   // m/s

    [Header("Debug mirrors (read-only)")]
    public float rx_f, vx_f;

    [Header("Meta")]
    public byte primaryBodyId;  // 1=Earth, 2=Moon (match your BodyId enum if you use it)

    [Header("Mass state (kg)")]
    public double dryMassKg;
    public double propMassKg;
    public double massKg; // dry + prop

    public void RecomputeMass()
    {
        massKg = dryMassKg + propMassKg;
        if (massKg < 1.0) massKg = 1.0;
    }

    void LateUpdate()
    {
        rx_f = (float)rx;
        vx_f = (float)vx;
    }

}
