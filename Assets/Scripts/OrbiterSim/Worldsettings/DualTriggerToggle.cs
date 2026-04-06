using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DualTriggerToggle : UdonSharpBehaviour
{
    [Header("Objects to Toggle")]
    public GameObject objectA;
    public GameObject objectB;

    [UdonSynced] private bool isAActive = true;

    void Start()
    {
        ApplyState();
    }

    public override void Interact()
    {
        // Take ownership before modifying synced state
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        isAActive = !isAActive;
        ApplyState();
        RequestSerialization();
    }

    private void ApplyState()
    {
        if (objectA != null) objectA.SetActive(isAActive);
        if (objectB != null) objectB.SetActive(!isAActive);
    }

    public override void OnDeserialization()
    {
        ApplyState();
    }
}