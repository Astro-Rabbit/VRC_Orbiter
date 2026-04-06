using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class InteractToggleWithBlacklist : UdonSharpBehaviour
{
    [Header("Target")]
    public GameObject targetObject;

    [Header("Blacklist")]
    public string[] blockedDisplayNames;

    [Header("Synced state")]
    private bool isOn;

    [Header("Optional")]
    public bool invertInitialState = false;
    public bool logBlockedAttempts = true;

    private void Start()
    {
        isOn = invertInitialState;
        ApplyState();
    }

    public override void Interact()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null) return;

        if (IsBlocked(localPlayer.displayName))
        {
            if (logBlockedAttempts)
                Debug.Log("[InteractToggleWithBlacklist] Blocked user tried to use toggle: " + localPlayer.displayName);
            return;
        }

        isOn = !isOn;
        ApplyState();
        RequestSerialization();
    }


    private void ApplyState()
    {
        if (targetObject != null)
            targetObject.SetActive(isOn);
    }

    private bool IsBlocked(string displayName)
    {
        if (blockedDisplayNames == null) return false;

        int count = blockedDisplayNames.Length;
        for (int i = 0; i < count; i++)
        {
            string blocked = blockedDisplayNames[i];
            if (blocked != null && blocked != "" && blocked == displayName)
                return true;
        }

        return false;
    }

    public bool IsOn()
    {
        return isOn;
    }

    public void SetOn()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null) return;
        if (IsBlocked(localPlayer.displayName)) return;

        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(localPlayer, gameObject);

        isOn = true;
        ApplyState();
        RequestSerialization();
    }

    public void SetOff()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null) return;
        if (IsBlocked(localPlayer.displayName)) return;

        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(localPlayer, gameObject);

        isOn = false;
        ApplyState();
        RequestSerialization();
    }

    public void Toggle()
    {
        Interact();
    }
}