using UdonSharp;
using UnityEngine;

/// <summary>
/// GuidanceNavCoreState
/// Per-tick navigation snapshot for guidance programs + UI planners.
/// Data-only container: GuidanceComputerCore is responsible for populating all fields.
/// 
/// Frame conventions:
/// - E: solver inertial frame (your heliocentric/SSB ecliptic inertial frame)
/// - B: craft body frame
/// - Primary body-fixed: uses +Z as north/pole axis (as per project convention)
/// 
/// Notes:
/// - rC/vC are craft heliocentric (in E).
/// - r/v are craft relative to primary body, still expressed in E basis (primary-centered inertial in E coords).
/// - RTN basis is derived from primary-relative r/v but expressed in E.
/// - Elements i/Ω/ω/ν are expressed in the primary body's equatorial reference plane.
/// </summary>
public class GuidanceNavCoreState : UdonSharpBehaviour
{
    // --------------------
    // Time
    // --------------------
    [Header("Time")]
    public double t;     // mission seconds
    public double jd;    // Julian date
    public double dt;    // seconds

    // --------------------
    // Craft heliocentric state (E)
    // --------------------
    [Header("Craft heliocentric inertial state (E)")]
    public double rC_x, rC_y, rC_z;   // meters
    public double vC_x, vC_y, vC_z;   // m/s

    // --------------------
    // Craft attitude (body -> E) and body rates
    // --------------------
    [Header("Craft attitude")]
    public Quaternion qBE = Quaternion.identity;  // body -> E

    [Header("Angular velocity (B)")]
    public double wB_x, wB_y, wB_z;              // rad/s in body frame

    // --------------------
    // Primary body selection + constants
    // --------------------
    [Header("Primary body")]
    public byte primaryId;
    public double muPrimary;         // m^3/s^2
    public double radiusPrimary;     // meters
    public double soiRadiusPrimary;  // meters (0 if not applicable)

    // --------------------
    // Primary inertial state (E)
    // --------------------
    [Header("Primary body state (E)")]
    public double rP_x, rP_y, rP_z;  // meters
    public double vP_x, vP_y, vP_z;  // m/s

    [Header("Primary rotation in inertial (E)")]
    public double omegaP_x, omegaP_y, omegaP_z;  // rad/s in E
    public Quaternion qPF2E = Quaternion.identity; // primary body-fixed -> E

    // Primary equator basis in E (derived from qPF2E; +Z is north)
    [Header("Primary equator basis (E)")]
    public Vector3 Ieq_E = Vector3.right; // primary-fixed +X expressed in E
    public Vector3 Jeq_E = Vector3.up;    // primary-fixed +Y expressed in E
    public Vector3 Keq_E = Vector3.forward; // primary-fixed +Z (north) expressed in E

    // --------------------
    // Primary-relative craft state (still in E basis)
    // --------------------
    [Header("Craft relative to primary (E basis)")]
    public double r_x, r_y, r_z;     // meters
    public double v_x, v_y, v_z;     // m/s
    public double rMag;             // meters
    public double vMag;             // m/s

    // --------------------
    // RTN basis (E)
    // --------------------
    [Header("RTN basis at craft (E)")]
    public Vector3 Rhat_E = Vector3.right;
    public Vector3 That_E = Vector3.up;
    public Vector3 Nhat_E = Vector3.forward;

    // --------------------
    // Orbit invariants / conic scalars (primary-relative)
    // --------------------
    [Header("Orbit invariants (primary-relative, in E basis)")]
    public Vector3 h_E = Vector3.forward;     // specific angular momentum vector (direction in E)
    public double hMag;                       // |h| (units: m^2/s)
    public Vector3 eVec_E = Vector3.zero;     // eccentricity vector (dimensionless)
    public double e;                          // eccentricity magnitude

    [Header("Conic scalars (primary-relative)")]
    public double a;                          // semi-major axis (m) (may be negative for hyperbolic)
    public double p;                          // semi-latus rectum (m)
    public double energy;                     // specific orbital energy (J/kg = m^2/s^2)

    // --------------------
    // Elements in primary equatorial reference plane
    // --------------------
    [Header("Elements in primary equatorial reference plane (radians)")]
    public double iRad;
    public double raanRad;
    public double argpRad;
    public double nuRad;

    // --------------------
    // Optional: quick UI/debug fields
    // --------------------
    [Header("Debug / validity")]
    public bool valid;            // core can set false if muPrimary is 0, etc.
    public double lastBuildTime;  // optional: time stamp when snapshot built (can mirror t)
}