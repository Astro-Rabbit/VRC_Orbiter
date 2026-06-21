using UdonSharp;
using UnityEngine;

public class TabletDockingTutorialSlot : UdonSharpBehaviour
{
    [Header("Metadata")]
    public string tutorialName = "Tutorial";
    [TextArea(2, 6)]
    public string tutorialDescription = "Tutorial description.";
    public int scenarioIndex = 0;

    [Header("Tutorial References")]
    public RendezvousTutorial rendezvousTutorial;
    public DockingTutorial dockingTutorial;

    public bool IsAssigned()
    {
        return rendezvousTutorial != null || dockingTutorial != null;
    }

    public bool IsActive()
    {
        if (rendezvousTutorial != null) return rendezvousTutorial.tutorialActive;
        if (dockingTutorial != null) return dockingTutorial.tutorialActive;
        return false;
    }

    public bool CanContinue()
    {
        if (rendezvousTutorial != null) return rendezvousTutorial.CanContinueNow();
        if (dockingTutorial != null) return dockingTutorial.CanContinueNow();
        return false;
    }

    public string GetStepText()
    {
        if (rendezvousTutorial != null) return rendezvousTutorial.GetCurrentClipName();
        if (dockingTutorial != null) return dockingTutorial.GetCurrentClipName();
        return "OFF";
    }

    public string GetStatusText()
    {
        if (rendezvousTutorial != null) return rendezvousTutorial.GetTutorialStatusText();
        if (dockingTutorial != null) return dockingTutorial.GetTutorialStatusText();
        return "OFF";
    }

    public void StartTutorial()
    {
        if (rendezvousTutorial != null)
        {
            rendezvousTutorial.tutorialScenarioIndex = scenarioIndex;
            rendezvousTutorial.API_StartTutorial();
        }
        else if (dockingTutorial != null)
        {
            dockingTutorial.tutorialScenarioIndex = scenarioIndex;
            dockingTutorial.API_StartTutorial();
        }
    }

    public void StopTutorial()
    {
        if (rendezvousTutorial != null) rendezvousTutorial.API_StopTutorial();
        else if (dockingTutorial != null) dockingTutorial.API_StopTutorial();
    }

    public void RestartTutorial()
    {
        if (rendezvousTutorial != null)
        {
            rendezvousTutorial.tutorialScenarioIndex = scenarioIndex;
            rendezvousTutorial.API_RestartTutorial();
        }
        else if (dockingTutorial != null)
        {
            dockingTutorial.tutorialScenarioIndex = scenarioIndex;
            dockingTutorial.API_RestartTutorial();
        }
    }

    public void ReplayTutorial()
    {
        if (rendezvousTutorial != null) rendezvousTutorial.API_ReplayTutorial();
        else if (dockingTutorial != null) dockingTutorial.API_ReplayTutorial();
    }

    public void ContinueTutorial()
    {
        // Note: DockingTutorial uses API_Continue internally
        if (rendezvousTutorial != null) rendezvousTutorial.API_ContinueTutorial();
        else if (dockingTutorial != null) dockingTutorial.API_Continue();
    }
}