using UdonSharp;
using UnityEngine;
using VRC.SDK3.Video.Components;

public class RendezvousTutorialVideoController : UdonSharpBehaviour
{
    [Header("Display")]
    public GameObject avatarDisplayRoot;
    public GameObject Avatar1;
    public GameObject Avatar2;

    [Header("Clip Objects")]
    public GameObject introObject;
    public GameObject menuPageObject;
    public GameObject targetPageObject;
    public GameObject selectTargetObject;
    public GameObject alignPageObject;
    public GameObject alignInfoObject;
    public GameObject alignNodeObject;
    public GameObject nodeAutoObject;
    public GameObject nodeTimeObject;
    public GameObject nodeExecObject;
    public GameObject transferPageObject;
    public GameObject transferInfoObject;
    public GameObject transferCalcObject;
    public GameObject transferNodeObject;
    public GameObject dockPageObject;
    public GameObject matchInfoObject;
    public GameObject matchTimeObject;
    public GameObject matchDirObject;
    public GameObject matchBurnObject;
    public GameObject finishBurnObject;
    public GameObject targetDirObject;
    public GameObject targetBurnObject;
    public GameObject restOfTheOwlObject;
    public GameObject outroObject;

    [Header("Docking Clip Objects")]
    public GameObject dockIntroObject;
    public GameObject dockRHCObject;
    public GameObject dockTHCObject;
    public GameObject dockHUDExplanationObject;
    public GameObject dockHUDApproachObject;
    public GameObject dockOrbitalDriftObject;
    public GameObject dockWarpTo2kmObject;
    public GameObject dockYellowGateObject;
    public GameObject dockPortOpenObject;
    public GameObject dockMFDRotationObject;
    public GameObject dockMFDTranslationObject;
    public GameObject dockFinalApproachObject;
    //public GameObject dockDistanceCalloutObject;
    //public GameObject dockSlowDownObject;
    //public GameObject dockTooSlowObject;
    //public GameObject dockAlignErrObject;
    //public GameObject dockContactObject;
    //public GameObject dockCaptureObject;
    //public GameObject dockHardDockObject;
    public GameObject dockWarpTo2km_2Object;
    public GameObject dockRetract;
    public GameObject dockOutro;

    [Header("Clip Durations (seconds, 0 = stay on until another clip/stop)")]
    public float introDuration = 0f;
    public float menuPageDuration = 0f;
    public float targetPageDuration = 0f;
    public float selectTargetDuration = 0f;
    public float alignPageDuration = 0f;
    public float alignInfoDuration = 0f;
    public float alignNodeDuration = 0f;
    public float nodeAutoDuration = 0f;
    public float nodeTimeDuration = 0f;
    public float nodeExecDuration = 0f;
    public float transferPageDuration = 0f;
    public float transferInfoDuration = 0f;
    public float transferCalcDuration = 0f;
    public float transferNodeDuration = 0f;
    public float dockPageDuration = 0f;
    public float matchInfoDuration = 0f;
    public float matchTimeDuration = 0f;
    public float matchDirDuration = 0f;
    public float matchBurnDuration = 0f;
    public float finishBurnDuration = 0f;
    public float targetDirDuration = 0f;
    public float targetBurnDuration = 0f;
    public float restOfTheOwlDuration = 0f;
    public float outroDuration = 0f;


    [Header("Docking Durations")]
    public float dockIntroDuration = 0f;
    public float dockRHCDuration = 0f;
    public float dockTHCDuration = 0f;
    public float dockHUDExplanationDuration = 0f;
    public float dockHUDApproachDuration = 0f;
    public float dockOrbitalDriftDuration = 0f;
    public float dockWarpTo2kmDuration = 0f;
    public float dockYellowGateDuration = 0f;
    public float dockPortOpenDuration = 0f;
    public float dockMFDRotationDuration = 0f;
    public float dockMFDTranslationDuration = 0f;
    public float dockFinalApproachDuration = 0f;
    //public float dockDistanceCalloutDuration = 0f;
    //public float dockSlowDownDuration = 0f;
    //public float dockTooSlowDuration = 0f;
    //public float dockAlignErrDuration = 0f;
    //public float dockContactDuration = 0f;
    //public float dockCaptureDuration = 0f;
    //public float dockHardDockDuration = 0f;
    public float dockWarpTo2km_2ObjectDuration = 0f;
    public float dockRetractDuration = 0f;
    public float dockOutroDuration = 0f;
    [Header("Minimum time between starting video clips")]
    public float requestInterval = 5.5f;

    private int _currentClip = -1;
    private int _pendingClipId = -1;
    private GameObject _pendingClipObject = null;

    private bool _playing = false;
    private float _hideAtTime = -1f;
    private float _throttleTime = -1f;

    void Start()
    {
        DisableAllClipObjects();

        if (avatarDisplayRoot != null)
        {
            avatarDisplayRoot.SetActive(false);


        }

        _throttleTime = Time.time;
    }

    void Update()
    {
        if (_playing && _hideAtTime > 0f && Time.time >= _hideAtTime)
        {
            StopPlayback();
        }
    }

    public void PlayClip(RendezvousTutorialClip clip, bool forceRestart)
    {
        if (_throttleTime > 0f && Time.time < _throttleTime)
        {
            return;
        }

        int clipId = (int)clip;

        if (!forceRestart && _currentClip == clipId)
        {
            return;
        }

        GameObject clipObject = GetClipObject(clip);
        if (clipObject == null)
        {
            StopPlayback();
            return;
        }

        DisableAllClipObjects();

        if (avatarDisplayRoot != null)
        {
            avatarDisplayRoot.SetActive(true);
            // --- NEW LOGIC START ---
            bool isDocking = IsDockingClip(clip);
            if (Avatar1 != null) Avatar1.SetActive(!isDocking); // On if NOT docking
            if (Avatar2 != null) Avatar2.SetActive(isDocking);  // On if IS docking
                                                                // --- NEW LOGIC END ---
        }

        _pendingClipId = clipId;
        _pendingClipObject = clipObject;

        _currentClip = -1;
        _playing = true;

        float duration = GetClipDuration(clip);
        if (duration > 0f)
        {
            _hideAtTime = Time.time + duration;
        }
        else
        {
            _hideAtTime = -1f;
        }

        SendCustomEventDelayedFrames(nameof(EnablePendingClip), 1);

        _throttleTime = Time.time + requestInterval;
    }

    public void EnablePendingClip()
    {
        if (_pendingClipObject == null)
        {
            StopPlayback();
            return;
        }

        _pendingClipObject.SetActive(true);
        _currentClip = _pendingClipId;
    }

    public void StopPlayback()
    {
        _playing = false;
        _hideAtTime = -1f;

        _currentClip = -1;
        _pendingClipId = -1;
        _pendingClipObject = null;

        DisableAllClipObjects();

        if (avatarDisplayRoot != null)
        {
            avatarDisplayRoot.SetActive(false);
        }

        // Ensure both are off when not playing
        if (Avatar1 != null) Avatar1.SetActive(false);
        if (Avatar2 != null) Avatar2.SetActive(false);
    }

    private void DisableAllClipObjects()
    {
        if (introObject != null) introObject.SetActive(false);
        if (menuPageObject != null) menuPageObject.SetActive(false);
        if (targetPageObject != null) targetPageObject.SetActive(false);
        if (selectTargetObject != null) selectTargetObject.SetActive(false);
        if (alignPageObject != null) alignPageObject.SetActive(false);
        if (alignInfoObject != null) alignInfoObject.SetActive(false);
        if (alignNodeObject != null) alignNodeObject.SetActive(false);
        if (nodeAutoObject != null) nodeAutoObject.SetActive(false);
        if (nodeTimeObject != null) nodeTimeObject.SetActive(false);
        if (nodeExecObject != null) nodeExecObject.SetActive(false);
        if (transferPageObject != null) transferPageObject.SetActive(false);
        if (transferInfoObject != null) transferInfoObject.SetActive(false);
        if (transferCalcObject != null) transferCalcObject.SetActive(false);
        if (transferNodeObject != null) transferNodeObject.SetActive(false);
        if (dockPageObject != null) dockPageObject.SetActive(false);
        if (matchInfoObject != null) matchInfoObject.SetActive(false);
        if (matchTimeObject != null) matchTimeObject.SetActive(false);
        if (matchDirObject != null) matchDirObject.SetActive(false);
        if (matchBurnObject != null) matchBurnObject.SetActive(false);
        if (finishBurnObject != null) finishBurnObject.SetActive(false);
        if (targetDirObject != null) targetDirObject.SetActive(false);
        if (targetBurnObject != null) targetBurnObject.SetActive(false);
        if (restOfTheOwlObject != null) restOfTheOwlObject.SetActive(false);
        if (outroObject != null) outroObject.SetActive(false);
        if (dockIntroObject != null) dockIntroObject.SetActive(false);
        if (dockRHCObject != null) dockRHCObject.SetActive(false);
        if (dockTHCObject != null) dockTHCObject.SetActive(false);
        if (dockHUDExplanationObject != null) dockHUDExplanationObject.SetActive(false);
        if (dockHUDApproachObject != null) dockHUDApproachObject.SetActive(false);
        if (dockOrbitalDriftObject != null) dockOrbitalDriftObject.SetActive(false);
        if (dockWarpTo2kmObject != null) dockWarpTo2kmObject.SetActive(false);
        if (dockYellowGateObject != null) dockYellowGateObject.SetActive(false);
        if (dockPortOpenObject != null) dockPortOpenObject.SetActive(false);
        if (dockMFDRotationObject != null) dockMFDRotationObject.SetActive(false);
        if (dockMFDTranslationObject != null) dockMFDTranslationObject.SetActive(false);
        if (dockFinalApproachObject != null) dockFinalApproachObject.SetActive(false);
        // if (dockDistanceCalloutObject != null) dockDistanceCalloutObject.SetActive(false);
        // if (dockSlowDownObject != null) dockSlowDownObject.SetActive(false);
        // if (dockTooSlowObject != null) dockTooSlowObject.SetActive(false);
        // if (dockAlignErrObject != null) dockAlignErrObject.SetActive(false);
        // if (dockContactObject != null) dockContactObject.SetActive(false);
        // if (dockCaptureObject != null) dockCaptureObject.SetActive(false);
        // if (dockHardDockObject != null) dockHardDockObject.SetActive(false);
        if (dockWarpTo2km_2Object != null) dockWarpTo2km_2Object.SetActive(false);
        if (dockRetract != null) dockRetract.SetActive(false);
        if (dockOutro != null) dockOutro.SetActive(false);
    }
    private bool IsDockingClip(RendezvousTutorialClip clip)
    {
        // Returns true if the clip enum value is Dock_Intro or any value defined after it
        return (int)clip >= (int)RendezvousTutorialClip.Dock_Intro;
    }
    private GameObject GetClipObject(RendezvousTutorialClip clip)
    {
        switch (clip)
        {
            case RendezvousTutorialClip.Intro: return introObject;
            case RendezvousTutorialClip.MenuPage: return menuPageObject;
            case RendezvousTutorialClip.TargetPage: return targetPageObject;
            case RendezvousTutorialClip.SelectTarget: return selectTargetObject;
            case RendezvousTutorialClip.AlignPage: return alignPageObject;
            case RendezvousTutorialClip.AlignInfo: return alignInfoObject;
            case RendezvousTutorialClip.AlignNode: return alignNodeObject;
            case RendezvousTutorialClip.NodeAuto: return nodeAutoObject;
            case RendezvousTutorialClip.NodeTime: return nodeTimeObject;
            case RendezvousTutorialClip.NodeExec: return nodeExecObject;
            case RendezvousTutorialClip.TransferPage: return transferPageObject;
            case RendezvousTutorialClip.TransferInfo: return transferInfoObject;
            case RendezvousTutorialClip.TransferCalc: return transferCalcObject;
            case RendezvousTutorialClip.TransferNode: return transferNodeObject;
            case RendezvousTutorialClip.DockPage: return dockPageObject;
            case RendezvousTutorialClip.MatchInfo: return matchInfoObject;
            case RendezvousTutorialClip.MatchTime: return matchTimeObject;
            case RendezvousTutorialClip.MatchDir: return matchDirObject;
            case RendezvousTutorialClip.MatchBurn: return matchBurnObject;
            case RendezvousTutorialClip.FinishBurn: return finishBurnObject;
            case RendezvousTutorialClip.TargetDir: return targetDirObject;
            case RendezvousTutorialClip.TargetBurn: return targetBurnObject;
            case RendezvousTutorialClip.RestOfTheOwl: return restOfTheOwlObject;
            case RendezvousTutorialClip.Outro: return outroObject;
            case RendezvousTutorialClip.Dock_Intro: return dockIntroObject;
            case RendezvousTutorialClip.Dock_ManeuverRHC: return dockRHCObject;
            case RendezvousTutorialClip.Dock_ManeuverTHC: return dockTHCObject;
            case RendezvousTutorialClip.Dock_HUD_Explanation: return dockHUDExplanationObject;
            case RendezvousTutorialClip.Dock_HUD_Approach: return dockHUDApproachObject;
            case RendezvousTutorialClip.Dock_OrbitalDrift: return dockOrbitalDriftObject;
            case RendezvousTutorialClip.Dock_2km: return dockWarpTo2kmObject;
            case RendezvousTutorialClip.Dock_YellowGate: return dockYellowGateObject;
            //case RendezvousTutorialClip.Dock_PortOpen: return dockPortOpenObject;
            case RendezvousTutorialClip.Dock_MFD_Rotation: return dockMFDRotationObject;
            case RendezvousTutorialClip.Dock_MFD_Translation: return dockMFDTranslationObject;
            case RendezvousTutorialClip.Dock_FinalApproach: return dockFinalApproachObject;
            // case RendezvousTutorialClip.Dock_Callout_Distance: return dockDistanceCalloutObject;
            // case RendezvousTutorialClip.Dock_Callout_SlowDown: return dockSlowDownObject;
            // case RendezvousTutorialClip.Dock_Callout_TooSlow: return dockTooSlowObject;
            // case RendezvousTutorialClip.Dock_Callout_AlignErr: return dockAlignErrObject;
            // case RendezvousTutorialClip.Dock_Contact: return dockContactObject;
            // case RendezvousTutorialClip.Dock_Capture: return dockCaptureObject;
            // case RendezvousTutorialClip.Dock_HardDock: return dockHardDockObject;
            case RendezvousTutorialClip.Dock_WarpTo2km_2: return dockWarpTo2km_2Object;
            case RendezvousTutorialClip.Dock_Retract: return dockRetract;
            case RendezvousTutorialClip.Dock_Outro: return dockOutro;
        }

        return null;
    }


    private float GetClipDuration(RendezvousTutorialClip clip)
    {
        switch (clip)
        {
            case RendezvousTutorialClip.Intro: return introDuration + 5;
            case RendezvousTutorialClip.MenuPage: return menuPageDuration + 5;
            case RendezvousTutorialClip.TargetPage: return targetPageDuration + 5;
            case RendezvousTutorialClip.SelectTarget: return selectTargetDuration + 5;
            case RendezvousTutorialClip.AlignPage: return alignPageDuration + 5;
            case RendezvousTutorialClip.AlignInfo: return alignInfoDuration + 5;
            case RendezvousTutorialClip.AlignNode: return alignNodeDuration + 5;
            case RendezvousTutorialClip.NodeAuto: return nodeAutoDuration + 5;
            case RendezvousTutorialClip.NodeTime: return nodeTimeDuration + 5;
            case RendezvousTutorialClip.NodeExec: return nodeExecDuration + 5;
            case RendezvousTutorialClip.TransferPage: return transferPageDuration + 5;
            case RendezvousTutorialClip.TransferInfo: return transferInfoDuration + 5;
            case RendezvousTutorialClip.TransferCalc: return transferCalcDuration + 5;
            case RendezvousTutorialClip.TransferNode: return transferNodeDuration + 5;
            case RendezvousTutorialClip.DockPage: return dockPageDuration + 5;
            case RendezvousTutorialClip.MatchInfo: return matchInfoDuration + 5;
            case RendezvousTutorialClip.MatchTime: return matchTimeDuration + 5;
            case RendezvousTutorialClip.MatchDir: return matchDirDuration + 5;
            case RendezvousTutorialClip.MatchBurn: return matchBurnDuration + 5;
            case RendezvousTutorialClip.FinishBurn: return finishBurnDuration + 5;
            case RendezvousTutorialClip.TargetDir: return targetDirDuration + 5;
            case RendezvousTutorialClip.TargetBurn: return targetBurnDuration + 5;
            case RendezvousTutorialClip.RestOfTheOwl: return restOfTheOwlDuration + 5;
            case RendezvousTutorialClip.Outro: return outroDuration + 5;
            case RendezvousTutorialClip.Dock_Intro: return dockIntroDuration + 5;
            case RendezvousTutorialClip.Dock_ManeuverRHC: return dockRHCDuration + 5;
            case RendezvousTutorialClip.Dock_ManeuverTHC: return dockTHCDuration + 5;
            case RendezvousTutorialClip.Dock_HUD_Explanation: return dockHUDExplanationDuration + 5;
            case RendezvousTutorialClip.Dock_HUD_Approach: return dockHUDApproachDuration + 5;
            case RendezvousTutorialClip.Dock_OrbitalDrift: return dockOrbitalDriftDuration + 5;
            case RendezvousTutorialClip.Dock_2km: return dockWarpTo2kmDuration + 5;
            case RendezvousTutorialClip.Dock_YellowGate: return dockYellowGateDuration + 5;
            //case RendezvousTutorialClip.Dock_PortOpen: return dockPortOpenDuration + 5;
            case RendezvousTutorialClip.Dock_MFD_Rotation: return dockMFDRotationDuration + 5;
            case RendezvousTutorialClip.Dock_MFD_Translation: return dockMFDTranslationDuration + 5;
            case RendezvousTutorialClip.Dock_FinalApproach: return dockFinalApproachDuration + 5;
            // case RendezvousTutorialClip.Dock_Callout_Distance: return dockDistanceCalloutDuration + 5;
            // case RendezvousTutorialClip.Dock_Callout_SlowDown: return dockSlowDownDuration + 5;
            // case RendezvousTutorialClip.Dock_Callout_TooSlow: return dockTooSlowDuration + 5;
            // case RendezvousTutorialClip.Dock_Callout_AlignErr: return dockAlignErrDuration + 5;
            // case RendezvousTutorialClip.Dock_Contact: return dockContactDuration + 5;
            // case RendezvousTutorialClip.Dock_Capture: return dockCaptureDuration + 5;
            // case RendezvousTutorialClip.Dock_HardDock: return dockHardDockDuration + 5;
            case RendezvousTutorialClip.Dock_WarpTo2km_2: return dockWarpTo2km_2ObjectDuration + 5;
            case RendezvousTutorialClip.Dock_Retract: return dockRetractDuration + 5;
            case RendezvousTutorialClip.Dock_Outro: return dockOutroDuration + 5;

        }

        return 0f;
    }


}