
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.EventSystems; // Required for Pointer handlers
public enum ButtonMode
{
    PageChanger,
    Event,
    ContinousEvent,
    None
}
//PageChanger changes the page
//Event triggers an event
//ContiousEvent runs an event every frame.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDButton : UdonSharpBehaviour
{
    public MFDNavigationManager MFDManager;
    public GameObject NextPage;
    public UdonBehaviour EventScript;
    public string EventName;
    public UdonBehaviour ContinousEventScript;
    public string ContinousEventName;
    public ButtonMode mode = ButtonMode.PageChanger;
    public int ButtonID;
    void Start()
    {

    }
    public void OnButtonDown()
    {
        if(mode == ButtonMode.None)
        {
            return;
        }
        if (mode == ButtonMode.PageChanger)
        {
            ChangePage();
        }
        else if(mode == ButtonMode.Event)
        {
            EventScript.SendCustomEvent(EventName);
        }
        else if(mode == ButtonMode.ContinousEvent)
        {
            MFDManager.ContinousEventScript = ContinousEventScript;
            MFDManager.ContinousEventName = ContinousEventName;
        }
    }
    public void OnButtonUp()
    {
        MFDManager.ContinousEventScript = null;
        MFDManager.ContinousEventName = null;
    }
    public void OnButtonChange()
    {
        OnButtonUp();
    }

    public void ChangePage()
    {
        if (NextPage == null)
        {
            return;
        }
        MFDManager.ChangePage(NextPage);
    }

}
