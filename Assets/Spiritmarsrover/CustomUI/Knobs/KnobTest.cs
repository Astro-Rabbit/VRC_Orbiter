using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Ensure you have TextMeshPro in your project

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KnobTest : UdonSharpBehaviour
{
    [Header("Visual Feedback")]
    public TextMeshProUGUI valueDisplay;
    public Transform visualIndicator; // An object to rotate or scale

    // The knob will look for this variable name if you set it in the inspector
    [Header("Data (Set by Knob)")]
    public float currentKnobValue;

    // This method name should match the 'eventName' field on your MFDKnob
    public void OnKnobUpdate()
    {
        // 1. Update the Text
        if (valueDisplay != null)
        {
            valueDisplay.text = $"Value: {currentKnobValue:F2}";
        }

        // 2. Update a visual object (e.g., scale it based on the value)
        if (visualIndicator != null)
        {
            float scale = 1.0f + (currentKnobValue / 100f);
            visualIndicator.localScale = new Vector3(scale, scale, scale);
        }

        // 3. Log to Console
        //Debug.Log($"[KnobTest] Received event. Current Value: {currentKnobValue}");
    }
    public void OnSwitchUp()
    {
        valueDisplay.color = Color.red;
    }
    public void OnSwitchDown()
    {
        valueDisplay.color = Color.green;
    }
    public int SwitchState;
    public void OnSwitch()
    {
        valueDisplay.color = SwitchState == 0 ? Color.red : Color.green;
    }
}