using UdonSharp;
using UnityEngine;

public class CraftCommandState : UdonSharpBehaviour
{
    // ----------------------------
    // Attitude command modes
    // ----------------------------
    public const byte ATT_CMD_TORQUE_DIRECT   = 0;  // tauDirect_B (Nm)
    public const byte ATT_CMD_RATE_TARGET     = 1;  // rateTarget_B (rad/s)
    public const byte ATT_CMD_ATTITUDE_TARGET = 2;  // qTarget_BE
    public const byte ATT_CMD_POINT_VECTOR    = 3;  // point a body axis at a direction (roll-free)

    // ----------------------------
    // Actuator selection modes
    // ----------------------------
    public const byte ATT_ACT_WHEELS_ONLY = 0;
    public const byte ATT_ACT_RCS_ONLY    = 1;
    public const byte ATT_ACT_GIMBAL_ONLY = 2;
    public const byte ATT_ACT_AUTO        = 3;

    // ----------------------------
    // RCS control modes
    // ----------------------------
    public const byte RCS_MODE_TRANSLATE = 0;
    public const byte RCS_MODE_ROTATE    = 1;
    public const byte RCS_MODE_BLENDED   = 2;

    // ----------------------------
    // Gimbal control modes (NEW)
    // ----------------------------
    public const byte GIMBAL_MODE_AUTO_TORQUE  = 0; // Actuation uses attitude tau request + throttle to compute gimbal angles
    public const byte GIMBAL_MODE_MANUAL_INPUT = 1; // Pilot/autopilot provides gimbalPitch/Yaw cmds directly

    [Header("Authority / source (optional)")]
    [Tooltip("0=pilot, 1=guidance/autopilot, 2=scripted/test. Purely informational.")]
    public byte commandSource = 0;

    // ----------------------------
    // Attitude targets / direct torque
    // ----------------------------
    [Header("Attitude command")]
    public byte attitudeCmdMode = ATT_CMD_ATTITUDE_TARGET;

    [Tooltip("Direct torque request in BODY frame (Nm). Used when attitudeCmdMode == ATT_CMD_TORQUE_DIRECT.")]
    public Vector3 tauDirect_B = Vector3.zero;

    [Tooltip("Body rate target (rad/s). Used when attitudeCmdMode == ATT_CMD_RATE_TARGET.")]
    public Vector3 rateTarget_B = Vector3.zero;

    [Tooltip("Attitude target qBE (body->inertial). Used when attitudeCmdMode == ATT_CMD_ATTITUDE_TARGET.")]
    public Quaternion qTarget_BE = Quaternion.identity;

    [Tooltip("Desired pointing direction in INERTIAL frame (unit preferred). Used when attitudeCmdMode == ATT_CMD_POINT_VECTOR.")]
    public Vector3 pointDirTarget_E = Vector3.forward;

    [Tooltip("Which BODY axis to align to pointDirTarget_E. 0=+X, 1=+Y, 2=+Z.")]
    public byte bodyAxisToPoint = 2;

    [Tooltip("If true, add tauDirect_B on top of PD torque (rate/attitude modes).")]
    public bool blendDirectTorqueWithPD = true;

    // ----------------------------
    // Translation commands
    // ----------------------------
    [Header("Translation command")]
    [Tooltip("Translation command in BODY frame, normalized [-1..1] per axis (X=right, Y=up, Z=forward).")]
    public Vector3 translateCmd_B = Vector3.zero;

    [Tooltip("RCS mode: translate/rotate/blended (allocator policy).")]
    public byte rcsMode = RCS_MODE_BLENDED;

    // ----------------------------
    // Throttle commands
    // ----------------------------
    [Header("Throttle")]
    [Range(0f, 1f)] public float mainThrottle01 = 0f;
    [Range(0f, 1f)] public float hoverThrottle01 = 0f;

    // ----------------------------
    // Gimbal commands (NEW)
    // ----------------------------
    [Header("Gimbal command (mains)")]
    [Tooltip("Gimbal control mode. AUTO uses attitude torque request; MANUAL uses gimbalPitchYawCmd.")]
    public byte gimbalMode = GIMBAL_MODE_AUTO_TORQUE;

    [Tooltip("Manual gimbal inputs, normalized [-1..1]. x=yaw, y=pitch. Used only when gimbalMode == MANUAL.")]
    public Vector2 gimbalPitchYawCmd = Vector2.zero;

    // ----------------------------
    // Actuator policy / enables
    // ----------------------------
    [Header("Actuator policy")]
    [Tooltip("Attitude actuator selection: wheels/rcs/gimbal/auto.")]
    public byte attitudeActuatorMode = ATT_ACT_AUTO;

    public bool allowWheels = true;
    public bool allowRCS    = true;
    public bool allowGimbal = true;

    // ----------------------------
    // Utility
    // ----------------------------
    public void ResetCommands()
    {
        commandSource = 0;

        attitudeCmdMode = ATT_CMD_ATTITUDE_TARGET;
        tauDirect_B = Vector3.zero;
        rateTarget_B = Vector3.zero;
        qTarget_BE = Quaternion.identity;
        pointDirTarget_E = Vector3.forward;
        bodyAxisToPoint = 2;
        blendDirectTorqueWithPD = true;

        translateCmd_B = Vector3.zero;
        rcsMode = RCS_MODE_BLENDED;

        mainThrottle01 = 0f;
        hoverThrottle01 = 0f;

        gimbalMode = GIMBAL_MODE_AUTO_TORQUE;
        gimbalPitchYawCmd = Vector2.zero;

        attitudeActuatorMode = ATT_ACT_AUTO;
        allowWheels = true;
        allowRCS = true;
        allowGimbal = true;
    }

    // Optional helper setters
    public void SetPointVectorTarget(Vector3 dirE, byte bodyAxis)
    {
        attitudeCmdMode = ATT_CMD_POINT_VECTOR;
        pointDirTarget_E = dirE;
        bodyAxisToPoint = bodyAxis;
    }

    public void SetAttitudeTarget(Quaternion qTargetBE)
    {
        attitudeCmdMode = ATT_CMD_ATTITUDE_TARGET;
        qTarget_BE = qTargetBE;
    }

    public void SetRateTarget(Vector3 rateTargetB_rad_s)
    {
        attitudeCmdMode = ATT_CMD_RATE_TARGET;
        rateTarget_B = rateTargetB_rad_s;
    }

    public void SetDirectTorque(Vector3 tauDirectB_Nm)
    {
        attitudeCmdMode = ATT_CMD_TORQUE_DIRECT;
        tauDirect_B = tauDirectB_Nm;
    }

    public void SetTranslateCmd(Vector3 cmdB)
    {
        translateCmd_B = ClampVec3(cmdB, -1f, 1f);
    }

    public void SetMainThrottle(float t01)
    {
        mainThrottle01 = Mathf.Clamp01(t01);
    }

    public void SetManualGimbal(float yaw01, float pitch01)
    {
        gimbalMode = GIMBAL_MODE_MANUAL_INPUT;
        gimbalPitchYawCmd = new Vector2(Mathf.Clamp(yaw01, -1f, 1f), Mathf.Clamp(pitch01, -1f, 1f));
    }

    private static Vector3 ClampVec3(Vector3 v, float lo, float hi)
    {
        v.x = Mathf.Clamp(v.x, lo, hi);
        v.y = Mathf.Clamp(v.y, lo, hi);
        v.z = Mathf.Clamp(v.z, lo, hi);
        return v;
    }
}
