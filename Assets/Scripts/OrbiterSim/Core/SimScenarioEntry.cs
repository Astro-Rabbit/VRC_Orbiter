using UdonSharp;
using UnityEngine;

public class SimScenarioEntry : UdonSharpBehaviour
{
    public const byte SCENARIO_CONIC = 0;
    public const byte SCENARIO_NEAR_STATION = 1;
    public const byte SCENARIO_DOCKED = 2;
    public const byte SCENARIO_SAVED_STATE = 3; // future

    [Header("Meta")]
    public string scenarioName = "Scenario";
    public byte scenarioType = SCENARIO_CONIC;

    [Header("Absolute epoch")]
    public bool overrideScenarioJd0 = false;
    public double scenarioJd0 = 2460000.5;

    [Header("Scenario refs")]
    public CraftInitializer_FromPrimaryOrbitElements orbitScenario;
    public CraftInitializer_NearStation nearStationScenario;
    public CraftInitializer_DockedToStation dockedScenario;
}