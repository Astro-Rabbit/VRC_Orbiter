using UdonSharp;
using UnityEngine;

/// <summary>
/// StationStateModel
/// Pure data container for a rails-only space station (no rendering, no physics).
///
/// Frames:
/// - "E" / solver inertial: your global SSB ecliptic inertial solver frame.
/// - Primary-relative values are still expressed in solver inertial axes, just with primary at origin.
///
/// Outputs:
/// - Primary-relative: rr*, rv* (meters, m/s)
/// - SSB inertial:     r*,  v*  (meters, m/s)
/// - Attitude:         q_B2E (body -> solver inertial)
/// </summary>
public class StationStateModel : UdonSharpBehaviour
{
    // --------------------
    // Attitude modes (byte)
    // --------------------
    public const byte ATT_MODE_FIXED_INERTIAL = 0; // Mode A
    public const byte ATT_MODE_RTN_LVLH       = 1; // Mode B

    // --------------------
    // RTN mapping presets (byte)
    // Define how station BODY axes map to RTN basis:
    // R = radial out, T = along-track, N = orbit normal (right-handed).
    // --------------------
    public const byte RTNMAP_Z_NADIR_X_PROGRADE_Y_NORMAL  = 0; // +Z=-R, +X=+T, +Y=+N
    public const byte RTNMAP_Z_ZENITH_X_PROGRADE_Y_NORMAL = 1; // +Z=+R, +X=+T, +Y=+N
    public const byte RTNMAP_X_NADIR_Y_PROGRADE_Z_NORMAL  = 2; // +X=-R, +Y=+T, +Z=+N (example alt)

    [Header("Validity / Metadata")]
    public bool valid = false;
    public byte primaryBodyId = 0;

    [Header("Primary-relative (solver inertial), meters")]
    public double rrx, rry, rrz;

    [Header("Primary-relative (solver inertial), m/s")]
    public double rvx, rvy, rvz;

    [Header("SSB inertial (solver frame), meters")]
    public double rx, ry, rz;

    [Header("SSB inertial (solver frame), m/s")]
    public double vx, vy, vz;

    [Header("Attitude output (body -> solver inertial)")]
    public Quaternion q_B2E = Quaternion.identity;

    [Header("Attitude config")]
    public byte attitudeMode = ATT_MODE_FIXED_INERTIAL;

    [Tooltip("Mode A: constant body->inertial orientation")]
    public Quaternion qFixed_B2E = Quaternion.identity;

    [Tooltip("Mode B: RTN mapping preset")]
    public byte rtnMap = RTNMAP_Z_NADIR_X_PROGRADE_Y_NORMAL;
}