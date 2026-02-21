using UdonSharp;
using UnityEngine;
using System;

public class ConicPropagator : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public ConicState conic;

    [Header("Outputs (PRIMARY-RELATIVE, solver inertial frame, meters & m/s)")]
    public double rel_rx, rel_ry, rel_rz;
    public double rel_vx, rel_vy, rel_vz;

    [Header("Solver")]
    public int keplerIters = 6;

    public void Evaluate(double t)
    {
        if (bodies == null || conic == null || !conic.valid) return;

        double mu = bodies.GetMu(conic.primaryBodyId);
        if (mu <= 0.0) return;

        double a = conic.aMeters;
        double e = conic.e;

        // Elliptic
        if (e < 1.0 - 1e-10)
        {
            double n = Math.Sqrt(mu / (a * a * a));
            double M = conic.M0Rad + n * (t - conic.epochT0);
            M = WrapPi(M);

            double E = (e < 1e-12) ? M : SolveKeplerE(M, e, keplerIters);

            double cosE = Math.Cos(E);
            double sinE = Math.Sin(E);

            double fac = Math.Sqrt(1.0 - e * e);
            double xpf = a * (cosE - e);
            double ypf = a * (fac * sinE);

            double edot = n / (1.0 - e * cosE);
            double vxpf = -a * sinE * edot;
            double vypf =  a * fac * cosE * edot;

            double xs, ys, zs;

            // PQW -> SOLVER INERTIAL (same frame your elements are defined in)
            PQWToInertial(conic.raanRad, conic.iRad, conic.argpRad, xpf, ypf, 0.0, out xs, out ys, out zs);
            rel_rx = xs; rel_ry = ys; rel_rz = zs;

            PQWToInertial(conic.raanRad, conic.iRad, conic.argpRad, vxpf, vypf, 0.0, out xs, out ys, out zs);
            rel_vx = xs; rel_vy = ys; rel_vz = zs;

            return;
        }

        // Hyperbolic
        if (e > 1.0 + 1e-10)
        {
            double absA = -a;
            if (absA <= 0.0) return;

            double n = Math.Sqrt(mu / (absA * absA * absA));
            double M = conic.M0Rad + n * (t - conic.epochT0);

            double H = SolveKeplerH(M, e, keplerIters);

            double coshH = Cosh(H);
            double sinhH = Sinh(H);
            double fac = Math.Sqrt(e * e - 1.0);

            double xpf = absA * (e - coshH);
            double ypf = absA * (fac * sinhH);

            double dMdH = (e * coshH - 1.0);
            if (Math.Abs(dMdH) < 1e-12) return;

            double Hdot = n / dMdH;

            double vxpf = (-absA * sinhH) * Hdot;
            double vypf = ( absA * fac * coshH) * Hdot;

            double xs, ys, zs;

            PQWToInertial(conic.raanRad, conic.iRad, conic.argpRad, xpf, ypf, 0.0, out xs, out ys, out zs);
            rel_rx = xs; rel_ry = ys; rel_rz = zs;

            PQWToInertial(conic.raanRad, conic.iRad, conic.argpRad, vxpf, vypf, 0.0, out xs, out ys, out zs);
            rel_vx = xs; rel_vy = ys; rel_vz = zs;

            return;
        }

        // Near-parabolic fallback (as before)
        double e2 = 1.0 - 1e-10;
        double n2 = Math.Sqrt(mu / (a * a * a));
        double M2 = conic.M0Rad + n2 * (t - conic.epochT0);
        double E2 = SolveKeplerE(M2, e2, keplerIters);

        double cosE2 = Math.Cos(E2);
        double sinE2 = Math.Sin(E2);

        double fac2 = Math.Sqrt(1.0 - e2 * e2);
        double xpf2 = a * (cosE2 - e2);
        double ypf2 = a * (fac2 * sinE2);

        double edot2 = n2 / (1.0 - e2 * cosE2);
        double vxpf2 = -a * sinE2 * edot2;
        double vypf2 =  a * fac2 * cosE2 * edot2;

        double xs2, ys2, zs2;

        PQWToInertial(conic.raanRad, conic.iRad, conic.argpRad, xpf2, ypf2, 0.0, out xs2, out ys2, out zs2);
        rel_rx = xs2; rel_ry = ys2; rel_rz = zs2;

        PQWToInertial(conic.raanRad, conic.iRad, conic.argpRad, vxpf2, vypf2, 0.0, out xs2, out ys2, out zs2);
        rel_vx = xs2; rel_vy = ys2; rel_vz = zs2;
    }

    // PQW -> inertial rotation (3-1-3: RAAN, INC, ARGP)
    private static void PQWToInertial(double raan, double inc, double argp,
                                      double x, double y, double z,
                                      out double ox, out double oy, out double oz)
    {
        double cO = Math.Cos(raan), sO = Math.Sin(raan);
        double ci = Math.Cos(inc),  si = Math.Sin(inc);
        double cw = Math.Cos(argp), sw = Math.Sin(argp);

        double m00 =  cO * cw - sO * sw * ci;
        double m01 = -cO * sw - sO * cw * ci;
        double m02 =  sO * si;

        double m10 =  sO * cw + cO * sw * ci;
        double m11 = -sO * sw + cO * cw * ci;
        double m12 = -cO * si;

        double m20 =  sw * si;
        double m21 =  cw * si;
        double m22 =  ci;

        ox = m00 * x + m01 * y + m02 * z;
        oy = m10 * x + m11 * y + m12 * z;
        oz = m20 * x + m21 * y + m22 * z;
    }

    private static double WrapPi(double a)
    {
        while (a > Math.PI) a -= 2.0 * Math.PI;
        while (a < -Math.PI) a += 2.0 * Math.PI;
        return a;
    }

    private static double SolveKeplerE(double M, double e, int iters)
    {
        double E = (e < 0.8) ? M : Math.PI;
        for (int k = 0; k < iters; k++)
        {
            double f = E - e * Math.Sin(E) - M;
            double fp = 1.0 - e * Math.Cos(E);
            E -= f / fp;
        }
        return E;
    }

    private static double SolveKeplerH(double M, double e, int iters)
    {
        double H = Asinh(M / e);
        if (double.IsNaN(H) || double.IsInfinity(H)) H = 0.0;

        for (int k = 0; k < iters; k++)
        {
            double s = Sinh(H);
            double c = Cosh(H);
            double f = e * s - H - M;
            double fp = e * c - 1.0;
            H -= f / fp;
        }
        return H;
    }

    private static double Sinh(double x) => 0.5 * (Math.Exp(x) - Math.Exp(-x));
    private static double Cosh(double x) => 0.5 * (Math.Exp(x) + Math.Exp(-x));

    private static double Asinh(double x) => Math.Log(x + Math.Sqrt(x * x + 1.0));
}
