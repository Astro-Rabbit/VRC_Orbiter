
using UdonSharp;
using UnityEngine;

public class CraftInitSystem : UdonSharpBehaviour
{
    [Header("References")]
    public CraftConfig config;
    public CraftStateModel craft;
    public CraftControlState control;

    [Header("Optional: conic seed")]
    public ConicState conic;
    public ConicFitter conicFitter;

    [Header("Behavior")]
    public bool initOnStart = true;
    public bool resetControls = true;

    [Tooltip("If true, seed conic from current craft ECI at start.")]
    public bool seedConicFromCraftState = false;

    [Tooltip("Primary to fit about when seeding conic (1=Earth, 2=Moon in your current setup).")]
    public byte seedPrimaryId = 2;

    [Tooltip("Sim time to use when seeding conic if SimClock isn't wired in. Usually 0.")]
    public double seedTime = 0.0;

    private bool _didInit = false;

    void Start()
    {
        if (initOnStart) Initialize();
    }

    public void Initialize()
    {
        if (_didInit) return;
        _didInit = true;

        if (config != null && craft != null)
        {
            // Mass initialization
            craft.dryMassKg = config.dryMassKg;
            craft.propMassKg = config.propMassKgInitial;
            craft.RecomputeMass();

            // If you later add inertia into CraftStateModel, copy it here too:
            // craft.Ixx = config.Ixx; craft.Iyy = config.Iyy; craft.Izz = config.Izz;
        }

        if (resetControls && control != null)
        {
            control.throttle01 = 0f;
            control.thrustMode = 0; // prograde default
            control.targetForwardECI = Vector3.forward;
        }

        // Optional conic seeding: helpful if you spawn with craft ECI state already placed
        if (seedConicFromCraftState && conicFitter != null && conic != null)
        {
            // Ensure conic's primary is set to what you intend before the fit
            conic.primaryBodyId = seedPrimaryId;

            // Fit using current craft state (must be valid at init)
            conicFitter.Fit(seedPrimaryId, seedTime);

            // Mark craft primary to match
            if (craft != null) craft.primaryBodyId = seedPrimaryId;
        }
        else
        {
            conic.primaryBodyId = 2;
            conic.epochT0 = 0;
            conic.M0Rad = 0;

            conic.aMeters = 13537400;
            conic.e = 0;
            conic.iRad = .5;
            conic.raanRad = 0;
            conic.argpRad = 0;

            conic.valid = true;
        }
    }
}

