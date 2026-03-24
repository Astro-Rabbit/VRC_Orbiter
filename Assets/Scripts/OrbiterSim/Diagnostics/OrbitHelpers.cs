using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// OrbitHelpers
///
/// Provides small, reusable orbital-geometry utilities intended to operate on either:
/// (1) an instantaneous relative inertial state (r,v) for osculating conic fitting, or
/// (2) already-computed conic quantities (elements) and invariant vectors (h, e).
///
/// The default outputs are expressed in the input inertial frame. Reference-plane re-expression
/// (e.g., body-equatorial angles) is provided as an explicit helper and is not applied implicitly.
///
/// Included helpers:
/// - Conic fit: computes osculating elements and invariant vectors (h, eVec) from a relative state (r,v).
/// - Angle conversion: re-expresses (i, Ω, ω) in a body-equatorial reference frame using a body-to-inertial quaternion.
/// - Node anomalies: returns true anomaly at ascending/descending nodes from ω (practical convention).
/// - Radius intersection: returns true anomaly solution(s) where r(ν) equals a requested radius.
/// - Time-of-flight: returns time to reach a target true anomaly (elliptic forward-wrapped; hyperbolic signed).
/// - Anomaly propagation: advances true anomaly by a time offset using Kepler’s equation.
/// - RTN basis: builds the instantaneous Radial–Transverse–Normal orthonormal basis from (r, v).
/// - RTN composition: builds an inertial vector from RTN components.
/// </summary>
public class OrbitHelpers : UdonSharpBehaviour
{

    /// <summary>
    /// Computes osculating two-body conic parameters from a relative inertial state (r,v).
    ///
    /// The solution is expressed in the input inertial frame (e.g., heliocentric ecliptic inertial).
    /// A reference-plane conversion is intentionally not performed here; angle re-expression is expected
    /// to be handled by higher-level helpers when required.
    ///
    /// Computed scalars:
    /// - a: semi-major axis (signed; a>0 ellipse, a<0 hyperbola)
    /// - e: eccentricity magnitude
    /// - i, Ω, ω, ν: classical elements relative to the inertial +Z reference plane
    ///
    /// Computed vectors:
    /// - h: specific angular momentum vector (r×v)
    /// - eVec: eccentricity vector
    ///
    /// Inputs:
    /// - (rx,ry,rz): relative position [m]
    /// - (vx,vy,vz): relative velocity [m/s]
    /// - mu: gravitational parameter [m^3/s^2]
    /// - eTol: circular tolerance
    /// - nTol: node tolerance
    /// - hTol: angular momentum tolerance
    /// - energyTol: near-parabolic exclusion tolerance (|ε| <= energyTol)
    ///
    /// Outputs:
    /// - aMeters, e, iRad, raanRad, argpRad, nuRad
    /// - hx,hy,hz, ex,ey,ez
    /// - rMeters, vMetersPerSec, specificEnergy
    ///
    /// Returns:
    /// True if a non-degenerate (non-radial, non-parabolic) solution is produced.
    /// </summary>
    public static bool TryConicFromState(
        double rx, double ry, double rz,
        double vx, double vy, double vz,
        double mu,
        double eTol,
        double nTol,
        double hTol,
        double energyTol,
        out double aMeters,
        out double e,
        out double iRad,
        out double raanRad,
        out double argpRad,
        out double nuRad,
        out double hx, out double hy, out double hz,
        out double ex, out double ey, out double ez,
        out double rMeters,
        out double vMetersPerSec,
        out double specificEnergy)
    {
        aMeters = 0.0;
        e = 0.0;
        iRad = 0.0;
        raanRad = 0.0;
        argpRad = 0.0;
        nuRad = 0.0;

        hx = hy = hz = 0.0;
        ex = ey = ez = 0.0;

        rMeters = 0.0;
        vMetersPerSec = 0.0;
        specificEnergy = 0.0;

        if (mu <= 0.0) return false;

        double r2 = rx * rx + ry * ry + rz * rz;
        if (r2 <= 0.0) return false;
        double rMag = Math.Sqrt(r2);
        if (rMag < 1e-12) return false;

        double v2 = vx * vx + vy * vy + vz * vz;
        double vMag = Math.Sqrt(v2);

        rMeters = rMag;
        vMetersPerSec = vMag;

        // h = r x v
        hx = ry * vz - rz * vy;
        hy = rz * vx - rx * vz;
        hz = rx * vy - ry * vx;

        double h2 = hx * hx + hy * hy + hz * hz;
        double hMag = Math.Sqrt(h2);
        if (hMag < hTol) return false;

        // Inclination relative to inertial +Z reference plane.
        iRad = Math.Acos(Clamp(hz / hMag, -1.0, 1.0));

        // Node vector n = k x h = (-hy, hx, 0)
        double nx = -hy;
        double ny = hx;
        double nz = 0.0;
        double nMag = Math.Sqrt(nx * nx + ny * ny);

        // eVec = (v x h)/mu - r/|r|
        double vxh_x = vy * hz - vz * hy;
        double vxh_y = vz * hx - vx * hz;
        double vxh_z = vx * hy - vy * hx;

        ex = (vxh_x / mu) - (rx / rMag);
        ey = (vxh_y / mu) - (ry / rMag);
        ez = (vxh_z / mu) - (rz / rMag);

        e = Math.Sqrt(ex * ex + ey * ey + ez * ez);

        // Specific orbital energy ε = v^2/2 - mu/r
        specificEnergy = 0.5 * v2 - mu / rMag;
        if (Math.Abs(specificEnergy) <= energyTol) return false; // near-parabolic excluded

        // Semi-major axis (signed)
        aMeters = -mu / (2.0 * specificEnergy);

        // RAAN Ω
        if (nMag < nTol)
        {
            raanRad = 0.0;
        }
        else
        {
            raanRad = Math.Atan2(ny, nx);
            if (raanRad < 0.0) raanRad += 2.0 * Math.PI;
        }

        // Argument of periapsis ω
        if (nMag < nTol || e < eTol)
        {
            argpRad = 0.0;
        }
        else
        {
            double ndote = nx * ex + ny * ey + nz * ez;
            double cosw = Clamp(ndote / (nMag * e), -1.0, 1.0);

            // sign = (n x e) · h
            double c_x = ny * ez - nz * ey;
            double c_y = nz * ex - nx * ez;
            double c_z = nx * ey - ny * ex;
            double sign = c_x * hx + c_y * hy + c_z * hz;

            double w = Math.Acos(cosw);
            argpRad = (sign >= 0.0) ? w : (2.0 * Math.PI - w);
        }

        // True anomaly ν
        if (e < eTol)
        {
            // Circular: measure from node if possible, otherwise from +X.
            if (nMag < nTol)
            {
                nuRad = Math.Atan2(ry, rx);
                if (nuRad < 0.0) nuRad += 2.0 * Math.PI;
            }
            else
            {
                double ndotr = nx * rx + ny * ry + nz * rz;
                double cosnu = Clamp(ndotr / (nMag * rMag), -1.0, 1.0);

                // sign = (n x r) · h
                double nr_x = ny * rz - nz * ry;
                double nr_y = nz * rx - nx * rz;
                double nr_z = nx * ry - ny * rx;
                double sign = nr_x * hx + nr_y * hy + nr_z * hz;

                double nu = Math.Acos(cosnu);
                nuRad = (sign >= 0.0) ? nu : (2.0 * Math.PI - nu);
            }
        }
        else
        {
            double edotr = ex * rx + ey * ry + ez * rz;
            double cosnu = Clamp(edotr / (e * rMag), -1.0, 1.0);

            // sign = (e x r) · h
            double er_x = ey * rz - ez * ry;
            double er_y = ez * rx - ex * rz;
            double er_z = ex * ry - ey * rx;
            double sign = er_x * hx + er_y * hy + er_z * hz;

            double nu = Math.Acos(cosnu);
            nuRad = (sign >= 0.0) ? nu : (2.0 * Math.PI - nu);
        }

        return true;
    }



    /// <summary>
    /// Converts orbital orientation angles (i, Ω, ω) into a body-equatorial reference frame without
    /// recomputing the conic shape (a, |e|, ν, energy).
    ///
    /// The body equator is defined by Zref = qBodyToSolver * (+Z_body). The reference X-axis is defined
    /// by projecting the solver +X direction (Aries) into that equatorial plane.
    ///
    /// Inputs:
    /// - (hx,hy,hz): specific angular momentum vector r×v in solver inertial coordinates.
    /// - (ex,ey,ez): eccentricity vector in solver inertial coordinates.
    /// - qBodyToSolver: rotation from body-fixed basis to solver inertial basis.
    /// - eTol: circular-orbit tolerance (|e| < eTol ⇒ ω set to 0 by convention).
    /// - nTol: equatorial-orbit tolerance (|n| < nTol ⇒ Ω set to 0; ω becomes longitude of periapsis).
    /// - hTol: validity tolerance for |h|.
    ///
    /// Outputs:
    /// - iRad: inclination relative to body equator [rad].
    /// - raanRad: RAAN relative to projected Aries axis [rad].
    /// - argpRad: argument of periapsis [rad] (or longitude of periapsis in the equatorial degeneracy).
    /// </summary>
    public static bool TryConvertAnglesToBodyEquatorial(
        double hx, double hy, double hz,
        double ex, double ey, double ez,
        Quaternion qBodyToSolver,
        double eTol, double nTol, double hTol,
        out double iRad, out double raanRad, out double argpRad)
    {
        iRad = 0.0;
        raanRad = 0.0;
        argpRad = 0.0;

        // |h|
        double hMag = Math.Sqrt(hx * hx + hy * hy + hz * hz);
        if (hMag < hTol) return false;

        // |e|
        double eMag = Math.Sqrt(ex * ex + ey * ey + ez * ez);

        // Zref = q * (+Z_body) expressed in solver inertial (+Z_body = Vector3.forward in standard coords).
        Vector3 zf = qBodyToSolver * Vector3.forward;
        double zx = zf.x, zy = zf.y, zz = zf.z;
        double zMag = Math.Sqrt(zx * zx + zy * zy + zz * zz);
        if (zMag < 1e-15) return false;
        zx /= zMag; zy /= zMag; zz /= zMag;

        // Xseed = +X_solver (Aries).
        double xsx = 1.0, xsy = 0.0, xsz = 0.0;

        // Xref = projection of Xseed into equatorial plane.
        double dotXZ = xsx * zx + xsy * zy + xsz * zz;
        double xrx = xsx - dotXZ * zx;
        double xry = xsy - dotXZ * zy;
        double xrz = xsz - dotXZ * zz;

        double xrMag = Math.Sqrt(xrx * xrx + xry * xry + xrz * xrz);
        if (xrMag < 1e-12)
        {
            // Fallback seed: +Y_solver.
            xsx = 0.0; xsy = 1.0; xsz = 0.0;
            dotXZ = xsx * zx + xsy * zy + xsz * zz;
            xrx = xsx - dotXZ * zx;
            xry = xsy - dotXZ * zy;
            xrz = xsz - dotXZ * zz;
            xrMag = Math.Sqrt(xrx * xrx + xry * xry + xrz * xrz);
            if (xrMag < 1e-12) return false;
        }
        xrx /= xrMag; xry /= xrMag; xrz /= xrMag;

        // Yref = Zref × Xref.
        double yrx = zy * xrz - zz * xry;
        double yry = zz * xrx - zx * xrz;
        double yrz = zx * xry - zy * xrx;
        double yrMag = Math.Sqrt(yrx * yrx + yry * yry + yrz * yrz);
        if (yrMag < 1e-12) return false;
        yrx /= yrMag; yry /= yrMag; yrz /= yrMag;

        // i = acos( (h·Zref)/|h| ).
        double cosi = Clamp((hx * zx + hy * zy + hz * zz) / hMag, -1.0, 1.0);
        iRad = Math.Acos(cosi);

        // n = Zref × h.
        double nx = zy * hz - zz * hy;
        double ny = zz * hx - zx * hz;
        double nz = zx * hy - zy * hx;
        double nMag = Math.Sqrt(nx * nx + ny * ny + nz * nz);

        // Ω = atan2( n·Yref, n·Xref ).
        if (nMag < nTol)
        {
            raanRad = 0.0;
        }
        else
        {
            double ndotX = nx * xrx + ny * xry + nz * xrz;
            double ndotY = nx * yrx + ny * yry + nz * yrz;
            raanRad = Wrap2Pi(Math.Atan2(ndotY, ndotX));
        }

        // ω handling.
        if (eMag < eTol)
        {
            // Circular: ω is undefined; set to 0 by convention.
            argpRad = 0.0;
            return true;
        }

        if (nMag < nTol)
        {
            // Equatorial: Ω undefined; store longitude of periapsis in ω relative to (Xref,Yref).
            double edotX = ex * xrx + ey * xry + ez * xrz;
            double edotY = ex * yrx + ey * yry + ez * yrz;
            argpRad = Wrap2Pi(Math.Atan2(edotY, edotX));
            return true;
        }

        // n-hat
        double nnx = nx / nMag;
        double nny = ny / nMag;
        double nnz = nz / nMag;

        // h-hat
        double hhx = hx / hMag;
        double hhy = hy / hMag;
        double hhz = hz / hMag;

        // t-hat = hhat × nhat.
        double tx = hhy * nnz - hhz * nny;
        double ty = hhz * nnx - hhx * nnz;
        double tz = hhx * nny - hhy * nnx;

        double edotN = ex * nnx + ey * nny + ez * nnz;
        double edotT = ex * tx  + ey * ty  + ez * tz;

        argpRad = Wrap2Pi(Math.Atan2(edotT, edotN));
        return true;
    }


    /// <summary>
    /// Computes the true anomaly at the ascending and descending nodes.
    ///
    /// Inputs:
    ///     argpRad  - Argument of periapsis (radians), already defined
    ///                relative to the desired reference plane.
    ///     nodeDefined - True if the orbit is not equatorial relative to
    ///                   the reference plane (i.e., node line exists).
    ///
    /// Outputs:
    ///     nuAscendingRad  - True anomaly at ascending node [0, 2π)
    ///     nuDescendingRad - True anomaly at descending node [0, 2π)
    ///
    /// Returns:
    ///     True if nodes are defined and values are valid.
    ///     False if orbit is equatorial relative to reference plane.
    /// </summary>
    public static bool TryGetNodeTrueAnomalies(
        double argpRad,
        bool nodeDefined,
        out double nuAscendingRad,
        out double nuDescendingRad)
    {
        nuAscendingRad = 0.0;
        nuDescendingRad = 0.0;

        if (!nodeDefined)
            return false;

        // ν_AN = -ω
        nuAscendingRad = Wrap2Pi(-argpRad);

        // ν_DN = π - ω
        nuDescendingRad = Wrap2Pi(Math.PI - argpRad);

        return true;
    }

    /// <summary>
    /// Computes true anomaly values at which the orbit radius equals a requested distance using
    /// r(ν) = p / (1 + e cosν), with p inferred from (a,e) as p = a(1 - e²).
    ///
    /// Inputs:
    /// - aMeters: semi-major axis [m] (signed: a>0 ellipse, a<0 hyperbola).
    /// - e: eccentricity magnitude.
    /// - rMeters: requested radius [m].
    /// - eTol: circular-orbit tolerance.
    /// - rTol: radius match tolerance used for the circular case.
    ///
    /// Outputs:
    /// - nu1Rad, nu2Rad: solution anomalies. Ellipse solutions are wrapped to [0,2π); hyperbola
    ///   solutions are returned as a symmetric pair (-ν,+ν) without wrapping.
    /// - twoSolutions: indicates whether two distinct solutions are returned.
    /// </summary>
    public static bool TryGetTrueAnomalyAtRadius(
        double aMeters,
        double e,
        double rMeters,
        double eTol,
        double rTol,
        out double nu1Rad,
        out double nu2Rad,
        out bool twoSolutions)
    {
        nu1Rad = 0.0;
        nu2Rad = 0.0;
        twoSolutions = false;

        if (rMeters <= 0.0) return false;

        // Near-circular: r is effectively constant.
        if (Math.Abs(e) < eTol)
        {
            if (Math.Abs(rMeters - Math.Abs(aMeters)) <= rTol)
            {
                nu1Rad = 0.0;
                nu2Rad = 0.0;
                twoSolutions = false;
                return true;
            }
            return false;
        }

        double p = aMeters * (1.0 - e * e);
        if (p <= 0.0) return false;

        double cosNu = (p / rMeters - 1.0) / e;

        if (cosNu > 1.0)
        {
            if (cosNu - 1.0 > 1e-12) return false;
            cosNu = 1.0;
        }
        else if (cosNu < -1.0)
        {
            if (-1.0 - cosNu > 1e-12) return false;
            cosNu = -1.0;
        }

        double nuAbs = Math.Acos(cosNu);

        if (e < 1.0)
        {
            nu1Rad = Wrap2Pi(nuAbs);
            nu2Rad = Wrap2Pi(2.0 * Math.PI - nuAbs);
            twoSolutions = Math.Abs(nu2Rad - nu1Rad) > 1e-12;
            return true;
        }
        else
        {
            nu1Rad = -nuAbs;
            nu2Rad = +nuAbs;
            twoSolutions = nuAbs > 1e-12;
            return true;
        }
    }

    /// <summary>
    /// Computes time-of-flight from a current true anomaly to a target true anomaly on a two-body conic.
    ///
    /// Ellipse: mean anomaly is wrapped to return forward time within the current revolution.
    /// Hyperbola: non-periodic; returned time may be negative.
    ///
    /// Inputs:
    /// - aMeters: semi-major axis [m] (a>0 ellipse, a<0 hyperbola).
    /// - e: eccentricity magnitude.
    /// - mu: gravitational parameter [m³/s²].
    /// - nuNowRad: current true anomaly [rad].
    /// - nuTargetRad: target true anomaly [rad].
    /// - eTol: near-parabolic exclusion tolerance (|1-e| <= eTol).
    ///
    /// Output:
    /// - dtSeconds: time-of-flight [s].
    /// </summary>
    public static bool TryTimeToTrueAnomaly(
        double aMeters,
        double e,
        double mu,
        double nuNowRad,
        double nuTargetRad,
        double eTol,
        out double dtSeconds)
    {
        dtSeconds = 0.0;

        if (mu <= 0.0) return false;
        if (aMeters == 0.0) return false;
        if (Math.Abs(1.0 - e) <= eTol) return false;

        if (e < 1.0)
        {
            if (aMeters <= 0.0) return false;

            double n = Math.Sqrt(mu / (aMeters * aMeters * aMeters));
            if (!(n > 0.0)) return false;

            double Mnow, Mtar;
            if (!TryMeanAnomalyFromTrueAnomaly_Ellipse(e, nuNowRad, out Mnow)) return false;
            if (!TryMeanAnomalyFromTrueAnomaly_Ellipse(e, nuTargetRad, out Mtar)) return false;

            double dM = Wrap2Pi(Mtar - Mnow);
            dtSeconds = dM / n;
            return true;
        }

        if (e > 1.0)
        {
            if (aMeters >= 0.0) return false;

            double aAbs = -aMeters;
            double n = Math.Sqrt(mu / (aAbs * aAbs * aAbs));
            if (!(n > 0.0)) return false;

            double MhNow, MhTar;
            if (!TryMeanAnomalyFromTrueAnomaly_Hyperbola(e, nuNowRad, out MhNow)) return false;
            if (!TryMeanAnomalyFromTrueAnomaly_Hyperbola(e, nuTargetRad, out MhTar)) return false;

            dtSeconds = (MhTar - MhNow) / n;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Propagates true anomaly by a time offset using Keplerian motion.
    ///
    /// Ellipse: advances mean anomaly by n·dt, solves Kepler’s equation, and returns ν wrapped to [0,2π).
    /// Hyperbola: advances hyperbolic mean anomaly by n·dt, solves Kepler’s equation, and returns ν (not wrapped).
    ///
    /// Inputs:
    /// - aMeters: semi-major axis [m] (a>0 ellipse, a<0 hyperbola).
    /// - e: eccentricity magnitude.
    /// - mu: gravitational parameter [m³/s²].
    /// - nuNowRad: current true anomaly [rad].
    /// - dtSeconds: time offset [s] (may be negative).
    /// - eTol: near-parabolic exclusion tolerance (|1-e| <= eTol).
    /// - maxIters: maximum Newton iterations.
    /// - tol: solver tolerance.
    ///
    /// Output:
    /// - nuFutureRad: propagated true anomaly [rad].
    /// </summary>
    public static bool TryTrueAnomalyAtTime(
        double aMeters,
        double e,
        double mu,
        double nuNowRad,
        double dtSeconds,
        double eTol,
        int maxIters,
        double tol,
        out double nuFutureRad)
    {
        nuFutureRad = 0.0;

        if (mu <= 0.0) return false;
        if (maxIters <= 0) maxIters = 12;
        if (tol <= 0.0) tol = 1e-10;
        if (Math.Abs(1.0 - e) <= eTol) return false;

        if (e < 1.0)
        {
            if (aMeters <= 0.0) return false;

            double n = Math.Sqrt(mu / (aMeters * aMeters * aMeters));
            if (!(n > 0.0)) return false;

            double M0;
            if (!TryMeanAnomalyFromTrueAnomaly_Ellipse(e, nuNowRad, out M0)) return false;

            double M1 = Wrap2Pi(M0 + n * dtSeconds);

            double E1;
            if (!TrySolveKeplerEllipse_E(M1, e, maxIters, tol, out E1)) return false;

            nuFutureRad = Wrap2Pi(TrueAnomalyFromE_Ellipse(E1, e));
            return true;
        }

        if (e > 1.0)
        {
            if (aMeters >= 0.0) return false;

            double aAbs = -aMeters;
            double n = Math.Sqrt(mu / (aAbs * aAbs * aAbs));
            if (!(n > 0.0)) return false;

            double Mh0;
            if (!TryMeanAnomalyFromTrueAnomaly_Hyperbola(e, nuNowRad, out Mh0)) return false;

            double Mh1 = Mh0 + n * dtSeconds;

            double H1;
            if (!TrySolveKeplerHyperbola_H(Mh1, e, maxIters, tol, out H1)) return false;

            nuFutureRad = TrueAnomalyFromH_Hyperbola(H1, e);
            return true;
        }

        return false;
    }


    /// <summary>
    /// Converts true anomaly to mean anomaly for either an elliptic or hyperbolic conic.
    ///
    /// Ellipse:
    /// - returns the standard mean anomaly M in [0, 2π).
    ///
    /// Hyperbola:
    /// - returns the hyperbolic mean anomaly Mh (signed, not wrapped).
    ///
    /// Inputs:
    /// - e: eccentricity magnitude.
    /// - nuRad: true anomaly [rad].
    /// - eTol: near-parabolic exclusion tolerance (|1-e| <= eTol).
    ///
    /// Output:
    /// - meanAnomalyRad:
    ///     ellipse   -> M
    ///     hyperbola -> Mh
    /// </summary>
    public static bool TryMeanAnomalyFromTrueAnomaly(
        double e,
        double nuRad,
        double eTol,
        out double meanAnomalyRad)
    {
        meanAnomalyRad = 0.0;

        if (Math.Abs(1.0 - e) <= eTol)
            return false;

        if (e < 1.0)
            return TryMeanAnomalyFromTrueAnomaly_Ellipse(e, nuRad, out meanAnomalyRad);

        if (e > 1.0)
            return TryMeanAnomalyFromTrueAnomaly_Hyperbola(e, nuRad, out meanAnomalyRad);

        return false;
    }

    /// <summary>
    /// Propagates a primary-relative two-body conic from inertial-frame elements and a true anomaly
    /// defined at epochT, returning future primary-relative state in the same solver inertial frame.
    ///
    /// IMPORTANT:
    /// - The orientation angles (i, Ω, ω) must be expressed in the solver inertial reference plane,
    ///   not a body-equatorial/display reference plane.
    /// - This helper is intended for propagation from nav's inertial-fit angles, e.g.
    ///   iInertialRad / raanInertialRad / argpInertialRad.
    ///
    /// Inputs:
    /// - aMeters, e: conic shape
    /// - iInertialRad, raanInertialRad, argpInertialRad: inertial-frame orientation
    /// - nuAtEpochRad: true anomaly at epochT
    /// - epochT: epoch mission time [s]
    /// - sampleT: target mission time [s]
    /// - mu: gravitational parameter [m^3/s^2]
    /// - maxIters: Kepler solver iterations
    /// - eTol: near-parabolic exclusion tolerance
    ///
    /// Outputs:
    /// - rx,ry,rz: propagated relative position [m]
    /// - vx,vy,vz: propagated relative velocity [m/s]
    /// </summary>
    public static bool TryPropagateConicStateFromElements(
        double aMeters,
        double e,
        double iInertialRad,
        double raanInertialRad,
        double argpInertialRad,
        double nuAtEpochRad,
        double epochT,
        double sampleT,
        double mu,
        int maxIters,
        double eTol,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        rx = ry = rz = 0.0;
        vx = vy = vz = 0.0;

        if (mu <= 0.0) return false;
        if (maxIters <= 0) maxIters = 12;
        if (Math.Abs(1.0 - e) <= eTol) return false;

        double mean0;
        if (!TryMeanAnomalyFromTrueAnomaly(e, nuAtEpochRad, eTol, out mean0))
            return false;

        double dt = sampleT - epochT;

        // -------------------------
        // Elliptic
        // -------------------------
        if (e < 1.0)
        {
            if (aMeters <= 0.0) return false;

            double n = Math.Sqrt(mu / (aMeters * aMeters * aMeters));
            if (!(n > 0.0)) return false;

            double M = Wrap2Pi(mean0 + n * dt);

            double E;
            if (!TrySolveKeplerEllipse_E(M, e, maxIters, 1e-10, out E))
                return false;

            double cosE = Math.Cos(E);
            double sinE = Math.Sin(E);

            double fac = Math.Sqrt(Math.Max(0.0, 1.0 - e * e));

            double xpf = aMeters * (cosE - e);
            double ypf = aMeters * (fac * sinE);

            double edot = n / (1.0 - e * cosE);
            double vxpf = -aMeters * sinE * edot;
            double vypf =  aMeters * fac * cosE * edot;

            return TryPQWStateToInertial(
                raanInertialRad, iInertialRad, argpInertialRad,
                xpf, ypf, 0.0,
                vxpf, vypf, 0.0,
                out rx, out ry, out rz,
                out vx, out vy, out vz);
        }

        // -------------------------
        // Hyperbolic
        // -------------------------
        if (e > 1.0)
        {
            if (aMeters >= 0.0) return false;

            double absA = -aMeters;
            double n = Math.Sqrt(mu / (absA * absA * absA));
            if (!(n > 0.0)) return false;

            double Mh = mean0 + n * dt;

            double H;
            if (!TrySolveKeplerHyperbola_H(Mh, e, maxIters, 1e-10, out H))
                return false;

            double coshH = Math.Cosh(H);
            double sinhH = Math.Sinh(H);
            double fac = Math.Sqrt(e * e - 1.0);

            double xpf = absA * (e - coshH);
            double ypf = absA * (fac * sinhH);

            double dMdH = e * coshH - 1.0;
            if (Math.Abs(dMdH) < 1e-15) return false;

            double Hdot = n / dMdH;

            double vxpf = (-absA * sinhH) * Hdot;
            double vypf = ( absA * fac * coshH) * Hdot;

            return TryPQWStateToInertial(
                raanInertialRad, iInertialRad, argpInertialRad,
                xpf, ypf, 0.0,
                vxpf, vypf, 0.0,
                out rx, out ry, out rz,
                out vx, out vy, out vz);
        }

        return false;
    }


    /// <summary>
    /// Builds the instantaneous RTN (Radial–Transverse–Normal) orthonormal basis from a relative state.
    ///
    /// R̂ = r/|r|, N̂ = (r×v)/|r×v|, and T̂ = N̂×R̂. The basis is returned in the same inertial frame as (r,v).
    ///
    /// Inputs:
    /// - (rx,ry,rz): relative position [m].
    /// - (vx,vy,vz): relative velocity [m/s].
    /// - rTol: minimum |r| for a valid basis.
    /// - hTol: minimum |r×v| for a valid basis.
    ///
    /// Outputs:
    /// - rHat, tHat, nHat: unit basis vectors.
    /// </summary>
    public static bool TryBuildRTNBasis(
        double rx, double ry, double rz,
        double vx, double vy, double vz,
        double rTol,
        double hTol,
        out Vector3 rHat,
        out Vector3 tHat,
        out Vector3 nHat)
    {
        rHat = Vector3.zero;
        tHat = Vector3.zero;
        nHat = Vector3.zero;

        double r2 = rx * rx + ry * ry + rz * rz;
        if (r2 <= 0.0) return false;

        double rMag = Math.Sqrt(r2);
        if (rMag < rTol) return false;

        double invR = 1.0 / rMag;
        double rhx = rx * invR;
        double rhy = ry * invR;
        double rhz = rz * invR;

        double hx = ry * vz - rz * vy;
        double hy = rz * vx - rx * vz;
        double hz = rx * vy - ry * vx;

        double h2 = hx * hx + hy * hy + hz * hz;
        if (h2 <= 0.0) return false;

        double hMag = Math.Sqrt(h2);
        if (hMag < hTol) return false;

        double invH = 1.0 / hMag;
        double nhx = hx * invH;
        double nhy = hy * invH;
        double nhz = hz * invH;

        double thx = nhy * rhz - nhz * rhy;
        double thy = nhz * rhx - nhx * rhz;
        double thz = nhx * rhy - nhy * rhx;

        double t2 = thx * thx + thy * thy + thz * thz;
        if (t2 <= 0.0) return false;

        double tMag = Math.Sqrt(t2);
        if (tMag < 1e-15) return false;

        double invT = 1.0 / tMag;
        thx *= invT; thy *= invT; thz *= invT;

        rHat = new Vector3((float)rhx, (float)rhy, (float)rhz);
        tHat = new Vector3((float)thx, (float)thy, (float)thz);
        nHat = new Vector3((float)nhx, (float)nhy, (float)nhz);

        return true;
    }

    /// <summary>
    /// Builds an inertial vector from RTN components using vec = rComp*R̂ + tComp*T̂ + nComp*N̂.
    ///
    /// Inputs:
    /// - rComp, tComp, nComp: RTN components.
    /// - rHat, tHat, nHat: RTN unit basis vectors.
    /// </summary>
    public static Vector3 BuildFromRTN(
        float rComp,
        float tComp,
        float nComp,
        Vector3 rHat,
        Vector3 tHat,
        Vector3 nHat)
    {
        return rComp * rHat + tComp * tHat + nComp * nHat;
    }

    // -------------------------
    // Internal anomaly helpers
    // -------------------------

    private static bool TryMeanAnomalyFromTrueAnomaly_Ellipse(double e, double nuRad, out double M)
    {
        M = 0.0;
        if (e < 0.0 || e >= 1.0) return false;

        double s = Math.Sin(0.5 * nuRad);
        double c = Math.Cos(0.5 * nuRad);

        double a = Math.Sqrt(1.0 - e) * s;
        double b = Math.Sqrt(1.0 + e) * c;

        double E = 2.0 * Math.Atan2(a, b);
        E = Wrap2Pi(E);

        M = Wrap2Pi(E - e * Math.Sin(E));
        return true;
    }

    private static bool TrySolveKeplerEllipse_E(double M, double e, int maxIters, double tol, out double E)
    {
        E = 0.0;
        if (e < 0.0 || e >= 1.0) return false;

        M = Wrap2Pi(M);

        double x = (e < 0.8) ? M : Math.PI;

        for (int k = 0; k < maxIters; k++)
        {
            double s = Math.Sin(x);
            double c = Math.Cos(x);

            double f = x - e * s - M;
            double fp = 1.0 - e * c;

            if (Math.Abs(fp) < 1e-15) return false;

            double dx = -f / fp;
            x += dx;

            if (Math.Abs(dx) <= tol)
            {
                E = Wrap2Pi(x);
                return true;
            }
        }

        E = Wrap2Pi(x);
        return true;
    }

    private static double TrueAnomalyFromE_Ellipse(double E, double e)
    {
        double t = Math.Tan(0.5 * E);
        double k = Math.Sqrt((1.0 + e) / (1.0 - e));
        return 2.0 * Math.Atan2(k * t, 1.0);
    }

    private static bool TryMeanAnomalyFromTrueAnomaly_Hyperbola(double e, double nuRad, out double Mh)
    {
        Mh = 0.0;
        if (e <= 1.0) return false;

        double tanHalfNu = Math.Tan(0.5 * nuRad);
        double k = Math.Sqrt((e - 1.0) / (e + 1.0));
        double x = k * tanHalfNu;

        if (Math.Abs(x) >= 1.0) return false;

        double H = 2.0 * Atanh(x);

        Mh = e * Math.Sinh(H) - H;
        return true;
    }

    private static bool TrySolveKeplerHyperbola_H(double Mh, double e, int maxIters, double tol, out double H)
    {
        H = 0.0;
        if (e <= 1.0) return false;

        double x = Asinh(Mh / e);

        for (int k = 0; k < maxIters; k++)
        {
            double sh = Math.Sinh(x);
            double ch = Math.Cosh(x);

            double f = e * sh - x - Mh;
            double fp = e * ch - 1.0;

            if (Math.Abs(fp) < 1e-15) return false;

            double dx = -f / fp;
            x += dx;

            if (Math.Abs(dx) <= tol)
            {
                H = x;
                return true;
            }
        }

        H = x;
        return true;
    }

    private static double TrueAnomalyFromH_Hyperbola(double H, double e)
    {
        double th = Math.Tanh(0.5 * H);
        double k = Math.Sqrt((e + 1.0) / (e - 1.0));
        return 2.0 * Math.Atan(k * th);
    }


    /// <summary>
    /// Rotates a PQW-frame position/velocity state into the solver inertial frame
    /// using the standard 3-1-3 sequence (Ω, i, ω).
    /// </summary>
    private static bool TryPQWStateToInertial(
        double raanRad,
        double iRad,
        double argpRad,
        double rpx, double rpy, double rpz,
        double vpx, double vpy, double vpz,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        rx = ry = rz = 0.0;
        vx = vy = vz = 0.0;

        double m00, m01, m02;
        double m10, m11, m12;
        double m20, m21, m22;

        BuildPQWToInertialMatrix(
            raanRad, iRad, argpRad,
            out m00, out m01, out m02,
            out m10, out m11, out m12,
            out m20, out m21, out m22);

        rx = m00 * rpx + m01 * rpy + m02 * rpz;
        ry = m10 * rpx + m11 * rpy + m12 * rpz;
        rz = m20 * rpx + m21 * rpy + m22 * rpz;

        vx = m00 * vpx + m01 * vpy + m02 * vpz;
        vy = m10 * vpx + m11 * vpy + m12 * vpz;
        vz = m20 * vpx + m21 * vpy + m22 * vpz;

        return true;
    }

    /// <summary>
    /// Builds the PQW->inertial direction-cosine matrix for the standard 3-1-3
    /// element rotation sequence (Ω, i, ω).
    /// </summary>
    private static void BuildPQWToInertialMatrix(
        double raanRad,
        double iRad,
        double argpRad,
        out double m00, out double m01, out double m02,
        out double m10, out double m11, out double m12,
        out double m20, out double m21, out double m22)
    {
        double cO = Math.Cos(raanRad);
        double sO = Math.Sin(raanRad);
        double ci = Math.Cos(iRad);
        double si = Math.Sin(iRad);
        double cw = Math.Cos(argpRad);
        double sw = Math.Sin(argpRad);

        m00 =  cO * cw - sO * sw * ci;
        m01 = -cO * sw - sO * cw * ci;
        m02 =  sO * si;

        m10 =  sO * cw + cO * sw * ci;
        m11 = -sO * sw + cO * cw * ci;
        m12 = -cO * si;

        m20 =  sw * si;
        m21 =  cw * si;
        m22 =  ci;
    }

    // -------------------------
    // Utility
    // -------------------------

    private static double Atanh(double x)
    {
        return 0.5 * Math.Log((1.0 + x) / (1.0 - x));
    }

    private static double Asinh(double x)
    {
        return Math.Log(x + Math.Sqrt(x * x + 1.0));
    }

    private static double Wrap2Pi(double a)
    {
        double twoPi = 2.0 * Math.PI;
        a %= twoPi;
        if (a < 0.0) a += twoPi;
        return a;
    }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }
}