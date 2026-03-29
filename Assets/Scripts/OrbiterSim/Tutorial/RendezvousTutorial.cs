using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public enum RendezvousTutorialClip
{
    // {
    Intro,
    MenuPage,
    TargetPage,
    SelectTarget,
    AlignPage,
    AlignInfo,
    AlignNode,
    NodeAuto,
    AlignTime,
    AlignExec,
    TransferPage,
    TransferInfo,
    TransferCalc,
    TransferNode,
    TransferTime,
    TransferExec,
    DockPage,
    MatchInfo,
    MatchTime,
    // }
    MatchDir,
    MatchBurn,
    FinishBurn,
    TargetDir,
    TargetBurn,
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
    public GC_Core gc;
    public TMP_Text output;

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
    public double velMatchInLim = 10.0;
    public double velMatchOutLim = 20.0;

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

    private int clip = (int)RendezvousTutorialClip.Intro;
    private double nodeBurnTime;
    private double transferInterceptTime;
    private bool correctTarget;
    private int execPhase;

    void Start()
    {
        Reset();
    }

    void Update()
    {
        UpdateStickyConditions();

        clip = SelectClip();
        output.text = ((RendezvousTutorialClip)clip).ToString();
    }

    private int SelectClip()
    {
        if (!readyStart) {
            return (int)RendezvousTutorialClip.Intro;
        }

        bool executing = execPhase != GC_RuntimeState.EXEC_PHASE_NONE && execPhase != GC_RuntimeState.EXEC_PHASE_WAIT;

        if (proximity) {

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
            if (!transferPage.solver.autoValid) {
                return (int)RendezvousTutorialClip.TransferCalc;
            }
            if (!hasTransferNode) {
                return (int)RendezvousTutorialClip.TransferNode;
            }
            if (!gc.runtime.autoExecuteArmedNodes) {
                return (int)RendezvousTutorialClip.NodeAuto;
            }
            if (!executing) {
                return (int)RendezvousTutorialClip.TransferTime;
            }
            return (int)RendezvousTutorialClip.TransferExec;
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
        if (!gc.runtime.autoExecuteArmedNodes) {
            return (int)RendezvousTutorialClip.NodeAuto;
        }
        if (!executing) {
            return (int)RendezvousTutorialClip.AlignTime;
        }
        return (int)RendezvousTutorialClip.AlignExec;
    }

    private bool PageOpened(MFDPage page)
    {
        for (int i = 0; i < mfds.Length; i++) {
            if (mfds[i].currentPage == page) {
                return true;
            }
        }

        return false;
    }

    // Conditions based on continuous variables that need hysteresis on their threshold are updated here
    void UpdateStickyConditions()
    {
        double time = clock.simTime;
        correctTarget = navContactsState.selectedStationIndex == targetIndex;
        execPhase = gc.runtime.executorPhase;
        bool execDone = execPhase == GC_RuntimeState.EXEC_PHASE_NONE || execPhase == GC_RuntimeState.EXEC_PHASE_POST;

        if (alignPage.hasTarget && correctTarget && execDone) {
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

        if (dockingPage.portSelected && correctTarget && execDone) {
            if (dockingPage.range < proximityInLim) {
                proximity = true;
            } else if (dockingPage.range > proximityOutLim) {
                proximity = false;
            }
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
    }

    public void Next()
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
    }

    public void Replay()
    {
    }
}

