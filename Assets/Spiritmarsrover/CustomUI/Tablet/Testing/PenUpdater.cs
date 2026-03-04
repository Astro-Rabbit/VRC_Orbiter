
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PenUpdater : UdonSharpBehaviour
{
    public GameObject PenHolderL;
    public GameObject PenHolderR;
    public VRC_Pickup PenLPickup;
    public VRC_Pickup PenRPickup;
    void Start()
    {
        
    }
    private void Update()
    {
        PenHolderL.transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
        PenHolderL.transform.rotation = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation;

        PenHolderR.transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
        PenHolderR.transform.rotation = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation;
    }
    public void TogglePickup(VRC_Pickup pen)
    {
        pen.pickupable = !pen.pickupable;
    }
    public void ToggleLeftPenPickup()
    {
        TogglePickup(PenLPickup);
    }
    public void ToggleRightPenPickup()
    {
        TogglePickup(PenRPickup);
    }
}
