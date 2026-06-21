using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.Base;
using VRC.SDK3.Components.Video;

public class DockingTutorialVideoController : UdonSharpBehaviour
{
    [Header("References")]
    public DockingTutorial tutorial;
    public BaseVRCVideoPlayer videoPlayer; // Optional: Drag VRCUnityVideoPlayer here
    public MeshRenderer displayRenderer;   // Mesh used to show diagrams/video

    [Header("Visual Content")]
    [Tooltip("Materials or Textures to show if no video is playing for a specific step")]
    public Texture2D[] instructionDiagrams; 

    [Header("Video Content")]
    [Tooltip("URLs for each enum state. Leave blank to use textures instead.")]
    public VRCUrl[] clipURLs;

    private int lastClipIndex = -1;

    void Start()
    {
        if (displayRenderer != null) displayRenderer.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called by DockingTutorial.cs when the clip state changes.
    /// </summary>
    public void PlayClip(DockingTutorialClip clip, bool forceRestart)
    {
        int index = (int)clip;
        
        // Don't restart the same video/image unless forced
        if (index == lastClipIndex && !forceRestart) return;
        lastClipIndex = index;

        if (displayRenderer != null) displayRenderer.gameObject.SetActive(true);

        // 1. Try to play Video if URL is provided
        if (videoPlayer != null && index < clipURLs.Length && !string.IsNullOrEmpty(clipURLs[index].Get()))
        {
            videoPlayer.Stop();
            videoPlayer.PlayURL(clipURLs[index]);
            Debug.Log($"[VideoController] Playing Video URL for step: {clip}");
        }
        // 2. Fallback to Texture Diagram
        else if (displayRenderer != null && index < instructionDiagrams.Length)
        {
            if (videoPlayer != null) videoPlayer.Stop(); // Stop any old video

            // Apply texture to the renderer's material
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", instructionDiagrams[index]);
            displayRenderer.SetPropertyBlock(block);
            
            Debug.Log($"[VideoController] Showing Diagram for step: {clip}");
        }
    }

    public void StopDisplay()
    {
        if (videoPlayer != null) videoPlayer.Stop();
        if (displayRenderer != null) displayRenderer.gameObject.SetActive(false);
        lastClipIndex = -1;
    }

    // This allows the controller to react automatically if the tutorial is active
    void LateUpdate()
    {
        if (tutorial == null || !tutorial.tutorialActive)
        {
            if (lastClipIndex != -1) StopDisplay();
            return;
        }

        // Auto-sync visual if tutorial state changed (failsafe)
        int currentClip = tutorial.GetCurrentClip();
        if (currentClip != lastClipIndex)
        {
            PlayClip((DockingTutorialClip)currentClip, false);
        }
    }
}