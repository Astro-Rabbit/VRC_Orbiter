
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletPageChanger : UdonSharpBehaviour
{
    public TabletNavigationManager TabletNavMan;
    public GameObject NextPage;
    void Start()
    {
        
    }
    public void ChangePage()
    {
        TabletNavMan.ChangePage(NextPage);
    }
}
