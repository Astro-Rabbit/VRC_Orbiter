using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDButtonChanger : UdonSharpBehaviour
{
    [Header("Which physical button is this for?")]
    public int ButtonID;

    [Header("Configuration")]
    public ButtonMode mode;
    public GameObject NextPage;
    public UdonBehaviour EventScript;
    public string EventName;
    public UdonBehaviour ContinousEventScript;
    public string ContinousEventName;
    public TMP_Text textToggleColor;
    public string boolVariableName;
}