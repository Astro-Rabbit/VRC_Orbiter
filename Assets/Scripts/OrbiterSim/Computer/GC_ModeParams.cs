using UdonSharp;
using UnityEngine;

/// <summary>
/// GC_ModeParams
/// Latched parameters for the active continuous mode (set by UI/API, consumed by GC_Core).
/// Data-only container.
/// </summary>
public class GC_ModeParams : UdonSharpBehaviour
{
    // --------------------
    // Shared axis conventions for point/hold-dir modes
    // --------------------
    public const byte AXIS_POS_X = 0;
    public const byte AXIS_POS_Y = 1;
    public const byte AXIS_POS_Z = 2;
    public const byte AXIS_NEG_X = 3;
    public const byte AXIS_NEG_Y = 4;
    public const byte AXIS_NEG_Z = 5;

    // --------------------
    // RTN basis selector (6 directions)
    // --------------------
    public const byte RTN_R_PLUS  = 0; // radial out
    public const byte RTN_R_MINUS = 1; // radial in
    public const byte RTN_T_PLUS  = 2; // prograde
    public const byte RTN_T_MINUS = 3; // retrograde
    public const byte RTN_N_PLUS  = 4; // +normal
    public const byte RTN_N_MINUS = 5; // -normal

    [Header("Hold quaternion")]
    public Quaternion qTarget_BE = Quaternion.identity;

    [Header("Hold / point direction in inertial (E)")]
    public Vector3 pointDirTarget_E = Vector3.forward;
    public byte bodyAxisToPoint = AXIS_POS_Z;

    [Header("Hold RTN direction")]
    public byte rtnDir = RTN_T_PLUS;

    [Header("Rate target (B)")]
    public Vector3 rateTarget_B = Vector3.zero;

    [Header("Direct torque (B)")]
    public Vector3 tauDirect_B = Vector3.zero;

    [Header("Blending")]
    public bool blendDirectTorqueWithPD = true;

    [Header("Maneuver node selection")]
    [Tooltip("Selected maneuver node index for node-vector pointing/display. Defaults to 0.")]
    public byte selectedNodeIndex = 0;

    public void ResetDefaults()
    {
        qTarget_BE = Quaternion.identity;

        pointDirTarget_E = Vector3.forward;
        bodyAxisToPoint = AXIS_POS_Z;

        rtnDir = RTN_T_PLUS;

        rateTarget_B = Vector3.zero;

        tauDirect_B = Vector3.zero;
        blendDirectTorqueWithPD = true;
        selectedNodeIndex = 0;        
    }
}