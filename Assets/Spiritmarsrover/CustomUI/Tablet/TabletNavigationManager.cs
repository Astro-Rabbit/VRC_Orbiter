
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletNavigationManager : UdonSharpBehaviour
{
    public GameObject HomePage;
    public GameObject CurrentPage;
    void Start()
    {
        CurrentPage = HomePage;
        ChangePage(HomePage);
        //CurrentPage = HomePage;
        
    }
    public void ChangePage(GameObject PageObject)
    {
        //Hides the contents
        //foreach (GameObject CurrentPageObject in CurrentPage.GetComponentsInChildren<GameObject>())
        //{
        //    if (CurrentPageObject.name.EndsWith("Contents"))
        //    {
        //        CurrentPageObject.SetActive(false);
        //        break;
        //    }
        //}
        CurrentPage.transform.GetChild(0).gameObject.SetActive(false);
        PageObject.SetActive(true);
        CurrentPage = PageObject;
        //Hides the pages
        //foreach (GameObject CurrentPageObject in CurrentPage.GetComponentsInChildren<GameObject>())
        //{
        //    if (!CurrentPageObject.name.EndsWith("Contents"))
        //    {
        //        CurrentPageObject.SetActive(false);
        //        //break;
        //    }
        //}
        for(int i = 0; i < CurrentPage.transform.childCount; i++)
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
}
