using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PenPosePage : UdonSharpBehaviour
{
    [Header("References")]
    public PenUpdater penUpdater;

    [Header("UI Elements")]
    public TMP_Text leftPoseText;
    public TMP_Text rightPoseText;

    // Called when the page is opened/enabled
     void Update()
     {
        if (penUpdater.PenRPickup.pickupable || penUpdater.PenLPickup.pickupable)
        {
            UpdatePoseDisplay();
        }
        
     }
    private void OnEnable()
    {
        UpdatePoseDisplay();
    }

    // This is the public method that will be called to refresh the UI
    public void UpdatePoseDisplay()
    {
        if (penUpdater == null) return;

        // Update Left Pen Info
        if (leftPoseText != null && penUpdater.PenLPickup != null)
        {
            Vector3 pos = penUpdater.PenLPickup.transform.localPosition;
            Vector3 rot = penUpdater.PenLPickup.transform.localRotation.eulerAngles;

            leftPoseText.text = $"POS: {pos.x:F3}, {pos.y:F3}, {pos.z:F3}\n" +
                               $"ROT: {rot.x:F1}, {rot.y:F1}, {rot.z:F1}";
        }

        // Update Right Pen Info
        if (rightPoseText != null && penUpdater.PenRPickup != null)
        {
            Vector3 pos = penUpdater.PenRPickup.transform.localPosition;
            Vector3 rot = penUpdater.PenRPickup.transform.localRotation.eulerAngles;

            rightPoseText.text = $"POS: {pos.x:F3}, {pos.y:F3}, {pos.z:F3}\n" +
                                $"ROT: {rot.x:F1}, {rot.y:F1}, {rot.z:F1}";
        }
    }
}