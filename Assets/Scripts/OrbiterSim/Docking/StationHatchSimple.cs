using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class StationHatchSimple : UdonSharpBehaviour
{
    [Header("Target")]
    public GameObject targetObject;

    [Header("Synced State")]
    [UdonSynced] public bool isOpen;

    void Start()
    {
        ApplyState();
    }

    public override void Interact()
    {
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

        isOpen = !isOpen;
        ApplyState();
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        ApplyState();
    }

    private void ApplyState()
    {
        if (targetObject != null)
            targetObject.SetActive(isOpen);
    }
}