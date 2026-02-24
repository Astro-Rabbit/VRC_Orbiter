using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// GC_IntentToCraftCommandWriter
/// Copies the guidance computer output register (GuidanceCommandIntentState)
/// into the craft command state (CraftCommandState) each tick.
///
/// This is a "bridge" layer to keep GC_Core UI-agnostic and keep craft sim decoupled.
/// </summary>
public class GC_IntentToCraftCommandWriter : UdonSharpBehaviour
{
    [Header("Inputs")]
    public GuidanceCommandIntentState intent;

    [Header("Output (what craft sim reads)")]
    public CraftCommandState craftCmd;

    [Header("Options")]
    [Tooltip("If true, only the object owner writes commands (recommended for networked craft).")]
    public bool ownerOnly = true;

    [Tooltip("If true, enforce commandSource as guidance=1 on the craft command state.")]
    public bool forceCommandSourceGuidance = true;

    [Tooltip("Guidance commandSource value (by convention, 1=guidance).")]
    public byte guidanceCommandSource = 1;

    void LateUpdate()
    {
        Tick();
    }

    public void Tick()
    {
        if (intent == null || craftCmd == null) return;

        if (ownerOnly && !Networking.IsOwner(gameObject)) return;

        // Authority/source (informational)
        if (forceCommandSourceGuidance)
            craftCmd.commandSource = guidanceCommandSource;
        else
            craftCmd.commandSource = intent.commandSource;

        // ----------------------------
        // Attitude
        // ----------------------------
        craftCmd.attitudeCmdMode = intent.attitudeCmdMode;

        craftCmd.tauDirect_B = intent.tauDirect_B;
        craftCmd.rateTarget_B = intent.rateTarget_B;
        craftCmd.qTarget_BE = intent.qTarget_BE;

        craftCmd.pointDirTarget_E = intent.pointDirTarget_E;

        // CraftCommandState currently supports 0=+X,1=+Y,2=+Z
        // Clamp to avoid undefined behavior if UI ever tries negatives later.
        byte axis = intent.bodyAxisToPoint;
        craftCmd.bodyAxisToPoint = (axis > 2) ? (byte)2 : axis;

        craftCmd.blendDirectTorqueWithPD = intent.blendDirectTorqueWithPD;

        // ----------------------------
        // Translation (future)
        // ----------------------------
        craftCmd.translateCmd_B = intent.translateCmd_B;
        craftCmd.rcsMode = intent.rcsMode;

        // ----------------------------
        // Throttle
        // ----------------------------
        craftCmd.mainThrottle01 = Mathf.Clamp01(intent.mainThrottle01);
        craftCmd.hoverThrottle01 = Mathf.Clamp01(intent.hoverThrottle01);

        // ----------------------------
        // Gimbal
        // ----------------------------
        craftCmd.gimbalMode = intent.gimbalMode;
        craftCmd.gimbalPitchYawCmd = intent.gimbalPitchYawCmd;

        // ----------------------------
        // Actuator policy / enables
        // ----------------------------
        craftCmd.attitudeActuatorMode = intent.attitudeActuatorMode;
        craftCmd.allowWheels = intent.allowWheels;
        craftCmd.allowRCS = intent.allowRCS;
        craftCmd.allowGimbal = intent.allowGimbal;
    }
}