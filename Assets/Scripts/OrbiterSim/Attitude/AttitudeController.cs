
using UdonSharp;
using UnityEngine;
using System;

public class AttitudeController : UdonSharpBehaviour
{
    [Header("References")]
    public CraftControlState control;
    public CraftAttitudeState att;

    [Header("Rate control gains (Nm per (rad/s))")]
    public double KpRate = 2000.0;   // torque per rad/s error
    public double KdRate = 0.0;     // optional extra damping (often unnecessary if using rate error)

    [Header("Hold attitude gains")]
    public double KpHold = 50.0;    // torque per rad of attitude error (small for now)
    public double KdHold = 2000.0;   // torque per rad/s (damping)

    [Header("Output torque command (Nm, body frame)")]
    public double cmdTx;
    public double cmdTy;
    public double cmdTz;

    // Hold-attitude target
    private Quaternion _qTarget = Quaternion.identity;
    private byte _lastMode = 255;

    public void Evaluate()
    {
        cmdTx = cmdTy = cmdTz = 0.0;
        if (control == null || att == null) return;

        // Detect mode transitions
        if (control.attitudeMode != _lastMode)
        {
            if (control.attitudeMode == 1) // entering HoldAttitude
                _qTarget = att.qBE;

            _lastMode = control.attitudeMode;
        }

        // Mode dispatch
        if (control.attitudeMode == 0)
        {
            EvaluateManualRate();
            return;
        }

        if (control.attitudeMode == 4)
        {
            EvaluateKillRot();
            return;
        }

        if (control.attitudeMode == 2)
        {
            // HoldTargetForward: update target every frame (guidance can change it continuously)
            _qTarget = BuildTargetQuat_FromForwardUp(control.targetForwardECI, control.targetUpECI);
            EvaluateHoldToTarget(_qTarget);
            return;
        }

        if (control.attitudeMode == 3)
        {
            // HoldTargetQuat: guidance provides full target
            _qTarget = control.targetQ_BE;
            EvaluateHoldToTarget(_qTarget);
            return;
        }

        // Default: HoldAttitude (mode 1)
        EvaluateHoldToTarget(_qTarget);
    }

    private void EvaluateManualRate()
    {
        // Stick inputs -> desired body rates (rad/s)
        double maxRate = Deg2Rad((double)control.maxRateDegS);

        // Convention: pitchCmd is rotation about +X? In aerospace pitch is usually about +X (right axis) if +Z forward, +Y up.
        // yaw about +Y, roll about +Z. We'll use:
        // pitch -> +X, yaw -> +Y, roll -> +Z
        double wdx = Clamp((double)control.pitchCmd, -1.0, 1.0) * maxRate;
        double wdy = Clamp((double)control.yawCmd,   -1.0, 1.0) * maxRate;
        double wdz = Clamp((double)control.rollCmd,  -1.0, 1.0) * maxRate;

        // Rate error
        double ex = wdx - att.wx;
        double ey = wdy - att.wy;
        double ez = wdz - att.wz;

        cmdTx = KpRate * ex - KdRate * att.wx;
        cmdTy = KpRate * ey - KdRate * att.wy;
        cmdTz = KpRate * ez - KdRate * att.wz;
    }

    private void EvaluateHoldAttitude()
    {
        // Minimal stable hold:
        // - Damps angular rate strongly (KdHold)
        // - Adds a small proportional torque toward target quaternion (KpHold)
        //
        // We'll compute a small-angle rotation vector (body frame) from q_err = q_target^-1 * q_current.
        Quaternion qErr = Multiply(Inverse(_qTarget), att.qBE);

        // Map quaternion error to axis-angle (small-angle approximation)
        // Ensure shortest path
        if (qErr.w < 0f) qErr = new Quaternion(-qErr.x, -qErr.y, -qErr.z, -qErr.w);

        // For small angles, rotation vector ≈ 2 * q.xyz
        double ex = 2.0 * (double)qErr.x;
        double ey = 2.0 * (double)qErr.y;
        double ez = 2.0 * (double)qErr.z;

        cmdTx = -KpHold * ex - KdHold * att.wx;
        cmdTy = -KpHold * ey - KdHold * att.wy;
        cmdTz = -KpHold * ez - KdHold * att.wz;
    }

    private void EvaluateKillRot()
    {
        // Pure rate damping to zero (Orbiter-like "killrot")
        cmdTx = -KdHold * att.wx;
        cmdTy = -KdHold * att.wy;
        cmdTz = -KdHold * att.wz;
    }

    private void EvaluateHoldToTarget(Quaternion qTarget)
    {
        // PD: torque = -Kp * angle_error - Kd * omega
        // Use qErr = q_target^-1 * q_current (same convention you used)
        Quaternion qErr = Multiply(Inverse(qTarget), att.qBE);

        // Ensure shortest path
        if (qErr.w < 0f) qErr = new Quaternion(-qErr.x, -qErr.y, -qErr.z, -qErr.w);

        // Small-angle rotation vector ≈ 2*q.xyz
        double ex = 2.0 * (double)qErr.x;
        double ey = 2.0 * (double)qErr.y;
        double ez = 2.0 * (double)qErr.z;

        cmdTx = -KpHold * ex - KdHold * att.wx;
        cmdTy = -KpHold * ey - KdHold * att.wy;
        cmdTz = -KpHold * ez - KdHold * att.wz;
    }

    // Build qBE such that body +Z points along forwardECI and body +Y as close as possible to upECI.
    // This defines roll about forward in a deterministic way.
    private static Quaternion BuildTargetQuat_FromForwardUp(Vector3 forwardECI, Vector3 upECI)
    {
        // Normalize forward; if bad, default
        if (forwardECI.sqrMagnitude < 1e-12f) forwardECI = Vector3.forward;
        forwardECI.Normalize();

        // Choose up reference; if bad or nearly parallel to forward, pick a safe fallback
        if (upECI.sqrMagnitude < 1e-12f) upECI = Vector3.up;
        upECI.Normalize();

        float d = Mathf.Abs(Vector3.Dot(forwardECI, upECI));
        if (d > 0.98f)
        {
            // fallback up to avoid singularity
            upECI = (Mathf.Abs(forwardECI.y) < 0.9f) ? Vector3.up : Vector3.right;
        }

        // Unity's LookRotation returns a rotation whose +Z points forward and +Y points up
        Quaternion q = Quaternion.LookRotation(forwardECI, upECI);

        // This is already body->ECI if we interpret body axes as Unity local axes (+Z fwd, +Y up).
        return q;
    }

    private static double Deg2Rad(double d) { return d * (Math.PI / 180.0); }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }

    // Quaternion helpers (avoid Unity Quaternion * overhead ambiguity in Udon)
    private static Quaternion Multiply(Quaternion a, Quaternion b)
    {
        return new Quaternion(
            a.w*b.x + a.x*b.w + a.y*b.z - a.z*b.y,
            a.w*b.y - a.x*b.z + a.y*b.w + a.z*b.x,
            a.w*b.z + a.x*b.y - a.y*b.x + a.z*b.w,
            a.w*b.w - a.x*b.x - a.y*b.y - a.z*b.z
        );
    }

    private static Quaternion Inverse(Quaternion q)
    {
        float inv = 1.0f / (q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w);
        return new Quaternion(-q.x*inv, -q.y*inv, -q.z*inv, q.w*inv);
    }
}
