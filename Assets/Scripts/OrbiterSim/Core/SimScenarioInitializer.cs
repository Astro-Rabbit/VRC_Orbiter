using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
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

    [Header("Read-only active scenario")]
    public int activeScenarioIndex = -1;
    public double activeScenarioJd0 = 2460000.5;

    [UdonSynced] private int _activeScenarioIndex = -1;
    [UdonSynced] private double _activeScenarioJd0 = 2460000.5;

    private int _appliedScenarioIndex = -999999;
    private double _appliedScenarioJd0 = -1.0;


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

        _activeScenarioIndex = index;
        activeScenarioIndex = index;

        if (entry.overrideScenarioJd0)
        {
            _activeScenarioJd0 = entry.scenarioJd0;
            activeScenarioJd0 = entry.scenarioJd0;

            if (ephem != null)
                ephem.jd0 = entry.scenarioJd0;
        }
        else if (ephem != null)
        {
            _activeScenarioJd0 = ephem.jd0;
            activeScenarioJd0 = ephem.jd0;
        }

        if (Networking.IsOwner(gameObject))
            RequestSerialization();

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


    public override void OnDeserialization()
    {
        activeScenarioIndex = _activeScenarioIndex;
        activeScenarioJd0 = _activeScenarioJd0;

        if (ephem != null)
            ephem.jd0 = _activeScenarioJd0;

        _appliedScenarioIndex = _activeScenarioIndex;
        _appliedScenarioJd0 = _activeScenarioJd0;
    }


}