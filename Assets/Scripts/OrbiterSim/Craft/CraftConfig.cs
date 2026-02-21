using UdonSharp;
using UnityEngine;

public class CraftConfig : UdonSharpBehaviour
{
    [Header("Mass (kg)")]
    public double dryMassKg = 12000.0;
    public double propMassKgInitial = 3000.0;

    [Header("Main engine")]
    public double maxThrustN = 200000.0;
    public double ispS = 310.0;

    [Header("Inertia (body frame, kg*m^2)")]
    // V1: diagonal inertia tensor
    public double Ixx = 8000.0;
    public double Iyy = 9000.0;
    public double Izz = 7000.0;

    [Header("Body axes convention")]
    [Tooltip("Which local axis is the main engine thrust axis? (V1: use +Z)")]
    public Vector3 engineAxisBody = new Vector3(0f, 0f, 1f);

    // Helpers (Udon-safe, no properties)
    public double GetInitialWetMass()
    {
        return dryMassKg + propMassKgInitial;
    }
}
