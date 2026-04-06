using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PersonalShipSoundState
///
/// Local-only player preference state for ship audio.
/// This is intentionally non-synced so each player can set their own ship sound mix.
///
/// Intended use:
/// - Other audio drivers hold a reference to this script
/// - Drivers multiply their computed source volume by the effective gain
/// - Unity UI sliders call the dedicated On...SliderChanged() events below
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PersonalShipSoundState : UdonSharpBehaviour
{
    [Header("Master ship sound")]
    [Range(0f, 1f)]
    public float masterShipSound = 1f;

    [Header("Category gains")]
    [Range(0f, 1f)]
    public float engineSound = 1f;

    [Range(0f, 1f)]
    public float rcsSound = 1f;

    [Range(0f, 1f)]
    public float interiorSound = 1f;

    [Range(0f, 1f)]
    public float dockingSound = 1f;

    [Range(0f, 1f)]
    public float warningSound = 1f;

    [Header("Optional UI Sliders")]
    public Slider masterShipSoundSlider;
    public Slider engineSoundSlider;
    public Slider rcsSoundSlider;
    public Slider interiorSoundSlider;
    public Slider dockingSoundSlider;
    public Slider warningSoundSlider;

    [Header("UI Sync")]
    public bool syncUiFromStateOnStart = true;

    private void Start()
    {
        ClampAll();

        if (syncUiFromStateOnStart)
        {
            SyncUIFromState();
        }
    }

    private void ClampAll()
    {
        masterShipSound = Mathf.Clamp01(masterShipSound);
        engineSound = Mathf.Clamp01(engineSound);
        rcsSound = Mathf.Clamp01(rcsSound);
        interiorSound = Mathf.Clamp01(interiorSound);
        dockingSound = Mathf.Clamp01(dockingSound);
        warningSound = Mathf.Clamp01(warningSound);
    }

    // ------------------------------------------------------------
    // Raw getters
    // ------------------------------------------------------------
    public float GetMasterShipSound()
    {
        return Mathf.Clamp01(masterShipSound);
    }

    public float GetEngineSound()
    {
        return Mathf.Clamp01(engineSound);
    }

    public float GetRcsSound()
    {
        return Mathf.Clamp01(rcsSound);
    }

    public float GetInteriorSound()
    {
        return Mathf.Clamp01(interiorSound);
    }

    public float GetDockingSound()
    {
        return Mathf.Clamp01(dockingSound);
    }

    public float GetWarningSound()
    {
        return Mathf.Clamp01(warningSound);
    }

    // ------------------------------------------------------------
    // Effective gains
    // ------------------------------------------------------------
    public float GetEffectiveEngineGain()
    {
        return Mathf.Clamp01(masterShipSound) * Mathf.Clamp01(engineSound);
    }

    public float GetEffectiveRcsGain()
    {
        return Mathf.Clamp01(masterShipSound) * Mathf.Clamp01(rcsSound);
    }

    public float GetEffectiveInteriorGain()
    {
        return Mathf.Clamp01(masterShipSound) * Mathf.Clamp01(interiorSound);
    }

    public float GetEffectiveDockingGain()
    {
        return Mathf.Clamp01(masterShipSound) * Mathf.Clamp01(dockingSound);
    }

    public float GetEffectiveWarningGain()
    {
        return Mathf.Clamp01(masterShipSound) * Mathf.Clamp01(warningSound);
    }

    // ------------------------------------------------------------
    // Setters
    // ------------------------------------------------------------
    public void SetMasterShipSound(float value)
    {
        masterShipSound = Mathf.Clamp01(value);
    }

    public void SetEngineSound(float value)
    {
        engineSound = Mathf.Clamp01(value);
    }

    public void SetRcsSound(float value)
    {
        rcsSound = Mathf.Clamp01(value);
    }

    public void SetInteriorSound(float value)
    {
        interiorSound = Mathf.Clamp01(value);
    }

    public void SetDockingSound(float value)
    {
        dockingSound = Mathf.Clamp01(value);
    }

    public void SetWarningSound(float value)
    {
        warningSound = Mathf.Clamp01(value);
    }

    // ------------------------------------------------------------
    // Increment helpers for UI buttons/knobs
    // ------------------------------------------------------------
    public void AddMasterShipSound(float delta)
    {
        masterShipSound = Mathf.Clamp01(masterShipSound + delta);
    }

    public void AddEngineSound(float delta)
    {
        engineSound = Mathf.Clamp01(engineSound + delta);
    }

    public void AddRcsSound(float delta)
    {
        rcsSound = Mathf.Clamp01(rcsSound + delta);
    }

    public void AddInteriorSound(float delta)
    {
        interiorSound = Mathf.Clamp01(interiorSound + delta);
    }

    public void AddDockingSound(float delta)
    {
        dockingSound = Mathf.Clamp01(dockingSound + delta);
    }

    public void AddWarningSound(float delta)
    {
        warningSound = Mathf.Clamp01(warningSound + delta);
    }

    // ------------------------------------------------------------
    // Reset helpers
    // ------------------------------------------------------------
    public void ResetAllToDefault()
    {
        masterShipSound = 1f;
        engineSound = 1f;
        rcsSound = 1f;
        interiorSound = 1f;
        dockingSound = 1f;
        warningSound = 1f;

        SyncUIFromState();
    }

    public void MuteAllShipSounds()
    {
        masterShipSound = 0f;
        SyncUIFromState();
    }

    // ------------------------------------------------------------
    // UI sync helpers
    // ------------------------------------------------------------
    public void SyncUIFromState()
    {
        if (masterShipSoundSlider != null) masterShipSoundSlider.value = Mathf.Clamp01(masterShipSound);
        if (engineSoundSlider != null) engineSoundSlider.value = Mathf.Clamp01(engineSound);
        if (rcsSoundSlider != null) rcsSoundSlider.value = Mathf.Clamp01(rcsSound);
        if (interiorSoundSlider != null) interiorSoundSlider.value = Mathf.Clamp01(interiorSound);
        if (dockingSoundSlider != null) dockingSoundSlider.value = Mathf.Clamp01(dockingSound);
        if (warningSoundSlider != null) warningSoundSlider.value = Mathf.Clamp01(warningSound);
    }

    // ------------------------------------------------------------
    // Unity UI slider events
    // Assign each slider's OnValueChanged to its matching method.
    // ------------------------------------------------------------
    public void OnMasterShipSoundSliderChanged()
    {
        if (masterShipSoundSlider == null) return;
        SetMasterShipSound(masterShipSoundSlider.value);
    }

    public void OnEngineSoundSliderChanged()
    {
        if (engineSoundSlider == null) return;
        SetEngineSound(engineSoundSlider.value);
    }

    public void OnRcsSoundSliderChanged()
    {
        if (rcsSoundSlider == null) return;
        SetRcsSound(rcsSoundSlider.value);
    }

    public void OnInteriorSoundSliderChanged()
    {
        if (interiorSoundSlider == null) return;
        SetInteriorSound(interiorSoundSlider.value);
    }

    public void OnDockingSoundSliderChanged()
    {
        if (dockingSoundSlider == null) return;
        SetDockingSound(dockingSoundSlider.value);
    }

    public void OnWarningSoundSliderChanged()
    {
        if (warningSoundSlider == null) return;
        SetWarningSound(warningSoundSlider.value);
    }
}