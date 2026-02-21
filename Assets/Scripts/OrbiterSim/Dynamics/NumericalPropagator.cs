using UdonSharp;
using UnityEngine;

public class NumericalPropagator : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public CraftStateModel craft;
    public GravityModel grav;

    [Header("External force input (set by SimManager each substep)")]
    [Tooltip("Net applied force on CG in INERTIAL frame (N). Set this once per substep.")]
    public Vector3 force_E = Vector3.zero;

    [Header("Safety")]
    public double minR = 1.0; // meters

    // relative state scratch
    private double _rrx, _rry, _rrz;
    private double _rvx, _rvy, _rvz;

    private double _ax, _ay, _az;

    public void Step(double dt, double tNow)
    {
        if (bodies == null || craft == null || grav == null) return;

        byte pid = craft.primaryBodyId;

        // Convert heliocentric -> primary-relative (still inertial axes)
        bodies.ToPrimaryRelative(pid, craft, out _rrx, out _rry, out _rrz, out _rvx, out _rvy, out _rvz);

        // Kick–Drift–Kick (Velocity Verlet) in primary-relative frame
        ComputeAccelerationPrimaryRel(pid, tNow, _rrx, _rry, _rrz, out _ax, out _ay, out _az);

        _rvx += _ax * (0.5 * dt);
        _rvy += _ay * (0.5 * dt);
        _rvz += _az * (0.5 * dt);

        _rrx += _rvx * dt;
        _rry += _rvy * dt;
        _rrz += _rvz * dt;

        ComputeAccelerationPrimaryRel(pid, tNow + dt, _rrx, _rry, _rrz, out _ax, out _ay, out _az);

        _rvx += _ax * (0.5 * dt);
        _rvy += _ay * (0.5 * dt);
        _rvz += _az * (0.5 * dt);

        // Compose back to heliocentric using current ephem snapshot
        bodies.FromPrimaryRelative(pid, _rrx, _rry, _rrz, _rvx, _rvy, _rvz, craft);
    }

    private void ComputeAccelerationPrimaryRel(
        byte primaryId, double tNow,
        double rrx, double rry, double rrz,
        out double ax, out double ay, out double az)
    {
        ax = ay = az = 0.0;

        // Gravity about primary (two-body, body-centered inertial)
        grav.minR = minR;
        grav.EvaluatePrimaryRel(primaryId, rrx, rry, rrz, out ax, out ay, out az);

        // Applied force (inertial axes); same components apply in primary-relative inertial frame
        double m = craft.massKg;
        if (m < 1.0) m = 1.0;

        ax += (double)force_E.x / m;
        ay += (double)force_E.y / m;
        az += (double)force_E.z / m;
    }
}
