using UdonSharp;
using UnityEngine;

/// <summary>
/// GC_ActuatorOverrideState
/// UI-driven overrides that apply after arbitration.
/// Tri-state per channel: 0=NoOverride, 1=ForceDisable, 2=ForceEnable.
/// </summary>
public class GC_ActuatorOverrideState : UdonSharpBehaviour
{
    public const byte NO_OVERRIDE   = 0;
    public const byte FORCE_DISABLE = 1;
    public const byte FORCE_ENABLE  = 2;

    [Header("Allow toggles")]
    public byte overrideAllowWheels = NO_OVERRIDE;
    public byte overrideAllowRCS    = NO_OVERRIDE;
    public byte overrideAllowGimbal = NO_OVERRIDE;

    [Header("Actuator selection mode override")]
    [Tooltip("0=NoOverride; otherwise set to a CraftCommandState.ATT_ACT_* value.")]
    public byte overrideAttitudeActuatorMode = 0;

    public void Clear()
    {
        overrideAllowWheels = NO_OVERRIDE;
        overrideAllowRCS = NO_OVERRIDE;
        overrideAllowGimbal = NO_OVERRIDE;
        overrideAttitudeActuatorMode = 0;
    }
}