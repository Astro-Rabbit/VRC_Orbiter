using UdonSharp;
using UnityEngine;

/// <summary>
/// CraftProxyRenderer
/// - Applies CraftAttitudeState.qBE to a Unity Transform for visualization.
/// - This is a *render-only* bridge: solver attitude is canonical; mapping to Unity happens here.
/// 
/// Usage:
/// - Assign targetTransform to the mesh root (or a proxy object).
/// - If your render frame differs from solver frame, set solverToUnityRotation.
///   Otherwise leave identity.
/// </summary>
public class CraftProxyRenderer : UdonSharpBehaviour
{
    [Header("References")]
    public CraftAttitudeState att;

    [Header("Render target")]
    public Transform targetTransform;

    [Header("Frame mapping")]
    [Tooltip("Optional fixed rotation to map solver inertial axes into Unity world axes.")]
    public Quaternion solverToUnityRotation = Quaternion.identity;

    [Tooltip("If true, treat qBE as body->inertial and apply directly as world rotation (after mapping).")]
    public bool applyAsWorldRotation = true;

    void LateUpdate()
    {
        if (att == null || targetTransform == null) return;

        // qBE = Body -> Inertial
        Quaternion qBE = att.qBE;

        // Map solver inertial into Unity axes if needed:
        // q_unity = Qmap * q_solver
        Quaternion qUnity = solverToUnityRotation * qBE;

        if (applyAsWorldRotation)
        {
            targetTransform.rotation = qUnity;
        }
        else
        {
            // Optional: local rotation (if proxy is parented to something already positioned)
            targetTransform.localRotation = qUnity;
        }
    }
}
