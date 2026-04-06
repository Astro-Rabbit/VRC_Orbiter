using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SimScenarioInitializer : UdonSharpBehaviour
{
    [Header("Core")]
    public EphemerisSystem ephem;
    public SimManager simManager;
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

    public bool ApplyScenarioByIndex(int index, double t0)
    {
        if (scenarios == null) return false;
        if (index < 0 || index >= scenarios.Length) return false;

        SimScenarioEntry entry = scenarios[index];
        if (entry == null) return false;


        if (simManager != null)
            simManager.AbortHandoffForAuthoritativeReset();


        _activeScenarioIndex = index;
        activeScenarioIndex = index;

        double resolvedJd0 = 2460000.5;
        bool haveResolvedJd0 = false;

        if (entry.overrideScenarioJd0)
        {
            resolvedJd0 = entry.scenarioJd0;
            haveResolvedJd0 = true;
        }
        else if (entry.scenarioType == SimScenarioEntry.SCENARIO_CONIC && entry.tleOrbitScenario != null)
        {
            double tleJd0;
            if (entry.tleOrbitScenario.TryGetScenarioJd0(out tleJd0))
            {
                resolvedJd0 = tleJd0;
                haveResolvedJd0 = true;
            }
        }
        else if (ephem != null)
        {
            resolvedJd0 = ephem.jd0;
            haveResolvedJd0 = true;
        }

        if (haveResolvedJd0)
        {
            _activeScenarioJd0 = resolvedJd0;
            activeScenarioJd0 = resolvedJd0;

            if (ephem != null)
                ephem.jd0 = resolvedJd0;
        }

        if (Networking.IsOwner(gameObject))
            RequestSerialization();

        switch (entry.scenarioType)
        {
            case SimScenarioEntry.SCENARIO_DOCKED:
                if (entry.dockedScenario == null) return false;
                return entry.dockedScenario.InitializeNow();

            case SimScenarioEntry.SCENARIO_NEAR_STATION:
                if (entry.nearStationScenario == null) return false;
                return entry.nearStationScenario.InitializeNow();

            case SimScenarioEntry.SCENARIO_CONIC:
            default:
                if (entry.tleOrbitScenario != null)
                {
                    entry.tleOrbitScenario.t0Seconds = t0;
                    return entry.tleOrbitScenario.InitializeNow();
                }

                if (entry.orbitScenario != null)
                {
                    entry.orbitScenario.t0Seconds = t0;
                    return entry.orbitScenario.InitializeNow();
                }

                return false;
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