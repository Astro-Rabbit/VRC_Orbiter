
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CheckboxUI : UdonSharpBehaviour
{
    //public GameObject checkmark;
    public Image panel;
    public bool isOn = false;
    public GameObject toggleObject;
    void Start()
    {
        updateVisual();
    }
    public void ToggleUI()
    {
        isOn = !isOn;
        updateVisual();


    }
    public void updateVisual()
    {
        //checkmark.SetActive(isOn);
        if (isOn)
        {
            panel.color = Color.green;
        }
        else
        {
            panel.color = Color.red;
        }
        toggleObject.SetActive(isOn);
    }
}
