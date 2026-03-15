using UdonSharp;
using UnityEngine;

public class SimScenarioInitializer : UdonSharpBehaviour
{
    [Header("Core")]
    public EphemerisSystem ephem;

    [Header("Scenario entries")]
    public SimScenarioEntry[] scenarios;

    [Header("Selection")]
    public int selectedScenarioIndex = 0;

    [Header("Debug")]
    public bool log = true;

    public bool ApplySelectedScenario(double t0)
    {
        return ApplyScenarioByIndex(selectedScenarioIndex, t0);
    }

    public bool ApplyScenarioByIndex(int index, double t0)
    {
        if (scenarios == null) return false;
        if (index < 0 || index >= scenarios.Length) return false;

        SimScenarioEntry entry = scenarios[index];
        if (entry == null) return false;

        if (entry.overrideScenarioJd0 && ephem != null)
            ephem.jd0 = entry.scenarioJd0;

        switch (entry.scenarioType)
        {
            case SimScenarioEntry.SCENARIO_DOCKED:
                if (entry.dockedScenario == null)
                {
                    if (log) Debug.Log("[SimScenarioInitializer] Missing docked scenario at index " + index);
                    return false;
                }
                return entry.dockedScenario.InitializeNow();

            case SimScenarioEntry.SCENARIO_NEAR_STATION:
                if (entry.nearStationScenario == null)
                {
                    if (log) Debug.Log("[SimScenarioInitializer] Missing near-station scenario at index " + index);
                    return false;
                }
                return entry.nearStationScenario.InitializeNow();

            case SimScenarioEntry.SCENARIO_CONIC:
            default:
                if (entry.orbitScenario == null)
                {
                    if (log) Debug.Log("[SimScenarioInitializer] Missing orbit scenario at index " + index);
                    return false;
                }

                entry.orbitScenario.t0Seconds = t0;
                entry.orbitScenario.InitializeNow();
                return true;
        }
    }

    public string GetSelectedScenarioName()
    {
        if (scenarios == null) return "";
        if (selectedScenarioIndex < 0 || selectedScenarioIndex >= scenarios.Length) return "";

        SimScenarioEntry entry = scenarios[selectedScenarioIndex];
        if (entry == null) return "";

        return entry.scenarioName;
    }

    public int GetScenarioCount()
    {
        return (scenarios == null) ? 0 : scenarios.Length;
    }

    public string GetScenarioNameByIndex(int index)
    {
        if (scenarios == null) return "";
        if (index < 0 || index >= scenarios.Length) return "";

        SimScenarioEntry entry = scenarios[index];
        if (entry == null) return "";

        return entry.scenarioName;
    }


}