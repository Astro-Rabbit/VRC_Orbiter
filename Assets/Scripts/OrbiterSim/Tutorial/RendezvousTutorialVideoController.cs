using UdonSharp;
using UnityEngine;

public class RendezvousTutorialVideoController : UdonSharpBehaviour
{
    [Header("Display")]
    public GameObject avatarDisplayRoot;

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

    private int _currentClip = -1;
    private int _pendingClipId = -1;
    private GameObject _pendingClipObject = null;

    private bool _playing = false;
    private float _hideAtTime = -1f;

    void Start()
    {
        DisableAllClipObjects();

        if (avatarDisplayRoot != null) {
            avatarDisplayRoot.SetActive(false);
        }
    }

    void Update()
    {
        if (_playing && _hideAtTime > 0f && Time.time >= _hideAtTime) {
            StopPlayback();
        }
    }

    public void PlayClip(RendezvousTutorialClip clip, bool forceRestart)
    {
        int clipId = (int)clip;

        if (!forceRestart && _currentClip == clipId) {
            return;
        }

        GameObject clipObject = GetClipObject(clip);
        if (clipObject == null) {
            StopPlayback();
            return;
        }

        DisableAllClipObjects();

        if (avatarDisplayRoot != null) {
            avatarDisplayRoot.SetActive(true);
        }

        _pendingClipId = clipId;
        _pendingClipObject = clipObject;

        _currentClip = -1;
        _playing = true;

        float duration = GetClipDuration(clip);
        if (duration > 0f) {
            _hideAtTime = Time.time + duration;
        } else {
            _hideAtTime = -1f;
        }

        SendCustomEventDelayedFrames(nameof(EnablePendingClip), 1);
    }

    public void EnablePendingClip()
    {
        if (_pendingClipObject == null) {
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

        if (avatarDisplayRoot != null) {
            avatarDisplayRoot.SetActive(false);
        }
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
    }

    private GameObject GetClipObject(RendezvousTutorialClip clip)
    {
        switch (clip) {
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
        }

        return null;
    }

    private float GetClipDuration(RendezvousTutorialClip clip)
    {
        switch (clip) {
            case RendezvousTutorialClip.Intro: return introDuration+5;
            case RendezvousTutorialClip.MenuPage: return menuPageDuration+5;
            case RendezvousTutorialClip.TargetPage: return targetPageDuration+5;
            case RendezvousTutorialClip.SelectTarget: return selectTargetDuration+5;
            case RendezvousTutorialClip.AlignPage: return alignPageDuration+5;
            case RendezvousTutorialClip.AlignInfo: return alignInfoDuration+5;
            case RendezvousTutorialClip.AlignNode: return alignNodeDuration+5;
            case RendezvousTutorialClip.NodeAuto: return nodeAutoDuration+5;
            case RendezvousTutorialClip.NodeTime: return nodeTimeDuration+5;
            case RendezvousTutorialClip.NodeExec: return nodeExecDuration+5;
            case RendezvousTutorialClip.TransferPage: return transferPageDuration+5;
            case RendezvousTutorialClip.TransferInfo: return transferInfoDuration+5;
            case RendezvousTutorialClip.TransferCalc: return transferCalcDuration+5;
            case RendezvousTutorialClip.TransferNode: return transferNodeDuration+5;
            case RendezvousTutorialClip.DockPage: return dockPageDuration+5;
            case RendezvousTutorialClip.MatchInfo: return matchInfoDuration+5;
            case RendezvousTutorialClip.MatchTime: return matchTimeDuration+5;
            case RendezvousTutorialClip.MatchDir: return matchDirDuration+5;
            case RendezvousTutorialClip.MatchBurn: return matchBurnDuration+5;
            case RendezvousTutorialClip.FinishBurn: return finishBurnDuration+5;
            case RendezvousTutorialClip.TargetDir: return targetDirDuration+5;
            case RendezvousTutorialClip.TargetBurn: return targetBurnDuration+5;
            case RendezvousTutorialClip.RestOfTheOwl: return restOfTheOwlDuration+5;
            case RendezvousTutorialClip.Outro: return outroDuration+5;
        }

        return 0f;
    }
}