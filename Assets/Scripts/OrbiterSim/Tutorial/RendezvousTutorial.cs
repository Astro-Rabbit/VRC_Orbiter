using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;
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
    // New
    Dock_Intro,
    Dock_ManeuverRHC,
    Dock_ManeuverTHC,
    Dock_HUD_Explanation,
    Dock_HUD_Approach,
    Dock_OrbitalDrift,
    Dock_2km,
    Dock_YellowGate,
    //Dock_PortOpen,
    Dock_MFD_Rotation,
    Dock_MFD_Translation,
    Dock_FinalApproach,

    // Callouts / Alerts
    Dock_Callout_Distance, // Contextual based on range
    Dock_Callout_SlowDown,
    Dock_Callout_TooSlow,
    Dock_Callout_AlignErr,

    // Capture
    Dock_Contact,
    Dock_Capture,
    Dock_HardDock,
    Dock_WarpTo2km_2,
    Dock_Outro,
    Dock_Retract
}

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
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

    [UdonSynced] private int clip = (int)RendezvousTutorialClip.Intro;
    [UdonSynced] private double nodeBurnTime;
    [UdonSynced] private double transferInterceptTime;
    [UdonSynced] private int flagSync;
    private bool correctTarget;
    private int execPhase;
    private int previousClip = -1;

    [Header("New Docking References")]
    //public GameObject dockingPort; // Reference to the ship's docking port component
    //public bool isPortOpen = false; // Should be linked to the actual port state

    //public DockingOpsController ops;
    //public DockingController dockingController;
    public DockingRuntimeState dockingRuntimeState;
    public DockingLSO lso;
    // ... (Existing Settings/Thresholds) ...

    [Header("Docking Thresholds")]
    public float dockRotationLim = 2.0f;//this should be small but its NOT the max the docking port can handel. 
    public float dockTranslationLim = 15.0f;//within the yellow gate, about 15 meters
    public float dockApproachSpeedIdeal = 0.25f;
    public float dockApproachSpeedMax = 0.5f;
    public float approachPhase1 = 5000f;
    public float approachPhase2 = 2000f;
    public float approachFinal = 200f;

    // Sticky Flags for Docking
    public bool dockReadyStart = false;
    public bool rhcExplained = false;
    public bool thcExplained = false;
    public bool hudExplained = false;
    public bool approach5kmDone = false;
    public bool at2kmDone = false;
    public bool yellowGateDone = false;
    public bool portOpenedDone = false;
    public bool alignmentDone = false;
    public bool finalApproachReady = false;
    public bool close5meterpersecond = false;
    public bool rotationAligned = false;
    public bool translationAlgined = false;
    public bool continueYellowGate = false;
    public bool hasHardLocked = false;

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
            {
                continueButton.SetActive(false);
            }

            return;
        }

        if (Networking.IsOwner(gameObject))
        {
            UpdateStickyConditions();

            //clip = SelectClip();
            clip = SelectDockingClip();
        }
        bool clipChanged = clip != previousClip;
        if (clipChanged)
        {
            previousClip = clip;
            RequestSerialization();
        }

        if (output != null)
        {
            output.text = ((RendezvousTutorialClip)clip).ToString();
        }

        if (continueButton != null)
        {
            switch (clip)
            {
                case (int)RendezvousTutorialClip.Dock_Intro:
                case (int)RendezvousTutorialClip.Dock_ManeuverRHC:
                case (int)RendezvousTutorialClip.Dock_ManeuverTHC:
                case (int)RendezvousTutorialClip.Dock_HUD_Explanation:
                case (int)RendezvousTutorialClip.Dock_FinalApproach:
                case (int)RendezvousTutorialClip.Dock_YellowGate:
                case (int)RendezvousTutorialClip.Intro:
                case (int)RendezvousTutorialClip.AlignInfo:
                case (int)RendezvousTutorialClip.TransferInfo:
                case (int)RendezvousTutorialClip.MatchInfo:
                case (int)RendezvousTutorialClip.Outro:
                    continueButton.SetActive(true);
                    break;
                default:
                    continueButton.SetActive(false);
                    break;
            }
        }




        if (videoController != null && clipChanged)
        {
            videoController.PlayClip((RendezvousTutorialClip)clip, false);
        }
    }
    public bool RendezvousTutorialPart1End = false;
    private int SelectClip()
    {
        if (!readyStart)
        {
            return (int)RendezvousTutorialClip.Intro;
        }

        bool executing = execPhase != GC_RuntimeState.EXEC_PHASE_NONE && execPhase != GC_RuntimeState.EXEC_PHASE_WAIT;

        if (proximity)
        {
            if (!PageOpened(dockingPage))
            {
                if (PageOpened(menuPage))
                {
                    return (int)RendezvousTutorialClip.DockPage;
                }
                else
                {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }

            if (velocityMatched)
            {
                float mainThrottle = 0f;
                int activeProgramId = -1;
                float relSpeed = 0f;
                double rangeMeters = 0.0;

                if (gc != null && gc.intent != null)
                {
                    mainThrottle = gc.intent.mainThrottle01;
                }
                if (gc != null && gc.runtime != null)
                {
                    activeProgramId = gc.runtime.activeProgramId;
                }
                if (dockingPage != null)
                {
                    relSpeed = (float)dockingPage.speed;
                    rangeMeters = dockingPage.range;
                }

                float desiredClosingSpeed = Mathf.Min((float)(rangeMeters / 1000.0) * closingSpeedPerKm, closingSpeedMax);

                if (inFinalZone)
                {
                    if (relSpeed > finalZoneSpeedLim)
                    {
                        if (!pointingDir || activeProgramId != GC_RuntimeState.PROG_RELVEL_RETRO)
                        {
                            return (int)RendezvousTutorialClip.MatchDir;
                        }
                        return (int)RendezvousTutorialClip.MatchBurn;
                    }
                    
                    return (int)RendezvousTutorialClip.Outro;
                }

                if (didClosingBurn)
                {
                    return (int)RendezvousTutorialClip.RestOfTheOwl;
                }

                if (!pointingDir || activeProgramId != GC_RuntimeState.PROG_DOCK_POINT_PORT)
                {
                    return (int)RendezvousTutorialClip.TargetDir;
                }

                if (closingSpeedReached)
                {
                    return (int)RendezvousTutorialClip.FinishBurn;
                }

                return (int)RendezvousTutorialClip.TargetBurn;
            }

            {
                int activeProgramId = -1;
                if (gc != null && gc.runtime != null)
                {
                    activeProgramId = gc.runtime.activeProgramId;
                }

                if (!pointingDir || activeProgramId != GC_RuntimeState.PROG_RELVEL_RETRO)
                {
                    return (int)RendezvousTutorialClip.MatchDir;
                }
            }
            return (int)RendezvousTutorialClip.MatchBurn;
        }

        if (onFlyby)
        {
            if (!PageOpened(dockingPage))
            {
                if (PageOpened(menuPage))
                {
                    return (int)RendezvousTutorialClip.DockPage;
                }
                else
                {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }

            if (!readyMatch)
            {
                return (int)RendezvousTutorialClip.MatchInfo;
            }
            return (int)RendezvousTutorialClip.MatchTime;
        }

        if (planeAligned)
        {
            if (!PageOpened(transferPage))
            {
                if (PageOpened(menuPage))
                {
                    return (int)RendezvousTutorialClip.TransferPage;
                }
                else
                {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }

            if (!readyTransfer)
            {
                return (int)RendezvousTutorialClip.TransferInfo;
            }

            bool autoValid = false;
            if (transferPage != null && transferPage.solver != null)
            {
                autoValid = transferPage.solver.autoValid;
            }

            if (!autoValid)
            {
                return (int)RendezvousTutorialClip.TransferCalc;
            }
            if (!hasTransferNode)
            {
                return (int)RendezvousTutorialClip.TransferNode;
            }

            bool autoExecuteArmedNodes = false;
            if (gc != null && gc.runtime != null)
            {
                autoExecuteArmedNodes = gc.runtime.autoExecuteArmedNodes;
            }

            if (!autoExecuteArmedNodes)
            {
                return (int)RendezvousTutorialClip.NodeAuto;
            }
            if (!executing)
            {
                return (int)RendezvousTutorialClip.NodeTime;
            }
            return (int)RendezvousTutorialClip.NodeExec;
        }

        if (!correctTarget)
        {
            if (!PageOpened(targetPage))
            {
                if (PageOpened(menuPage))
                {
                    return (int)RendezvousTutorialClip.TargetPage;
                }
                else
                {
                    return (int)RendezvousTutorialClip.MenuPage;
                }
            }

            return (int)RendezvousTutorialClip.SelectTarget;
        }

        if (!PageOpened(alignPage))
        {
            if (PageOpened(menuPage))
            {
                return (int)RendezvousTutorialClip.AlignPage;
            }
            else
            {
                return (int)RendezvousTutorialClip.MenuPage;
            }
        }

        if (!readyAlign)
        {
            return (int)RendezvousTutorialClip.AlignInfo;
        }
        if (!hasAlignNode)
        {
            return (int)RendezvousTutorialClip.AlignNode;
        }

        {
            bool autoExecuteArmedNodes = false;
            if (gc != null && gc.runtime != null)
            {
                autoExecuteArmedNodes = gc.runtime.autoExecuteArmedNodes;
            }

            if (!autoExecuteArmedNodes)
            {
                return (int)RendezvousTutorialClip.NodeAuto;
            }
        }

        if (!executing)
        {
            return (int)RendezvousTutorialClip.NodeTime;
        }
        return (int)RendezvousTutorialClip.NodeExec;


    }
    // private int SelectDockingClip()
    // {
    //     // 1. RUN RENDEZVOUS LOGIC FIRST
    //     // If rendezvous isn't "finished", run the existing logic
    //     if (!velocityMatched || !proximity || !inFinalZone || !RendezvousTutorialPart1End)
    //     {
    //         return SelectClip();
    //     }

    //     // 2. DOCKING LOGIC (Starts once rendezvous "FinishBurn" or "Outro" would have played)
    //     if (!dockReadyStart)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_Intro");
    //         return (int)RendezvousTutorialClip.Dock_Intro;
    //     }
    //     if (!rhcExplained)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_ManeuverRHC");
    //         return (int)RendezvousTutorialClip.Dock_ManeuverRHC;
    //     }
    //     if (!thcExplained)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_ManeuverTHC");
    //         return (int)RendezvousTutorialClip.Dock_ManeuverTHC;
    //     }
    //     if (!hudExplained)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_HUD_Explanation");
    //         return (int)RendezvousTutorialClip.Dock_HUD_Explanation;
    //     }

    //     double range = dockingPage != null ? dockingPage.range : 10000;
    //     float speed = dockingPage != null ? (float)dockingPage.speed : 0f;

    //     // Approach phase
    //     if (range > approachPhase1)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_HUD_Approach");
    //         return (int)RendezvousTutorialClip.Dock_HUD_Approach;
    //     }
    //     if (range < approachPhase2)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_WarpTo2km");
    //         return (int)RendezvousTutorialClip.Dock_2km;
    //     }


    //     // Gate phase
    //     if (range < approachFinal && yellowGateDone)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_YellowGate");
    //         return (int)RendezvousTutorialClip.Dock_YellowGate;
    //     }
    //     if (close5meterpersecond)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_WarpTo2km_new");
    //         return (int)RendezvousTutorialClip.Dock_WarpTo2km_2;
    //     }

    //     // // Port Phase
    //     // if (!isPortOpen)
    //     // {
    //     //     Debug.Log("[RedezvousTutorial]" + "Dock_PortOpen");
    //     //     return (int)RendezvousTutorialClip.Dock_PortOpen;
    //     // }

    //     // Alignment Phase (Rotation)
    //     float rotErr = 100f; // Mock error
    //     if (dockingPage != null) rotErr = Mathf.Max(Mathf.Abs(dockingPage.roll), Mathf.Abs(dockingPage.angleX), Mathf.Abs(dockingPage.angleY));
    //     if (rotErr > dockRotationLim)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_MFD_Rotation");
    //         return (int)RendezvousTutorialClip.Dock_MFD_Rotation;
    //     }

    //     // Alignment Phase (Translation)
    //     float transErr = 100f;
    //     if (dockingPage != null) transErr = Mathf.Max((float)dockingPage.offsetX, (float)dockingPage.offsetY);//consider adding another condition for around 150 to 200 meters range. 
    //     if (transErr > dockTranslationLim)
    //     {
    //         Debug.Log("[RedezvousTutorial]" + "Dock_MFD_Translation");
    //         return (int)RendezvousTutorialClip.Dock_MFD_Translation;
    //     }

    //     // Final Approach
    //     // if (range > 1.0)
    //     // {
    //     //     // Speed Callouts
    //     //     if (speed > dockApproachSpeedMax)
    //     //     {
    //     //         Debug.Log("[RedezvousTutorial]" + "Dock_Callout_SlowDown");
    //     //         return (int)RendezvousTutorialClip.Dock_Callout_SlowDown;
    //     //     }
    //     //     if (speed < 0.05f)
    //     //     {
    //     //         Debug.Log("[RedezvousTutorial]" + "Dock_Callout_TooSlow");
    //     //         return (int)RendezvousTutorialClip.Dock_Callout_TooSlow;
    //     //     }
    //     //     Debug.Log("[RedezvousTutorial]" + "Dock_FinalApproach");
    //     //     return (int)RendezvousTutorialClip.Dock_FinalApproach;
    //     // }

    //     // Capture
    //     // if (range < 0.2)
    //     // {
    //     //     Debug.Log("[RedezvousTutorial]" + "Dock_Contact");
    //     //     return (int)RendezvousTutorialClip.Dock_Contact;//change this to port capture instead of range. think its possible to get within 0.2 m without being docked

    //     // }
    //     Debug.Log("[RedezvousTutorial]" + "Outro");
    //     return (int)RendezvousTutorialClip.Outro;
    // }
    //private int _hardCaptureStableFrames = 0;
    private int SelectDockingClip()
    {
        // 1. GATEKEEPER: RENDEZVOUS COMPLETION
        // If rendezvous isn't "finished", run the existing logic
        //if (!velocityMatched || !proximity || !inFinalZone || !RendezvousTutorialPart1End)
        if (!RendezvousTutorialPart1End)
        {
            return SelectClip();
        }
        //Debug.Log("[RedezvousTutorial]" + " DockingState "+dockingController.state.ToString());
        if (hasHardLocked)//docking controller goes hardcapture for 1 frame when softcapturing... seems to be a bug
        {
            // Debug.Log("[RedezvousTutorial]" + "Dock_Outro");
            return (int)RendezvousTutorialClip.Dock_Outro;
        }
        if (dockingRuntimeState.phase == 1)
        {
            //Debug.Log("[RedezvousTutorial]" + "Dock_Retract");
            return (int)RendezvousTutorialClip.Dock_Retract;
        }

        // if (dockingController.state == DockingState.HardCapture)
        // {
        //     _hardCaptureStableFrames++;
        // }
        // else
        // {
        //     _hardCaptureStableFrames = 0;
        // }

        // // Only trigger Outro if we've been in HardCapture for more than 2 frames
        // if (_hardCaptureStableFrames >= 3)
        // {
        //     Debug.Log("[RedezvousTutorial] HardCapture Stable. Playing Outro.");
        //     return (int)RendezvousTutorialClip.Dock_Outro;
        // }

        // // Soft Capture logic (usually doesn't glitch, but keep an eye on it)
        // if (dockingController.state == DockingState.SoftCapture)
        // {
        //     Debug.Log("[RedezvousTutorial]" + "Dock_Retract");
        //     return (int)RendezvousTutorialClip.Dock_Retract;
        // }
        // 2. UI GATEKEEPER (Following your SelectClip pattern)
        // Don't explain docking if they don't even have the page open!
        // if (dockingPage == null || !PageOpened(dockingPage))
        // {
        //     if (PageOpened(menuPage)) return (int)RendezvousTutorialClip.DockPage;
        //     else return (int)RendezvousTutorialClip.MenuPage;
        // }

        // 3. KNOWLEDGE GATEKEEPERS (One-time explanations)
        // These ensure the player knows HOW to move before we ask them to move.
        if (!dockReadyStart)
        {
            //Debug.Log("[RedezvousTutorial]" + "Dock_Intro");
            return (int)RendezvousTutorialClip.Dock_Intro;
        }
        if (!rhcExplained)
        {
            //Debug.Log("[RedezvousTutorial]" + "Dock_ManeuverRHC");
            return (int)RendezvousTutorialClip.Dock_ManeuverRHC;
        }
        if (!thcExplained)
        {
            //Debug.Log("[RedezvousTutorial]" + "Dock_ManeuverTHC");
            return (int)RendezvousTutorialClip.Dock_ManeuverTHC;
        }
        if (!hudExplained)
        {
            //Debug.Log("[RedezvousTutorial]" + "Dock_HUD_Explanation");
            return (int)RendezvousTutorialClip.Dock_HUD_Explanation;
        }
        // If aligned but moving too slow/fast

        // double range = dockingPage.range;
        //float range = (float)dockingPage.contacts.dockErr_pz_B0;
        Vector3 relPosShipSpace = new Vector3((float)dockingPage.contacts.dockErr_px_B0, (float)dockingPage.contacts.dockErr_py_B0, (float)dockingPage.contacts.dockErr_pz_B0);

        // 2. Get the direction the Target Port is facing (expressed in ship's local space)
        // Note: We use -Vector3.forward because docking ports usually face 'out' 
        // and we want the vector pointing 'away' from the port face.
        Vector3 portFacingDir = dockingPage.contacts.qTargetPortInB0 * Vector3.forward;

        // 3. Dot product gives you the distance along that specific axis
        // This is your "Depth" relative to the port face.
        //float range = Vector3.Dot(relPosShipSpace, -portFacingDir);
        float range = (float)dockingPage.range;

        // 4. DATA SENSORS

        //float speed = (float)dockingPage.speed;
        //float rotErr = Mathf.Max(Mathf.Abs(dockingPage.roll), Mathf.Abs(dockingPage.angleX), Mathf.Abs(dockingPage.angleY));
        //float transErr = Mathf.Max((float)dockingPage.offsetX, (float)dockingPage.offsetY);
        //float transErr = Mathf.Max((float)dockingPage.contacts.dockErr_px_B0, (float)dockingPage.contacts.dockErr_px_B0);
        //Debug.Log("[RedezvousTutorial]" + " x: " + portFacingDir.x.ToString("f1") + " y: " + portFacingDir.y.ToString("f1") + " z: " + portFacingDir.z.ToString("f1"));
        //Debug.Log("[RedezvousTutorial]" + " range: " + range.ToString("f1"));
        // 5. HIERARCHY OF MISSION PROGRESSION (From Finished to Start)


        // // // PHASE: FINAL APPROACH (Inside 10-15 meters)
        // if (range < approachFinal)
        // {

        //     if (!translationAlgined && rotationAligned && continueYellowGate)
        //     {
        //         Debug.Log("[RedezvousTutorial]" + "Dock_MFD_Translation");
        //         return (int)RendezvousTutorialClip.Dock_MFD_Translation;
        //     }
        //     if (!rotationAligned && continueYellowGate)
        //     {
        //         Debug.Log("[RedezvousTutorial]" + "Dock_MFD_Rotation");
        //         return (int)RendezvousTutorialClip.Dock_MFD_Rotation;
        //     }

        //     if (yellowGateDone&&!translationAlgined&&!rotationAligned)
        //     {
        //         Debug.Log("[RedezvousTutorial]" + "Dock_YellowGate");
        //         return (int)RendezvousTutorialClip.Dock_YellowGate;
        //     }


        //     return (int)RendezvousTutorialClip.Dock_FinalApproach;
        // }
        if (range < approachFinal && dockingPage.hasTarget)
        {
            //Debug.Log("[RendezvousTutorial] "+ " range: "+range);
            // 1. Initial Explanation Gate (Requires "Continue" button click)
            if (!continueYellowGate)
            {
                //Debug.Log("[RedezvousTutorial]" + "Dock_YellowGate");
                return (int)RendezvousTutorialClip.Dock_YellowGate;
            }

            // 2. Rotation Phase (Automatic)
            // Plays as long as the ship is not aligned. 
            // The moment 'rotationAligned' becomes true, this block is skipped.
            if (!rotationAligned)
            {
                //Debug.Log("[RedezvousTutorial]" + "Dock_MFD_Rotation");
                return (int)RendezvousTutorialClip.Dock_MFD_Rotation;
            }

            // 3. Translation Phase (Automatic)
            // This only starts once rotationAligned is true.
            // If the player is already translation-aligned, it will skip to step 4.
            if (!translationAlgined)
            {
                //Debug.Log("[RedezvousTutorial]" + "Dock_MFD_Translation");
                return (int)RendezvousTutorialClip.Dock_MFD_Translation;
            }

            // 4. Final Approach instructions
            // This plays only when BOTH rotation and translation are currently aligned.
            //Debug.Log("[RedezvousTutorial]" + "Dock_FinalApproach");
            return (int)RendezvousTutorialClip.Dock_FinalApproach;
        }
        // PHASE: PRECISION ALIGNMENT (The "Zone of Alignment")
        // We only care about rotation/translation once we are relatively close (e.g., < 200m)

        if (range < approachPhase2 && dockingPage.hasTarget)
        {
            //Debug.Log("[RedezvousTutorial]" + "Dock_2km");
            return (int)RendezvousTutorialClip.Dock_2km;

        }
        if (close5meterpersecond)
        {
            //Debug.Log("[RedezvousTutorial]" + "Dock_WarpTo2km_2");
            return (int)RendezvousTutorialClip.Dock_WarpTo2km_2;
        }
        if (range < approachPhase1 && dockingPage.hasTarget)
        {
            //Debug.Log("[RedezvousTutorial]" + "Dock_HUD_Approach");
            return (int)RendezvousTutorialClip.Dock_HUD_Approach;
        }



        // FALLBACK (The "Middle Ground")
        // If we are between approachPhase1 and approachPhase2, keep them approaching.
        return (int)RendezvousTutorialClip.Dock_HUD_Approach;
    }

    private bool PageOpened(MFDPage page)
    {
        if (page == null || mfds == null)
        {
            return false;
        }

        for (int i = 0; i < mfds.Length; i++)
        {
            if (mfds[i] == null) continue;
            if (mfds[i].currentPage == page)
            {
                return true;
            }
        }

        return false;
    }

    void UpdateStickyConditions()
    {
        double time = 0.0;
        if (clock != null)
        {
            time = clock.Now();
        }

        correctTarget = false;
        if (navContactsState != null)
        {
            correctTarget = navContactsState.selectedStationIndex == targetIndex;
        }

        execPhase = GC_RuntimeState.EXEC_PHASE_NONE;
        if (gc != null && gc.runtime != null)
        {
            execPhase = gc.runtime.executorPhase;
        }

        bool execDone =
            execPhase == GC_RuntimeState.EXEC_PHASE_NONE ||
            execPhase == GC_RuntimeState.EXEC_PHASE_WAIT ||
            execPhase == GC_RuntimeState.EXEC_PHASE_POST;

        if (alignPage != null && alignPage.hasTarget && correctTarget && execDone)
        {
            double inclination = 180.0 / Math.PI * alignPage.inclination;
            if (inclination < alignInLim)
            {
                planeAligned = true;
            }
            else if (inclination > alignOutLim)
            {
                planeAligned = false;
            }
        }

        if (hasAlignNode && execDone && time > nodeBurnTime)
        {
            hasAlignNode = false;
        }

        if (hasTransferNode && execDone && time > nodeBurnTime)
        {
            hasTransferNode = false;
            onFlyby = true;
        }
        if (onFlyby && time > transferInterceptTime + interceptTimeLim)
        {
            onFlyby = false;
        }

        if (dockingPage != null && dockingPage.hasTarget && correctTarget && execDone)
        {
            bool wasProximity = proximity;

            if (dockingPage.range < proximityInLim)
            {
                proximity = true;
            }
            else if (dockingPage.range > proximityOutLim)
            {
                proximity = false;
            }

            if (!wasProximity && proximity && !matchWarpDropDone)
            {
                if (simManager != null)
                {
                    simManager.SetRequestedWarp(1.0);
                }
                matchWarpDropDone = true;
            }

            if (dockingPage.speed < velMatchInLim)
            {
                velocityMatched = true;
            }
            else if (dockingPage.speed > velMatchOutLim)
            {
                velocityMatched = false;
            }

            if (dockingPage.range < finalZoneInLim)
            {
                inFinalZone = true;
            }
            else if (dockingPage.range > finalZoneOutLim)
            {
                inFinalZone = false;
            }

            if (velocityMatched && proximity)
            {
                float desiredClosingSpeed = Mathf.Min((float)(dockingPage.range / 1000.0) * closingSpeedPerKm, closingSpeedMax);

                if (!closingSpeedReached && dockingPage.speed >= desiredClosingSpeed - closingSpeedTolerance)
                {
                    closingSpeedReached = true;
                }

                if (closingSpeedReached)
                {
                    float mainThrottle = 0f;
                    if (gc != null && gc.intent != null)
                    {
                        mainThrottle = gc.intent.mainThrottle01;
                    }

                    if (mainThrottle <= 0.01f)
                    {
                        didClosingBurn = true;
                    }
                }
            }

            if (dockingPage == null || !dockingPage.hasTarget) return;
            if (!RendezvousTutorialPart1End)
            {
                return;
            }

            //double range = dockingPage.range;
            Vector3 relPosShipSpace = new Vector3((float)dockingPage.contacts.dockErr_px_B0, (float)dockingPage.contacts.dockErr_py_B0, (float)dockingPage.contacts.dockErr_pz_B0);

            // 2. Get the direction the Target Port is facing (expressed in ship's local space)
            // Note: We use -Vector3.forward because docking ports usually face 'out' 
            // and we want the vector pointing 'away' from the port face.
            Vector3 portFacingDir = dockingPage.contacts.qTargetPortInB0 * Vector3.forward;

            // 3. Dot product gives you the distance along that specific axis
            // This is your "Depth" relative to the port face.
            //float range = Vector3.Dot(relPosShipSpace, -portFacingDir);
            float range = (float)dockingPage.range;
            Quaternion qTarget = dockingPage.contacts.qTargetPortInB0;
            Vector3 portRight = qTarget * Vector3.right;
            Vector3 portUp = qTarget * Vector3.up;

            // 2. Use Dot Products to project the relative position onto those axes
            // This gives you the displacement in meters relative to the center of the port.
            float offsetX = Vector3.Dot(relPosShipSpace, portRight);
            float offsetY = Vector3.Dot(relPosShipSpace, portUp);

            // 3. (Optional) For the MFD crosshair, you usually want the absolute error 
            // to check if the player is "centered" enough
            float transErr = Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY));
            float speed = (float)dockingPage.speed;

            // 1. Proximity Gates (Sticky once reached)
            if (range < approachPhase1) approach5kmDone = true;
            if (range < approachPhase2) at2kmDone = true;
            if (range < approachFinal) yellowGateDone = true;

            // 2. Port Status
            // Replace 'isPortOpen' with the actual boolean from your Docking Port script
            //if (isPortOpen) portOpenedDone = true;
            // if (ops.portState == 2)
            // {
            //     portOpenedDone = true;
            //     isPortOpen = true;
            // }

            // 3. Alignment (Using the logic we refined)
            float normalizedRoll = Mathf.Atan2(Mathf.Sin(dockingPage.roll), Mathf.Cos(dockingPage.roll));
            float rotErr = Mathf.Max(Mathf.Abs(normalizedRoll), Mathf.Abs(dockingPage.angleX), Mathf.Abs(dockingPage.angleY));
            //float transErr = Mathf.Max((float)dockingPage.offsetX, (float)dockingPage.offsetY);
            //float transErr = Mathf.Max(portFacingDir.x, portFacingDir.y);
            //Debug.Log("[RedezvousTutorial]" + " transErr: " + transErr);
            //Debug.Log("[RedezvousTutorial]" + " roll: " + dockingPage.roll + " x: " + dockingPage.angleX + " y: " + dockingPage.angleY);
            //Debug.Log("[RedezvousTutorial]" + " offsetX: " + (float)dockingPage.offsetX + " offsetY: " + (float)dockingPage.offsetY);
            // if (rotErr < dockRotationLim && transErr < dockTranslationLim)
            // {
            //     alignmentDone = true;
            // }
            // else if (rotErr > (dockRotationLim * 1.5f) || transErr > (dockTranslationLim * 1.5f))
            // {
            //     // Optional: add a small buffer so it doesn't flicker
            //     alignmentDone = false;
            // }
            // Debug.Log("[RendezvousTutorial]" + "Docking Closure: " + dockingPage.closure);
            //if (dockingPage.closure <= -5.0f)//think it needs another check in here so it doesn't stick early
            if (dockingPage.speed >= 5.0f)
            {
                close5meterpersecond = true;
            }
            if (!yellowGateDone)
            {
                return;
            }

            if (rotErr < dockRotationLim && range < approachFinal)
            {
                rotationAligned = true;
            }
            if (transErr < dockTranslationLim && range < approachFinal && rotationAligned)
            {
                translationAlgined = true;
            }
            if (dockingRuntimeState.phase == 2)
            {
                hasHardLocked = true;
            }

            // // 4. Capture Check
            // if (range < 0.2 && speed < 0.15f && isPortOpen)
            // {
            //     // Logic for hard-dock or success state
            // }
            //why do we need this if there is nothing inside?
        }

        float dirErr = 999999f;
        if (pd != null)
        {
            dirErr = (float)(180.0 / Math.PI) * pd.attErr_B.magnitude;
        }

        if (dirErr < dirInLim)
        {
            pointingDir = true;
        }
        else if (dirErr > dirOutLim)
        {
            pointingDir = false;
        }
    }

    [NetworkCallable]
    public void OnAlignNodeCreate(double time)
    {
        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnAlignNodeCreate), time);
            return;
        }
        if (!tutorialActive) return;

        hasAlignNode = true;
        nodeBurnTime = time;
        RequestSerialization();
    }

    [NetworkCallable]
    public void OnTransferNodeCreate(double time, double interceptTime)
    {
        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnTransferNodeCreate), time, interceptTime);
            return;
        }
        if (!tutorialActive) return;

        hasTransferNode = true;
        nodeBurnTime = time;
        transferInterceptTime = interceptTime;
        RequestSerialization();
    }

    [NetworkCallable]
    public void Reset()
    {
        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Reset));
            return;
        }
        if (!Networking.IsOwner(gameObject)) return;

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

        //docking flags
        dockReadyStart = false;
        rhcExplained = false;
        thcExplained = false;
        hudExplained = false;
        approach5kmDone = false;
        at2kmDone = false;
        yellowGateDone = false;
        portOpenedDone = false;
        alignmentDone = false;
        //isPortOpen = false;
        finalApproachReady = false;

        RendezvousTutorialPart1End = false;

        close5meterpersecond = false;
        rotationAligned = false;
        translationAlgined = false;
        continueYellowGate = false;
        hasHardLocked = false;

        //_hardCaptureStableFrames = 0;

        clip = (int)RendezvousTutorialClip.Intro;

        if (videoController != null)
        {
            videoController.StopPlayback();
        }
        tutorialActive = false;
    }

    [NetworkCallable]
    public void Continue()
    {
        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Continue));
            return;
        }
        if (!tutorialActive) return;

        switch (clip)
        {
            case (int)RendezvousTutorialClip.Dock_Intro:
                
                dockReadyStart = true;
                break;
            case (int)RendezvousTutorialClip.Dock_ManeuverRHC:
                rhcExplained = true;
                break;
            case (int)RendezvousTutorialClip.Dock_ManeuverTHC:
                thcExplained = true;
                break;
            case (int)RendezvousTutorialClip.Dock_HUD_Explanation:
                hudExplained = true;
                break;
            case (int)RendezvousTutorialClip.Dock_YellowGate:
                continueYellowGate = true;
                break;
            case (int)RendezvousTutorialClip.Dock_FinalApproach:
                finalApproachReady = true;
                lso.startLSO = true;
                break;
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
            case (int)RendezvousTutorialClip.Outro:
                RendezvousTutorialPart1End = true;
                break;
            default:
                Debug.Log("WARNING: Tutorial next button pressed during invalid clip");
                break;
        }
    }

    [NetworkCallable]
    public void Replay()
    {

        if (!tutorialActive) return;

        if (videoController != null)
        {
            videoController.PlayClip((RendezvousTutorialClip)clip, true);
        }
    }

    [NetworkCallable]
    public void StartTutorial()
    {
        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Replay));
            return;
        }

        if (simManager != null)
        {
            scenarioInitializer.ApplyScenarioByIndex(tutorialScenarioIndex, 0.0);
            simManager.SetRequestedWarp(1.0);
        }

        Reset();
        tutorialActive = true;
    }
    [NetworkCallable]
    public void StartDockingTutorial()
    {
        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Replay));
            return;
        }

        if (simManager != null)
        {
            scenarioInitializer.ApplyScenarioByIndex(10, 0.0);//initialize the docking scenario, which is 10 in this case. 7km away with some drifting velocity
            simManager.SetRequestedWarp(1.0);
        }

        Reset();
        //Setting the state of the tutorial.
        closingSpeedReached = true;
        didClosingBurn = true;
        inFinalZone = true;


        planeAligned = true;
        onFlyby = true;
        proximity = true;
        pointingDir = true;
        velocityMatched = true;

        hasAlignNode = true;
        hasTransferNode = true;

        readyStart = true;
        readyAlign = true;
        readyTransfer = true;
        readyMatch = true;
        matchWarpDropDone = true;

        velocityMatched = true;
        proximity = true;
        inFinalZone = true;
        RendezvousTutorialPart1End = true;

        tutorialActive = true;
    }

    [NetworkCallable]
    public void StopTutorial()
    {
        if (!Networking.IsOwner(gameObject)) return;
        tutorialActive = false;
        Reset();
    }

    public void API_StartTutorial()
    {
        StartTutorial();
    }

    public void API_StartDockingTutorial()
    {
        StartDockingTutorial();
    }

    public void API_StopTutorial()
    {
        StopTutorial();
    }

    public void API_RestartTutorial()
    {
        API_StartTutorial();
    }

    public void API_ReplayTutorial()
    {
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Replay));
    }

    public void API_ContinueTutorial()
    {
        Continue();
    }

    public bool CanContinueNow()
    {
        switch (clip)
        {
            case (int)RendezvousTutorialClip.Dock_Intro:
            case (int)RendezvousTutorialClip.Dock_ManeuverRHC:
            case (int)RendezvousTutorialClip.Dock_ManeuverTHC:
            case (int)RendezvousTutorialClip.Dock_HUD_Explanation:
            case (int)RendezvousTutorialClip.Dock_YellowGate:
            case (int)RendezvousTutorialClip.Dock_FinalApproach:
            case (int)RendezvousTutorialClip.Intro:
            case (int)RendezvousTutorialClip.AlignInfo:
            case (int)RendezvousTutorialClip.TransferInfo:
            case (int)RendezvousTutorialClip.MatchInfo:
            case (int)RendezvousTutorialClip.Outro:
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

    public override void OnPreSerialization()
    {
        flagSync = 0;
        flagSync |= (tutorialActive ? 1 : 0) << 0;
        flagSync |= (closingSpeedReached ? 1 : 0) << 1;
        flagSync |= (didClosingBurn ? 1 : 0) << 2;
        flagSync |= (inFinalZone ? 1 : 0) << 3;

        flagSync |= (planeAligned ? 1 : 0) << 4;
        flagSync |= (onFlyby ? 1 : 0) << 5;
        flagSync |= (proximity ? 1 : 0) << 6;
        flagSync |= (pointingDir ? 1 : 0) << 7;
        flagSync |= (velocityMatched ? 1 : 0) << 8;

        flagSync |= (hasAlignNode ? 1 : 0) << 9;
        flagSync |= (hasTransferNode ? 1 : 0) << 10;

        flagSync |= (readyStart ? 1 : 0) << 11;
        flagSync |= (readyAlign ? 1 : 0) << 12;
        flagSync |= (readyTransfer ? 1 : 0) << 13;
        flagSync |= (readyMatch ? 1 : 0) << 14;
        flagSync |= (matchWarpDropDone ? 1 : 0) << 15;
        //docking sync
        flagSync |= (dockReadyStart ? 1 : 0) << 16;
        flagSync |= (rhcExplained ? 1 : 0) << 17;
        flagSync |= (thcExplained ? 1 : 0) << 18;
        flagSync |= (hudExplained ? 1 : 0) << 19;
        flagSync |= (approach5kmDone ? 1 : 0) << 20;
        flagSync |= (at2kmDone ? 1 : 0) << 21;
        flagSync |= (yellowGateDone ? 1 : 0) << 22;
        flagSync |= (portOpenedDone ? 1 : 0) << 23;
        flagSync |= (alignmentDone ? 1 : 0) << 24;

        flagSync |= (continueYellowGate ? 1 : 0) << 25;
        flagSync |= (rotationAligned ? 1 : 0) << 26;
        flagSync |= (translationAlgined ? 1 : 0) << 27;
        flagSync |= (hasHardLocked ? 1 : 0) << 28;
        flagSync |= (close5meterpersecond ? 1 : 0) << 29;
    }


    public override void OnDeserialization()
    {
        tutorialActive = ((flagSync >> 0) & 1) == 1;
        closingSpeedReached = ((flagSync >> 1) & 1) == 1;
        didClosingBurn = ((flagSync >> 2) & 1) == 1;
        inFinalZone = ((flagSync >> 3) & 1) == 1;

        planeAligned = ((flagSync >> 4) & 1) == 1;
        onFlyby = ((flagSync >> 5) & 1) == 1;
        proximity = ((flagSync >> 6) & 1) == 1;
        pointingDir = ((flagSync >> 7) & 1) == 1;
        velocityMatched = ((flagSync >> 8) & 1) == 1;

        hasAlignNode = ((flagSync >> 9) & 1) == 1;
        hasTransferNode = ((flagSync >> 10) & 1) == 1;

        readyStart = ((flagSync >> 11) & 1) == 1;
        readyAlign = ((flagSync >> 12) & 1) == 1;
        readyTransfer = ((flagSync >> 13) & 1) == 1;
        readyMatch = ((flagSync >> 14) & 1) == 1;
        matchWarpDropDone = ((flagSync >> 15) & 1) == 1;

        //docking flags
        dockReadyStart = ((flagSync >> 16) & 1) == 1;
        rhcExplained = ((flagSync >> 17) & 1) == 1;
        thcExplained = ((flagSync >> 18) & 1) == 1;
        hudExplained = ((flagSync >> 19) & 1) == 1;
        approach5kmDone = ((flagSync >> 20) & 1) == 1;
        at2kmDone = ((flagSync >> 21) & 1) == 1;
        yellowGateDone = ((flagSync >> 22) & 1) == 1;
        portOpenedDone = ((flagSync >> 23) & 1) == 1;
        alignmentDone = ((flagSync >> 24) & 1) == 1;

        continueYellowGate = ((flagSync >> 25) & 1) == 1;
        rotationAligned = ((flagSync >> 26) & 1) == 1;
        translationAlgined = ((flagSync >> 27) & 1) == 1;
        hasHardLocked = ((flagSync >> 28) & 1) == 1;
        close5meterpersecond = ((flagSync >> 29) & 1) == 1;
    }
}