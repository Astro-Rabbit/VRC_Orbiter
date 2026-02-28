using UdonSharp;
using UnityEngine;

public class SpaceCraftState : UdonSharpBehaviour
{
    [Header("Components")]
    public SpaceCraftContactProbe[] probes;

    [Header("State (Double Precision)")]
    public double px, py, pz;
    public double qx, qy, qz, qw = 1.0;
    public double vx, vy, vz;
    public double wx, wy, wz;

    [Header("Inertial Properties")]
    public double massKg = 1500.0;
    public double Ix = 2000.0;
    public double Iy = 2000.0;
    public double Iz = 2000.0;

    [Header("Settings")]
    public bool isStation = false;

    public void Start()
    {
        // FIXED: Use generics instead of typeof()
        if (probes == null || probes.Length == 0)
            probes = GetComponentsInChildren<SpaceCraftContactProbe>();

        foreach (SpaceCraftContactProbe p in probes)
        {
            if (p != null) p.myState = this;
        }
    }
}