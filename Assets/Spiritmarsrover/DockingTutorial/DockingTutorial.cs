using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
public enum DockingTutorialClip
{
    Intro,              // Welcome
    Maneuver,           // RHC/THC Explanation
    HUDSetup,           // Switch to DOCK mode
    HUDManeuvering,     // 5m/s approach
    OrbitalDrift,       // Explanation of drift
    Intermediate,       // 2km / Gates
    AlignPhilosophy,    // Stewart Platform / Face Parallel
    MFDAlignment,       // White X and Roll
    TranslationAlign,   // Green Cross / 70m park
    FinalApproach,      // The "Groove" / LSO Start
    Overshoot,          // Passed the port
    WaveOff,            // Safety Abort (Too fast)
    Capture             // Successful docking
}
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DockingTutorial : UdonSharpBehaviour
{
    [Header("References")]
    public MFD[] mfds;
    public MFDDockingPage dockingPage;
    public CraftStateModel craft;
    public SimManager simManager;
    public SimScenarioInitializer scenarioInitializer;
    public GuidanceNavContactsState contacts;
    public HudDriver_Colimated hudDriver;

    [Header("Settings")]
    public int tutorialScenarioIndex = 7;
    public float approachSpeedTarget = 5.0f;
    public float finalApproachSpeed = 0.25f;

    [Header("Thresholds")]
    public float distYellowGate = 200f;
    public float distFinalStart = 70f;
    public float alignAngleLimit = 5.0f; // degrees
    public float translationLimit = 0.5f; // meters
    public float waveOffSpeed = 1.2f;

    [Header("State")]
    public bool tutorialActive = false;
    private bool readyStart = false;
    private bool readyManeuver = false;
    private bool driftNoteDone = false;

    [UdonSynced] private int clip = (int)DockingTutorialClip.Intro;
    private int previousClip = -1;

    public int GetCurrentClip() => clip;

    public DockingTutorialVideoController videoController;

    void LateUpdate()
    {
        if (!tutorialActive) return;

        if (Networking.IsOwner(gameObject))
        {
            clip = (int)SelectClip();
            if (clip != previousClip)
            {
                previousClip = clip;
                RequestSerialization();
                if (videoController != null)
                    videoController.PlayClip((DockingTutorialClip)clip, false);
            }
        }
    }

    private DockingTutorialClip SelectClip()
    {
        // 1. Basic Flow
        if (!readyStart) return DockingTutorialClip.Intro;
        if (!readyManeuver) return DockingTutorialClip.Maneuver;

        // 2. HUD Check (Mode 4 is Docking)
        if (hudDriver == null || hudDriver.hudMode != 4) return DockingTutorialClip.HUDSetup;

        // 3. Target Check
        if (contacts == null || !contacts.dockValid0) return DockingTutorialClip.HUDManeuvering;

        // Data extraction
        float range = (float)contacts.dockErr_pz_B0; // Z is range to port in docking mode
        float speed = (float)dockingPage.speed;
        float closure = (float)dockingPage.closure;

        // Fail Case: Overshoot
        if (range < -2.0f) return DockingTutorialClip.Overshoot;

        // 4. Initial Approach (Far)
        if (range > 1500)
        {
            if (speed < approachSpeedTarget - 1.0f) return DockingTutorialClip.HUDManeuvering;
            if (!driftNoteDone) return DockingTutorialClip.OrbitalDrift;
            return DockingTutorialClip.Intermediate;
        }

        // 5. Preparation at Gate (200m)
        if (range > distFinalStart)
        {
            // Ensure they have the MFD page open
            if (!IsMFDOnDockingPage()) return DockingTutorialClip.AlignPhilosophy;

            // Check Angular Alignment (White X / Roll)
            if (!IsRotationAligned()) return DockingTutorialClip.MFDAlignment;

            // Check Translation (Green Cross)
            if (!IsTranslationAligned()) return DockingTutorialClip.TranslationAlign;

            return DockingTutorialClip.FinalApproach;
        }

        // 6. Final Approach / LSO Active
        if (range <= distFinalStart && range > 0.3f)
        {
            if (closure > waveOffSpeed) return DockingTutorialClip.WaveOff;
            return DockingTutorialClip.FinalApproach;
        }

        // 7. Success
        if (range <= 0.3f) return DockingTutorialClip.Capture;

        return DockingTutorialClip.Intro;
    }

    private bool IsMFDOnDockingPage()
    {
        foreach (MFD mfd in mfds)
        {
            if (mfd != null && mfd.currentPage == dockingPage) return true;
        }
        return false;
    }

    private bool IsRotationAligned()
    {
        // angleX/Y are normalized 0-1 based on a 20 degree scale in MFDDockingPage
        float pointingErr = Mathf.Sqrt(dockingPage.angleX * dockingPage.angleX + dockingPage.angleY * dockingPage.angleY) * 20f;
        float rollErr = Mathf.Abs(Mathf.DeltaAngle(dockingPage.roll * Mathf.Rad2Deg, 0f));
        return pointingErr < alignAngleLimit && rollErr < alignAngleLimit;
    }

    private bool IsTranslationAligned()
    {
        // Docking error px/py are the lateral offsets in meters
        float offX = (float)contacts.dockErr_px_B0;
        float offY = (float)contacts.dockErr_py_B0;
        return (Mathf.Sqrt(offX * offX + offY * offY) < translationLimit);
    }

    public void API_Continue()
    {
        if (!Networking.IsOwner(gameObject)) { SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(API_Continue)); return; }

        if (clip == (int)DockingTutorialClip.Intro) readyStart = true;
        else if (clip == (int)DockingTutorialClip.Maneuver) readyManeuver = true;
        else if (clip == (int)DockingTutorialClip.OrbitalDrift) driftNoteDone = true;
    }

    public void API_StartTutorial()
    {
        if (!Networking.IsOwner(gameObject)) { SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(API_StartTutorial)); return; }
        tutorialActive = true;
        scenarioInitializer.ApplyScenarioByIndex(tutorialScenarioIndex, 0);
        RequestSerialization();
    }
    // Add these inside the DockingTutorial class
    public void API_StopTutorial()
    {
        if (!Networking.IsOwner(gameObject)) { SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(API_StopTutorial)); return; }
        tutorialActive = false;
        RequestSerialization();
    }

    public void API_RestartTutorial() => API_StartTutorial();

    public void API_ReplayTutorial() => API_Continue(); // Or logic to play last clip

    public bool CanContinueNow()
    {
        // Clicks "Continue" only during the dialogue-heavy parts
        return (clip == (int)DockingTutorialClip.Intro ||
                clip == (int)DockingTutorialClip.Maneuver ||
                clip == (int)DockingTutorialClip.OrbitalDrift);
    }

    public string GetCurrentClipName()
    {
        // Returns the name of the Enum as a string (e.g., "FinalApproach")
        return ((DockingTutorialClip)clip).ToString();
    }

    public string GetTutorialStatusText()
    {
        if (!tutorialActive) return "READY";
        float range = (float)contacts.dockErr_pz_B0;
        return range > 1000 ? "APPROACH" : "FINAL";
    }
}