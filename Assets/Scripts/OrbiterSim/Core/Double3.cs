using System;
using UnityEngine;

[Serializable]
public struct Double3
{
    public double x;
    public double y;
    public double z;

    public Double3(double x, double y, double z)
    {
        this.x = x; this.y = y; this.z = z;
    }

    public static Double3 Zero() => new Double3(0.0, 0.0, 0.0);

    public static Double3 Add(in Double3 a, in Double3 b) => new Double3(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Double3 Sub(in Double3 a, in Double3 b) => new Double3(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Double3 Mul(in Double3 a, double s) => new Double3(a.x * s, a.y * s, a.z * s);

    public static double Dot(in Double3 a, in Double3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
    public static double Mag2(in Double3 a) => Dot(a, a);
    public static double Mag(in Double3 a) => Math.Sqrt(Mag2(a));

    public static Double3 Normalize(in Double3 a)
    {
        double m = Mag(a);
        if (m <= 0.0) return Zero();
        return Mul(a, 1.0 / m);
    }

    public Vector3 ToVector3(float metersToUnity)
    {
        return new Vector3(
            (float)(x * metersToUnity),
            (float)(y * metersToUnity),
            (float)(z * metersToUnity)
        );
    }
}
