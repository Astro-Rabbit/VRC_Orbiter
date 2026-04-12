using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TabletGlassSkyOverlayControl : UdonSharpBehaviour
{
    [Header("Materials using the canopy glass shader")]
    public Material[] targetMaterials;

    [Header("Optional startup synced defaults")]
    public bool startOverlay0Enabled = false;
    public bool startOverlay1Enabled = false;
    public bool startOverlay2Enabled = false;

    [Header("Read-only UI mirrors (effective state after local override)")]
    public bool overlay0Display = false;
    public bool overlay1Display = false;
    public bool overlay2Display = false;
    public bool anyOverlayDisplay = false;
    public bool localOverrideActive = false;

    [Header("Read-only synced mirrors")]
    public bool syncedOverlay0Enabled = false;
    public bool syncedOverlay1Enabled = false;
    public bool syncedOverlay2Enabled = false;

    [UdonSynced] private bool _overlay0Enabled = false;
    [UdonSynced] private bool _overlay1Enabled = false;
    [UdonSynced] private bool _overlay2Enabled = false;
    [UdonSynced] private uint _rev = 0;

    private const string PROP_OVERLAY0 = "_SkyOverlayEnable";
    private const string PROP_OVERLAY1 = "_SkyOverlayEnable1";
    private const string PROP_OVERLAY2 = "_SkyOverlayEnable2";

    void Start()
    {
        if (Networking.IsOwner(gameObject) && _rev == 0)
        {
            _overlay0Enabled = startOverlay0Enabled;
            _overlay1Enabled = startOverlay1Enabled;
            _overlay2Enabled = startOverlay2Enabled;
            _rev = 1;
            RequestSerialization();
        }

        ApplyStateToMaterials();
    }

    public override void OnDeserialization()
    {
        ApplyStateToMaterials();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ApplyStateToMaterials();
    }

    private void EnsureOwnership()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
    }

    private void ApplyStateToMaterials()
    {
        syncedOverlay0Enabled = _overlay0Enabled;
        syncedOverlay1Enabled = _overlay1Enabled;
        syncedOverlay2Enabled = _overlay2Enabled;

        bool e0 = _overlay0Enabled;
        bool e1 = _overlay1Enabled;
        bool e2 = _overlay2Enabled;

        if (localOverrideActive)
        {
            e0 = false;
            e1 = false;
            e2 = false;
        }

        overlay0Display = e0;
        overlay1Display = e1;
        overlay2Display = e2;
        anyOverlayDisplay = e0 || e1 || e2;

        if (targetMaterials == null) return;

        int count = targetMaterials.Length;
        for (int i = 0; i < count; i++)
        {
            Material m = targetMaterials[i];
            if (m == null) continue;

            if (m.HasProperty(PROP_OVERLAY0)) m.SetFloat(PROP_OVERLAY0, e0 ? 1f : 0f);
            if (m.HasProperty(PROP_OVERLAY1)) m.SetFloat(PROP_OVERLAY1, e1 ? 1f : 0f);
            if (m.HasProperty(PROP_OVERLAY2)) m.SetFloat(PROP_OVERLAY2, e2 ? 1f : 0f);
        }
    }

    private void PushSyncedState()
    {
        _rev++;
        RequestSerialization();
        ApplyStateToMaterials();
    }

    public void ToggleOverlay0()
    {
        EnsureOwnership();
        _overlay0Enabled = !_overlay0Enabled;
        PushSyncedState();
    }

    public void ToggleOverlay1()
    {
        EnsureOwnership();
        _overlay1Enabled = !_overlay1Enabled;
        PushSyncedState();
    }

    public void ToggleOverlay2()
    {
        EnsureOwnership();
        _overlay2Enabled = !_overlay2Enabled;
        PushSyncedState();
    }

    public void EnableAllSynced()
    {
        EnsureOwnership();
        _overlay0Enabled = true;
        _overlay1Enabled = true;
        _overlay2Enabled = true;
        PushSyncedState();
    }

    public void DisableAllSynced()
    {
        EnsureOwnership();
        _overlay0Enabled = false;
        _overlay1Enabled = false;
        _overlay2Enabled = false;
        PushSyncedState();
    }

    public void ToggleAllSynced()
    {
        EnsureOwnership();

        bool anyOn = _overlay0Enabled || _overlay1Enabled || _overlay2Enabled;
        bool newState = !anyOn;

        _overlay0Enabled = newState;
        _overlay1Enabled = newState;
        _overlay2Enabled = newState;
        PushSyncedState();
    }

    // local-only override
    public void SetLocalOverlayOverrideOn()
    {
        localOverrideActive = true;
        ApplyStateToMaterials();
    }

    public void SetLocalOverlayOverrideOff()
    {
        localOverrideActive = false;
        ApplyStateToMaterials();
    }

    public void ToggleLocalOverlayOverride()
    {
        localOverrideActive = !localOverrideActive;
        ApplyStateToMaterials();
    }

    public void DisableAllOverlaysLocal()
    {
        SetLocalOverlayOverrideOn();
    }

    public void RestoreSyncedOverlaysLocal()
    {
        SetLocalOverlayOverrideOff();
    }

    public void RefreshNow()
    {
        ApplyStateToMaterials();
    }
}