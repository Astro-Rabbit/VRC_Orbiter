using UdonSharp;
using UnityEngine;
using System;

public class NumericalPropagator : UdonSharpBehaviour
{
    [Header("References")]
    public EphemerisSystem ephemSys;   // << ADD THIS
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
        if (ephemSys == null || bodies == null || craft == null || grav == null) return;

        byte pid = craft.primaryBodyId;

        // ---- Ensure ephem snapshot matches t0 ----
        ephemSys.Evaluate(tNow);

        // Convert heliocentric -> primary-relative using PRIMARY state at t0
        bodies.ToPrimaryRelative(pid, craft, out _rrx, out _rry, out _rrz, out _rvx, out _rvy, out _rvz);

        // Kick–Drift–Kick (Velocity Verlet) in primary-relative frame
        ComputeAccelerationPrimaryRel(pid, _rrx, _rry, _rrz, out _ax, out _ay, out _az);

        _rvx += _ax * (0.5 * dt);
        _rvy += _ay * (0.5 * dt);
        _rvz += _az * (0.5 * dt);

        _rrx += _rvx * dt;
        _rry += _rvy * dt;
        _rrz += _rvz * dt;

        // ---- Ensure ephem snapshot matches t1 ----
        double t1 = tNow + dt;
        ephemSys.Evaluate(t1);

        ComputeAccelerationPrimaryRel(pid, _rrx, _rry, _rrz, out _ax, out _ay, out _az);

        _rvx += _ax * (0.5 * dt);
        _rvy += _ay * (0.5 * dt);
        _rvz += _az * (0.5 * dt);

        // Compose back to heliocentric using PRIMARY state at t1
        bodies.FromPrimaryRelative(pid, _rrx, _rry, _rrz, _rvx, _rvy, _rvz, craft);
    }

    private void ComputeAccelerationPrimaryRel(
        byte primaryId,
        double rrx, double rry, double rrz,
        out double ax, out double ay, out double az)
    {
        ax = ay = az = 0.0;

        // 1) Primary gravity (two-body, body-centered inertial)
        grav.minR = minR;
        grav.EvaluatePrimaryRel(primaryId, rrx, rry, rrz, out ax, out ay, out az);

        // 2) Optional: differential third-body gravity (recommended if you want realism)
        AddDifferentialThirdBody(primaryId, bodies.sunId,   ref ax, ref ay, ref az, rrx, rry, rrz);
        AddDifferentialThirdBody(primaryId, bodies.earthId, ref ax, ref ay, ref az, rrx, rry, rrz);
        AddDifferentialThirdBody(primaryId, bodies.moonId,  ref ax, ref ay, ref az, rrx, rry, rrz);

        // 3) Applied force in inertial axes
        double m = craft.massKg;
        if (m < 1.0) m = 1.0;

        ax += (double)force_E.x / m;
        ay += (double)force_E.y / m;
        az += (double)force_E.z / m;
    }

    private void AddDifferentialThirdBody(
        byte primaryId, byte otherId,
        ref double ax, ref double ay, ref double az,
        double rrx, double rry, double rrz)
    {
        if (otherId == primaryId) return;

        double mu = bodies.GetMu(otherId);
        if (mu <= 0.0) return;

        // Primary and other body positions come from *current* ephem snapshot
        double px, py, pz; bodies.GetBodyPos(primaryId, out px, out py, out pz);
        double ox, oy, oz; bodies.GetBodyPos(otherId,  out ox, out oy, out oz);

        // Craft helio = primary + rel
        double cx = px + rrx, cy = py + rry, cz = pz + rrz;

        double rcx = ox - cx, rcy = oy - cy, rcz = oz - cz; // other - craft
        double rpx = ox - px, rpy = oy - py, rpz = oz - pz; // other - primary

        double rc2 = rcx*rcx + rcy*rcy + rcz*rcz;
        double rp2 = rpx*rpx + rpy*rpy + rpz*rpz;
        double min2 = minR * minR;
        if (rc2 < min2) rc2 = min2;
        if (rp2 < min2) rp2 = min2;

        double rcInv = 1.0 / Math.Sqrt(rc2);
        double rpInv = 1.0 / Math.Sqrt(rp2);
        double rcInv3 = rcInv*rcInv*rcInv;
        double rpInv3 = rpInv*rpInv*rpInv;

        ax += mu * (rcx*rcInv3 - rpx*rpInv3);
        ay += mu * (rcy*rcInv3 - rpy*rpInv3);
        az += mu * (rcz*rcInv3 - rpz*rpInv3);
    }
}