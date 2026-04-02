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

    private int _currentClip = -1;

    void Start()
    {
        DisableAllClipObjects();

        if (avatarDisplayRoot != null) {
            avatarDisplayRoot.SetActive(false);
        }
    }

    public void PlayClip(RendezvousTutorialClip clip, bool forceRestart)
    {
        int clipId = (int)clip;

        if (!forceRestart && _currentClip == clipId) {
            return;
        }

        GameObject clipObject = GetClipObject(clip);

        DisableAllClipObjects();

        if (clipObject == null) {
            _currentClip = -1;

            if (avatarDisplayRoot != null) {
                avatarDisplayRoot.SetActive(false);
            }
            return;
        }

        _currentClip = clipId;

        if (avatarDisplayRoot != null) {
            avatarDisplayRoot.SetActive(true);
        }

        // Re-enable to restart autoplay cleanly.
        clipObject.SetActive(false);
        clipObject.SetActive(true);
    }

    public void StopPlayback()
    {
        _currentClip = -1;
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
}