using UdonSharp;
using UnityEngine;

/// <summary>
/// GC_ManualDraft
/// Per-tick manual command draft routed through guidance (human inputs).
/// 
/// V1: manual inputs are stick-style attitude (either direct torque or rate target),
/// translation stick (later), and throttles.
/// This should NOT contain autopilot-style targets like pointDir/quaternion.
/// </summary>
public class GC_ManualDraft : UdonSharpBehaviour
{
    [Header("Manual activity flags (set by core)")]
    public bool manualAttitudeActive;
    public bool manualThrottleActive;

    [Header("Manual attitude inputs (B)")]
    [Tooltip("Direct torque command in body frame (for DIRECT_TORQUE manual style).")]
    public Vector3 tauCmd_B;

    [Tooltip("Rate target in body frame (for RATE_TARGET manual style; e.g., stick = desired rate).")]
    public Vector3 rateCmd_B;

    [Tooltip("If true, interpret attitude stick as rate commands; otherwise as direct torque.")]
    public bool useRateControl = true;

    [Header("Manual throttles")]
    public float mainThrottle01;
    public float hoverThrottle01;

    [Header("Manual translation inputs (B)")]
    [Tooltip("Translation stick command in body frame, normalized [-1..1]. X=right, Y=up, Z=forward.")]
    public Vector3 translateCmd_B;

    [Tooltip("Manual RCS mode preference (translate/rotate/blended).")]
    public byte rcsMode;

    [Header("Actuator policy (manual)")]
    public byte attitudeActuatorMode;
    public bool allowWheels = true;
    public bool allowRCS = true;
    public bool allowGimbal = true;

    public void Clear()
    {
        manualAttitudeActive = false;
        manualThrottleActive = false;

        tauCmd_B = Vector3.zero;
        rateCmd_B = Vector3.zero;
        useRateControl = true;

        mainThrottle01 = 0f;
        hoverThrottle01 = 0f;

        translateCmd_B = Vector3.zero;
        rcsMode = 2; // or BLENDED if you prefer default


        attitudeActuatorMode = 3;
        allowWheels = true;
        allowRCS = true;
        allowGimbal = true;
    }
}