using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// StationPropSystem
/// Rails-only kinematics + attitude evaluator for a station.
///
/// Inputs:
/// - ConicState + ConicPropagator produce primary-relative rr/rv in solver inertial
/// - BodyCatalog provides primary body SSB r/v to compose station into SSB
///
/// Outputs written into StationStateModel:
/// - rr/rv (primary-relative)
/// - r/v   (SSB)
/// - q_B2E attitude (Mode A fixed inertial, Mode B RTN/LVLH)
///
/// No rendering here.
/// </summary>
public class StationPropSystem : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public ConicState conic;
    public ConicPropagator conicProp;
    public StationStateModel station;

    [Header("Attitude (Mode B)")]
    [Tooltip("If rr or h is degenerate, fall back to fixed attitude.")]
    public bool fallbackToFixedIfDegenerate = true;

    [Tooltip("Minimum magnitude thresholds to avoid NaNs.")]
    public double minRrMeters = 0.01;
    public double minH        = 1e-6; // |rr x rv| in (m^2/s) units; small but nonzero

    [Header("Debug")]
    public bool logMissing = false;
    public bool logDegenerate = false;

    public void Evaluate(double t)
    {
        if (bodies == null || conic == null || conicProp == null || station == null)
        {
            if (logMissing) Debug.Log("[StationPropSystem] Missing references.");
            return;
        }

        if (!conic.valid)
        {
            station.valid = false;
            return;
        }

        byte pid = conic.primaryBodyId;

        // 1) Primary-relative rr/rv (solver inertial)
        conicProp.Evaluate(t);

        // Copy primary-relative outputs
        station.rrx = conicProp.rel_rx;
        station.rry = conicProp.rel_ry;
        station.rrz = conicProp.rel_rz;

        station.rvx = conicProp.rel_vx;
        station.rvy = conicProp.rel_vy;
        station.rvz = conicProp.rel_vz;

        // 2) Primary SSB state
        double px, py, pz, pvx, pvy, pvz;
        bodies.GetBodyState(pid, out px, out py, out pz, out pvx, out pvy, out pvz);

        // 3) Compose SSB station state
        station.rx = px + station.rrx;
        station.ry = py + station.rry;
        station.rz = pz + station.rrz;

        station.vx = pvx + station.rvx;
        station.vy = pvy + station.rvy;
        station.vz = pvz + station.rvz;

        station.primaryBodyId = pid;
        station.valid = true;

        // 4) Attitude
        byte mode = station.attitudeMode;

        if (mode == StationStateModel.ATT_MODE_FIXED_INERTIAL)
        {
            station.q_B2E = station.qFixed_B2E;
            return;
        }

        if (mode == StationStateModel.ATT_MODE_RTN_LVLH)
        {
            Quaternion q;
            bool ok = TryComputeRtnAttitude(
                station.rrx, station.rry, station.rrz,
                station.rvx, station.rvy, station.rvz,
                station.rtnMap,
                out q
            );

            if (ok)
            {
                station.q_B2E = q;
                return;
            }

            if (fallbackToFixedIfDegenerate)
            {
                station.q_B2E = station.qFixed_B2E;
                return;
            }

            // If not falling back, leave last attitude.
            return;
        }

        // Unknown mode -> fixed
        station.q_B2E = station.qFixed_B2E;
    }

    // -------------------------
    // RTN / LVLH attitude
    // -------------------------
    private bool TryComputeRtnAttitude(
        double rrx, double rry, double rrz,
        double rvx, double rvy, double rvz,
        byte rtnMap,
        out Quaternion q_B2E)
    {
        q_B2E = Quaternion.identity;

        // R = normalize(rr)
        double r2 = rrx * rrx + rry * rry + rrz * rrz;
        if (r2 < minRrMeters * minRrMeters)
        {
            if (logDegenerate) Debug.Log("[StationPropSystem] Degenerate rr magnitude.");
            return false;
        }

        double invR = 1.0 / Math.Sqrt(r2);
        double Rx = rrx * invR;
        double Ry = rry * invR;
        double Rz = rrz * invR;

        // N = normalize(rr x rv)
        double hx = rry * rvz - rrz * rvy;
        double hy = rrz * rvx - rrx * rvz;
        double hz = rrx * rvy - rry * rvx;
        double h2 = hx * hx + hy * hy + hz * hz;

        if (h2 < minH * minH)
        {
            if (logDegenerate) Debug.Log("[StationPropSystem] Degenerate angular momentum (rr x rv).");
            return false;
        }

        double invH = 1.0 / Math.Sqrt(h2);
        double Nx = hx * invH;
        double Ny = hy * invH;
        double Nz = hz * invH;

        // T = N x R (right-handed)
        double Tx = Ny * Rz - Nz * Ry;
        double Ty = Nz * Rx - Nx * Rz;
        double Tz = Nx * Ry - Ny * Rx;

        // Normalize T defensively
        double t2 = Tx * Tx + Ty * Ty + Tz * Tz;
        if (t2 < 1e-18)
        {
            if (logDegenerate) Debug.Log("[StationPropSystem] Degenerate T (N x R).");
            return false;
        }
        double invT = 1.0 / Math.Sqrt(t2);
        Tx *= invT; Ty *= invT; Tz *= invT;

        // Map station BODY axes to inertial axes using RTN basis.
        // We will construct a body->inertial rotation matrix whose columns are:
        //   col0 = X_body in inertial
        //   col1 = Y_body in inertial
        //   col2 = Z_body in inertial
        double Xx, Xy, Xz;
        double Yx, Yy, Yz;
        double Zx, Zy, Zz;

        if (rtnMap == StationStateModel.RTNMAP_Z_NADIR_X_PROGRADE_Y_NORMAL)
        {
            // +Z = -R, +X = +T, +Y = +N
            Xx = Tx; Xy = Ty; Xz = Tz;
            Yx = Nx; Yy = Ny; Yz = Nz;
            Zx = -Rx; Zy = -Ry; Zz = -Rz;
        }
        else if (rtnMap == StationStateModel.RTNMAP_Z_ZENITH_X_PROGRADE_Y_NORMAL)
        {
            // +Z = +R, +X = +T, +Y = +N
            Xx = Tx; Xy = Ty; Xz = Tz;
            Yx = Nx; Yy = Ny; Yz = Nz;
            Zx = Rx; Zy = Ry; Zz = Rz;
        }
        else if (rtnMap == StationStateModel.RTNMAP_X_NADIR_Y_PROGRADE_Z_NORMAL)
        {
            // +X = -R, +Y = +T, +Z = +N
            Xx = -Rx; Xy = -Ry; Xz = -Rz;
            Yx = Tx;  Yy = Ty;  Yz = Tz;
            Zx = Nx;  Zy = Ny;  Zz = Nz;
        }
        else
        {
            // Default to the first mapping
            Xx = Tx; Xy = Ty; Xz = Tz;
            Yx = Nx; Yy = Ny; Yz = Nz;
            Zx = -Rx; Zy = -Ry; Zz = -Rz;
        }

        // Convert rotation matrix to Quaternion (body->inertial).
        q_B2E = RotationFromMatrixCols(
            (float)Xx, (float)Xy, (float)Xz,
            (float)Yx, (float)Yy, (float)Yz,
            (float)Zx, (float)Zy, (float)Zz
        );

        return true;
    }

    /// <summary>
    /// Build a Unity Quaternion from a 3x3 rotation matrix given by column vectors (X,Y,Z in world).
    /// Matrix is:
    /// [ Xx Yx Zx ]
    /// [ Xy Yy Zy ]
    /// [ Xz Yz Zz ]
    /// </summary>
    private static Quaternion RotationFromMatrixCols(
        float Xx, float Xy, float Xz,
        float Yx, float Yy, float Yz,
        float Zx, float Zy, float Zz)
    {
        // Convert to row-major elements for standard algorithms
        float m00 = Xx, m01 = Yx, m02 = Zx;
        float m10 = Xy, m11 = Yy, m12 = Zy;
        float m20 = Xz, m21 = Yz, m22 = Zz;

        float trace = m00 + m11 + m22;
        Quaternion q = new Quaternion();

        if (trace > 0f)
        {
            float s = Mathf.Sqrt(trace + 1f) * 2f; // s = 4*qw
            q.w = 0.25f * s;
            q.x = (m21 - m12) / s;
            q.y = (m02 - m20) / s;
            q.z = (m10 - m01) / s;
        }
        else if (m00 > m11 && m00 > m22)
        {
            float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f; // s = 4*qx
            q.w = (m21 - m12) / s;
            q.x = 0.25f * s;
            q.y = (m01 + m10) / s;
            q.z = (m02 + m20) / s;
        }
        else if (m11 > m22)
        {
            float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f; // s = 4*qy
            q.w = (m02 - m20) / s;
            q.x = (m01 + m10) / s;
            q.y = 0.25f * s;
            q.z = (m12 + m21) / s;
        }
        else
        {
            float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f; // s = 4*qz
            q.w = (m10 - m01) / s;
            q.x = (m02 + m20) / s;
            q.y = (m12 + m21) / s;
            q.z = 0.25f * s;
        }

        // Normalize to be safe
        float inv = 1f / Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        q.x *= inv; q.y *= inv; q.z *= inv; q.w *= inv;
        return q;
    }
}