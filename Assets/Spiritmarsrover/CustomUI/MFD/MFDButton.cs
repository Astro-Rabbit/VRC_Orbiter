using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public enum ButtonMode { PageChanger, Event, ContinousEvent, None }

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDButton : UdonSharpBehaviour
{
    [Header("References")]
    public MFDNavigationManager MFDManager;
    public int ButtonID;

    [Header("Visual Feedback")]
    public Transform buttonMesh;
    public Vector3 pressedOffset = new Vector3(0, -0.005f, 0);
    private Vector3 _startLocalPos;
    private bool _posInitialized = false;

    [Header("Current Configuration")]
    public ButtonMode mode = ButtonMode.None;
    public GameObject NextPage;
    public UdonBehaviour EventScript;
    public string EventName;
    public UdonBehaviour ContinousEventScript;
    public string ContinousEventName;

    public TMP_Text textToggleColor;
    public string boolVariableName;

    private int _lastPenID = -1;
    //only useful if testing on desktop. will need to build own raycaster for desktop users. 
    //public override void Interact()
    //{
    //    OnDown(0);
    //}

    //public AudioSource ButtonAudioSource;
    void Start()
    {
       // ButtonAudioSource = gameObject.GetComponent<AudioSource>();
        InitializePosition();
    }

    private void InitializePosition()
    {
        if (_posInitialized) return;
        if (buttonMesh != null)
        {
            _startLocalPos = buttonMesh.localPosition;
            _posInitialized = true;
        }
    }

    public void OnDown(int id,TabletPen pen)
    {
        //Debug.Log("[MFDButton] OnDown");
        if (mode == ButtonMode.None) return;
        _lastPenID = id;

        InitializePosition();

        //if (Networking.LocalPlayer.IsUserInVR())
        //{
        //    // Move mesh DOWN
        //    if (buttonMesh != null) buttonMesh.localPosition = _startLocalPos + pressedOffset;
        //}
        // Move mesh DOWN
        if (buttonMesh != null) buttonMesh.localPosition = _startLocalPos + pressedOffset;


        if (mode == ButtonMode.PageChanger)
        {
            //Can do Events assocated with a page change.
            if (EventScript != null)
            {
                EventScript.SendCustomEvent(EventName);
            }
            ChangePage();
            
        }
        else if (mode == ButtonMode.Event && EventScript != null)
        {
            EventScript.SendCustomEvent(EventName);
        }
        else if (mode == ButtonMode.ContinousEvent)
        {
            MFDManager.ContinousEventScript = ContinousEventScript;
            MFDManager.ContinousEventName = ContinousEventName;
        }

        pen.PlayButtonDownClip();
        pen.TriggerHapticEvent();
        if (EventScript == null || boolVariableName == "")
        {
            return;
        }
        updateToggleText();
        

    }

    // NEW: Handle logic while the pen is held down
    public void OnStay(int id)
    {
        if (id != _lastPenID || mode == ButtonMode.None) return;

        // If for some reason the button was reset by a page change 
        // but the pen is still held, keep the mesh DOWN.
        if (buttonMesh != null && buttonMesh.localPosition == _startLocalPos)
        {
            buttonMesh.localPosition = _startLocalPos + pressedOffset;
        }
    }

    public void OnUp(int id,TabletPen pen)
    {
        if (id != _lastPenID) return;
        ResetVisuals();
        pen.PlayButtonUpClip();
    }

    //public void OnHoverEnter(int id) { }
    //public void OnHoverExit(int id)
    //{
    //    if (id == _lastPenID) OnUp(id);
    //}

    public void OnButtonChange()
    {
        // When the page changes, we stop events, but we ONLY move the mesh UP
        // if the pen is no longer holding this button.
        updateToggleText();
        if (_lastPenID == -1)
        {
            ResetVisuals();
        }
        else
        {
            // Stop continuous events even if held
            if (MFDManager != null && MFDManager.ContinousEventScript == ContinousEventScript)
            {
                MFDManager.ContinousEventScript = null;
                MFDManager.ContinousEventName = null;
            }
        }
    }

    private void ResetVisuals()
    {
        InitializePosition();

        // Move mesh UP
        if (buttonMesh != null)
            buttonMesh.localPosition = _startLocalPos;

        if (MFDManager != null && MFDManager.ContinousEventScript == ContinousEventScript)
        {
            MFDManager.ContinousEventScript = null;
            MFDManager.ContinousEventName = null;
        }

        _lastPenID = -1;
    }

    public void ChangePage()
    {
        if (NextPage != null) MFDManager.ChangePage(NextPage);

    }

    public void updateToggleText()
    {
        if(EventScript == null || boolVariableName == "")
        {
            return;
        }
        bool state = (bool)EventScript.GetProgramVariable(boolVariableName);

        textToggleColor.text = state ? "ON" : "OFF";
        textToggleColor.color = state ? Color.green : Color.red;
    }
}