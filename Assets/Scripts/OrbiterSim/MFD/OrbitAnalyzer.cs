using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class OrbitAnalyzer : UdonSharpBehaviour
{
    public BodyCatalog bodies;
    public ConicState conic;
    public double a; // semi-major axis
    public double e; // eccentricity
    public double t; // orbit period
    public double ap; // apoapsis
    public double pe; // periapsis


    private double m00, m01, m02;
    private double m10, m11, m12;
    private double m20, m21, m22;

    private double lastEpochT0 = Double.NegativeInfinity;

    void Update()
    {
        if (conic.epochT0 != lastEpochT0) {
            UpdateInfo();
        }
    }

    public void UpdateInfo()
    {
        lastEpochT0 = conic.epochT0;

        a = conic.aMeters;
        e = conic.e;

        if (conic.e < 1.0 && a > 0.0) {
            double mu = bodies.GetMu(conic.primaryBodyId);
            t = 2.0 * Math.PI * Math.Sqrt(a * a * a / mu);
            ap = a * (1.0 + e);
        } else {
            t = 0;
            ap = 0;
        }

        pe = a * (1 - e);

        // build perifocal -> ecliptic rotation matrix
        double cr = Math.Cos(conic.raanRad);
        double sr = Math.Sin(conic.raanRad);
        double ci = Math.Cos(conic.iRad);
        double si = Math.Sin(conic.iRad);
        double ca = Math.Cos(conic.argpRad);
        double sa = Math.Sin(conic.argpRad);

        m00 =  cr*ca - sr*sa*ci;
        m01 = -cr*sa - sr*ca*ci;
        m02 =  sr*si;

        m10 =  sr*ca + cr*sa*ci;
        m11 = -sr*sa + cr*ca*ci;
        m12 = -cr*si;

        m20 =  sa*si;
        m21 =  ca*si;
        m22 =  ci;
    }


    // Perform the minimum rotation to align the given orbit with this orbit
    // and output the direction of the rotated periapsis in the perifocal frame
    // of this orbit
    public void GetAligned(OrbitAnalyzer other, out double px, out double py)
    {
        // dot product of plane normals
        double dot = m02*other.m02 + m12*other.m12 + m22*other.m22;

        // cross product of plane normals
        double cx = other.m12*m22 - other.m22*m12;
        double cy = other.m22*m02 - other.m02*m22;
        double cz = other.m02*m12 - other.m12*m02;

        double cdot = other.m00*cx + other.m10*cy + other.m20*cz;
        cdot /= cx*cx + cy*cy + cz*cz;

        // rotated periapsis direction
        double rx = other.m00*dot + cx*cdot*(1 - dot) + cy*other.m20 - cz*other.m10;
        double ry = other.m10*dot + cy*cdot*(1 - dot) + cz*other.m00 - cx*other.m20;
        double rz = other.m20*dot + cz*cdot*(1 - dot) + cx*other.m10 - cy*other.m00;

        // project to perifocal frame for this orbit (z should always be zero so it's left out)
        px = rx*m00 + ry*m10 + rz*m20;
        py = rx*m01 + ry*m11 + rz*m21;

        // Ensure it's normalized even in edge cases
        double mag = Math.Sqrt(px*px + py*py);
        if (mag > 0) {
            px /= mag;
            py /= mag;
        } else {
            px = 1;
            py = 0;
        }
    }
}
