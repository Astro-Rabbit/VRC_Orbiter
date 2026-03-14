using UdonSharp;
using UnityEngine;
using System;

public class EarthRotationModelSimple : UdonSharpBehaviour
{
    [Header("Earth constants")]
    public double obliquityRad = 23.4392911 * Math.PI / 180.0; // J2000 mean obliquity
    public double earthOmegaRadSec = 7.2921150e-5;

    [Header("Moon constants")]
    public double moonOmegaRadSec = 2.6616995e-6; // ~2π / 27.321661 days

    public void Evaluate(double jd,
        // Need Earth/Moon relative state for lunar lock
        double earth_rx, double earth_ry, double earth_rz,
        double earth_vx, double earth_vy, double earth_vz,
        double moon_rx,  double moon_ry,  double moon_rz,
        double moon_vx,  double moon_vy,  double moon_vz,
        out double earth_ox, out double earth_oy, out double earth_oz,
        out float  earth_qx, out float  earth_qy, out float  earth_qz, out float  earth_qw,
        out double moon_ox,  out double moon_oy,  out double moon_oz,
        out float  moon_qx,  out float  moon_qy,  out float  moon_qz,  out float  moon_qw)
    {
        // --- Earth ---
        EarthFromGMST(jd,
            out earth_ox, out earth_oy, out earth_oz,
            out earth_qx, out earth_qy, out earth_qz, out earth_qw);

        // --- Moon (tidal lock, robust handedness) ---
        MoonTidallyLocked(
            earth_rx, earth_ry, earth_rz,
            earth_vx, earth_vy, earth_vz,
            moon_rx,  moon_ry,  moon_rz,
            moon_vx,  moon_vy,  moon_vz,
            out moon_ox, out moon_oy, out moon_oz,
            out moon_qx, out moon_qy, out moon_qz, out moon_qw);
    }

    private void EarthFromGMST(double jd,
        out double ox, out double oy, out double oz,
        out float qx, out float qy, out float qz, out float qw)
    {
        // Spin axis in ecliptic frame (X toward equinox, Z ecliptic north).
        // Using mean obliquity: axis tilts from +Z toward +Y.
        double sX = 0.0;
        double sY = Math.Sin(obliquityRad);
        double sZ = Math.Cos(obliquityRad);

        ox = earthOmegaRadSec * sX;
        oy = earthOmegaRadSec * sY;
        oz = earthOmegaRadSec * sZ;

        // IAU 1982-style GMST (hours)
        // d = days since J2000.0
        double d = jd - 2451545.0;
        double T = d / 36525.0;

        // UT in hours from fractional day:
        double jdFloor = Math.Floor(jd + 0.5) - 0.5; // start of UT day
        double fracDay = jd - jdFloor;
        double UT = fracDay * 24.0;

        // Classic form (Stjarnhimlen / Meeus-like):
        double GMST = 6.697374558 + 0.06570982441908 * d + 1.00273790935 * UT + 0.000026 * (T * T);
        GMST = GMST % 24.0;
        if (GMST < 0.0) GMST += 24.0;

        double theta = (GMST / 24.0) * (2.0 * Math.PI); // radians

        // Build fixed->ecliptic basis:
        // z = spin axis (sX,sY,sZ)
        // x = projection of +X (equinox direction) into equator plane, rotated by theta about z
        double refXx = 1.0, refXy = 0.0, refXz = 0.0;

        double dot = refXx * sX + refXy * sY + refXz * sZ;
        double ux = refXx - dot * sX;
        double uy = refXy - dot * sY;
        double uz = refXz - dot * sZ;
        Normalize(ref ux, ref uy, ref uz);

        // v = z × u
        double vx = sY * uz - sZ * uy;
        double vy = sZ * ux - sX * uz;
        double vz = sX * uy - sY * ux;

        double c = Math.Cos(theta), s = Math.Sin(theta);
        double xX = c * ux + s * vx;
        double xY = c * uy + s * vy;
        double xZ = c * uz + s * vz;

        // y = z × x
        double yX = sY * xZ - sZ * xY;
        double yY = sZ * xX - sX * xZ;
        double yZ = sX * xY - sY * xX;

        Quaternion q = QuaternionFromBasis(
            (float)xX, (float)xY, (float)xZ,
            (float)yX, (float)yY, (float)yZ,
            (float)sX, (float)sY, (float)sZ
        );

        qx = q.x; qy = q.y; qz = q.z; qw = q.w;
    }

    private void MoonTidallyLocked(
        double ex, double ey, double ez,
        double evx, double evy, double evz,
        double mx, double my, double mz,
        double mvx, double mvy, double mvz,
        out double ox, out double oy, out double oz,
        out float qx, out float qy, out float qz, out float qw)
    {
        // Earth->Moon relative
        double rx = mx - ex, ry = my - ey, rz = mz - ez;
        double vx = mvx - evx, vy = mvy - evy, vz = mvz - evz;

        // Orbital angular momentum h = r x v
        double zx = ry * vz - rz * vy;
        double zy = rz * vx - rx * vz;
        double zz = rx * vy - ry * vx;
        Normalize(ref zx, ref zy, ref zz);

        // x axis: points toward Earth (Moon->Earth = -r), projected into equator plane
        double tx = -rx, ty = -ry, tz = -rz;
        Normalize(ref tx, ref ty, ref tz);

        double dot = tx * zx + ty * zy + tz * zz;
        double xX = tx - dot * zx;
        double xY = ty - dot * zy;
        double xZ = tz - dot * zz;
        Normalize(ref xX, ref xY, ref xZ);

        // y = z × x
        double yX = zy * xZ - zz * xY;
        double yY = zz * xX - zx * xZ;
        double yZ = zx * xY - zy * xX;

        // Enforce prograde handedness: y should roughly align with velocity direction
        // double vmag = Math.Sqrt(vx * vx + vy * vy + vz * vz);
        // if (vmag > 1e-9)
        // {
        //     double vnx = vx / vmag, vny = vy / vmag, vnz = vz / vmag;
        //     double align = yX * vnx + yY * vny + yZ * vnz;
        //     if (align < 0.0)
        //     {
        //         // Flip z and y to keep x pointing to Earth but fix spin direction
        //         zx = -zx; zy = -zy; zz = -zz;
        //         yX = -yX; yY = -yY; yZ = -yZ;
        //     }
        // }

        // Spin vector (rad/s) about z axis (right-hand rule)
        ox = moonOmegaRadSec * zx;
        oy = moonOmegaRadSec * zy;
        oz = moonOmegaRadSec * zz;

        Quaternion q = QuaternionFromBasis(
            (float)xX, (float)xY, (float)xZ,
            (float)yX, (float)yY, (float)yZ,
            (float)zx, (float)zy, (float)zz
        );

        qx = q.x; qy = q.y; qz = q.z; qw = q.w;
    }

    private void Normalize(ref double x, ref double y, ref double z)
    {
        double m = Math.Sqrt(x * x + y * y + z * z);
        if (m < 1e-30)
        {
            x = 1.0; y = 0.0; z = 0.0;
            return;
        }
        x /= m; y /= m; z /= m;
    }

    // Basis vectors are columns of rotation matrix (fixed->inertial/ecliptic)
    private Quaternion QuaternionFromBasis(
        float x_x, float x_y, float x_z,
        float y_x, float y_y, float y_z,
        float z_x, float z_y, float z_z)
    {
        float m00 = x_x, m01 = y_x, m02 = z_x;
        float m10 = x_y, m11 = y_y, m12 = z_y;
        float m20 = x_z, m21 = y_z, m22 = z_z;

        float tr = m00 + m11 + m22;
        Quaternion q = new Quaternion();

        if (tr > 0f)
        {
            float S = Mathf.Sqrt(tr + 1f) * 2f;
            q.w = 0.25f * S;
            q.x = (m21 - m12) / S;
            q.y = (m02 - m20) / S;
            q.z = (m10 - m01) / S;
        }
        else if (m00 > m11 && m00 > m22)
        {
            float S = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
            q.w = (m21 - m12) / S;
            q.x = 0.25f * S;
            q.y = (m01 + m10) / S;
            q.z = (m02 + m20) / S;
        }
        else if (m11 > m22)
        {
            float S = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
            q.w = (m02 - m20) / S;
            q.x = (m01 + m10) / S;
            q.y = 0.25f * S;
            q.z = (m12 + m21) / S;
        }
        else
        {
            float S = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
            q.w = (m10 - m01) / S;
            q.x = (m02 + m20) / S;
            q.y = (m12 + m21) / S;
            q.z = 0.25f * S;
        }

        q.Normalize();
        return q;
    }
}
