using UdonSharp;
using UnityEngine;

/// <summary>
/// StationStateModel
/// Pure data container for a rails-only space station (no rendering, no physics).
///
/// Frames:
/// - "E" / solver inertial: global SSB ecliptic inertial solver frame.
/// - Primary-relative values are still expressed in solver inertial axes, just with primary at origin.
///
/// Outputs:
/// - Primary-relative: rr*, rv* (meters, m/s)
/// - SSB inertial:     r*,  v*  (meters, m/s)
/// - Attitude:         q_B2E (body -> solver inertial)
///
/// Docking ports:
/// - Cached in STATION BODY frame:
///   portPos_B[i] = position relative to station origin, expressed in station body axes (meters)
///   portRot_B[i] = orientation of the port frame expressed in station body axes (Quaternion)
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

    // --------------------------------------------------------------------
    // Docking ports cache (station body frame)
    // --------------------------------------------------------------------
    [Header("Docking ports (cached, station BODY frame)")]
    public int dockingPortCount = 0;

    // Positions relative to station origin, expressed in station body axes (meters)
    public double[] dock_px_B;
    public double[] dock_py_B;
    public double[] dock_pz_B;

    // Orientation of each docking port frame expressed in station body axes
    public Quaternion[] dock_q_B;

    /// <summary>
    /// Ensures arrays are sized to n. Does not populate values.
    /// </summary>
    public void EnsureDockPortSize(int n)
    {
        if (n < 0) n = 0;
        dockingPortCount = n;

        if (dock_px_B == null || dock_px_B.Length != n) dock_px_B = new double[n];
        if (dock_py_B == null || dock_py_B.Length != n) dock_py_B = new double[n];
        if (dock_pz_B == null || dock_pz_B.Length != n) dock_pz_B = new double[n];
        if (dock_q_B  == null || dock_q_B.Length  != n) dock_q_B  = new Quaternion[n];
    }
}