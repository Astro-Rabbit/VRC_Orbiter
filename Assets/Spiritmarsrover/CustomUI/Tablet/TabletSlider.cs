using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletSlider : UdonSharpBehaviour
{//
    [Header("Targeting")]
    public UdonBehaviour targetScript;
    public string eventName;
    public string variableName; // Name of the float variable on targetScript to update

    [Header("Slider Settings")]
    public float minValue = 0f;
    public float maxValue = 1f;
    public bool wholeNumbers = false;
    public float currentValue;

    [Header("Visual Components")]
    public RectTransform handle;
    public RectTransform fill;
    public TMP_Text valueText;

    private RectTransform _rectTransform;
    private float _width;

    void Start()
    {
        _rectTransform = GetComponent<RectTransform>();
        _width = _rectTransform.rect.width;
        UpdateUI();
        if (!string.IsNullOrEmpty(variableName))
            targetScript.SetProgramVariable(variableName, currentValue);
    }

    public bool IsPointInside(Vector3 localPoint)
    {
        if (_rectTransform == null) return false;
        return _rectTransform.rect.Contains(localPoint);
    }

    // Change these signatures
    public void OnDown(int id, Vector3 worldPoint) => ProcessInput(worldPoint);
    public void OnStay(int id, Vector3 worldPoint) => ProcessInput(worldPoint);

    private void ProcessInput(Vector3 worldPoint)
    {
        // Convert the WORLD point specifically to THIS slider's local space
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        float rectLeft = _rectTransform.rect.xMin;
        float percent = Mathf.Clamp01((localPoint.x - rectLeft) / _rectTransform.rect.width);

        currentValue = Mathf.Lerp(minValue, maxValue, percent);
        if (wholeNumbers) currentValue = Mathf.Round(currentValue);

        // ... (rest of your targetScript and UpdateUI logic remains same)
        if (targetScript != null)
        {
            if (!string.IsNullOrEmpty(variableName))
                targetScript.SetProgramVariable(variableName, currentValue);
            if (!string.IsNullOrEmpty(eventName))
                targetScript.SendCustomEvent(eventName);
        }
        UpdateUI();
    }

    public void UpdateUI()
    {
        // Calculate percentage for visuals
        float percent = Mathf.InverseLerp(minValue, maxValue, currentValue);

        // Update Fill (assumes Left-to-Right)
        if (fill != null)
        {
            fill.anchorMax = new Vector2(percent, 1);
        }

        // Update Handle Position
        if (handle != null)
        {
            float xPos = Mathf.Lerp(_rectTransform.rect.xMin, _rectTransform.rect.xMax, percent);
            handle.anchoredPosition = new Vector2(xPos, 0);
        }

        // Update Text
        if (valueText != null)
        {
            valueText.text = wholeNumbers ? currentValue.ToString("F0") : currentValue.ToString("F2");
        }
    }

    // Allows external scripts to set the slider value visually
    public void SetValue(float val)
    {
        currentValue = Mathf.Clamp(val, minValue, maxValue);
        UpdateUI();
    }
}