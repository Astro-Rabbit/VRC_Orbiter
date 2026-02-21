using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class SimManager : UdonSharpBehaviour
{
    public const byte MODE_RAILS = 0;
    public const byte MODE_INTEGRATED = 1;

    [Header("Core")]
    public SimClock clock;
    public EphemerisSystem ephem;
    public BodyCatalog bodies;
    public ConicFitter conicFitter;

    [Header("Rails objects")]
    public CraftPropSystem[] railObjects;

    [Header("Active craft")]
    public CraftStateModel craft;
    public CraftPropSystem craftProp;
    public ConicState craftConic;
    public SOISwitchSystem soiSwitch;

    [Header("Dynamics (owner only)")]
    public NumericalPropagator numeric;

    [Header("Attitude (owner integrates)")]
    public AttitudeControllerPD attitudeController;
    public ActuationController actuation;
    public AttitudePropagator attitudeProp;

    [Header("Networking")]
    public CraftNetState netCore;
    public CraftNetKinematics netKin;
    public CraftNetConic netConic;
    public CraftNetAttitude netAtt;

    [Header("Stepping (Integrated mode)")]
    [Tooltip("Fixed simulation step used ONLY in integrated mode.")]
    public float fixedDt = 0.05f;

    [Tooltip("Max fixed substeps per FixedUpdate to prevent spiraling.")]
    public int maxSubstepsPerFixed = 8;

    [Tooltip("If budget hit, drop remainder accumulator.")]
    public bool dropAccumIfBudgetHit = true;

    [Header("Pause")]
    public bool paused = false;

    [Header("Force-based mode switching (owner only)")]
    public double enterIntegratedForceN = 1.0;
    public double exitIntegratedForceN = 0.5;
    public float settleSeconds = 3.0f;
    public bool autoModeSwitch = true;

    [Header("Warp policy")]
    [Tooltip("If true, entering integrated will force warp to x1.")]
    public bool forceWarpTo1OnIntegrated = true;

    [Tooltip("If true, entering integrated is blocked while warp != 1 (instead of forcing).")]
    public bool blockEnterIntegratedDuringWarp = false;

    [Header("Render")]
    public OrbitDiagnostics orbit;
    public PrimaryOrbitDiagnostics primaryDiag;
    public OrbitRenderer orbitline;

    [Header("Initialize")]
    public OrbitInitializerFromPrimaryElements InitialConic;

    // --- internal ---
    private float _settleAccum = 0f;

    // fixed-step accumulator for integrated mode (driven by Time.fixedDeltaTime)
    private float _accumFixed = 0f;

    // integrated-mode sim time (seconds), anchored to clock.Now() at entry
    private double _simT = 0.0;
    private bool _simTValid = false;

    void Start()
    {
        if (InitialConic != null)
            InitialConic.InitializeNow();
    }

    void Update()
    {
        if (paused || clock == null) return;

        // Global mission time (includes warp)
        double T = clock.Now();

        // 1) Everyone runs ephemeris for rails + rendering (fast, deterministic vs T)
        if (ephem != null) ephem.Evaluate(T);

        // 2) Everyone runs rails objects (deterministic vs T)
        TickRailsObjects(T);

        // 3) Craft (remote apply in Update; owner integrated is stepped in FixedUpdate)
        TickCraft_UpdateSide(T);

        // 4) Render
        ApplyRender();
    }

    private void FixedUpdate()
    {
        if (paused || clock == null || netCore == null || craft == null) return;

        bool isOwner = Networking.IsOwner(netCore.gameObject);
        if (!isOwner) return;

        byte mode = netCore.mode;

        // In rails mode we do NOT run fixed-step physics. (Timewarp-friendly)
        if (mode != MODE_INTEGRATED) return;

        // Integrated mode must be warp=1 (by policy)
        if (clock != null)
        {
            if (clock.timeScale != 1.0)
            {
                if (blockEnterIntegratedDuringWarp)
                {
                    // If we somehow ended up integrated while warped, force back to rails safely.
                    EnterRails();
                    return;
                }

                if (forceWarpTo1OnIntegrated)
                {
                    // Owner-only; SimClock will re-anchor without jumping time.
                    clock.SetTimeScale(1.0);
                }
            }
        }

        // Anchor sim time on first integrated tick (or after ownership changes)
        if (!_simTValid)
        {
            _simT = clock.Now(); // anchor to shared mission time at entry
            _simTValid = true;
        }

        // Accumulate real fixed time, step in fixedDt chunks
        float realFixedDt = Time.fixedDeltaTime;
        if (realFixedDt < 0f) realFixedDt = 0f;

        _accumFixed += realFixedDt;

        int steps = 0;
        while (_accumFixed >= fixedDt && steps < maxSubstepsPerFixed)
        {
            _accumFixed -= fixedDt;
            steps++;

            // Advance integrated sim time deterministically
            _simT += (double)fixedDt;

            // Physics-time ephemeris sampling (important for gravity/frames if used)
            if (ephem != null) ephem.Evaluate(_simT);

            // Owner attitude + actuation for THIS substep
            TickAttitudeOwner(fixedDt);

            if (craft != null && actuation != null)
            {
                double dProp = actuation.mainMdot_kgps * (double)fixedDt;
                if (dProp > 0.0)
                {
                    craft.propMassKg = System.Math.Max(0.0, craft.propMassKg - dProp);
                    craft.RecomputeMass();
                }
            }

            // Owner translation integration
            if (numeric != null && actuation != null)
            {
                numeric.force_E = actuation.F_E;
                numeric.Step(fixedDt, _simT);
            }

            // Auto settle back to rails (based on current net force)
            if (autoModeSwitch)
            {
                double F = GetNetForceMagN();
                if (F < exitIntegratedForceN) _settleAccum += fixedDt;
                else _settleAccum = 0f;

                if (_settleAccum >= settleSeconds)
                {
                    _settleAccum = 0f;
                    EnterRails(); // fits conic at current time and switches
                    return;
                }
            }
        }

        if (steps == maxSubstepsPerFixed && dropAccumIfBudgetHit)
            _accumFixed = 0f;

        // Publish once per FixedUpdate (your publish scripts are already rate-limited internally)
        if (netCore != null) netCore.PublishCore();
        if (netKin != null)  netKin.PublishKinematics();
        if (netAtt != null)  netAtt.PublishAttitude();
    }

    // Update-side craft handling: rails owner + all remote application
    private void TickCraft_UpdateSide(double T)
    {
        if (craft == null || netCore == null) return;

        bool isOwner = Networking.IsOwner(netCore.gameObject);
        byte mode = netCore.mode;

        if (isOwner)
        {
            if (mode == MODE_RAILS)
            {
                // Rails propagation uses global mission time (warp supported)
                if (craftProp != null)
                    craftProp.Evaluate(T);

                if (soiSwitch != null)
                    TryHandleSOISwitchRails(T);

                // Attitude should NOT be stepped with fixedDt-per-frame.
                // Use real frame dt (clamped) so attitude behaves consistently.
                float dtFrame = Mathf.Min(Time.deltaTime, 0.05f);
                TickAttitudeOwner(dtFrame);

                // Auto switch to integrated if force exceeds threshold
                if (autoModeSwitch)
                {
                    // If warp != 1, obey policy (force warp->1 or block)
                    double F = GetNetForceMagN();
                    if (F > enterIntegratedForceN)
                    {
                        if (clock != null && clock.timeScale != 1.0)
                        {
                            if (blockEnterIntegratedDuringWarp) return;

                            if (forceWarpTo1OnIntegrated)
                                clock.SetTimeScale(1.0);
                        }

                        EnterIntegrated(); // will reset accumulators + anchor time next FixedUpdate
                        return;
                    }
                }

                // Publish rails state (core+conic+attitude)
                if (netCore != null)  netCore.PublishCore();
                if (netConic != null) netConic.PublishConic();
                if (netAtt != null)   netAtt.PublishAttitude();
            }
            else
            {
                // Integrated owner is stepped in FixedUpdate; nothing to do here
            }
        }
        else
        {
            // Remotes apply networked state in Update (smooth visuals)
            if (mode == MODE_RAILS)
            {
                if (craftProp != null)
                    craftProp.Evaluate(T);

                if (netAtt != null)
                    netAtt.ApplyRemoteAttitude();
            }
            else
            {
                if (netKin != null)
                    netKin.ApplyRemoteKinematics();

                if (netAtt != null)
                    netAtt.ApplyRemoteAttitude();
            }
        }
    }

    private void TickRailsObjects(double T)
    {
        if (railObjects == null) return;
        for (int i = 0; i < railObjects.Length; i++)
        {
            if (railObjects[i] != null)
                railObjects[i].Evaluate(T);
        }
    }

    private void TickAttitudeOwner(float dt)
    {
        if (attitudeController != null)
            attitudeController.Evaluate();

        if (actuation != null)
            actuation.Evaluate();

        if (attitudeProp != null && actuation != null)
        {
            attitudeProp.tau_B = actuation.Tau_B;
            attitudeProp.Step(dt);
        }
    }

    public void EnterIntegrated()
    {
        if (netCore == null) return;
        if (!Networking.IsOwner(netCore.gameObject)) return;

        // Reset fixed-step accumulator and settle timer
        _accumFixed = 0f;
        _settleAccum = 0f;

        // Invalidate _simT so FixedUpdate anchors to clock.Now() cleanly
        _simTValid = false;

        // Switch mode + sync primary
        netCore.SetMode(MODE_INTEGRATED, craft != null ? craft.primaryBodyId : (byte)0, true);

        // Push immediate snapshots
        if (netKin != null) netKin.ForcePublishKinematics();
        if (netAtt != null) netAtt.ForcePublishAttitude();

        // Optional conic publish (useful for UI)
        if (netConic != null) netConic.ForcePublishConic();
    }

    public void EnterRails()
    {
        if (netCore == null || craft == null) return;
        if (!Networking.IsOwner(netCore.gameObject)) return;

        _accumFixed = 0f;
        _settleAccum = 0f;

        // When leaving integrated, define rails conic at CURRENT authoritative time.
        // Prefer integrated sim time if valid, else clock.Now().
        double T = _simTValid ? _simT : (clock != null ? clock.Now() : 0.0);
        byte pid = craft.primaryBodyId;

        if (conicFitter != null)
            conicFitter.Fit(pid, T);

        if (netConic != null)
            netConic.ForcePublishConic();

        netCore.SetMode(MODE_RAILS, pid, true);

        if (netAtt != null)
            netAtt.ForcePublishAttitude();

        // Reset integrated time anchor
        _simTValid = false;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (netCore == null) return;
        if (!Networking.IsOwner(netCore.gameObject)) return;

        // New owner should re-anchor integrated sim time on next FixedUpdate
        _simTValid = false;
        _accumFixed = 0f;
        _settleAccum = 0f;

        netCore.ForcePublishCore();

        byte mode = netCore.mode;
        if (mode == MODE_INTEGRATED)
        {
            if (netKin != null) netKin.ForcePublishKinematics();
            if (netAtt != null) netAtt.ForcePublishAttitude();
        }
        else
        {
            if (netConic != null) netConic.ForcePublishConic();
            if (netAtt != null)   netAtt.ForcePublishAttitude();
        }
    }

    private void TryHandleSOISwitchRails(double T)
    {
        if (soiSwitch == null) return;
        if (netCore == null) return;
        if (!Networking.IsOwner(netCore.gameObject)) return;

        byte newPrimary;
        double dMoon, rSOI;
        bool wants = soiSwitch.Evaluate(out newPrimary, out dMoon, out rSOI);
        if (!wants) return;

        byte oldPrimary = craft != null ? craft.primaryBodyId : (byte)0;

        if (craft != null) craft.primaryBodyId = newPrimary;

        if (conicFitter != null)
            conicFitter.Fit(newPrimary, T);
        else if (craftConic != null)
            craftConic.primaryBodyId = newPrimary;

        if (netConic != null)
            netConic.ForcePublishConic();

        netCore.SetMode(MODE_RAILS, newPrimary, true);

        if (netAtt != null)
            netAtt.ForcePublishAttitude();

        Debug.Log($"[SOI] primary {oldPrimary} -> {newPrimary}  (T={T:F2}s)");
    }

    private double GetNetForceMagN()
    {
        if (actuation == null) return 0.0;
        return (double)actuation.F_E.magnitude;
    }

    private void ApplyRender()
    {
        if (orbit != null) orbit.Evaluate();
        if (primaryDiag != null) primaryDiag.Evaluate();
        if (orbitline != null) orbitline.Apply();
    }
}
