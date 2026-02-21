using UdonSharp;
using UnityEngine;

/// <summary>
/// AttitudeControllerPD (combined controller + command mux)
/// - Reads CraftCommandState.attitudeCmdMode:
///     * TORQUE_DIRECT: uses tauDirect_B
///     * RATE_TARGET:   PD on body-rate error
///     * ATTITUDE_TARGET: PD on attitude error (quat) + rate damping
/// - Optionally blends tauDirect_B on top of PD (when not in TORQUE_DIRECT)
/// - Outputs tauCmd_B in BODY frame (Nm request) for ActuationController.
/// </summary>
public class AttitudeControllerPD : UdonSharpBehaviour
{
    [Header("References")]
    public CraftCommandState cmd;
    public CraftAttitudeState attState;

    [Header("Gains (Attitude target)")]
    [Tooltip("Proportional gain on attitude error vector (Nm/rad).")]
    public float kpAtt = 50f;

    [Tooltip("Derivative gain on body rates (Nm/(rad/s)). Typically damping.")]
    public float kdAtt = 20f;

    [Header("Gains (Rate target)")]
    [Tooltip("Proportional gain on body rate error (Nm/(rad/s)).")]
    public float kpRate = 20f;

    [Tooltip("Extra damping on body rates (Nm/(rad/s)). Usually small or zero if kpRate is enough.")]
    public float kdRate = 0f;

    [Header("Limits / shaping")]
    [Tooltip("Clamp final torque request magnitude (Nm). 0 disables.")]
    public float maxTorqueNm = 0f;

    [Tooltip("Deadband on |tau| (Nm). Below this, output zero.")]
    public float torqueDeadbandNm = 0f;

    [Header("Outputs (BODY frame)")]
    public Vector3 tauCmd_B = Vector3.zero;

    [Header("Debug (BODY frame)")]
    public Vector3 attErr_B = Vector3.zero;   // rad (axis*angle)
    public Vector3 rateErr_B = Vector3.zero;  // rad/s

    /// <summary>
    /// Call once per sim tick/substep (owner-side), before ActuationController.
    /// </summary>
    public void Evaluate()
    {
        tauCmd_B = Vector3.zero;
        attErr_B = Vector3.zero;
        rateErr_B = Vector3.zero;

        if (cmd == null || attState == null) return;

        // Current body rates (rad/s) in BODY frame (state uses doubles)
        Vector3 w_B = new Vector3((float)attState.wx, (float)attState.wy, (float)attState.wz);

        byte mode = cmd.attitudeCmdMode;

        // --- TORQUE DIRECT: bypass PD entirely ---
        if (mode == CraftCommandState.ATT_CMD_TORQUE_DIRECT)
        {
            tauCmd_B = cmd.tauDirect_B;
            ApplyLimits();
            return;
        }

        Vector3 tauPD_B = Vector3.zero;

        // --- RATE TARGET: PD on body rate error ---
        if (mode == CraftCommandState.ATT_CMD_RATE_TARGET)
        {
            Vector3 wTgt_B = cmd.rateTarget_B;
            rateErr_B = wTgt_B - w_B;

            // Simple rate controller: tau = Kp * (wTarget - w) - Kd * w
            // (kdRate is optional extra damping)
            tauPD_B = kpRate * rateErr_B - kdRate * w_B;
        }
        else if (mode == CraftCommandState.ATT_CMD_POINT_VECTOR)
        {
            // Roll-free pointing: align a chosen BODY axis with an inertial direction.
            // Uses cross-product error on the axis direction, so roll about that axis is unconstrained.

            Quaternion qBE = attState.qBE;

            // Pick body axis in BODY frame
            Vector3 axis_B;
            byte ax = cmd.bodyAxisToPoint;
            if (ax == 0)      axis_B = Vector3.right;
            else if (ax == 1) axis_B = Vector3.up;
            else              axis_B = Vector3.forward;

            // Convert body axis to inertial
            Vector3 axis_E = qBE * axis_B;

            // Desired direction in inertial (normalize, handle degenerate)
            Vector3 dir_E = cmd.pointDirTarget_E;
            float d2 = dir_E.sqrMagnitude;
            if (d2 < 1e-12f)
            {
                // No valid target direction; request no torque beyond damping
                attErr_B = Vector3.zero;
                tauPD_B = -kdAtt * w_B;
            }
            else
            {
                dir_E *= 1.0f / Mathf.Sqrt(d2);

                // Error vector in inertial: axis_E x dir_E
                // Magnitude ~ sin(theta), direction is rotation axis.
                Vector3 err_E = Vector3.Cross(axis_E, dir_E);

                // Convert error into BODY frame for a body-frame PD law
                Vector3 err_B = Quaternion.Inverse(qBE) * err_E;

                attErr_B = err_B; // "radians-ish" small-angle error

                // PD: use kpAtt on this error + damping on body rates
                tauPD_B = kpAtt * attErr_B - kdAtt * w_B;
            }
        }
        // --- ATTITUDE TARGET: PD on attitude error + rate damping ---
        else // CraftCommandState.ATT_CMD_ATTITUDE_TARGET (default)
        {
            Quaternion qBE = attState.qBE;
            Quaternion qT  = cmd.qTarget_BE;

            // Rotation from current body to target body, expressed in BODY frame:
            // qDelta = inv(qCurrent) * qTarget
            Quaternion qDelta = Quaternion.Inverse(qBE) * qT;

            // Ensure shortest-arc (avoid unwinding)
            if (qDelta.w < 0f)
            {
                qDelta.x = -qDelta.x;
                qDelta.y = -qDelta.y;
                qDelta.z = -qDelta.z;
                qDelta.w = -qDelta.w;
            }

            attErr_B = QuatToAxisAngleVector(qDelta); // rad (axis * angle)

            // Attitude PD: tau = Kp * err - Kd * w
            tauPD_B = kpAtt * attErr_B - kdAtt * w_B;
        }

        // Optional blend in direct torque "trim" on top of PD
        if (cmd.blendDirectTorqueWithPD)
            tauPD_B += cmd.tauDirect_B;

        tauCmd_B = tauPD_B;

        ApplyLimits();
    }

    private void ApplyLimits()
    {
        // Deadband
        if (torqueDeadbandNm > 0f)
        {
            float mag = tauCmd_B.magnitude;
            if (mag < torqueDeadbandNm)
            {
                tauCmd_B = Vector3.zero;
                return;
            }
        }

        // Clamp magnitude
        if (maxTorqueNm > 0f)
        {
            float mag = tauCmd_B.magnitude;
            if (mag > maxTorqueNm && mag > 1e-9f)
                tauCmd_B *= (maxTorqueNm / mag);
        }
    }

    /// <summary>
    /// Convert a quaternion (assumed shortest-arc) into axis*angle vector in radians.
    /// For small angles this is well-behaved; for larger angles, still ok for PD.
    /// </summary>
    private static Vector3 QuatToAxisAngleVector(Quaternion q)
    {
        // q = [v, w], angle = 2*acos(w), axis = v / sin(angle/2)
        float w = Mathf.Clamp(q.w, -1f, 1f);
        float angle = 2f * Mathf.Acos(w);

        // If angle is tiny, use linear approximation: axis*angle ≈ 2*v
        float s = Mathf.Sqrt(Mathf.Max(0f, 1f - w * w)); // = |sin(angle/2)|
        if (s < 1e-6f || angle < 1e-6f)
        {
            return new Vector3(2f * q.x, 2f * q.y, 2f * q.z);
        }

        float invS = 1f / s;
        Vector3 axis = new Vector3(q.x * invS, q.y * invS, q.z * invS);

        // axis * angle (rad)
        return axis * angle;
    }
}
