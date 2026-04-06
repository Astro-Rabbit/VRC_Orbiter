using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AmbientShipSoundDriver : UdonSharpBehaviour
{
    [Header("Refs")]
    public PersonalShipSoundState personalSound;
    public AudioSource ambientAudio;

    [Header("Tuning")]
    [Range(0f, 1f)] public float baseVolume = 1f;
    public bool autoPlayOnStart = true;
    public bool keepPlayingWhenMuted = true;

    void Start()
    {
        if (ambientAudio == null) return;

        ambientAudio.loop = true;

        if (autoPlayOnStart && !ambientAudio.isPlaying)
            ambientAudio.Play();

        ApplyVolume();
    }

    void Update()
    {
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (ambientAudio == null) return;

        float gain = 1f;
        if (personalSound != null)
            gain = Mathf.Clamp01(personalSound.GetEffectiveInteriorGain());

        float v = Mathf.Clamp01(baseVolume * gain);
        ambientAudio.volume = v;

        if (!keepPlayingWhenMuted)
        {
            if (v > 0.0001f)
            {
                if (!ambientAudio.isPlaying)
                    ambientAudio.Play();
            }
            else
            {
                if (ambientAudio.isPlaying)
                    ambientAudio.Stop();
            }
        }
        else
        {
            if (!ambientAudio.isPlaying)
                ambientAudio.Play();
        }
    }
}