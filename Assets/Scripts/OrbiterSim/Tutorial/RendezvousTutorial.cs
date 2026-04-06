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
    public RendezvousTutorialVideoController videoController;
    public SimScenarioInitializer scenarioInitializer;

    [Header("Tutorial Scenario")]
    public int tutorialScenarioIndex = 6;

    [Header("Tutorial Runtime")]
    public bool tutorialActive = false;

    [Header("Settings")]
    public int targetIndex = 2;

    [Header("Thresholds")]
    public double alignInLim = 0.15;
    public double alignOutLim = 0.2;
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
    private int lastClip = -1;
    private double nodeBurnTime;
    private double transferInterceptTime;
    private bool correctTarget;
    private int execPhase;

    void Start()
    {
        Reset();
    }

    void LateUpdate()
    {
        if (!tutorialActive)
        {
            clip = (int)RendezvousTutorialClip.Intro;

            if (continueButton != null)
                continueButton.SetActive(false);

            return;
        }

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

                if (inFinalZone) {
                    if (relSpeed > finalZoneSpeedLim) {
                        if (!pointingDir || activeProgramId != GC_RuntimeState.PROG_RELVEL_RETRO) {
                            return (int)RendezvousTutorialClip.MatchDir;
                        }
                        return (int)RendezvousTutorialClip.MatchBurn;
                    }
                    return (int)RendezvousTutorialClip.Outro;
                }

                if (didClosingBurn) {
                    return (int)RendezvousTutorialClip.RestOfTheOwl;
                }

                if (!pointingDir || activeProgramId != GC_RuntimeState.PROG_DOCK_POINT_PORT) {
                    return (int)RendezvousTutorialClip.TargetDir;
                }

                if (closingSpeedReached) {
                    return (int)RendezvousTutorialClip.FinishBurn;
                }

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
        tutorialActive = false;
    }

    public void Continue()
    {
        if (!tutorialActive) return;

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
    }

    public void Replay()
    {
        if (!tutorialActive) return;

        if (videoController != null) {
            videoController.PlayClip((RendezvousTutorialClip)clip, true);
        }
    }

    public void API_StartTutorial()
    {
        if (simManager != null) {
            scenarioInitializer.ApplyScenarioByIndex(tutorialScenarioIndex, 0.0);
            simManager.SetRequestedWarp(1.0);
        }

        Reset();
        tutorialActive = true;
    }

    public void API_StopTutorial()
    {
        tutorialActive = false;
        Reset();
    }

    public void API_RestartTutorial()
    {
        API_StartTutorial();
    }

    public void API_ReplayTutorial()
    {
        if (!tutorialActive) return;
        Replay();
    }

    public void API_ContinueTutorial()
    {
        if (!tutorialActive) return;
        Continue();
    }

    public bool CanContinueNow()
    {
        switch (clip) {
            case (int)RendezvousTutorialClip.Intro:
            case (int)RendezvousTutorialClip.AlignInfo:
            case (int)RendezvousTutorialClip.TransferInfo:
            case (int)RendezvousTutorialClip.MatchInfo:
                return true;
            default:
                return false;
        }
    }

    public string GetCurrentClipName()
    {
        return tutorialActive ? ((RendezvousTutorialClip)clip).ToString() : "OFF";
    }

    public string GetTutorialStatusText()
    {
        if (!tutorialActive) return "OFF";
        if (clip == (int)RendezvousTutorialClip.Outro) return "COMPLETE";
        return "RUNNING";
    }
}