using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletTutorialPage : UdonSharpBehaviour
{
    [Header("Core")]
    public SimClock clock;
    public SimManager simManager;
    public MFDTargetPage MFDTarget;

    [Header("Tutorials")]
    public TabletTutorialSlot[] tutorials;
    public int selectedTutorialIndex = 0;

    [Header("UI Text")]
    public TMP_Text simOwnerText;
    public TMP_Text pageStatusText;
    public TMP_Text warpText;

    public TMP_Text tutorialNameText;
    public TMP_Text tutorialDescriptionText;
    public TMP_Text tutorialStepText;
    public TMP_Text tutorialStatusText;

    [Header("Buttons")]
    public TabletButton tutorialPrevButton;
    public TabletButton tutorialNextButton;
    public TabletButton tutorialStartButton;
    public TabletButton tutorialStopButton;
    public TabletButton tutorialReplayButton;
    public TabletButton tutorialContinueButton;
    public TabletButton tutorialRestartButton;

    public TabletButton warpDownButton;
    public TabletButton warp1xButton;
    public TabletButton warpUpButton;

    [Header("Refresh")]
    public float refreshInterval = 0.1f;

    private readonly double[] _warpLadder = new double[]
    {
        1.0, 2.0, 5.0, 10.0, 50.0, 100.0, 500.0, 1000.0
    };

    private float _refreshTimer = 0f;

    private bool _lastCanBrowse = false;
    private bool _lastCanStart = false;
    private bool _lastCanStop = false;
    private bool _lastCanReplay = false;
    private bool _lastCanContinue = false;
    private bool _lastCanRestart = false;
    private bool _lastCanWarp = false;

    void Start()
    {
        ClampSelectedTutorialIndex();
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

    public void TutorialPrev()
    {
        if (!CanBrowseTutorials()) return;

        int oldIndex = selectedTutorialIndex;

        selectedTutorialIndex--;
        if (selectedTutorialIndex < 0)
            selectedTutorialIndex = tutorials.Length - 1;

        OnTutorialSelectionChanged(oldIndex, selectedTutorialIndex);
        RefreshUI(true);
    }

    public void TutorialNext()
    {
        if (!CanBrowseTutorials()) return;

        int oldIndex = selectedTutorialIndex;

        selectedTutorialIndex++;
        if (selectedTutorialIndex >= tutorials.Length)
            selectedTutorialIndex = 0;

        OnTutorialSelectionChanged(oldIndex, selectedTutorialIndex);
        RefreshUI(true);
    }

    public void StartSelectedTutorial()
    {
        if (!CanStartSelectedTutorial()) return;

        StopAllOtherTutorials(selectedTutorialIndex);

        TabletTutorialSlot slot = GetSelectedTutorial();
        if (slot == null) return;

        slot.StartTutorial();
        RefreshUI(true);
    }
    public void StartSelectedTutorialDockingTesting()
    {
        if (!CanStartSelectedTutorial()) return;

        MFDTarget.SelectStation(2);

        StopAllOtherTutorials(selectedTutorialIndex);

        TabletTutorialSlot slot = GetSelectedTutorial();
        if (slot == null) return;

        slot.StartDockingTutorial();
        RefreshUI(true);
    }

    public void StopCurrentTutorial()
    {
        TabletTutorialSlot active = GetActiveTutorial();
        if (active == null) return;

        active.StopTutorial();
        RefreshUI(true);
    }

    public void ReplayCurrentTutorial()
    {
        TabletTutorialSlot active = GetActiveTutorial();
        if (active == null) return;

        active.ReplayTutorial();
    }

    public void ContinueCurrentTutorial()
    {
        TabletTutorialSlot active = GetActiveTutorial();
        if (active == null) return;
        if (!active.CanContinue()) return;

        active.ContinueTutorial();
        RefreshUI(true);
    }

    public void RestartCurrentTutorial()
    {
        TabletTutorialSlot active = GetActiveTutorial();
        if (active == null) return;
        if (!CanStartSelectedTutorial()) return;

        active.RestartTutorial();
        RefreshUI(true);
    }

    public void WarpUp()
    {
        if (!CanControlWarp()) return;
        if (simManager == null || clock == null) return;

        double current = clock.timeScale;
        double next = GetNextHigherWarp(current);
        simManager.SetRequestedWarp(next);

        RefreshUI(true);
    }

    public void WarpDown()
    {
        if (!CanControlWarp()) return;
        if (simManager == null || clock == null) return;

        double current = clock.timeScale;
        double next = GetNextLowerWarp(current);
        simManager.SetRequestedWarp(next);

        RefreshUI(true);
    }

    public void WarpTo1x()
    {
        if (!CanControlWarp()) return;
        if (simManager == null) return;

        simManager.SetRequestedWarp(1.0);
        RefreshUI(true);
    }

    private void OnTutorialSelectionChanged(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex) return;

        TabletTutorialSlot oldSlot = GetTutorialByIndex(oldIndex);
        if (oldSlot != null && oldSlot.IsActive())
            oldSlot.StopTutorial();
    }

    private void StopAllOtherTutorials(int keepIndex)
    {
        if (tutorials == null) return;

        for (int i = 0; i < tutorials.Length; i++)
        {
            if (i == keepIndex) continue;
            if (tutorials[i] == null) continue;

            if (tutorials[i].IsActive())
                tutorials[i].StopTutorial();
        }
    }

    private void RefreshUI(bool forceButtonRefresh)
    {
        ClampSelectedTutorialIndex();

        UpdateSimOwnerText();
        UpdatePageStatusText();
        UpdateWarpText();
        UpdateTutorialInfoText();

        bool canBrowse = CanBrowseTutorials();
        bool canStart = CanStartSelectedTutorial();
        bool canStop = (GetActiveTutorial() != null);
        bool canReplay = (GetActiveTutorial() != null);
        bool canContinue = false;
        bool canRestart = false;

        TabletTutorialSlot active = GetActiveTutorial();
        if (active != null)
        {
            canContinue = active.CanContinue();
            canRestart = CanStartSelectedTutorial();
        }

        bool canWarp = CanControlWarp();

        if (forceButtonRefresh || canBrowse != _lastCanBrowse)
        {
            ApplyButtonState(tutorialPrevButton, canBrowse);
            ApplyButtonState(tutorialNextButton, canBrowse);
            _lastCanBrowse = canBrowse;
        }

        if (forceButtonRefresh || canStart != _lastCanStart)
        {
            ApplyButtonState(tutorialStartButton, canStart);
            _lastCanStart = canStart;
        }

        if (forceButtonRefresh || canStop != _lastCanStop)
        {
            ApplyButtonState(tutorialStopButton, canStop);
            _lastCanStop = canStop;
        }

        if (forceButtonRefresh || canReplay != _lastCanReplay)
        {
            ApplyButtonState(tutorialReplayButton, canReplay);
            _lastCanReplay = canReplay;
        }

        if (forceButtonRefresh || canContinue != _lastCanContinue)
        {
            ApplyButtonState(tutorialContinueButton, canContinue);
            _lastCanContinue = canContinue;
        }

        if (forceButtonRefresh || canRestart != _lastCanRestart)
        {
            ApplyButtonState(tutorialRestartButton, canRestart);
            _lastCanRestart = canRestart;
        }

        if (forceButtonRefresh || canWarp != _lastCanWarp)
        {
            ApplyButtonState(warpDownButton, canWarp);
            ApplyButtonState(warp1xButton, canWarp);
            ApplyButtonState(warpUpButton, canWarp);
            _lastCanWarp = canWarp;
        }
    }

    private void ClampSelectedTutorialIndex()
    {
        if (tutorials == null || tutorials.Length == 0)
        {
            selectedTutorialIndex = 0;
            return;
        }

        if (selectedTutorialIndex < 0) selectedTutorialIndex = 0;
        if (selectedTutorialIndex >= tutorials.Length) selectedTutorialIndex = tutorials.Length - 1;
    }

    private bool CanBrowseTutorials()
    {
        return tutorials != null && tutorials.Length > 0;
    }

    private bool CanStartSelectedTutorial()
    {
        if (!CanBrowseTutorials()) return false;
        if (simManager == null) return false;

        return simManager.CanLocalUserReset();
    }

    private bool CanControlWarp()
    {
        if (simManager == null) return false;
        if (clock == null) return false;

        return simManager.IsSimOwner() && Networking.IsOwner(clock.gameObject);
    }

    private TabletTutorialSlot GetSelectedTutorial()
    {
        return GetTutorialByIndex(selectedTutorialIndex);
    }

    private TabletTutorialSlot GetTutorialByIndex(int idx)
    {
        if (tutorials == null) return null;
        if (idx < 0 || idx >= tutorials.Length) return null;
        return tutorials[idx];
    }

    private TabletTutorialSlot GetActiveTutorial()
    {
        if (tutorials == null) return null;

        for (int i = 0; i < tutorials.Length; i++)
        {
            if (tutorials[i] != null && tutorials[i].IsActive())
                return tutorials[i];
        }

        return null;
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

    private void UpdatePageStatusText()
    {
        if (pageStatusText == null) return;

        TabletTutorialSlot active = GetActiveTutorial();

        if (active != null)
        {
            pageStatusText.text = "STATUS: " + active.GetStatusText();
            return;
        }

        if (CanStartSelectedTutorial())
            pageStatusText.text = "STATUS: READY";
        else
            pageStatusText.text = "STATUS: READ ONLY";
    }

    private void UpdateWarpText()
    {
        if (warpText == null) return;

        double actualWarp = 1.0;
        if (clock != null)
            actualWarp = clock.timeScale;

        double allowedWarp = actualWarp;
        if (simManager != null && simManager.warpPolicy != null)
            allowedWarp = simManager.warpPolicy.currentAllowedTimeScale;

        warpText.text = "WARP: " + FormatWarp(actualWarp) + " / ALLOW: " + FormatWarp(allowedWarp);
    }

    private void UpdateTutorialInfoText()
    {
        TabletTutorialSlot selected = GetSelectedTutorial();
        TabletTutorialSlot active = GetActiveTutorial();

        if (tutorialNameText != null)
            tutorialNameText.text = "TUTORIAL: " + ((selected != null) ? selected.tutorialName : "---");

        if (tutorialDescriptionText != null)
            tutorialDescriptionText.text = (selected != null) ? selected.tutorialDescription : "---";

        if (tutorialStepText != null)
            tutorialStepText.text = "STEP: " + ((active != null) ? active.GetStepText() : "OFF");

        if (tutorialStatusText != null)
        {
            if (active != null)
                tutorialStatusText.text = "ACTIVE: " + active.tutorialName;
            else
                tutorialStatusText.text = "ACTIVE: NONE";
        }
    }

    private void ApplyButtonState(TabletButton btn, bool enabledState)
    {
        if (btn == null) return;

        btn.mode = enabledState ? TabletButtonMode.Trigger : TabletButtonMode.None;

        if (btn.targetGraphic != null)
            btn.targetGraphic.color = enabledState ? btn.normalColor : btn.disabledColor;
    }

    private double GetNextHigherWarp(double current)
    {
        int n = _warpLadder.Length;
        for (int i = 0; i < n; i++)
        {
            if (_warpLadder[i] > current + 1e-9)
                return _warpLadder[i];
        }

        return _warpLadder[n - 1];
    }

    private double GetNextLowerWarp(double current)
    {
        for (int i = _warpLadder.Length - 1; i >= 0; i--)
        {
            if (_warpLadder[i] < current - 1e-9)
                return _warpLadder[i];
        }

        return _warpLadder[0];
    }

    private string FormatWarp(double warp)
    {
        double rounded = System.Math.Round(warp);

        if (System.Math.Abs(warp - rounded) < 1e-9)
            return ((int)rounded).ToString() + "x";

        return warp.ToString("F2") + "x";
    }
}