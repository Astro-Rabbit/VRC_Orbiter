using UdonSharp;
using UnityEngine;

/// <summary>
/// SimUnityFrameBridge
///
/// Converts between:
/// - Sim frame:   right-handed, +Z = north/up, XY = ecliptic plane
/// - Unity frame: left-handed,  +Y = up
///
/// Core mapping:
///     (x, y, z)_sim -> (x, z, y)_unity
///
/// This is a pure basis remap:
/// - sim +X stays unity +X
/// - sim +Y becomes unity +Z
/// - sim +Z becomes unity +Y
///
/// Important:
/// - Use this for ALL sim->Unity positions, velocities, directions, and angular velocities
/// - Do NOT assign sim quaternions directly to Unity transforms
/// - Use SimRotationToUnityRotation() for body/craft attitudes coming from sim space
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SimUnityFrameBridge : UdonSharpBehaviour
{
    [Header("Optional")]
    [Tooltip("Meters-to-Unity scale for position conversion helpers that use defaultScale.")]
    public float defaultScale = 1.0f;

    // ---------------------------------------------------------------------
    // Sim -> Unity vector conversions
    // ---------------------------------------------------------------------

    public Vector3 SimPositionToUnity(double sx, double sy, double sz)
    {
        return new Vector3(
            (float)(sx * defaultScale),
            (float)(sz * defaultScale),
            (float)(sy * defaultScale)
        );
    }

    public Vector3 SimPositionToUnityScaled(double sx, double sy, double sz, float scale)
    {
        return new Vector3(
            (float)(sx * scale),
            (float)(sz * scale),
            (float)(sy * scale)
        );
    }

    public Vector3 SimDirectionToUnity(double sx, double sy, double sz)
    {
        return new Vector3(
            (float)sx,
            (float)sz,
            (float)sy
        );
    }

    public Vector3 SimDirectionToUnityVec3(Vector3 simVec)
    {
        return new Vector3(simVec.x, simVec.z, simVec.y);
    }

    public Vector3 SimAngularVelocityToUnity(double ox, double oy, double oz)
    {
        return new Vector3(
            (float)ox,
            (float)oz,
            (float)oy
        );
    }

    // ---------------------------------------------------------------------
    // Unity -> Sim vector conversions
    // ---------------------------------------------------------------------

    public Vector3 UnityDirectionToSim(Vector3 unityVec)
    {
        return new Vector3(unityVec.x, unityVec.z, unityVec.y);
    }

    public Vector3 UnityPositionToSim(Vector3 unityPos)
    {
        float inv = (Mathf.Abs(defaultScale) > 1e-20f) ? (1.0f / defaultScale) : 0.0f;
        return new Vector3(unityPos.x * inv, unityPos.z * inv, unityPos.y * inv);
    }

    public Vector3 UnityPositionToSimScaled(Vector3 unityPos, float scale)
    {
        float inv = (Mathf.Abs(scale) > 1e-20f) ? (1.0f / scale) : 0.0f;
        return new Vector3(unityPos.x * inv, unityPos.z * inv, unityPos.y * inv);
    }

    // ---------------------------------------------------------------------
    // Sim quaternion -> Unity quaternion
    //
    // This is the IMPORTANT one.
    // It converts a rotation that lives in sim basis into a Unity-world rotation.
    // ---------------------------------------------------------------------

    public Quaternion SimRotationToUnityRotation(Quaternion simQ)
    {
        // Sim basis directions transformed by sim-space rotation
        Vector3 simX = simQ * Vector3.right;
        Vector3 simY = simQ * Vector3.up;
        Vector3 simZ = simQ * Vector3.forward;

        // Remap those basis directions into Unity basis
        Vector3 unityX = SimDirectionToUnityVec3(simX);
        Vector3 unityY = SimDirectionToUnityVec3(simY);
        Vector3 unityZ = SimDirectionToUnityVec3(simZ);

        unityX = SafeNormalize(unityX, Vector3.right);
        unityY = SafeNormalize(unityY, Vector3.up);
        unityZ = SafeNormalize(unityZ, Vector3.forward);

        // Re-orthonormalize to protect against tiny numerical drift
        unityZ = SafeNormalize(unityZ, Vector3.forward);
        unityX = SafeNormalize(Vector3.Cross(unityY, unityZ), Vector3.right);
        unityY = SafeNormalize(Vector3.Cross(unityZ, unityX), Vector3.up);

        if (unityZ.sqrMagnitude < 1e-12f) unityZ = Vector3.forward;
        if (unityY.sqrMagnitude < 1e-12f) unityY = Vector3.up;

        return Quaternion.LookRotation(unityZ, unityY);
    }

    // ---------------------------------------------------------------------
    // Unity quaternion -> Sim quaternion
    //
    // Less commonly needed, but useful for feeding Unity-authored orientations
    // back into sim-space logic.
    // ---------------------------------------------------------------------

    public Quaternion UnityRotationToSimRotation(Quaternion unityQ)
    {
        Vector3 unityX = unityQ * Vector3.right;
        Vector3 unityY = unityQ * Vector3.up;
        Vector3 unityZ = unityQ * Vector3.forward;

        Vector3 simX = UnityDirectionToSim(unityX);
        Vector3 simY = UnityDirectionToSim(unityY);
        Vector3 simZ = UnityDirectionToSim(unityZ);

        simX = SafeNormalize(simX, Vector3.right);
        simY = SafeNormalize(simY, Vector3.up);
        simZ = SafeNormalize(simZ, Vector3.forward);

        simZ = SafeNormalize(simZ, Vector3.forward);
        simX = SafeNormalize(Vector3.Cross(simY, simZ), Vector3.right);
        simY = SafeNormalize(Vector3.Cross(simZ, simX), Vector3.up);

        if (simZ.sqrMagnitude < 1e-12f) simZ = Vector3.forward;
        if (simY.sqrMagnitude < 1e-12f) simY = Vector3.up;

        return Quaternion.LookRotation(simZ, simY);
    }

    // ---------------------------------------------------------------------
    // Relative position helper
    // Keeps double precision until after subtraction.
    // Use this for body/craft render placement.
    // ---------------------------------------------------------------------

    public Vector3 SimRelativePositionToUnity(
        double ax, double ay, double az,
        double bx, double by, double bz,
        float scale)
    {
        double rx = ax - bx;
        double ry = ay - by;
        double rz = az - bz;

        return new Vector3(
            (float)(rx * scale),
            (float)(rz * scale),
            (float)(ry * scale)
        );
    }

    // ---------------------------------------------------------------------
    // Debug helpers
    // ---------------------------------------------------------------------

    public bool SimOrbitLooksPrograde(double rx, double ry, double rz, double vx, double vy, double vz)
    {
        // In sim frame, prograde around +Z means h.z > 0
        double hz = rx * vy - ry * vx;
        return hz > 0.0;
    }

    public bool UnityOrbitLooksPrograde(Vector3 rUnity, Vector3 vUnity)
    {
        // In Unity after mapping, top-down is +Y, so prograde means h.y > 0
        Vector3 h = Vector3.Cross(rUnity, vUnity);
        return h.y > 0.0f;
    }

    // ---------------------------------------------------------------------

    private Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float m = v.magnitude;
        if (m < 1e-8f) return fallback;
        return v / m;
    }
}