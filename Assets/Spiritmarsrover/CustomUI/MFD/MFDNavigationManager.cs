using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDNavigationManager : UdonSharpBehaviour
{
    public GameObject HomePage;
    public GameObject CurrentPage;
    public MFDButton[] BezelButtons;

    [HideInInspector] public UdonBehaviour ContinousEventScript;
    [HideInInspector] public string ContinousEventName;

    void Start()
    {
        ResetAllButtons();
        if (HomePage != null) ChangePage(HomePage);
    }

    public void ChangePage(GameObject PageObject)
    {
        if (PageObject == null) return;

        ResetAllButtons();

        // 1. UI Management
        if (CurrentPage != null) CurrentPage.transform.GetChild(0).gameObject.SetActive(false);
        PageObject.SetActive(true);
        CurrentPage = PageObject;

        // Ensure contents (Child 0) are shown
        if (CurrentPage.transform.childCount > 0)
            CurrentPage.transform.GetChild(0).gameObject.SetActive(true);

        // 2. Hardware Mapping (Look at Child 1: "ChangedButtons")
        if (CurrentPage.transform.childCount > 1)
        {
            Transform changedButtonsFolder = CurrentPage.transform.GetChild(1);

            for (int i = 0; i < changedButtonsFolder.childCount; i++)
            {
                MFDButtonChanger changer = changedButtonsFolder.GetChild(i).GetComponent<MFDButtonChanger>();
                if (changer != null)
                {
                    ApplyAssignment(changer);
                }
            }
        }
        //// Inside ChangePage(GameObject PageObject)
        //MFDPageController controller = PageObject.GetComponent<MFDPageController>();
        //if (controller != null) controller.OnPageOpen();
    }

    private void ApplyAssignment(MFDButtonChanger config)
    {
        if (config.ButtonID >= 0 && config.ButtonID < BezelButtons.Length)
        {
            MFDButton btn = BezelButtons[config.ButtonID];
            btn.mode = config.mode;
            btn.NextPage = config.NextPage;
            btn.EventScript = config.EventScript;
            btn.EventName = config.EventName;
            btn.ContinousEventScript = config.ContinousEventScript;
            btn.ContinousEventName = config.ContinousEventName;
            btn.textToggleColor = config.textToggleColor;
            btn.boolVariableName = config.boolVariableName;
            btn.OnButtonChange();
        }
    }

    public void ResetAllButtons()
    {
        ContinousEventScript = null;
        ContinousEventName = null;
        
        foreach (MFDButton btn in BezelButtons)
        {
            if (btn == null) continue;
            btn.mode = ButtonMode.None;
            btn.boolVariableName = "";
            btn.OnButtonChange();
        }
    }

    public void Update()
    {
        if (ContinousEventScript != null) ContinousEventScript.SendCustomEvent(ContinousEventName);
    }
}