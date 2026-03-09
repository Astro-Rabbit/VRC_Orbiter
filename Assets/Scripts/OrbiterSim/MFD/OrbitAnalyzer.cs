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

    private void UpdateInfo() 
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
        double sa = Math.Cos(conic.argpRad);

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
        double dot = m20*other.m20 + m21*other.m21 + m22*other.m22;

        // cross product of plane normals
        double cx = m21*other.m22 - m22*other.m21;
        double cy = m22*other.m20 - m20*other.m22;
        double cz = m20*other.m21 - m21*other.m20;

        // rotated periapsis direction
        double rx = other.m00*dot + other.m01*cz - other.m02*cy;
        double ry = other.m01*dot + other.m02*cx - other.m00*cz;
        double rz = other.m02*dot + other.m00*cy - other.m01*cx;

        // project to perifocal frame for this orbit (z should always be zero so it's left out)
        px = rx*m00 + ry*m01 + rz*m02;
        py = rx*m10 + ry*m11 + rz*m12;

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
