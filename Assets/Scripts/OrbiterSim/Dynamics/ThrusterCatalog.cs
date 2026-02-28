using UdonSharp;
using UnityEngine;

/// <summary>
/// ThrusterCatalog (Udon-safe)
/// - Author thrusters as Transform arrays + maxForce arrays.
/// - Caches posRelCg_B and dir_B arrays at Start() for runtime efficiency.
/// 
/// Conventions:
/// - craftRoot defines BODY frame.
/// - cgTransform defines CG origin.
/// - thrusterTransform.forward (+Z) is thrust force direction (direction of FORCE).
/// 
/// Main engine gimbal conventions (authoring):
/// - mainTf[i].forward = nominal thrust direction
/// - mainTf[i].right   = pitch gimbal rotation axis (local +X)
/// - mainTf[i].up      = yaw gimbal rotation axis   (local +Y)
/// (If your model differs, we can swap mapping in CacheGroupMain()).
/// </summary>
public class ThrusterCatalog : UdonSharpBehaviour
{
    [Header("Frame References")]
    public Transform craftRoot;
    public Transform cgTransform;

    [Header("Reaction Wheels (torque-only, V1)")]
    public float wheelMaxTorqueNm = 0f;

    [Header("RCS Settings")]
    public bool rcsHasLowMode = true;
    [Range(0f, 1f)] public float rcsLowScale = 0.25f;

    // ---------------- MAIN ENGINES ----------------
    [Header("Main Engines")]
    public Transform[] mainTf;
    public float[] mainMaxForceN;

    [Tooltip("Specific impulse per main engine (seconds). If missing or <=0, engine produces thrust but consumes no fuel.")]
    public float[] mainIspSec;

    [Tooltip("Per-engine gimbal enable.")]
    public bool[] mainHasGimbal;

    [Tooltip("Max gimbal angle per engine (degrees). Used only if mainHasGimbal[i]=true.")]
    public float[] mainMaxGimbalDeg;

    // ---------------- HOVER ENGINES ----------------
    [Header("Hover Engines")]
    public Transform[] hoverTf;
    public float[] hoverMaxForceN;

    // ---------------- RCS JETS ----------------
    [Header("RCS Jets")]
    public Transform[] rcsTf;
    public float[] rcsMaxForceN;

    // ---------------- CACHED (READ-ONLY) ----------------
    [Header("Cached (read-only) - Main")]
    public Vector3[] mainPosRelCg_B;
    public Vector3[] mainDir_B;
    public Vector3[] mainGimbalPitchAxis_B; // axis of rotation in BODY frame
    public Vector3[] mainGimbalYawAxis_B;   // axis of rotation in BODY frame
    public bool[]    mainCached;

    [Header("Cached (read-only) - Hover")]
    public Vector3[] hoverPosRelCg_B;
    public Vector3[] hoverDir_B;
    public bool[]    hoverCached;

    [Header("Cached (read-only) - RCS")]
    public Vector3[] rcsPosRelCg_B;
    public Vector3[] rcsDir_B;
    public bool[]    rcsCached;

    void Start()
    {
        CacheAll();
    }

    public void CacheAll()
    {
        if (craftRoot == null || cgTransform == null) return;

        CacheGroupMain();
        CacheGroup(hoverTf, hoverMaxForceN, ref hoverPosRelCg_B, ref hoverDir_B, ref hoverCached);
        CacheGroup(rcsTf,  rcsMaxForceN,  ref rcsPosRelCg_B,  ref rcsDir_B,  ref rcsCached);
    }

    // Main group needs extra cached axes
    private void CacheGroupMain()
    {
        int n = (mainTf != null) ? mainTf.Length : 0;

        EnsureVec3Array(ref mainPosRelCg_B, n);
        EnsureVec3Array(ref mainDir_B, n);
        EnsureVec3Array(ref mainGimbalPitchAxis_B, n);
        EnsureVec3Array(ref mainGimbalYawAxis_B, n);
        EnsureBoolArray(ref mainCached, n);

        for (int i = 0; i < n; i++)
        {
            Transform t = mainTf[i];
            if (t == null)
            {
                mainCached[i] = false;
                mainPosRelCg_B[i] = Vector3.zero;
                mainDir_B[i] = Vector3.forward;
                mainGimbalPitchAxis_B[i] = Vector3.right;
                mainGimbalYawAxis_B[i]   = Vector3.up;
                continue;
            }

            // Position relative to CG, expressed in BODY frame
            Vector3 rWorld = t.position - cgTransform.position;
            mainPosRelCg_B[i] = craftRoot.InverseTransformVector(rWorld);

            // Nominal thrust direction expressed in BODY frame
            Vector3 dWorld = -t.forward;
            Vector3 dBody = craftRoot.InverseTransformDirection(dWorld);
            if (dBody.sqrMagnitude > 1e-12f) dBody.Normalize();
            else dBody = Vector3.forward;
            mainDir_B[i] = dBody;

            // Gimbal axes (axis of rotation), expressed in BODY frame
            // Convention: pitch about local +X (right), yaw about local +Y (up)
            Vector3 pitchAxisW = t.right;
            Vector3 yawAxisW   = t.up;

            Vector3 pitchAxisB = craftRoot.InverseTransformDirection(pitchAxisW);
            Vector3 yawAxisB   = craftRoot.InverseTransformDirection(yawAxisW);

            if (pitchAxisB.sqrMagnitude > 1e-12f) pitchAxisB.Normalize();
            else pitchAxisB = Vector3.right;

            if (yawAxisB.sqrMagnitude > 1e-12f) yawAxisB.Normalize();
            else yawAxisB = Vector3.up;

            mainGimbalPitchAxis_B[i] = pitchAxisB;
            mainGimbalYawAxis_B[i]   = yawAxisB;

            mainCached[i] = true;

            // Clamp forces non-negative if array exists
            if (mainMaxForceN != null && i < mainMaxForceN.Length && mainMaxForceN[i] < 0f)
                mainMaxForceN[i] = 0f;

            // Reasonable defaults / clamps for new arrays (optional, but prevents NaNs later)
            if (mainIspSec != null && i < mainIspSec.Length && mainIspSec[i] < 0f)
                mainIspSec[i] = 0f;

            if (mainMaxGimbalDeg != null && i < mainMaxGimbalDeg.Length && mainMaxGimbalDeg[i] < 0f)
                mainMaxGimbalDeg[i] = 0f;
        }
    }

    private void CacheGroup(
        Transform[] tf, float[] maxForce,
        ref Vector3[] posRelCg_B, ref Vector3[] dir_B, ref bool[] cached)
    {
        int n = (tf != null) ? tf.Length : 0;

        EnsureVec3Array(ref posRelCg_B, n);
        EnsureVec3Array(ref dir_B, n);
        EnsureBoolArray(ref cached, n);

        for (int i = 0; i < n; i++)
        {
            Transform t = tf[i];
            if (t == null)
            {
                cached[i] = false;
                posRelCg_B[i] = Vector3.zero;
                dir_B[i] = Vector3.forward;
                continue;
            }

            Vector3 rWorld = t.position - cgTransform.position;
            posRelCg_B[i] = craftRoot.InverseTransformVector(rWorld);

            Vector3 dWorld = t.forward;
            Vector3 dBody = craftRoot.InverseTransformDirection(dWorld);
            if (dBody.sqrMagnitude > 1e-12f) dBody.Normalize();
            else dBody = Vector3.forward;

            dir_B[i] = dBody;
            cached[i] = true;

            if (maxForce != null && i < maxForce.Length && maxForce[i] < 0f)
                maxForce[i] = 0f;
        }
    }

    private static void EnsureVec3Array(ref Vector3[] a, int n)
    {
        if (n <= 0) { a = null; return; }
        if (a == null || a.Length != n) a = new Vector3[n];
    }

    private static void EnsureBoolArray(ref bool[] a, int n)
    {
        if (n <= 0) { a = null; return; }
        if (a == null || a.Length != n) a = new bool[n];
    }

    // ---------- Safe getters ----------
    public float GetRcsMaxForceN(int i)
    {
        if (rcsMaxForceN == null || i < 0 || i >= rcsMaxForceN.Length) return 0f;
        return rcsMaxForceN[i];
    }

    public float GetMainMaxForceN(int i)
    {
        if (mainMaxForceN == null || i < 0 || i >= mainMaxForceN.Length) return 0f;
        return mainMaxForceN[i];
    }

    public float GetMainIspSec(int i)
    {
        if (mainIspSec == null || i < 0 || i >= mainIspSec.Length) return 0f;
        return mainIspSec[i];
    }

    public float GetMainMaxGimbalDeg(int i)
    {
        if (mainMaxGimbalDeg == null || i < 0 || i >= mainMaxGimbalDeg.Length) return 0f;
        return mainMaxGimbalDeg[i];
    }

    public bool GetMainHasGimbal(int i)
    {
        if (mainHasGimbal == null || i < 0 || i >= mainHasGimbal.Length) return false;
        return mainHasGimbal[i];
    }
}
