using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TabletNavigationManager : UdonSharpBehaviour
{
    [Header("Page Setup")]
    public GameObject[] pages; // Assign all your page objects here in the inspector

    [UdonSynced]
    private int _currentPageIndex = 0; // Sync the ID, not the object

    //private int _localPageIndex = -1; // To track changes locally
    public GameObject CurrentPage;
    public GameObject HomePage;
   // public GameObject LastPage;
    void Start()
    {
        // Initial setup
        if (Networking.IsOwner(gameObject))
        {
            _currentPageIndex = 0;
            //LastPage = pages[0];

            RequestSerialization();
        }
        CurrentPage = HomePage;
        UpdatePageVisibility();
        //LastPage = CurrentPage;

    }

    // Called by your buttons
    public void ChangePage(GameObject pageObject)
    {
        // 1. Find the index of the object passed in
        int index = -1;
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] == pageObject)
            {
                index = i;
                break;
            }
        }

        if (index == -1) return; // Page not found in array

        // 2. Take ownership and sync the index
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);

        _currentPageIndex = index;
        RequestSerialization();

        // 3. Update locally immediately for snappiness
        UpdatePageVisibility();
    }

    public override void OnDeserialization()
    {
        // When someone else changes the page, update our view
        UpdatePageVisibility();
    }

    private void UpdatePageVisibility()
    {
        // Safety check
        if (_currentPageIndex < 0 || _currentPageIndex >= pages.Length) return;
        //if(CurrentPage != null)
        //{
        //    CurrentPage.transform.GetChild(0).gameObject.SetActive(false);
        //}
        //else
        //{
        //    Debug.Log("[TabletNavigationManger] LastPage didn't exist");
        //}

        CurrentPage.transform.GetChild(0).gameObject.SetActive(false);
        CurrentPage = pages[_currentPageIndex];
        CurrentPage.SetActive(true);

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

        // Loop through all pages and enable only the synced index
        //for (int i = 0; i < pages.Length; i++)
        //{
        //    bool isTargetPage = (i == _currentPageIndex);

        //    if (pages[i] != null)
        //    {
        //        pages[i].SetActive(isTargetPage);

        //        // Handle your specific "Contents" logic if necessary
        //        if (isTargetPage && pages[i].transform.childCount > 0)
        //        {
        //            // Ensure first child is active, others inactive (based on your original logic)
        //            for (int j = 0; j < pages[i].transform.childCount; j++)
        //            {
        //                pages[i].transform.GetChild(j).gameObject.SetActive(j == 0);
        //            }
        //        }
        //    }
        //}

    }
}