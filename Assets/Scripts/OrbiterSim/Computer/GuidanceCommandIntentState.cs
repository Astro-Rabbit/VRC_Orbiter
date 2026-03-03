using UdonSharp;
using UnityEngine;

public class GuidanceCommandIntentState : UdonSharpBehaviour
{
    [Header("Authority / source")]
    public byte commandSource = 1; // 1=guidance by convention

    [Header("Attitude command (mirrors CraftCommandState)")]
    public byte attitudeCmdMode;
    public Vector3 tauDirect_B;
    public Vector3 rateTarget_B;
    public Quaternion qTarget_BE;
    public Vector3 pointDirTarget_E;
    public byte bodyAxisToPoint;
    public bool blendDirectTorqueWithPD;

    [Header("Translation")]
    public Vector3 translateCmd_B;
    public byte rcsMode;

    [Header("Throttle")]
    public float mainThrottle01;
    public float hoverThrottle01;

    [Header("Gimbal")]
    public byte gimbalMode;
    public Vector2 gimbalPitchYawCmd;

    [Header("Actuator policy")]
    public byte attitudeActuatorMode;
    public bool allowWheels;
    public bool allowRCS;
    public bool allowGimbal;

    public void ClearToSafeDefaults(CraftCommandState craftDefaults)
    {
        // Optional: you can copy defaults from CraftCommandState.ResetCommands later.
        // For now just zero most things.
        commandSource = 1;
        attitudeCmdMode = CraftCommandState.ATT_CMD_RATE_TARGET;
        tauDirect_B = Vector3.zero;
        rateTarget_B = Vector3.zero;
        qTarget_BE = Quaternion.identity;
        pointDirTarget_E = Vector3.forward;
        bodyAxisToPoint = 2;
        blendDirectTorqueWithPD = true;

        translateCmd_B = Vector3.zero;
        rcsMode = CraftCommandState.RCS_MODE_BLENDED;

        mainThrottle01 = 0f;
        hoverThrottle01 = 0f;

        gimbalMode = CraftCommandState.GIMBAL_MODE_AUTO_TORQUE;
        gimbalPitchYawCmd = Vector2.zero;

        attitudeActuatorMode = CraftCommandState.ATT_ACT_AUTO;
        allowWheels = true;
        allowRCS = true;
        allowGimbal = true;
    }
}