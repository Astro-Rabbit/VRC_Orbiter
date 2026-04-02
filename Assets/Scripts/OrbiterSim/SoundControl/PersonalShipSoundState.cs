using UdonSharp;
using UnityEngine;

/// <summary>
/// PersonalShipSoundState
///
/// Local-only player preference state for ship audio.
/// This is intentionally non-synced so each player can set their own ship sound mix.
///
/// Intended use:
/// - Other audio drivers hold a reference to this script
/// - Drivers multiply their computed source volume by the effective gain
///
/// V1 scope:
/// - No networking
/// - No persistence
/// - No UI logic
/// - Just a simple shared state + API
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

    [Header("Optional future categories")]
    [Range(0f, 1f)]
    public float interiorSound = 1f;

    [Range(0f, 1f)]
    public float warningSound = 1f;

    private void Start()
    {
        ClampAll();
    }

    // ------------------------------------------------------------
    // Internal
    // ------------------------------------------------------------
    private void ClampAll()
    {
        masterShipSound = Mathf.Clamp01(masterShipSound);
        engineSound = Mathf.Clamp01(engineSound);
        rcsSound = Mathf.Clamp01(rcsSound);
        interiorSound = Mathf.Clamp01(interiorSound);
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
        warningSound = 1f;
    }

    public void MuteAllShipSounds()
    {
        masterShipSound = 0f;
    }
}