using UdonSharp;
using UnityEngine;

/// <summary>
/// OrreryCraftOrbitLine
///
/// Robust first-pass orbit renderer for the orrery.
/// Uses nav invariants directly:
/// - eVec_E
/// - h_E
/// - p
/// - e
///
/// Fixes:
/// - double-precision sampling
/// - more stable circular fallback basis
/// - more conservative hyperbola detection
/// </summary>
public class OrreryCraftOrbitLine : UdonSharpBehaviour
{
    [Header("References")]
    public OrreryController orrery;
    public GuidanceNavCoreState nav;
    public BodyCatalog bodies;
    public LineRenderer line;

    [Header("Sampling")]
    [Range(16, 512)]
    public int ellipseSegments = 180;

    [Range(16, 256)]
    public int hyperbolaSegments = 96;

    [Tooltip("Shrink hyperbolic anomaly limit slightly to avoid huge endpoint excursions.")]
    [Range(0.5f, 0.999f)]
    public float hyperbolaNuLimitScale = 0.96f;

    [Header("Numerics")]
    [Tooltip("Treat orbits below this eccentricity as circular for basis construction.")]
    public double circularETol = 1e-3;

    [Tooltip("Extra margin around e=1 for orbit-type classification.")]
    public double parabolicTol = 1e-4;

    [Header("Visibility")]
    public bool hideWhenInvalid = true;

    [Header("Appearance")]
    public float lineWidth = 0.003f;

    private Vector3[] _positions;

    void LateUpdate()
    {
        if (orrery == null || nav == null || bodies == null || line == null)
            return;

        line.useWorldSpace = false;
        line.widthMultiplier = lineWidth;
        line.loop = false;

        if (!nav.valid || nav.muPrimary <= 0.0 || nav.p <= 0.0)
        {
            if (hideWhenInvalid) line.positionCount = 0;
            return;
        }

        // -----------------------------------------------------------------
        // Build stable orbit basis in inertial E using doubles
        // -----------------------------------------------------------------

        // W = orbit normal
        double wx = (double)nav.h_E.x;
        double wy = (double)nav.h_E.y;
        double wz = (double)nav.h_E.z;
        if (!NormalizeD(ref wx, ref wy, ref wz))
        {
            if (hideWhenInvalid) line.positionCount = 0;
            return;
        }

        // P = periapsis direction if eccentric enough, otherwise use current radial direction
        double px, py, pz;

        if (nav.e > circularETol && nav.eVec_E.sqrMagnitude > 1e-12f)
        {
            px = (double)nav.eVec_E.x;
            py = (double)nav.eVec_E.y;
            pz = (double)nav.eVec_E.z;

            // Project into plane defensively
            double pdotw = px * wx + py * wy + pz * wz;
            px -= pdotw * wx;
            py -= pdotw * wy;
            pz -= pdotw * wz;

            if (!NormalizeD(ref px, ref py, ref pz))
            {
                // fallback to current radial direction
                px = nav.r_x;
                py = nav.r_y;
                pz = nav.r_z;

                double rdotw = px * wx + py * wy + pz * wz;
                px -= rdotw * wx;
                py -= rdotw * wy;
                pz -= rdotw * wz;

                if (!NormalizeD(ref px, ref py, ref pz))
                {
                    if (hideWhenInvalid) line.positionCount = 0;
                    return;
                }
            }
        }
        else
        {
            // Circular fallback: current radial direction projected into orbit plane
            px = nav.r_x;
            py = nav.r_y;
            pz = nav.r_z;

            double rdotw = px * wx + py * wy + pz * wz;
            px -= rdotw * wx;
            py -= rdotw * wy;
            pz -= rdotw * wz;

            if (!NormalizeD(ref px, ref py, ref pz))
            {
                if (hideWhenInvalid) line.positionCount = 0;
                return;
            }
        }

        // Q = W x P
        double qx = wy * pz - wz * py;
        double qy = wz * px - wx * pz;
        double qz = wx * py - wy * px;

        if (!NormalizeD(ref qx, ref qy, ref qz))
        {
            if (hideWhenInvalid) line.positionCount = 0;
            return;
        }

        // Rebuild P = Q x W to ensure orthonormality
        px = qy * wz - qz * wy;
        py = qz * wx - qx * wz;
        pz = qx * wy - qy * wx;
        if (!NormalizeD(ref px, ref py, ref pz))
        {
            if (hideWhenInvalid) line.positionCount = 0;
            return;
        }

        // -----------------------------------------------------------------
        // Primary body inertial position
        // -----------------------------------------------------------------
        double bx, by, bz;
        bodies.GetBodyPos(nav.primaryId, out bx, out by, out bz);

        // -----------------------------------------------------------------
        // Orbit type
        // -----------------------------------------------------------------
        double e = nav.e;
        bool isEllipse = e < (1.0 - parabolicTol);
        bool isHyperbola = e > (1.0 + parabolicTol);

        if (!isEllipse && !isHyperbola)
        {
            // Near-parabolic region: skip for now
            if (hideWhenInvalid) line.positionCount = 0;
            return;
        }

        // -----------------------------------------------------------------
        // Sample ellipse
        // -----------------------------------------------------------------
        if (isEllipse)
        {
            int count = ellipseSegments + 1; // duplicate endpoint to close loop
            EnsureArray(count);

            for (int i = 0; i < count; i++)
            {
                double u = (double)i / (double)ellipseSegments;
                double nu = u * (2.0 * System.Math.PI);

                double rx, ry, rz;
                SampleOrbitPointInE(nav.p, e, nu, px, py, pz, qx, qy, qz, out rx, out ry, out rz);

                _positions[i] = orrery.MapWorldPointEToOrreryLocal(
                    bx + rx,
                    by + ry,
                    bz + rz
                );
            }

            line.positionCount = count;
            line.SetPositions(_positions);
            return;
        }

        // -----------------------------------------------------------------
        // Sample hyperbola
        // -----------------------------------------------------------------
        {
            double nuLimit = System.Math.Acos(-1.0 / e);
            nuLimit *= (double)hyperbolaNuLimitScale;

            int count = hyperbolaSegments + 1;
            EnsureArray(count);

            for (int i = 0; i < count; i++)
            {
                double u = (double)i / (double)hyperbolaSegments;
                double nu = (-nuLimit) + (2.0 * nuLimit * u);

                double rx, ry, rz;
                SampleOrbitPointInE(nav.p, e, nu, px, py, pz, qx, qy, qz, out rx, out ry, out rz);

                _positions[i] = orrery.MapWorldPointEToOrreryLocal(
                    bx + rx,
                    by + ry,
                    bz + rz
                );
            }

            line.positionCount = count;
            line.SetPositions(_positions);
        }
    }

    private void EnsureArray(int count)
    {
        if (_positions == null || _positions.Length != count)
            _positions = new Vector3[count];
    }

    /// <summary>
    /// r(nu) = p / (1 + e cos nu) * (cos nu * P + sin nu * Q)
    /// All math done in double precision.
    /// </summary>
    private static void SampleOrbitPointInE(
        double p, double e, double nu,
        double px, double py, double pz,
        double qx, double qy, double qz,
        out double rx, out double ry, out double rz)
    {
        double c = System.Math.Cos(nu);
        double s = System.Math.Sin(nu);

        double denom = 1.0 + e * c;
        if (System.Math.Abs(denom) < 1e-12)
            denom = (denom >= 0.0) ? 1e-12 : -1e-12;

        double r = p / denom;

        rx = r * (c * px + s * qx);
        ry = r * (c * py + s * qy);
        rz = r * (c * pz + s * qz);
    }

    private static bool NormalizeD(ref double x, ref double y, ref double z)
    {
        double m2 = x * x + y * y + z * z;
        if (m2 < 1e-24) return false;

        double inv = 1.0 / System.Math.Sqrt(m2);
        x *= inv;
        y *= inv;
        z *= inv;
        return true;
    }
}