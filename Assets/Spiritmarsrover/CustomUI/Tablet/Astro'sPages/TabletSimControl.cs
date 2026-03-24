using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletSimControl : UdonSharpBehaviour
{
    [Header("Core")]
    public SimManager simManager;
    public SimScenarioInitializer scenarioInitializer;

    [Header("UI Text")]
    public TMP_Text simOwnerText;
    public TMP_Text masterText;
    public TMP_Text statusText;
    public TMP_Text scenarioText;
    public TMP_Text lockText;

    [Header("Buttons")]
    public TabletButton scenarioPrevButton;
    public TabletButton scenarioNextButton;
    public TabletButton resetButton;
    public TabletButton lockToggleButton;

    [Header("Refresh")]
    public float refreshInterval = 0.1f;

    [Header("Selection")]
    public int selectedScenarioIndex = 0;

    [Header("Debug")]
    public bool debugLog = true;

    private float _refreshTimer = 0f;

    private bool _lastCanReset = false;
    private bool _lastCanToggleLock = false;
    private bool _lastCanBrowse = false;

    void Start()
    {
        ClampScenarioIndex();
        RefreshUI(true);
    }

    void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= refreshInterval)
        {
            _refreshTimer = 0f;
            RefreshUI(false);
        }
    }


    public void OwnerSwap()
    {
        if (simManager == null)
        {
            Debug.Log("[HandoffUIButton] ERROR: SimManager not assigned");
            return;
        }

        VRCPlayerApi local = Networking.LocalPlayer;

        if (local == null)
        {
            Debug.Log("[HandoffUIButton] ERROR: No local player");
            return;
        }

        int myId = local.playerId;

        if (debugLog)
            Debug.Log($"[HandoffUIButton] Requesting handoff → playerId={myId}");

        // Send request ONLY to current owner of SimManager
        simManager.SendCustomNetworkEvent(
            VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner,
            nameof(SimManager.Evt_RequestHandoff),
            myId
        );
    }


    public void ScenarioPrev()
    {
        if (!CanBrowseScenarios()) return;
        if (scenarioInitializer == null) return;

        int count = scenarioInitializer.GetScenarioCount();
        if (count <= 0) return;

        selectedScenarioIndex--;
        if (selectedScenarioIndex < 0)
            selectedScenarioIndex = count - 1;

        RefreshUI(true);
    }

    public void ScenarioNext()
    {
        if (!CanBrowseScenarios()) return;
        if (scenarioInitializer == null) return;

        int count = scenarioInitializer.GetScenarioCount();
        if (count <= 0) return;

        selectedScenarioIndex++;
        if (selectedScenarioIndex >= count)
            selectedScenarioIndex = 0;

        RefreshUI(true);
    }

    public void ResetToSelectedScenario()
    {
        if (!CanReset()) return;
        if (simManager == null) return;

        simManager.RestartToScenarioIndex(selectedScenarioIndex);
        RefreshUI(true);
    }

    public void ToggleResetLock()
    {
        if (!CanToggleLock()) return;
        if (simManager == null) return;

        simManager.SetResetLockByMaster(!simManager.resetLockedByMaster);
        RefreshUI(true);
    }

    private void RefreshUI(bool forceButtonRefresh)
    {
        ClampScenarioIndex();

        UpdateSimOwnerText();
        UpdateMasterText();
        UpdateStatusText();
        UpdateScenarioText();
        UpdateLockText();

        bool canReset = CanReset();
        bool canToggleLock = CanToggleLock();
        bool canBrowse = CanBrowseScenarios();

        if (forceButtonRefresh || canBrowse != _lastCanBrowse)
        {
            ApplyButtonState(scenarioPrevButton, canBrowse);
            ApplyButtonState(scenarioNextButton, canBrowse);
            _lastCanBrowse = canBrowse;
        }

        if (forceButtonRefresh || canReset != _lastCanReset)
        {
            ApplyButtonState(resetButton, canReset);
            _lastCanReset = canReset;
        }

        if (forceButtonRefresh || canToggleLock != _lastCanToggleLock)
        {
            ApplyButtonState(lockToggleButton, canToggleLock);
            _lastCanToggleLock = canToggleLock;
        }
    }

    private void ClampScenarioIndex()
    {
        if (scenarioInitializer == null)
        {
            selectedScenarioIndex = 0;
            return;
        }

        int count = scenarioInitializer.GetScenarioCount();
        if (count <= 0)
        {
            selectedScenarioIndex = 0;
            return;
        }

        if (selectedScenarioIndex < 0) selectedScenarioIndex = 0;
        if (selectedScenarioIndex >= count) selectedScenarioIndex = count - 1;
    }

    private bool CanBrowseScenarios()
    {
        return scenarioInitializer != null && scenarioInitializer.GetScenarioCount() > 0;
    }

    private bool CanReset()
    {
        if (simManager == null) return false;
        return simManager.CanLocalUserReset();
    }

    private bool CanToggleLock()
    {
        return Networking.IsMaster;
    }

    private void UpdateSimOwnerText()
    {
        if (simOwnerText == null) return;

        string name = "---";
        if (simManager != null)
        {
            VRCPlayerApi owner = Networking.GetOwner(simManager.gameObject);
            if (owner != null)
                name = owner.displayName;
        }

        simOwnerText.text = "SIM OWNER: " + name;
    }

    private void UpdateMasterText()
    {
        if (masterText == null) return;

        string name = "---";

        int count = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi[] players = new VRCPlayerApi[count];
        VRCPlayerApi.GetPlayers(players);

        for (int i = 0; i < players.Length; i++)
        {
            VRCPlayerApi p = players[i];
            if (p != null && p.isMaster)
            {
                name = p.displayName;
                break;
            }
        }

        masterText.text = "INSTANCE MASTER: " + name;
    }

    private void UpdateStatusText()
    {
        if (statusText == null) return;

        bool isSimOwner = (simManager != null) && simManager.IsSimOwner();
        bool isMaster = Networking.IsMaster;
        bool locked = (simManager != null) && simManager.resetLockedByMaster;

        string txt;

        if (locked && !isMaster)
            txt = "STATUS: RESET LOCKED";
        else if (CanReset())
            txt = "STATUS: RESET CONTROL";
        else if (isSimOwner)
            txt = "STATUS: OWNER / LOCKED OUT";
        else
            txt = "STATUS: READ ONLY";

        statusText.text = txt;
    }

    private void UpdateScenarioText()
    {
        if (scenarioText == null) return;

        string name = "---";
        int count = 0;

        if (scenarioInitializer != null)
        {
            count = scenarioInitializer.GetScenarioCount();
            if (count > 0)
                name = scenarioInitializer.GetScenarioNameByIndex(selectedScenarioIndex);
        }

        scenarioText.text = "SCENARIO: " + name + " [" + selectedScenarioIndex + "/" + ((count > 0) ? (count - 1) : 0) + "]";
    }

    private void UpdateLockText()
    {
        if (lockText == null) return;
        if (simManager == null)
        {
            lockText.text = "RESET LOCK: ---";
            return;
        }

        lockText.text = simManager.resetLockedByMaster ? "RESET LOCK: ON" : "RESET LOCK: OFF";
    }

    private void ApplyButtonState(TabletButton btn, bool enabledState)
    {
        if (btn == null) return;

        btn.mode = enabledState ? TabletButtonMode.Trigger : TabletButtonMode.None;

        if (btn.targetGraphic != null)
            btn.targetGraphic.color = enabledState ? btn.normalColor : btn.disabledColor;
    }
}