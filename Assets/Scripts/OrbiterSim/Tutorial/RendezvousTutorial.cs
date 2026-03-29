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
    // }
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

    [Header("Sticky Condition Flags")]
    public bool planeAligned = false;
    public bool onFlyby = false;
    public bool proximity = false;
    public bool pointingDir = false;

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

        if (proximity) {

        }

        if (onFlyby) {

        }

        if (planeAligned) {
            if (!PageOpened(transferPage)) {
                if (PageOpened(menuPage)) {
                    return (int)RendezvousTutorialClip.TransferPage;
                } else {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }
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
        return (int)RendezvousTutorialClip.AlignTime;
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
        correctTarget = navContactsState.selectedStationIndex == targetIndex;

        if (alignPage.hasTarget && correctTarget) {
            double inclination = 180.0 / Math.PI * alignPage.inclination;
            if (inclination < alignInLim) {
                planeAligned = true;
            } else if (inclination > alignOutLim) {
                planeAligned = false;
            }
        }

        if (dockingPage.portSelected && correctTarget) {
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

