using UdonSharp;
using UnityEngine;

/// <summary>
/// FixedConicPropagatorElliptic
/// Coasting-only conic propagation for a fixed ellipse.
/// Inputs: mu, a, e, current mean anomaly M0.
/// Query: GetRelPosAtDt(dt) -> relPos meters, moon-centered.
/// Frame: "orbit frame" (XY orbital plane) rotated by planeRotation.
/// </summary>
public class FixedConicPropagatorElliptic : UdonSharpBehaviour
{
    [Header("Conic (fixed)")]
    public double mu = 4.9048695e12;  // Moon mu (m^3/s^2) as a default
    public double aMeters = 1800000.0;
    [Range(0, 0.99f)]
    public float e = 0.1f;

    [Header("Current state")]
    [Tooltip("Current mean anomaly M0 (radians, 0..2π). Update this from UI or a driver.")]
    public float meanAnomalyRad = 0f;

    [Header("Frame")]
    [Tooltip("Optional rotation applied to the orbital plane (PQW) into your test frame.")]
    public Quaternion planeRotation = Quaternion.identity;

    public void SetMeanAnomalyRad(float M)
    {
        meanAnomalyRad = WrapTwoPi(M);
    }

    public float GetMeanMotionRadPerSec()
    {
        double n = System.Math.Sqrt(mu / (aMeters * aMeters * aMeters));
        return (float)n;
    }

    public bool TryGetRelPosAtDt(float dtSeconds, out Vector3 relPosMeters)
    {
        relPosMeters = Vector3.zero;

        float ef = Mathf.Clamp(e, 0f, 0.999999f);
        if (aMeters <= 0.0 || mu <= 0.0) return false;

        double n = System.Math.Sqrt(mu / (aMeters * aMeters * aMeters));
        double M0 = meanAnomalyRad;
        double M  = WrapTwoPi((float)(M0 + n * dtSeconds));

        // Solve Kepler: M = E - e sin E
        double E = SolveKeplerElliptic(M, ef);

        double cosE = System.Math.Cos(E);
        double sinE = System.Math.Sin(E);
        double sqrt1me2 = System.Math.Sqrt(1.0 - ef * ef);

        // Position in orbital plane (PQW): x along periapsis, y 90deg ahead
        // x = a(cosE - e)
        // y = a*sqrt(1-e^2)*sinE
        double x = aMeters * (cosE - ef);
        double y = aMeters * (sqrt1me2 * sinE);

        Vector3 p = new Vector3((float)x, (float)y, 0f);
        relPosMeters = planeRotation * p;
        return true;
    }

    private static float WrapTwoPi(float a)
    {
        float twoPi = Mathf.PI * 2f;
        a = a - Mathf.Floor(a / twoPi) * twoPi;
        if (a < 0f) a += twoPi;
        return a;
    }

    private static double SolveKeplerElliptic(double M, double e)
    {
        // Newton-Raphson. Good enough for planner sampling.
        // Initial guess: E=M for small e; otherwise a heuristic.
        double E = (e < 0.8) ? M : System.Math.PI;

        for (int i = 0; i < 8; i++)
        {
            double f  = E - e * System.Math.Sin(E) - M;
            double fp = 1.0 - e * System.Math.Cos(E);
            double dE = -f / fp;
            E += dE;
            if (System.Math.Abs(dE) < 1e-10) break;
        }

        // Wrap to 0..2π
        double twoPi = System.Math.PI * 2.0;
        E = E - System.Math.Floor(E / twoPi) * twoPi;
        if (E < 0.0) E += twoPi;
        return E;
    }
}