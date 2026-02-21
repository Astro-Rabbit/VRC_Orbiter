using UdonSharp;
using UnityEngine;

public class CraftControlState : UdonSharpBehaviour
{
    [Header("Main engine")]
    [Range(0f, 1f)]
    public float throttle01 = 0f;

    

    [Header("Thrust direction mode")]
    [Tooltip("0=Prograde, 1=Retrograde, 2=Fixed ECI vector, 3=BodyAxis")]
    public byte thrustMode = 0;

    [Tooltip("Used when thrustMode=3. Body-frame thrust axis (will be rotated by attitude qBE). Typically +Z.")]
    public Vector3 thrustAxisBody = Vector3.forward;

    [Header("Attitude targets (for autopilot / guidance)")]
    [Tooltip("Target forward direction in Unity-ECI (used when attitudeMode=2).")]
    public Vector3 targetForwardECI = Vector3.forward;

    [Tooltip("Target up reference in Unity-ECI (optional; used when attitudeMode=2). If zero, controller will choose a default.")]
    public Vector3 targetUpECI = Vector3.up;

    [Tooltip("Target body->ECI quaternion (used when attitudeMode=3).")]
    public Quaternion targetQ_BE = Quaternion.identity;

    [Header("Attitude commands (normalized -1..1)")]
    public float pitchCmd = 0f;
    public float yawCmd   = 0f;
    public float rollCmd  = 0f;

    [Header("Attitude control")]
    [Tooltip("0=ManualRate, 1=HoldAttitude, 2=HoldTargetForward, 3=HoldTargetQuat, 4-killRot")]

    public byte attitudeMode = 0;

    [Tooltip("Max body rate when in ManualRate mode (deg/s).")]
    public float maxRateDegS = 30f;

    [Header("Actuator selection")]
    [Tooltip("0=Auto, 1=GyroOnly, 2=RcsOnly, 3=Mix")]
    public byte actuatorMode = 0;

    [Range(0f, 1f)] public float gyroWeight = 1f;
    [Range(0f, 1f)] public float rcsWeight  = 0f;
}
