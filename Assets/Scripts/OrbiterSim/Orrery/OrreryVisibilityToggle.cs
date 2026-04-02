
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class OrreryVisibilityToggle : UdonSharpBehaviour
{
    public GameObject Orrery;
    public byte OrreryState = 1;
    public GameObject OrreryRemote;
    public GameObject OrreryRemoteRespawn;
    public VRC_Pickup pickup;
    void Start()
    {
        
    }
    public void OrreryOn()
    {
        Orrery.SetActive(true);
    }
    public void OrreryOff()
    {
        Orrery.SetActive(false);
    }
    public void OrreryToggle()
    {
        if(OrreryState == 1)
        {
            Orrery.SetActive(true);
        }
        else
        {
            Orrery.SetActive(false);
        }
        
    }
    public void RecallRemote()
    {
        Networking.SetOwner(Networking.LocalPlayer, OrreryRemote);
        OrreryRemote.transform.position = OrreryRemoteRespawn.transform.position;
        OrreryRemote.transform.rotation = OrreryRemoteRespawn.transform.rotation;

    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (pickup.IsHeld)
        {
            pickup.Drop();
        }
    }
}
