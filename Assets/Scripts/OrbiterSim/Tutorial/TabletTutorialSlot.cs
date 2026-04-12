using UdonSharp;
using UnityEngine;

public class TabletTutorialSlot : UdonSharpBehaviour
{
    [Header("Metadata")]
    public string tutorialName = "Tutorial";
    [TextArea(2, 6)]
    public string tutorialDescription = "Tutorial description.";
    public int scenarioIndex = 0;

    [Header("Tutorial References")]
    public RendezvousTutorial rendezvousTutorial;
    // Future:
    // public DockingTutorial dockingTutorial;
    // public LaunchTutorial launchTutorial;

    public bool IsAssigned()
    {
        if (rendezvousTutorial != null) return true;
        // if (dockingTutorial != null) return true;
        // if (launchTutorial != null) return true;
        return false;
    }

    public bool IsActive()
    {
        if (rendezvousTutorial != null) return rendezvousTutorial.tutorialActive;
        // if (dockingTutorial != null) return dockingTutorial.tutorialActive;
        // if (launchTutorial != null) return launchTutorial.tutorialActive;
        return false;
    }

    public bool CanContinue()
    {
        if (rendezvousTutorial != null) return rendezvousTutorial.CanContinueNow();
        // if (dockingTutorial != null) return dockingTutorial.CanContinueNow();
        // if (launchTutorial != null) return launchTutorial.CanContinueNow();
        return false;
    }

    public string GetStepText()
    {
        if (rendezvousTutorial != null) return rendezvousTutorial.GetCurrentClipName();
        // if (dockingTutorial != null) return dockingTutorial.GetCurrentClipName();
        // if (launchTutorial != null) return launchTutorial.GetCurrentClipName();
        return "OFF";
    }

    public string GetStatusText()
    {
        if (rendezvousTutorial != null) return rendezvousTutorial.GetTutorialStatusText();
        // if (dockingTutorial != null) return dockingTutorial.GetTutorialStatusText();
        // if (launchTutorial != null) return launchTutorial.GetTutorialStatusText();
        return "OFF";
    }

    public void StartTutorial()
    {
        if (rendezvousTutorial != null)
        {
            rendezvousTutorial.tutorialScenarioIndex = scenarioIndex;
            rendezvousTutorial.API_StartTutorial();
            return;
        }

        // if (dockingTutorial != null)
        // {
        //     dockingTutorial.tutorialScenarioIndex = scenarioIndex;
        //     dockingTutorial.API_StartTutorial();
        //     return;
        // }
    }

    public void StopTutorial()
    {
        if (rendezvousTutorial != null)
        {
            rendezvousTutorial.API_StopTutorial();
            return;
        }

        // if (dockingTutorial != null)
        // {
        //     dockingTutorial.API_StopTutorial();
        //     return;
        // }
    }

    public void RestartTutorial()
    {
        if (rendezvousTutorial != null)
        {
            rendezvousTutorial.tutorialScenarioIndex = scenarioIndex;
            rendezvousTutorial.API_RestartTutorial();
            return;
        }

        // if (dockingTutorial != null)
        // {
        //     dockingTutorial.tutorialScenarioIndex = scenarioIndex;
        //     dockingTutorial.API_RestartTutorial();
        //     return;
        // }
    }

    public void ReplayTutorial()
    {
        if (rendezvousTutorial != null)
        {
            rendezvousTutorial.API_ReplayTutorial();
            return;
        }

        // if (dockingTutorial != null)
        // {
        //     dockingTutorial.API_ReplayTutorial();
        //     return;
        // }
    }

    public void ContinueTutorial()
    {
        if (rendezvousTutorial != null)
        {
            rendezvousTutorial.API_ContinueTutorial();
            return;
        }

        // if (dockingTutorial != null)
        // {
        //     dockingTutorial.API_ContinueTutorial();
        //     return;
        // }
    }
}