using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class SimManager : UdonSharpBehaviour
{
    public const byte MODE_RAILS = 0;
    public const byte MODE_INTEGRATED = 1;
    public const byte MODE_DOCKED = 2;

    [Header("Core")]
    public SimClock clock;
    public EphemerisSystem ephem;
    public BodyCatalog bodies;
    public ConicFitter conicFitter;

    [Header("Rails objects")]
    public StationPropSystem[] railObjects;

    [Header("Contacts (craft <-> stations)")]
    public StationStateModel[] stations;            // list of station state models (same ordering used by UI/targeting)
    public CraftAttitudeState craftAtt;             // craft attitude qBE
    public GuidanceNavContactsComputer contactsComp; // computes snapshot

    [Header("Docking (craft <-> station attachment authority)")]
    public DockingComputer dockingComp;
    public DockingRuntimeState dock; // optional if dockingComp already has it
    public StewartPlatformTargetResolver stewartResolver;
    [Header("Docking policy")]
    [Tooltip("If false, docking detection + docking motion authority are disabled (craft behaves as free-flight).")]
    public bool dockingAllowed = true;

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

    [Tooltip("If budget hit, clamp sim time to mission time (keeps ephem/render consistent).")]
    public bool clampToMissionTimeIfBudgetHit = true;

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
    public SkyBoxDriver skyrender;
    public StationRenderManager stationRender;

    [Header("Initialize")]
    public OrbitInitializerFromPrimaryElements InitialConic;
    public CraftInitializer_NearStation initialNearStation;
    public CraftInitializer_DockedToStation initialDocked;

    // --- internal ---
    private float _settleAccum = 0f;

    // Integrated-mode sim time (seconds), kept consistent with mission time
    private double _simT = 0.0;
    private bool _simTValid = false;

    // Mission-time accumulator (seconds) used to run fixedDt substeps
    private double _accumSim = 0.0;

    void Start()
    {
        if (initialDocked != null)
        {
            bool okDocked = initialDocked.InitializeNow();
            if (okDocked) return;
        }

        if (initialNearStation != null)
        {
            bool ok = initialNearStation.InitializeNow();
            if (ok) return;
        }

        if (InitialConic != null)
            InitialConic.InitializeNow();
    }

    void Update()
    {
        if (paused || clock == null) return;

        double Tmission = clock.Now();

        // Choose a single authoritative time for "world/ephem rendering" this frame.
        // - Owner + integrated: use _simT (craft state is integrated against _simT)
        // - Everyone else: use mission time
        double Tview = Tmission;
        if (netCore != null && netCore.mode == MODE_INTEGRATED)
        {
            if (Networking.IsOwner(netCore.gameObject) && _simTValid)
                Tview = _simT;
        }

        // 1) Everyone runs ephemeris for rails + rendering
        if (ephem != null) ephem.Evaluate(Tview);

        // 2) Everyone runs rails objects (deterministic vs Tview)
        TickRailsObjects(Tview);


        // 3) Craft update-side handling:
        //    - Owner rails: rails propagation uses mission time (warp supported)
        //    - Owner integrated: stepped in FixedUpdate (no-op here)
        //    - Owner docked: dock kinematics in Update (same timing as station/render)
        //    - Remotes: apply net state
        TickCraft_UpdateSide(Tmission);


        // --- Undock release request: DockingComputer has already written released craft state.
        // Switch directly to rails/conic to avoid integrated-mode transition oddities.
        // If owner is docked and undock was requested, the craft has NOW been updated
        // to the current docked pose for this frame. Release from that pose.
        if (dockingComp != null && dockingComp.requestUndock)
        {
            dockingComp.ExecuteUndockRelease(Tmission);
        }

        // Now consume the leave-docked-to-rails request
        if (dockingComp != null && dockingComp.requestLeaveDockedToRails)
        {
            dockingComp.requestLeaveDockedToRails = false;

            _settleAccum = 0f;
            _simTValid = false;
            _accumSim = 0.0;

            EnterRails();
            dock.ResetState();
        }

        // --- Docking capture check (owner only, gated by dockingAllowed) ---
        if (DockingAllowedNow() && dockingComp != null && netCore != null && clock != null)
        {
            bool isOwner = Networking.IsOwner(netCore.gameObject);

            if (isOwner && netCore.mode != MODE_DOCKED)
            {
                dockingComp.EvaluateLatchAndStart(Tmission);

                if (dockingComp.requestEnterDocked)
                {
                    // DockingComputer should already have called netCore.SetDocked(...) to populate dock snapshot.
                    // We just switch mode here.
                    byte pid = craft != null ? craft.primaryBodyId : (byte)0;
                    netCore.SetMode(MODE_DOCKED, pid, true);

                    // Immediate attitude publish helps remotes snap cleanly.
                    if (netAtt != null) netAtt.ForcePublishAttitude();
                }
            }
        }

        // 4) Contacts snapshot (render/GC/orrery all read this)
        if (contactsComp != null)
        {
            contactsComp.craft = craft;
            contactsComp.craftAtt = craftAtt;
            contactsComp.stations = stations;
            contactsComp.Evaluate();
        }

        // 5) Render
        ApplyRender();
    }

    private void FixedUpdate()
    {
        if (paused || clock == null || netCore == null || craft == null) return;

        bool isOwner = Networking.IsOwner(netCore.gameObject);
        if (!isOwner) return;

        byte mode = netCore.mode;

        // Integrated-only in FixedUpdate
        if (mode != MODE_INTEGRATED) return;

        // Integrated mode must be warp=1 (by policy)
        if (clock != null && clock.timeScale != 1.0)
        {
            if (blockEnterIntegratedDuringWarp)
            {
                EnterRails();
                return;
            }

            if (forceWarpTo1OnIntegrated)
                clock.SetTimeScale(1.0);
        }

        double targetT = clock.Now(); // mission time (warp is 1 by policy)

        // Accumulate mission-time that has elapsed since our last integrated step
        double dtMission = targetT - _simT;
        if (dtMission < 0.0) dtMission = 0.0; // guard for re-anchors / ownership edges
        _accumSim += dtMission;

        int steps = 0;
        double h = (double)fixedDt;
        if (h <= 0.0) h = 0.02; // sane fallback

        while (_accumSim >= h && steps < maxSubstepsPerFixed)
        {
            _accumSim -= h;
            steps++;

            // Advance integrated sim time deterministically
            double t0 = _simT;
            double t1 = t0 + h;
            _simT = t1;

            // Owner attitude + actuation for THIS substep
            TickAttitudeOwner((float)h);

            // Propellant bookkeeping
            if (craft != null && actuation != null)
            {
                double dProp = actuation.mainMdot_kgps * h;
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

                // IMPORTANT: Step expects tNow as the START of the step (it samples tNow and tNow+dt)
                numeric.Step(h, t0);
            }

            // Auto settle back to rails (based on current net force)
            if (autoModeSwitch)
            {
                double F = GetNetForceMagN();
                if (F < exitIntegratedForceN) _settleAccum += (float)h;
                else _settleAccum = 0f;

                if (_settleAccum >= settleSeconds)
                {
                    _settleAccum = 0f;
                    EnterRails(); // fits conic at current time and switches
                    return;
                }
            }
        }

        // Finish the remaining fractional time so _simT matches mission time.
        double rem = _accumSim;
        if (rem > 0.0)
        {
            if (rem > h) rem = h;

            _accumSim -= rem;
            if (_accumSim < 0.0) _accumSim = 0.0;

            double t0r = _simT;
            double t1r = t0r + rem;
            _simT = t1r;

            TickAttitudeOwner((float)rem);

            if (craft != null && actuation != null)
            {
                double dProp = actuation.mainMdot_kgps * rem;
                if (dProp > 0.0)
                {
                    craft.propMassKg = System.Math.Max(0.0, craft.propMassKg - dProp);
                    craft.RecomputeMass();
                }
            }

            if (numeric != null && actuation != null)
            {
                numeric.force_E = actuation.F_E;
                numeric.Step(rem, t0r);
            }

            if (autoModeSwitch)
            {
                double F = GetNetForceMagN();
                if (F < exitIntegratedForceN) _settleAccum += (float)rem;
                else _settleAccum = 0f;

                if (_settleAccum >= settleSeconds)
                {
                    _settleAccum = 0f;
                    EnterRails();
                    return;
                }
            }
        }

        // If we hit the step budget, clamp sim time to mission time and clear accumulator.
        if (steps == maxSubstepsPerFixed && clampToMissionTimeIfBudgetHit && _accumSim > 0.0)
        {
            _simT = targetT;
            _accumSim = 0.0;
        }

        // Publish once per FixedUpdate (publish scripts are already rate-limited internally)
        if (netCore != null) netCore.PublishCore();
        if (netKin != null)  netKin.PublishKinematics();
        if (netAtt != null)  netAtt.PublishAttitude();
    }

    // Update-side craft handling: rails owner + docked owner + all remote application
    private void TickCraft_UpdateSide(double Tmission)
    {
        if (craft == null || netCore == null) return;

        bool isOwner = Networking.IsOwner(netCore.gameObject);
        byte mode = netCore.mode;

        if (isOwner)
        {
            if (mode == MODE_RAILS)
            {
                // Rails propagation uses mission time (warp supported)
                if (craftProp != null)
                    craftProp.Evaluate(Tmission);

                if (soiSwitch != null)
                    TryHandleSOISwitchRails(Tmission);

                // Attitude in rails mode: run on frame dt (clamped)
                float dtFrame = Mathf.Min(Time.deltaTime, 0.05f);
                TickAttitudeOwner(dtFrame);

                // Auto switch to integrated if force exceeds threshold
                if (autoModeSwitch)
                {
                    double F = GetNetForceMagN();
                    if (F > enterIntegratedForceN)
                    {
                        if (clock != null && clock.timeScale != 1.0)
                        {
                            if (blockEnterIntegratedDuringWarp) return;
                            if (forceWarpTo1OnIntegrated) clock.SetTimeScale(1.0);
                        }

                        EnterIntegrated(); // anchors next FixedUpdate
                        return;
                    }
                }

                // Publish rails state (core+conic+attitude)
                if (netCore != null)  netCore.PublishCore();
                if (netConic != null) netConic.PublishConic();
                if (netAtt != null)   netAtt.PublishAttitude();
            }
            else if (mode == MODE_DOCKED)
            {
                // Docked is kinematic, so keep it in Update with station/render timing.
                if (!DockingAllowedNow())
                {
                    EnterRails();
                    return;
                }

                if (dockingComp != null)
                {
                    float dtFrame = Mathf.Min(Time.deltaTime, 0.05f);
                    dockingComp.EvaluateDocked(dtFrame, Tmission);
                }

                if (netAtt != null) netAtt.PublishAttitude();
                if (netCore != null) netCore.PublishCore();
            }
            else
            {
                // Integrated owner is stepped in FixedUpdate
            }
        }
        else
        {
            // Remotes apply networked state in Update (smooth visuals)
            if (mode == MODE_RAILS)
            {
                if (craftProp != null)
                    craftProp.Evaluate(Tmission);

                if (netAtt != null)
                    netAtt.ApplyRemoteAttitude();
            }
            else if (mode == MODE_INTEGRATED)
            {
                if (netKin != null)
                    netKin.ApplyRemoteKinematics();

                if (netAtt != null)
                    netAtt.ApplyRemoteAttitude();
            }
            else if (mode == MODE_DOCKED)
            {
                // IMPORTANT: do NOT apply netKin while docked; docking is deterministic from station + snapshot.
                if (DockingAllowedNow() && dockingComp != null)
                {
                    dockingComp.EvaluateDockedRemote(Tmission);
                }
                else
                {
                    // If docking not allowed, fall back to remote rails propagation
                    if (craftProp != null)
                        craftProp.Evaluate(Tmission);

                    if (netAtt != null)
                        netAtt.ApplyRemoteAttitude();
                }
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

        _settleAccum = 0f;

        double T = (clock != null) ? clock.Now() : 0.0;

        if (craftProp != null)
            craftProp.Evaluate(T);

        if (soiSwitch != null)
            TryHandleSOISwitchRails(T);

        _simT = T;
        _simTValid = true;
        _accumSim = 0.0;

        netCore.SetMode(MODE_INTEGRATED, craft != null ? craft.primaryBodyId : (byte)0, true);

        if (netKin != null) netKin.ForcePublishKinematics();
        if (netAtt != null) netAtt.ForcePublishAttitude();
        if (netConic != null) netConic.ForcePublishConic();
    }

    public void EnterRails()
    {
        if (netCore == null || craft == null) return;
        if (!Networking.IsOwner(netCore.gameObject)) return;

        _settleAccum = 0f;

        double T = _simTValid ? _simT : (clock != null ? clock.Now() : 0.0);
        byte pid = craft.primaryBodyId;

        if (conicFitter != null)
            conicFitter.Fit(pid, T);

        if (netConic != null)
            netConic.ForcePublishConic();

        netCore.SetMode(MODE_RAILS, pid, true);

        if (netAtt != null)
            netAtt.ForcePublishAttitude();

        _simTValid = false;
        _accumSim = 0.0;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (netCore == null) return;
        if (!Networking.IsOwner(netCore.gameObject)) return;

        _simTValid = false;
        _accumSim = 0.0;
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

    private bool DockingAllowedNow()
    {
        if (!dockingAllowed) return false;
        return true;
    }

    private void ApplyRender()
    {
        if (orbit != null) orbit.Evaluate();
        if (primaryDiag != null) primaryDiag.Evaluate();
        if (orbitline != null) orbitline.Apply();
        skyrender.Tick();
        if (stationRender != null)
            stationRender.Tick();

        if (stewartResolver != null)
            stewartResolver.Tick();

    }
}