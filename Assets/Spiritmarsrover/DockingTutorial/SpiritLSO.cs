using UdonSharp;
using UnityEngine;

public class SpiritLSO : UdonSharpBehaviour
{
    public DockingTutorial tutorial;
    public MFDDockingPage dockingPage;
    public AudioSource spiritAudio;
    
    [Header("Audio Clips")]
    // Assign in inspector: 0=60m, 1=50m, 2=40m, 3=30m, 4=20m, 5=15m, 6=10m, 7=5m, 8=1m
    public AudioClip[] distanceClips; 
    public AudioClip tooFast, tooSlow, alignBad, contact, capture;

    private float nextCalloutTime = 0f;
    private float calloutCooldown = 3.0f;
    private int lastDistIndex = -1; // Index in the marks array

    void Update()
    {
        // Only run LSO during the Final Approach phase
        if (!tutorial.tutorialActive || tutorial.GetCurrentClip() != (int)DockingTutorialClip.FinalApproach)
        {
            lastDistIndex = -1;
            return;
        }

        if (Time.time < nextCalloutTime) return;

        float range = (float)dockingPage.range;
        float speed = (float)dockingPage.speed;
        float closure = (float)dockingPage.closure;

        // 1. Priority: Safety (Speed) - Closure is relative speed towards port
        if (closure > 0.5f && range < 20f) { PlayLSO(tooFast); return; }
        if (closure < 0.02f && range > 5f) { PlayLSO(tooSlow); return; }

        // 2. Priority: Alignment (If they drift out of the "groove")
        float translationErr = Mathf.Sqrt((float)dockingPage.offsetX * (float)dockingPage.offsetX + (float)dockingPage.offsetY * (float)dockingPage.offsetY);
        if (translationErr > 0.8f && range < 30f) { PlayLSO(alignBad); return; }

        // 3. Priority: Distance Callouts
        CheckDistanceCallout(range);
    }

    void CheckDistanceCallout(float range)
    {
        float[] marks = { 60f, 50f, 40f, 30f, 20f, 15f, 10f, 5f, 1f };
        
        for (int i = 0; i < marks.Length; i++)
        {
            // If we just crossed a mark threshold
            if (range <= marks[i] && lastDistIndex < i)
            {
                lastDistIndex = i;
                if (i < distanceClips.Length)
                {
                    PlayLSO(distanceClips[i]);
                }
                return;
            }
        }
    }

    void PlayLSO(AudioClip clip)
    {
        if (clip == null || spiritAudio.isPlaying) return;
        spiritAudio.PlayOneShot(clip);
        nextCalloutTime = Time.time + calloutCooldown;
    }
}