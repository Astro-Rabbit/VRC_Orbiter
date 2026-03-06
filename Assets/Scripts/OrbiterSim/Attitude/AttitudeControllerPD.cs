using UdonSharp;
using UnityEngine;

/// <summary>
/// AttitudeControllerPD (V2.0 - anti-kick shaping + optional max-rate + torque slew limiting)
/// - Reads CraftCommandState.attitudeCmdMode:
///     * TORQUE_DIRECT:     uses tauDirect_B (still passes through shaping/limits)
///     * RATE_TARGET:       PD on body-rate error (uses measured body rates for D)
///     * POINT_VECTOR:      roll-free pointing (axis align) + rate damping
///     * ATTITUDE_TARGET:   PD on attitude error (quat) + rate damping
///
/// New in V2:
/// - Optional per-axis max angular rate limit (rad/s):
///     * RATE_TARGET: clamps cmd.rateTarget_B components
///     * POINT_VECTOR / ATTITUDE_TARGET: uses a rate-limited outer loop:
///           wTgt_B = clamp(kRateFromAtt * attErr_B, maxRate_B)
///           tau = kpRate * (wTgt_B - w_B) - kdAtt * w_B
///   This significantly reduces chatter/derivative “kicks” because the controller won’t demand
///   aggressive rate reversals near zero error.
/// - Optional torque command slew rate limiting (Nm/s) to prevent frame-to-frame spikes.
///
/// Outputs:
/// - tauCmd_B in BODY frame (Nm request) for ActuationController.
/// </summary>
public class AttitudeControllerPD : UdonSharpBehaviour
{
    [Header("References")]
    public CraftCommandState cmd;
    public CraftAttitudeState attState;

    [Header("Gains (Attitude target - direct torque PD)")]
    [Tooltip("Proportional gain on attitude error vector (Nm/rad). Used when max-rate outer loop is disabled.")]
    public float kpAtt = 50f;

    [Tooltip("Derivative gain on body rates (Nm/(rad/s)). Used as damping in all modes.")]
    public float kdAtt = 20f;

    [Header("Gains (Rate target / inner loop)")]
    [Tooltip("Proportional gain on body rate error (Nm/(rad/s)). Used in RATE_TARGET and in max-rate outer-loop modes.")]
    public float kpRate = 20f;

    [Tooltip("Extra damping on body rates (Nm/(rad/s)). Usually small or zero if kpRate is enough.")]
    public float kdRate = 0f;

    [Header("D-term conditioning (NEW)")]
    [Tooltip("Low-pass time constant for D term rate (seconds). 0 disables filtering.")]
    public float dRateFilterTau = 0.08f;

    [Tooltip("Clamp magnitude of D contribution (Nm). 0 disables.")]
    public float maxDTermNm = 0f;

    private Vector3 _wFilt_B = Vector3.zero;

    [Header("Max angular rate limiter (NEW)")]
    [Tooltip("Enable per-axis angular rate limiting (rad/s).")]
    public bool enableMaxAngularRate = false;

    [Tooltip("Max |wx| (rad/s) when limiter enabled. 0 disables that axis limit.")]
    public float maxRateX = 0.0f;

    [Tooltip("Max |wy| (rad/s) when limiter enabled. 0 disables that axis limit.")]
    public float maxRateY = 0.0f;

    [Tooltip("Max |wz| (rad/s) when limiter enabled. 0 disables that axis limit.")]
    public float maxRateZ = 0.0f;

    [Tooltip("Outer-loop gain from attitude error (rad) to desired body rate (rad/s): wTgt = kRateFromAtt * attErr.")]
    public float kRateFromAtt = 2.0f;

    [Header("Output shaping (NEW)")]
    [Tooltip("Limit how fast tauCmd_B can change (Nm/s). 0 disables.")]
    public float maxTorqueSlewNmPerSec = 0f;

    [Header("Limits / deadband")]
    [Tooltip("Clamp final torque request magnitude (Nm). 0 disables.")]
    public float maxTorqueNm = 0f;

    [Tooltip("Deadband on |tau| (Nm). Below this, output zero.")]
    public float torqueDeadbandNm = 0f;

    [Header("Outputs (BODY frame)")]
    public Vector3 tauCmd_B = Vector3.zero;

    [Header("Debug (BODY frame)")]
    public Vector3 attErr_B = Vector3.zero;   // rad (axis*angle) or "radians-ish" for point-vector
    public Vector3 rateErr_B = Vector3.zero;  // rad/s

    // Slew limiter state
    private Vector3 _tauPrev_B = Vector3.zero;

    /// <summary>
    /// Call once per sim tick/substep (owner-side), before ActuationController.
    /// </summary>
    public void Evaluate()
    {
        Vector3 tauDesired_B = Vector3.zero;

        attErr_B = Vector3.zero;
        rateErr_B = Vector3.zero;

        if (cmd == null || attState == null)
        {
            tauCmd_B = Vector3.zero;
            _tauPrev_B = tauCmd_B;
            return;
        }

        // Current body rates (rad/s) in BODY frame (state uses doubles)
        Vector3 w_B = new Vector3((float)attState.wx, (float)attState.wy, (float)attState.wz);
        Vector3 wD_B = FilterRateForD(w_B);

        byte mode = cmd.attitudeCmdMode;

        // ---- TORQUE DIRECT: bypass controller law, but still apply shaping/limits ----
        if (mode == CraftCommandState.ATT_CMD_TORQUE_DIRECT)
        {
            tauDesired_B = cmd.tauDirect_B;
        }
        else if (mode == CraftCommandState.ATT_CMD_RATE_TARGET)
        {
            // RATE TARGET: tau = Kp * (wTarget - w) - Kd * w
            Vector3 wTgt_B = cmd.rateTarget_B;

            if (enableMaxAngularRate)
                wTgt_B = ClampRateTarget(wTgt_B);

            rateErr_B = wTgt_B - w_B;
            tauDesired_B = kpRate * rateErr_B - kdRate * w_B;
        }
        else if (mode == CraftCommandState.ATT_CMD_POINT_VECTOR)
        {
            // Roll-free pointing: align chosen BODY axis with inertial direction, roll unconstrained.
            Quaternion qBE = attState.qBE;

            Vector3 axis_B;
            byte ax = cmd.bodyAxisToPoint;
            if (ax == 0)      axis_B = Vector3.right;
            else if (ax == 1) axis_B = Vector3.up;
            else              axis_B = Vector3.forward;

            Vector3 axis_E = qBE * axis_B;

            Vector3 dir_E = cmd.pointDirTarget_E;
            float d2 = dir_E.sqrMagnitude;

            if (d2 < 1e-12f)
            {
                // No target => damping only
                tauDesired_B = -kdAtt * wD_B;
            }
            else
            {
                dir_E *= 1.0f / Mathf.Sqrt(d2);

                // Error vector in inertial: axis_E x dir_E (small-angle ~ radians)
                Vector3 err_E = Vector3.Cross(axis_E, dir_E);

                // To BODY frame
                Vector3 err_B = Quaternion.Inverse(qBE) * err_E;
                attErr_B = err_B;

                if (enableMaxAngularRate)
                {
                    // Rate-limited outer loop: wTgt = clamp(kRateFromAtt * err, maxRate)
                    Vector3 wTgt_B = ClampRateTarget(attErr_B * kRateFromAtt);
                    rateErr_B = wTgt_B - wD_B;

                    // Inner loop: rate PD (use measured rate for damping)
                    tauDesired_B = kpRate * rateErr_B - kdAtt * wD_B;
                }
                else
                {
                    // Direct attitude PD torque
                    tauDesired_B = kpAtt * attErr_B - kdAtt * wD_B;
                }
            }
        }
        else
        {
            // ATTITUDE TARGET (quat)
            Quaternion qBE = attState.qBE;
            Quaternion qT  = cmd.qTarget_BE;

            // qDelta = inv(qCurrent) * qTarget
            Quaternion qDelta = Quaternion.Inverse(qBE) * qT;

            // shortest-arc
            if (qDelta.w < 0f)
            {
                qDelta.x = -qDelta.x;
                qDelta.y = -qDelta.y;
                qDelta.z = -qDelta.z;
                qDelta.w = -qDelta.w;
            }

            attErr_B = QuatToAxisAngleVector(qDelta); // rad axis*angle

            if (enableMaxAngularRate)
            {
                // Rate-limited outer loop
                Vector3 wTgt_B = ClampRateTarget(attErr_B * kRateFromAtt);
                rateErr_B = wTgt_B - wD_B;

                tauDesired_B = kpRate * rateErr_B - kdAtt * wD_B;
            }
            else
            {
                // Direct attitude PD torque
                tauDesired_B = kpAtt * attErr_B - kdAtt * wD_B;
            }
        }

        // Optional blend in direct torque "trim" on top (except TORQUE_DIRECT which already set it)
        if (mode != CraftCommandState.ATT_CMD_TORQUE_DIRECT && cmd.blendDirectTorqueWithPD)
            tauDesired_B += cmd.tauDirect_B;

        // Apply output shaping + limits
        tauCmd_B = ApplyShapingAndLimits(tauDesired_B);

        // Save state for slew limiter
        _tauPrev_B = tauCmd_B;
    }

    private Vector3 ApplyShapingAndLimits(Vector3 tauIn_B)
    {
        Vector3 tau = tauIn_B;

        // Torque slew limiting (prevents frame-to-frame spikes / “kick” behavior)
        if (maxTorqueSlewNmPerSec > 0f)
        {
            float dt = Time.deltaTime;
            if (dt < 0f) dt = 0f;

            float maxStep = maxTorqueSlewNmPerSec * dt;

            // Per-component MoveTowards is usually what you want for Udon stability.
            tau.x = Mathf.MoveTowards(_tauPrev_B.x, tau.x, maxStep);
            tau.y = Mathf.MoveTowards(_tauPrev_B.y, tau.y, maxStep);
            tau.z = Mathf.MoveTowards(_tauPrev_B.z, tau.z, maxStep);
        }

        // Deadband (after slew so we don’t “stick” just above zero)
        if (torqueDeadbandNm > 0f)
        {
            float mag = tau.magnitude;
            if (mag < torqueDeadbandNm)
                return Vector3.zero;
        }

        // Clamp magnitude
        if (maxTorqueNm > 0f)
        {
            float mag = tau.magnitude;
            if (mag > maxTorqueNm && mag > 1e-9f)
                tau *= (maxTorqueNm / mag);
        }

        return tau;
    }

    private Vector3 FilterRateForD(Vector3 w_B)
    {
        float tau = dRateFilterTau;
        if (tau <= 0f) return w_B;

        float dt = Time.deltaTime;
        if (dt <= 0f) return _wFilt_B;

        // alpha = dt/(tau+dt) is cheap and stable
        float a = dt / (tau + dt);
        _wFilt_B = Vector3.Lerp(_wFilt_B, w_B, a);
        return _wFilt_B;
    }

    private Vector3 ClampDTerm(Vector3 d)
    {
        if (maxDTermNm <= 0f) return d;
        float m = d.magnitude;
        if (m <= maxDTermNm || m < 1e-9f) return d;
        return d * (maxDTermNm / m);
    }

    private Vector3 ClampRateTarget(Vector3 wTgt_B)
    {
        // Per-axis clamp with optional disabling per axis (max=0)
        if (maxRateX > 0f) wTgt_B.x = Mathf.Clamp(wTgt_B.x, -maxRateX, maxRateX);
        if (maxRateY > 0f) wTgt_B.y = Mathf.Clamp(wTgt_B.y, -maxRateY, maxRateY);
        if (maxRateZ > 0f) wTgt_B.z = Mathf.Clamp(wTgt_B.z, -maxRateZ, maxRateZ);
        return wTgt_B;
    }

    /// <summary>
    /// Convert a quaternion (assumed shortest-arc) into axis*angle vector in radians.
    /// For small angles: axis*angle ≈ 2*v.
    /// </summary>
    private static Vector3 QuatToAxisAngleVector(Quaternion q)
    {
        float w = Mathf.Clamp(q.w, -1f, 1f);
        float angle = 2f * Mathf.Acos(w);

        float s = Mathf.Sqrt(Mathf.Max(0f, 1f - w * w)); // |sin(angle/2)|
        if (s < 1e-6f || angle < 1e-6f)
        {
            return new Vector3(2f * q.x, 2f * q.y, 2f * q.z);
        }

        float invS = 1f / s;
        Vector3 axis = new Vector3(q.x * invS, q.y * invS, q.z * invS);
        return axis * angle;
    }
}