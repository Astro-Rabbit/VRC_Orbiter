using UdonSharp;
using UnityEngine;

public class ConicState : UdonSharpBehaviour
{
    [Header("Anchor definition")]
    public byte primaryBodyId = 2;      // 1=Earth, 2=Moon
    public double epochT0 = 0.0;        // seconds (sim time)
    public double M0Rad = 0.0;          // mean anomaly at epoch

    [Header("Classical elements (standard convention, radians)")]
    public double aMeters = 0.0;
    public double e = 0.0;
    public double iRad = 0.0;
    public double raanRad = 0.0;
    public double argpRad = 0.0;

    [Header("Validity")]
    public bool valid = false;
}
