using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using TMPro;
public enum TabletButtonMode { Trigger, Continuous, None }
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletButton : UdonSharpBehaviour
{
    public UdonBehaviour targetScript;
    public string eventName;

    private RectTransform rectTransform;
    public TabletButtonMode mode;
    //public UdonBehaviour targetScript;
    //public string eventName;
    public string releaseEventName; // Optional for Trigger mode
    [Header("Colors")]
    public Image targetGraphic;
    public Color normalColor = Color.white;
    public Color highlightedColor = new Color(0.95f, 0.95f, 0.95f);
    public Color pressedColor = new Color(0.78f, 0.78f, 0.78f);
    public Color disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);

    [Header("Toggle Feedback")]
    public TMP_Text textToggleColor;
    public string boolVariableName;

    //void Start()
    //{
    //    rectTransform = (RectTransform)transform;
    //}

    //public bool IsPointInside(Vector3 localPoint)
    //{
    //    // Check if the local point is within the width/height of the RectTransform
    //    Rect rect = rectTransform.rect;
    //    return rect.Contains(localPoint);
    //}

    void Start()
    {
        // GetComponent is more reliable in Udon than casting the 'transform' property
        rectTransform = GetComponent<RectTransform>();
        updateToggleText(); // Initialize text state
    }

    public bool IsPointInside(Vector3 localPoint)
    {
        // Safety check to prevent the EXTERN crash
        if (rectTransform == null) return false;

        // Check if the local point is within the width/height of the RectTransform
        Rect rect = rectTransform.rect;
        return rect.Contains(localPoint);
    }
    private int _lastPenID = -1; // Tracks who currently "owns" the button visuals/logic

    public void OnDown(int id)
    {
        if (mode == TabletButtonMode.None) return;
        _lastPenID = id;
        SetColor(pressedColor);
        if (mode == TabletButtonMode.Trigger) targetScript.SendCustomEvent(eventName);

        // Add this to update the UI immediately after the click
        updateToggleText();
    }

    public void OnStay(int id)
    {
        if (mode != TabletButtonMode.Continuous) return;
        // Only fire continuous events for the pen that currently owns the button
        if (id == _lastPenID) targetScript.SendCustomEvent(eventName);
    }

    public void OnUp(int id)
    {
        if (id != _lastPenID || mode == TabletButtonMode.None) return;

        SetColor(normalColor);

        // Trigger event
        if (mode == TabletButtonMode.Trigger && !string.IsNullOrEmpty(releaseEventName))
            targetScript.SendCustomEvent(releaseEventName);

        // Update text after the event has fired
        updateToggleText();

        _lastPenID = -1;
    }

    public void OnHoverEnter(int id)
    {
        if (mode == TabletButtonMode.None) return;
        _lastPenID = id; // Newest pen takes the highlight
        //SetColor(highlightedColor);
    }

    public void OnHoverExit(int id)
    {
        // Only reset color if the pen LEAVING is the one currently owning the hover
        if (id == _lastPenID)
        {
            SetColor(normalColor);
            _lastPenID = -1;
        }
    }

    private void SetColor(Color c)
    {
        if (targetGraphic != null) targetGraphic.color = c;
    }

    public void updateToggleText()
    {
        if (targetScript == null || string.IsNullOrEmpty(boolVariableName) || textToggleColor == null)
        {
            return;
        }

        object value = targetScript.GetProgramVariable(boolVariableName);
        if (value != null)
        {
            bool state = (bool)value;
            textToggleColor.text = state ? "ON" : "OFF";
            textToggleColor.color = state ? Color.green : Color.red;
        }
    }
    public void OnEnable()
    {
        updateToggleText();
        SetColor(normalColor);
    }
}