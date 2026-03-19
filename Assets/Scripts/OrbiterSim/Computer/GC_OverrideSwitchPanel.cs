using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GC_OverrideSwitchPanel : UdonSharpBehaviour
{
    [Header("Seat-local override state")]
    [Tooltip("This should point to the LOCAL override holder for this cockpit side, not the shared live override state.")]
    public GC_ActuatorOverrideState overrides;

    [Header("Raw switch states written by MFDSwitch")]
    public byte rcsSwitchState = 1;
    public byte wheelsSwitchState = 1;
    public byte gimbalSwitchState = 1;

    [Header("Raw knob values")]
    public float actuatorModeKnobValue = 0f;
    public float rcsModeKnobValue = 0f;

    private void Start()
    {
        ApplyAll();
    }

    private byte MapThreeWayAllowPolicy(byte rawState)
    {
        switch (rawState)
        {
            case 0: return GC_ActuatorOverrideState.FORCE_DISABLE; // down
            case 1: return GC_ActuatorOverrideState.NO_OVERRIDE;   // center
            case 2: return GC_ActuatorOverrideState.FORCE_ENABLE;  // up
            default: return GC_ActuatorOverrideState.NO_OVERRIDE;
        }
    }

    public void ApplyRCSSwitch()
    {
        if (overrides == null) return;
        overrides.overrideAllowRCS = MapThreeWayAllowPolicy(rcsSwitchState);
    }

    public void ApplyWheelsSwitch()
    {
        if (overrides == null) return;
        overrides.overrideAllowWheels = MapThreeWayAllowPolicy(wheelsSwitchState);
    }

    public void ApplyGimbalSwitch()
    {
        if (overrides == null) return;
        overrides.overrideAllowGimbal = MapThreeWayAllowPolicy(gimbalSwitchState);
    }

    public void ApplyRcsModeKnob()
    {
        if (overrides == null) return;

        // 4-position knob:
        // 0   = auto / no override
        // 45  = translate
        // 90  = rotate
        // 135 = blended
        int idx = Mathf.RoundToInt(rcsModeKnobValue / 45f);
        idx = Mathf.Clamp(idx, 0, 3);

        switch (idx)
        {
            default:
            case 0:
                overrides.overrideRcsMode = GC_ActuatorOverrideState.RCSMODE_NO_OVERRIDE;
                break;

            case 1:
                overrides.overrideRcsMode = GC_ActuatorOverrideState.RCSMODE_FORCE_TRANSLATE;
                break;

            case 2:
                overrides.overrideRcsMode = GC_ActuatorOverrideState.RCSMODE_FORCE_ROTATE;
                break;

            case 3:
                overrides.overrideRcsMode = GC_ActuatorOverrideState.RCSMODE_FORCE_BLENDED;
                break;
        }
    }

    public void ApplyActuatorModeKnob()
    {
        if (overrides == null) return;

        // Fill in once you finalize the real ATT_ACT_* mapping.
        // For now leave as no override.
        overrides.overrideAttitudeActuatorMode = 0;
    }

    public void ApplyAll()
    {
        if (overrides == null) return;

        ApplyRCSSwitch();
        ApplyWheelsSwitch();
        ApplyGimbalSwitch();
        ApplyRcsModeKnob();
        ApplyActuatorModeKnob();
    }
}