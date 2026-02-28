
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PilotSeat : UdonSharpBehaviour
{
    void Start()
    {
        
    }
    public bool _isLocalInStation;


    // This event runs automatically when anyone sits in a station
    public override void OnStationEntered(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            _isLocalInStation = true;
            Debug.Log("[PilotSeat] Seated");
        }
    }

    // This event runs automatically when anyone stands up
    public override void OnStationExited(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            _isLocalInStation = false;
            Debug.Log("[PilotSeat] Exited");
        }
    }
}
