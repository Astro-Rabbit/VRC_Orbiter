
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDNavigationManager : UdonSharpBehaviour
{
    public GameObject HomePage;
    public GameObject CurrentPage;
    public UdonBehaviour ContinousEventScript;
    public string ContinousEventName;
    void Start()
    {
        CurrentPage = HomePage;
    }

    public void ChangePage(GameObject PageObject)
    {
        //Hides the contents
        CurrentPage.transform.GetChild(0).gameObject.SetActive(false);
        PageObject.SetActive(true);
        CurrentPage = PageObject;
        //Hides the pages
        for (int i = 0; i < CurrentPage.transform.childCount; i++)
        {
            if (i == 0)
            {
                CurrentPage.transform.GetChild(i).gameObject.SetActive(true);
            }
            if (i != 0)
            {
                CurrentPage.transform.GetChild(i).gameObject.SetActive(false);
            }
        }
    }
    public void Update()
    {
        if(ContinousEventScript == null || ContinousEventName == null)
        {
            return;
        }
        ContinousEventScript.SendCustomEvent(ContinousEventName);
    }
}
