using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CraftState : UdonSharpBehaviour
{
    [Header("State (Universal Doubles)")]
    public double px; public double py; public double pz;
    public double vx; public double vy; public double vz;
    public double qx; public double qy; public double qz; public double qw = 1.0;
    public double wx; public double wy; public double wz;

    [Header("Constants")]
    public double mass = 1500.0;
    public double Ix = 2000.0;
    public double Iy = 2000.0;
    public double Iz = 2000.0;

    [System.NonSerialized] public double forceX, forceY, forceZ;
    [System.NonSerialized] public double torqueX, torqueY, torqueZ;

    [Header("Collision")]
    public AeroContactProbe[] probes;

    void Start()
    {
        // Cache the local offsets of all probes relative to this craft's visual root
        for (int i = 0; i < probes.Length; i++)
        {
            if (probes[i] != null) probes[i].CacheLocal(this.transform);
        }
    }

    // Call this from other scripts to apply forces
    public void AddRelativeForce(Vector3 localForce)
    {
        Quaternion q = new Quaternion((float)qx, (float)qy, (float)qz, (float)qw);
        Vector3 worldForce = q * localForce;
        forceX += worldForce.x;
        forceY += worldForce.y;
        forceZ += worldForce.z;
    }

    public void AddLocalTorque(Vector3 localTorque)
    {
        torqueX += localTorque.x;
        torqueY += localTorque.y;
        torqueZ += localTorque.z;
    }
}