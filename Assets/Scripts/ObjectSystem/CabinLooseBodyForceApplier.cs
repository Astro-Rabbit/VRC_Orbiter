using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class CabinLooseBodyForceApplier : UdonSharpBehaviour
{
    [Header("Wiring")]
    public Rigidbody[] bodies;
    public VRC_Pickup[] pickups;

    public SimManager simManager;
    public SimClock clock;
    public CraftNetCabinAccel netAccel;
    public CraftAttitudeState att;
    public CraftNetAttitude netAtt;
    public Transform craftCG;

    [Header("Behavior")]
    public bool disableWhileHeld = true;
    public bool useEulerTerm = true;
    public bool useCoriolis = true;
    public bool useCentrifugal = true;
    public bool applyRotationCompensation = true;

    [Header("Rotation compensation")]
    [Tooltip("Rate at which objects converge to inertial angular velocity")]
    public float angularVelFollowGain = 10f;

    [Header("Optional extra damping in craft frame")]
    public float linearCabinDamping = 0.0f;

    private Vector3 _lastOmegaB = Vector3.zero;
    private bool _lastOmegaValid = false;

    void FixedUpdate()
    {
        if (bodies == null || craftCG == null || att == null || netAccel == null) return;

        bool isOwner = (simManager != null && simManager.IsSimOwner());

        double tRender = 0.0;
        if (!isOwner && clock != null)
            tRender = clock.GetCachedRemoteRenderTime();

        // Felt translational acceleration (body frame)
        Vector3 aFelt_B = isOwner
            ? netAccel.GetImmediateOwnerAccelB()
            : netAccel.SampleRenderAccelB(tRender);

        // Craft angular velocity (body frame)
        Vector3 omega_B;
        if (isOwner)
        {
            omega_B = new Vector3((float)att.wx, (float)att.wy, (float)att.wz);
        }
        else
        {
            omega_B = (netAtt != null)
                ? netAtt.SampleRenderOmegaB(tRender)
                : new Vector3((float)att.wx, (float)att.wy, (float)att.wz);
        }

        // Angular acceleration
        Vector3 alpha_B = Vector3.zero;
        if (_lastOmegaValid)
        {
            float dt = Time.fixedDeltaTime;
            if (dt > 1e-6f)
                alpha_B = (omega_B - _lastOmegaB) / dt;
        }

        _lastOmegaB = omega_B;
        _lastOmegaValid = true;

        Vector3 omegaCraft_W = craftCG.TransformDirection(omega_B);

        int n = bodies.Length;
        for (int i = 0; i < n; i++)
        {
            Rigidbody rb = bodies[i];
            if (rb == null) continue;

            VRC_Pickup pickup = null;
            if (pickups != null && i < pickups.Length)
                pickup = pickups[i];

            if (disableWhileHeld && pickup != null && pickup.IsHeld)
                continue;

            // Position / velocity relative to craft
            Vector3 r_B = craftCG.InverseTransformPoint(rb.worldCenterOfMass);
            Vector3 v_B = craftCG.InverseTransformDirection(rb.velocity);

            Vector3 aApp_B = -aFelt_B;

            if (useEulerTerm)
                aApp_B -= Vector3.Cross(alpha_B, r_B);

            if (useCentrifugal)
                aApp_B -= Vector3.Cross(omega_B, Vector3.Cross(omega_B, r_B));

            if (useCoriolis)
                aApp_B -= 2f * Vector3.Cross(omega_B, v_B);

            if (linearCabinDamping > 0f)
                aApp_B += (-linearCabinDamping * v_B);

            Vector3 aApp_W = craftCG.TransformDirection(aApp_B);
            rb.AddForce(aApp_W, ForceMode.Acceleration);

            // Rotation compensation (inertial hold)
            if (applyRotationCompensation)
            {
                Vector3 omegaTarget_W = -omegaCraft_W;

                float blend = 1f - Mathf.Exp(-angularVelFollowGain * Time.fixedDeltaTime);
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, omegaTarget_W, blend);
            }
        }
    }
}