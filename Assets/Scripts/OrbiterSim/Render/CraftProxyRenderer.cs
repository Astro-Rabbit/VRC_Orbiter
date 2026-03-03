using UdonSharp;
using UnityEngine;

/// <summary>
/// CraftProxyRenderer
/// Converts solver attitude (body->H, H is +Z up ecliptic) into Unity world rotation.
/// Includes an optional *local* model-axis fix (applied on the RIGHT).
/// </summary>
public class CraftProxyRenderer : UdonSharpBehaviour
{
    [Header("References")]
    public CraftAttitudeState att;

    [Header("Render target")]
    public Transform targetTransform;

    [Header("Solver frame to Unity frame")]
    [Tooltip("H(+Z up) -> Unity(+Y up). Keep this unless you change the sim frame.")]
    public bool applyHelioBasis = true;

    [Header("If your stored qBE is actually inertial->body, enable this.")]
    public bool invertQBE = false;

    [Header("Model axis fix (LOCAL)")]
    [Tooltip("Apply a constant model->body (or model authoring) correction in LOCAL space. This is the 'x=270' you tried.")]
    public Vector3 modelFixEulerDeg = Vector3.zero;

    private Quaternion qBasis;
    private Quaternion qBasisInv;

    void Start()
    {
        // H(+Z up) -> Unity(+Y up): RotX(-90°)
        qBasis = Quaternion.AngleAxis(-90f, Vector3.right);
        qBasisInv = Quaternion.Inverse(qBasis);
    }

    void LateUpdate()
    {
        if (att == null || targetTransform == null) return;

        Quaternion qBE = att.qBE;               // expected: body -> H
        if (invertQBE) qBE = Quaternion.Inverse(qBE);

        // Convert to Unity basis if requested: body -> UnityInert
        Quaternion qBodyToUnity = qBE;
        if (applyHelioBasis)
            qBodyToUnity = qBasis * qBE * qBasisInv;

        // IMPORTANT: model fix must be LOCAL (right-multiply)
        Quaternion qModelFix = Quaternion.Euler(modelFixEulerDeg);
        Quaternion qFinal = qBodyToUnity * qModelFix;

        targetTransform.rotation = qFinal;
    }
}