using UdonSharp;
using UnityEngine;

/// <summary>
/// GC_ActuatorOverrideState
/// Global “hardware/safety switches” applied AFTER arbitration.
/// Tri-state: 0=NoOverride, 1=ForceDisable, 2=ForceEnable.
/// </summary>
public class GC_ActuatorOverrideState : UdonSharpBehaviour
{
    public const byte NO_OVERRIDE   = 0;
    public const byte FORCE_DISABLE = 1;
    public const byte FORCE_ENABLE  = 2;

    public const byte RCSMODE_NO_OVERRIDE      = 0;
    public const byte RCSMODE_FORCE_TRANSLATE  = 1;
    public const byte RCSMODE_FORCE_ROTATE     = 2;
    public const byte RCSMODE_FORCE_BLENDED    = 3;

    [Header("Allow toggles (tri-state)")]
    public byte overrideAllowWheels = NO_OVERRIDE;
    public byte overrideAllowRCS    = NO_OVERRIDE;
    public byte overrideAllowGimbal = NO_OVERRIDE;

    [Header("Attitude actuator selection override")]
    [Tooltip("0 = NoOverride; otherwise set to CraftCommandState.ATT_ACT_*")]
    public byte overrideAttitudeActuatorMode = 0;

    [Header("Translation / RCS mode override")]
    [Tooltip("0=NoOverride, 1=ForceTranslate, 2=ForceRotate, 3=ForceBlended")]
    public byte overrideRcsMode = RCSMODE_NO_OVERRIDE;

    public void Clear()
    {
        overrideAllowWheels = NO_OVERRIDE;
        overrideAllowRCS = NO_OVERRIDE;
        overrideAllowGimbal = NO_OVERRIDE;
        overrideAttitudeActuatorMode = 0;
        overrideRcsMode = RCSMODE_NO_OVERRIDE;        
    }

    // Optional convenience toggles for UI buttons/switches:
    public void SetRCSOff()  { overrideAllowRCS = FORCE_DISABLE; }
    public void SetRCSOn()   { overrideAllowRCS = FORCE_ENABLE; }
    public void ClearRCS()   { overrideAllowRCS = NO_OVERRIDE; }

    public void SetGimbalOff(){ overrideAllowGimbal = FORCE_DISABLE; }
    public void SetGimbalOn() { overrideAllowGimbal = FORCE_ENABLE; }
    public void ClearGimbal() { overrideAllowGimbal = NO_OVERRIDE; }

    public void SetWheelsOff(){ overrideAllowWheels = FORCE_DISABLE; }
    public void SetWheelsOn() { overrideAllowWheels = FORCE_ENABLE; }
    public void ClearWheels() { overrideAllowWheels = NO_OVERRIDE; }

    public void SetRcsModeTranslate() { overrideRcsMode = RCSMODE_FORCE_TRANSLATE; }
    public void SetRcsModeRotate()    { overrideRcsMode = RCSMODE_FORCE_ROTATE; }
    public void SetRcsModeBlended()   { overrideRcsMode = RCSMODE_FORCE_BLENDED; }
    public void ClearRcsMode()        { overrideRcsMode = RCSMODE_NO_OVERRIDE; }


}