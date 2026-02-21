using UdonSharp;
using UnityEngine;
using System;

public class MoonRotationModelTidallyLocked : UdonSharpBehaviour
{
    [Header("Mean sidereal period (seconds)")]
    public double moonPeriodS = 27.321661 * 86400.0;

    public void Evaluate(
        // Earth->Moon relative state in inertial (ecliptic)
        double em_rx, double em_ry, double em_rz,
        double em_vx, double em_vy, double em_vz,
        out double omega_x, out double omega_y, out double omega_z,
        out float qx, out float qy, out float qz, out float qw)
    {
        // Orbital angular momentum h = r x v
        double hx = em_ry * em_vz - em_rz * em_vy;
        double hy = em_rz * em_vx - em_rx * em_vz;
        double hz = em_rx * em_vy - em_ry * em_vx;

        Normalize(ref hx, ref hy, ref hz); // h-hat = spin axis approx

        // Mean motion magnitude
        double n = 2.0 * Math.PI / Math.Max(1.0, moonPeriodS);

        omega_x = n * hx;
        omega_y = n * hy;
        omega_z = n * hz;

        // x-axis: point toward Earth (from Moon to Earth is -r_em)
        double toEarth_x = -em_rx;
        double toEarth_y = -em_ry;
        double toEarth_z = -em_rz;
        Normalize(ref toEarth_x, ref toEarth_y, ref toEarth_z);

        // Project toEarth onto Moon equator plane (perp to spin axis)
        double dot = toEarth_x * hx + toEarth_y * hy + toEarth_z * hz;
        double x_x = toEarth_x - dot * hx;
        double x_y = toEarth_y - dot * hy;
        double x_z = toEarth_z - dot * hz;
        Normalize(ref x_x, ref x_y, ref x_z);

        // y-axis = z × x
        double y_x = hy * x_z - hz * x_y;
        double y_y = hz * x_x - hx * x_z;
        double y_z = hx * x_y - hy * x_x;

        Quaternion q = QuaternionFromBasis(
            (float)x_x, (float)x_y, (float)x_z,
            (float)y_x, (float)y_y, (float)y_z,
            (float)hx,  (float)hy,  (float)hz
        );

        qx = q.x; qy = q.y; qz = q.z; qw = q.w;
    }

    private void Normalize(ref double x, ref double y, ref double z)
    {
        double m = Math.Sqrt(x * x + y * y + z * z);
        if (m < 1e-30)
        {
            x = 0.0; y = 0.0; z = 1.0;
            return;
        }
        x /= m; y /= m; z /= m;
    }

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
