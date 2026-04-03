using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public enum RendezvousTutorialClip
{
    Intro,
    MenuPage,
    TargetPage,
    SelectTarget,
    AlignPage,
    AlignInfo,
    AlignNode,
    NodeAuto,
    NodeTime,
    NodeExec,
    TransferPage,
    TransferInfo,
    TransferCalc,
    TransferNode,
    DockPage,
    MatchInfo,
    MatchTime,
    MatchDir,
    MatchBurn,
    FinishBurn,
    TargetDir,
    TargetBurn,
    RestOfTheOwl,
    Outro,
}

public class RendezvousTutorial : UdonSharpBehaviour
{
    [Header("References")]
    public MFD[] mfds;
    public MFDMenuPage menuPage;
    public MFDTargetPage targetPage;
    public MFDAlignPage alignPage;
    public MFDTransferPage transferPage;
    public MFDDockingPage dockingPage;
    public SimClock clock;
    public CraftStateModel craft;
    public GuidanceNavContactsState navContactsState;
    public AttitudeControllerPD pd;
    public GC_Core gc;
    public TMP_Text output;
    public GameObject continueButton;

    public SimManager simManager;


    [Header("Tablet Tutorial UI")]
    public TMP_Text simOwnerText;
    public TMP_Text statusText;
    public TMP_Text warpText;

    [Header("Tablet Tutorial Buttons")]
    public TabletButton startButton;
    public TabletButton replayButton;
    public TabletButton continueTabletButton;
    public TabletButton restartButton;

    [Header("Tablet Tutorial Refresh")]
    public float refreshInterval = 0.1f;

    [Header("Tutorial Scenario")]
    public int tutorialScenarioIndex = 0;

    private float _refreshTimer = 0f;
    private bool _lastCanStart = false;
    private bool _lastCanReplay = false;
    private bool _lastCanContinue = false;
    private bool _lastCanRestart = false;

    public RendezvousTutorialVideoController videoController;
    private int lastClip = -1;

    [Header("Settings")]
    public int targetIndex = 2;

    [Header("Thresholds")]
    public double alignInLim = 0.15;
    public double alignOutLim = 0.2;
    //public double flybyInLim = 150000;
    //public double flybyOutLim = 200000;
    public double proximityInLim = 200000;
    public double proximityOutLim = 300000;
    public double dirInLim = 2.0;
    public double dirOutLim = 3.0;
    public double interceptTimeLim = 300;
    public float velMatchInLim = 20.0f;
    public float velMatchOutLim = 200.0f;


    [Header("Close approach thresholds")]
    public float closingSpeedPerKm = 1.0f;
    public float closingSpeedMax = 100.0f;
    public float closingSpeedTolerance = 2.0f;
    public double finalZoneInLim = 8000.0;
    public double finalZoneOutLim = 10000.0;
    public float finalZoneSpeedLim = 10.0f;

    [Header("Close approach state")]
    public bool closingSpeedReached = false;
    public bool didClosingBurn = false;
    public bool inFinalZone = false;


    [Header("Sticky Condition Flags")]
    public bool planeAligned = false;
    public bool onFlyby = false;
    public bool proximity = false;
    public bool pointingDir = false;
    public bool velocityMatched = false;

    public bool hasAlignNode = false;
    public bool hasTransferNode = false;

    public bool readyStart = false;
    public bool readyAlign = false;
    public bool readyTransfer = false;
    public bool readyMatch = false;
    public bool matchWarpDropDone = false;
    private int clip = (int)RendezvousTutorialClip.Intro;
    private double nodeBurnTime;
    private double transferInterceptTime;
    private bool correctTarget;
    private int execPhase;

    void Start()
    {
        Reset();
        RefreshTabletUI(true);
    }

    void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= refreshInterval) {
            _refreshTimer = 0f;
            RefreshTabletUI(false);
        }
    }

    void LateUpdate()
    {
        UpdateStickyConditions();

        clip = SelectClip();

        if (output != null) {
            output.text = ((RendezvousTutorialClip)clip).ToString();
        }

        if (continueButton != null) {
            switch (clip) {
            case (int)RendezvousTutorialClip.Intro:
            case (int)RendezvousTutorialClip.AlignInfo:
            case (int)RendezvousTutorialClip.TransferInfo:
            case (int)RendezvousTutorialClip.MatchInfo:
                continueButton.SetActive(true);
                break;
            default:
                continueButton.SetActive(false);
                break;
            }
        }

        if (clip != lastClip) {
            if (videoController != null) {
                videoController.PlayClip((RendezvousTutorialClip)clip, false);
            }
            lastClip = clip;
        }


    }

    private int SelectClip()
    {
        if (!readyStart) {
            return (int)RendezvousTutorialClip.Intro;
        }

        bool executing = execPhase != GC_RuntimeState.EXEC_PHASE_NONE && execPhase != GC_RuntimeState.EXEC_PHASE_WAIT;

        if (proximity) {
            if (!PageOpened(dockingPage)) {
                if (PageOpened(menuPage)) {
                    return (int)RendezvousTutorialClip.DockPage;
                } else {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }

            if (velocityMatched) {
                float mainThrottle = 0f;
                int activeProgramId = -1;
                float relSpeed = 0f;
                double rangeMeters = 0.0;

                if (gc != null && gc.intent != null) {
                    mainThrottle = gc.intent.mainThrottle01;
                }
                if (gc != null && gc.runtime != null) {
                    activeProgramId = gc.runtime.activeProgramId;
                }
                if (dockingPage != null) {
                    relSpeed = (float)dockingPage.speed;
                    rangeMeters = dockingPage.range;
                }

                float desiredClosingSpeed = Mathf.Min((float)(rangeMeters / 1000.0) * closingSpeedPerKm, closingSpeedMax);

                // Final completion zone:
                // if close and still too fast, go back to canceling relative velocity.
                // if close and slow enough, tutorial is done.
                if (inFinalZone) {
                    if (relSpeed > finalZoneSpeedLim) {
                        if (!pointingDir || activeProgramId != GC_RuntimeState.PROG_RELVEL_RETRO) {
                            return (int)RendezvousTutorialClip.MatchDir;
                        }
                        return (int)RendezvousTutorialClip.MatchBurn;
                    }
                    return (int)RendezvousTutorialClip.Outro;
                }

                // After the first target-burn cycle, hand off to the repeat-cycle guidance.
                if (didClosingBurn) {
                    return (int)RendezvousTutorialClip.RestOfTheOwl;
                }

                // Before first target burn is complete, make sure we're pointing at target.
                if (!pointingDir || activeProgramId != GC_RuntimeState.PROG_DOCK_POINT_PORT) {
                    return (int)RendezvousTutorialClip.TargetDir;
                }

                // Once the player has accelerated enough, ask for throttle cut.
                if (closingSpeedReached) {
                    return (int)RendezvousTutorialClip.FinishBurn;
                }

                // Keep asking for forward burn until desired closing speed is reached.
                return (int)RendezvousTutorialClip.TargetBurn;
            }

            {
                int activeProgramId = -1;
                if (gc != null && gc.runtime != null) {
                    activeProgramId = gc.runtime.activeProgramId;
                }

                if (!pointingDir || activeProgramId != GC_RuntimeState.PROG_RELVEL_RETRO) {
                    return (int)RendezvousTutorialClip.MatchDir;
                }
            }
            return (int)RendezvousTutorialClip.MatchBurn;
        }

        if (onFlyby) {
            if (!PageOpened(dockingPage)) {
                if (PageOpened(menuPage)) {
                    return (int)RendezvousTutorialClip.DockPage;
                } else {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }

            if (!readyMatch) {
                return (int)RendezvousTutorialClip.MatchInfo;
            }
            return (int)RendezvousTutorialClip.MatchTime;
        }

        if (planeAligned) {
            if (!PageOpened(transferPage)) {
                if (PageOpened(menuPage)) {
                    return (int)RendezvousTutorialClip.TransferPage;
                } else {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }

            if (!readyTransfer) {
                return (int)RendezvousTutorialClip.TransferInfo;
            }

            bool autoValid = false;
            if (transferPage != null && transferPage.solver != null) {
                autoValid = transferPage.solver.autoValid;
            }

            if (!autoValid) {
                return (int)RendezvousTutorialClip.TransferCalc;
            }
            if (!hasTransferNode) {
                return (int)RendezvousTutorialClip.TransferNode;
            }

            bool autoExecuteArmedNodes = false;
            if (gc != null && gc.runtime != null) {
                autoExecuteArmedNodes = gc.runtime.autoExecuteArmedNodes;
            }

            if (!autoExecuteArmedNodes) {
                return (int)RendezvousTutorialClip.NodeAuto;
            }
            if (!executing) {
                return (int)RendezvousTutorialClip.NodeTime;
            }
            return (int)RendezvousTutorialClip.NodeExec;
        }

        if (!correctTarget) {
            if (!PageOpened(targetPage)) {
                if (PageOpened(menuPage)) {
                    return (int)RendezvousTutorialClip.TargetPage;
                } else {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }

            return (int)RendezvousTutorialClip.SelectTarget;
        }

        if (!PageOpened(alignPage)) {
            if (PageOpened(menuPage)) {
                return (int)RendezvousTutorialClip.AlignPage;
            } else {
                return (int)RendezvousTutorialClip.MenuPage;
            }
        }

        if (!readyAlign) {
            return (int)RendezvousTutorialClip.AlignInfo;
        }
        if (!hasAlignNode) {
            return (int)RendezvousTutorialClip.AlignNode;
        }

        {
            bool autoExecuteArmedNodes = false;
            if (gc != null && gc.runtime != null) {
                autoExecuteArmedNodes = gc.runtime.autoExecuteArmedNodes;
            }

            if (!autoExecuteArmedNodes) {
                return (int)RendezvousTutorialClip.NodeAuto;
            }
        }

        if (!executing) {
            return (int)RendezvousTutorialClip.NodeTime;
        }
        return (int)RendezvousTutorialClip.NodeExec;
    }

    private bool PageOpened(MFDPage page)
    {
        if (page == null || mfds == null) {
            return false;
        }

        for (int i = 0; i < mfds.Length; i++) {
            if (mfds[i] == null) continue;
            if (mfds[i].currentPage == page) {
                return true;
            }
        }

        return false;
    }

    // Conditions based on continuous variables that need hysteresis on their threshold are updated here
    void UpdateStickyConditions()
    {
        double time = 0.0;
        if (clock != null) {
            time = clock.Now();
        }

        correctTarget = false;
        if (navContactsState != null) {
            correctTarget = navContactsState.selectedStationIndex == targetIndex;
        }

        execPhase = GC_RuntimeState.EXEC_PHASE_NONE;
        if (gc != null && gc.runtime != null) {
            execPhase = gc.runtime.executorPhase;
        }

        bool execDone =
            execPhase == GC_RuntimeState.EXEC_PHASE_NONE ||
            execPhase == GC_RuntimeState.EXEC_PHASE_WAIT ||
            execPhase == GC_RuntimeState.EXEC_PHASE_POST;

        if (alignPage != null && alignPage.hasTarget && correctTarget && execDone) {
            double inclination = 180.0 / Math.PI * alignPage.inclination;
            if (inclination < alignInLim) {
                planeAligned = true;
            } else if (inclination > alignOutLim) {
                planeAligned = false;
            }
        }

        if (hasAlignNode && execDone && time > nodeBurnTime) {
            hasAlignNode = false;
        }

        // Ideally we'd actually check if we got a flyby, but this is much easier to implement for now
        if (hasTransferNode && execDone && time > nodeBurnTime) {
            hasTransferNode = false;
            onFlyby = true;
        }
        if (onFlyby && time > transferInterceptTime + interceptTimeLim) {
            onFlyby = false;
        }

        if (dockingPage != null && dockingPage.hasTarget && correctTarget && execDone) {


            bool wasProximity = proximity;

            if (dockingPage.range < proximityInLim) {
                proximity = true;
            } else if (dockingPage.range > proximityOutLim) {
                proximity = false;
            }

            if (!wasProximity && proximity && !matchWarpDropDone) {
                if (simManager != null) {
                    simManager.SetRequestedWarp(1.0);
                }
                matchWarpDropDone = true;
            }

            if (dockingPage.speed < velMatchInLim) {
                velocityMatched = true;
            } else if (dockingPage.speed > velMatchOutLim) {
                velocityMatched = false;
            }

            if (dockingPage.range < finalZoneInLim) {
                inFinalZone = true;
            } else if (dockingPage.range > finalZoneOutLim) {
                inFinalZone = false;
            }

            if (velocityMatched && proximity) {
                float desiredClosingSpeed = Mathf.Min((float)(dockingPage.range / 1000.0) * closingSpeedPerKm, closingSpeedMax);

                if (!closingSpeedReached && dockingPage.speed >= desiredClosingSpeed - closingSpeedTolerance) {
                    closingSpeedReached = true;
                }

                // Once the user has reached the closing speed and brought throttle back down,
                // transition to the repeat-cycle instruction.
                if (closingSpeedReached) {
                    float mainThrottle = 0f;
                    if (gc != null && gc.intent != null) {
                        mainThrottle = gc.intent.mainThrottle01;
                    }

                    if (mainThrottle <= 0.01f) {
                        didClosingBurn = true;
                    }
                }
            }
        }

        float dirErr = 999999f;
        if (pd != null) {
            dirErr = (float)(180.0 / Math.PI) * pd.attErr_B.magnitude;
        }

        if (dirErr < dirInLim) {
            pointingDir = true;
        } else if (dirErr > dirOutLim) {
            pointingDir = false;
        }
    }
    public void OnAlignNodeCreate(double time)
    {
        hasAlignNode = true;
        nodeBurnTime = time;
    }

    public void OnTransferNodeCreate(double time, double interceptTime)
    {
        hasTransferNode = true;
        nodeBurnTime = time;
        transferInterceptTime = interceptTime;
    }

    public void Reset()
    {
        planeAligned = false;
        onFlyby = false;
        proximity = false;
        pointingDir = false;
        velocityMatched = false;

        hasAlignNode = false;
        hasTransferNode = false;

        readyStart = false;
        readyAlign = false;
        readyTransfer = false;
        readyMatch = false;

        closingSpeedReached = false;
        didClosingBurn = false;
        inFinalZone = false;
        matchWarpDropDone = false;

        clip = (int)RendezvousTutorialClip.Intro;
        lastClip = -1;

        if (videoController != null) {
            videoController.StopPlayback();
        }


    }


    public void StartTutorial()
    {
        if (!CanStartOrRestart()) return;

        if (simManager != null) {
            simManager.RestartToScenarioIndex(tutorialScenarioIndex);
            simManager.SetRequestedWarp(1.0);
        }

        Reset();
        RefreshTabletUI(true);
    }

    public void RestartTutorial()
    {
        if (!CanStartOrRestart()) return;

        if (simManager != null) {
            simManager.RestartToScenarioIndex(tutorialScenarioIndex);
            simManager.SetRequestedWarp(1.0);
        }

        Reset();
        RefreshTabletUI(true);
    }

    public void Continue()
    {
        switch (clip) {
        case (int)RendezvousTutorialClip.Intro:
            readyStart = true;
            break;
        case (int)RendezvousTutorialClip.AlignInfo:
            readyAlign = true;
            break;
        case (int)RendezvousTutorialClip.TransferInfo:
            readyTransfer = true;
            break;
        case (int)RendezvousTutorialClip.MatchInfo:
            readyMatch = true;
            break;
        default:
            Debug.Log("WARNING: Tutorial next button pressed during invalid clip");
            break;
        }

        RefreshTabletUI(true);

    }

    public void Replay()
    {
        if (videoController != null) {
            videoController.PlayClip((RendezvousTutorialClip)clip, true);
        }
    }

    private void RefreshTabletUI(bool forceButtonRefresh)
    {
        UpdateSimOwnerText();
        UpdateStatusText();
        UpdateWarpText();

        bool canStart = CanStartOrRestart();
        bool canReplay = true;
        bool canContinue = CanContinue();
        bool canRestart = CanStartOrRestart();

        if (forceButtonRefresh || canStart != _lastCanStart) {
            ApplyButtonState(startButton, canStart);
            _lastCanStart = canStart;
        }

        if (forceButtonRefresh || canReplay != _lastCanReplay) {
            ApplyButtonState(replayButton, canReplay);
            _lastCanReplay = canReplay;
        }

        if (forceButtonRefresh || canContinue != _lastCanContinue) {
            ApplyButtonState(continueTabletButton, canContinue);
            _lastCanContinue = canContinue;
        }

        if (forceButtonRefresh || canRestart != _lastCanRestart) {
            ApplyButtonState(restartButton, canRestart);
            _lastCanRestart = canRestart;
        }
    }

    private bool CanStartOrRestart()
    {
        if (simManager == null) return false;
        return simManager.CanLocalUserReset();
    }

    private bool CanContinue()
    {
        switch (clip) {
            case (int)RendezvousTutorialClip.Intro:
            case (int)RendezvousTutorialClip.AlignInfo:
            case (int)RendezvousTutorialClip.TransferInfo:
            case (int)RendezvousTutorialClip.MatchInfo:
                return true;
        }

        return false;
    }

    private void UpdateSimOwnerText()
    {
        if (simOwnerText == null) return;

        string name = "---";
        if (simManager != null) {
            VRCPlayerApi owner = Networking.GetOwner(simManager.gameObject);
            if (owner != null)
                name = owner.displayName;
        }

        simOwnerText.text = "SIM OWNER: " + name;
    }

    private void UpdateStatusText()
    {
        if (statusText == null) return;

        if (simManager == null) {
            statusText.text = "STATUS: ---";
            return;
        }

        if (simManager.CanLocalUserReset()) {
            statusText.text = "STATUS: CONTROL";
        } else {
            statusText.text = "STATUS: READ ONLY";
        }
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

    private void ApplyButtonState(TabletButton btn, bool enabledState)
    {
        if (btn == null) return;

        btn.mode = enabledState ? TabletButtonMode.Trigger : TabletButtonMode.None;

        if (btn.targetGraphic != null)
            btn.targetGraphic.color = enabledState ? btn.normalColor : btn.disabledColor;
    }

    private string FormatWarp(double warp)
    {
        double rounded = System.Math.Round(warp);

        if (System.Math.Abs(warp - rounded) < 1e-9)
            return ((int)rounded).ToString() + "x";

        return warp.ToString("F2") + "x";
    }


}

