using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// SimManager
///
/// High-level sim orchestrator for a single craft.
///
/// Responsibilities:
/// - Advance deterministic world state:
///     * ephemeris
///     * rails objects
///     * owner craft propagation / attitude / docking
/// - Choose the correct presentation time for owner vs remote rendering
/// - Coordinate mode transitions:
///     * RAILS
///     * INTEGRATED
///     * DOCKED
/// - Trigger networking publishes for the appropriate streams
///
/// Timing model:
/// - Update():
///     * render-time world evaluation
///     * rails propagation
///     * docked kinematics
///     * remote presentation
/// - FixedUpdate():
///     * owner-only integrated translation/attitude stepping
///
/// Frame conventions:
/// - mission time: clock.Now()
/// - owner integrated sim time: _simT
/// - remote render sample time: delayed network presentation time
/// </summary>
public class SimManager : UdonSharpBehaviour
{
    public const byte MODE_RAILS = 0;
    public const byte MODE_INTEGRATED = 1;
    public const byte MODE_DOCKED = 2;

    // -------------------------------------------------------------------------
    // Core systems
    // -------------------------------------------------------------------------

    [Header("Core")]
    public SimClock clock;
    public EphemerisSystem ephem;
    public BodyCatalog bodies;
    public ConicFitter conicFitter;

    [Header("Rails objects")]
    public StationPropSystem[] railObjects;

    // -------------------------------------------------------------------------
    // Contacts / docking
    // -------------------------------------------------------------------------

    [Header("Contacts (craft <-> stations)")]
    public StationStateModel[] stations;               // ordering matches UI/targeting
    public CraftAttitudeState craftAtt;                // craft attitude qBE
    public GuidanceNavContactsComputer contactsComp;   // computes contact snapshot

    [Header("Docking (craft <-> station attachment authority)")]
    public DockingComputer dockingComp;
    public DockingRuntimeState dock;                   // optional if dockingComp already owns this
    public StewartPlatformTargetResolver stewartResolver;

    [Header("Docking policy")]
    [Tooltip("If false, docking detection + docking motion authority are disabled (craft behaves as free-flight).")]
    public bool dockingAllowed = true;

    // -------------------------------------------------------------------------
    // Active craft
    // -------------------------------------------------------------------------

    [Header("Active craft")]
    public CraftStateModel craft;
    public CraftPropSystem craftProp;
    public ConicState craftConic;
    public SOISwitchSystem soiSwitch;

    // -------------------------------------------------------------------------
    // Owner-only dynamics / attitude
    // -------------------------------------------------------------------------

    [Header("Dynamics (owner only)")]
    public NumericalPropagator numeric;

    [Header("Attitude (owner integrates)")]
    public AttitudeControllerPD attitudeController;
    public ActuationController actuation;
    public AttitudePropagator attitudeProp;

    // -------------------------------------------------------------------------
    // Networking
    // -------------------------------------------------------------------------

    [Header("Networking")]
    public CraftNetState netCore;
    public CraftNetKinematics netKin;
    public CraftNetConic netConic;
    public CraftNetAttitude netAtt;

    [Header("Ownership handoff")]
    [Tooltip("Objects that should follow SimManager ownership during sim authority handoff.")]
    public GameObject[] ownershipObjects;

    // -------------------------------------------------------------------------
    // Integrated stepping
    // -------------------------------------------------------------------------

    [Header("Stepping (Integrated mode)")]
    [Tooltip("Fixed simulation step used ONLY in integrated mode.")]
    public float fixedDt = 0.05f;

    [Tooltip("Max fixed substeps per FixedUpdate to prevent spiraling.")]
    public int maxSubstepsPerFixed = 8;

    [Tooltip("If budget hit, clamp sim time to mission time (keeps ephem/render consistent).")]
    public bool clampToMissionTimeIfBudgetHit = true;

    [Header("Pause")]
    public bool paused = false;

    // -------------------------------------------------------------------------
    // Auto mode switching
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Render / diagnostics
    // -------------------------------------------------------------------------

    [Header("Render")]
    public OrbitDiagnostics orbit;
    public PrimaryOrbitDiagnostics primaryDiag;
    public OrbitRenderer orbitline;
    public SkyBoxDriver skyrender;
    public StationRenderManager stationRender;
    public CraftNetCabinAccel netCabinAccel;
    // -------------------------------------------------------------------------
    // Initialization helpers
    // -------------------------------------------------------------------------

    [Header("Initialize")]
    public OrbitInitializerFromPrimaryElements InitialConic;
    public CraftInitializer_NearStation initialNearStation;
    public CraftInitializer_DockedToStation initialDocked;


    // -------------------------------------------------------------------------
    // Restart / scenario reset
    // -------------------------------------------------------------------------

    [Header("Scenario initializer")]
    public SimScenarioInitializer scenarioInitializer;

    [Header("Restart / Reset")]
    [Tooltip("If true, restart transaction is in progress and normal sim ticking is suppressed.")]
    public bool isRestarting = false;

    [Tooltip("If true, instance master has locked scenario resets.")]
    public bool resetLockedByMaster = false;

    [Tooltip("Optional: if true, Start() will run the selected startup scenario through the shared restart pipeline.")]
    public bool useSharedStartupRestart = false;

    [Tooltip("Which authored startup scenario INDEX to use.")]
    public int startupScenarioIndex = 0;


    [Header("Restart runtime clears")]
    public GC_RuntimeState gcRuntime;
    public NodePlanState nodePlan;
    public GuidanceNavContactsState contactsState;
    public GC_Core gcCore;
    public GC_RuntimeNetState gcRuntimeNet;
    public GC_NodePlanNetState nodePlanNet;
    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    private float _settleAccum = 0f;

    // Integrated-mode sim time, kept aligned to mission time progression
    private double _simT = 0.0;
    private bool _simTValid = false;

    // Mission-time accumulator used for fixed integrated stepping
    private double _accumSim = 0.0;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        if (!useSharedStartupRestart)
        {
            if (initialDocked != null)
            {
                bool okDocked = initialDocked.InitializeNow();
                if (okDocked) return;
            }

            if (initialNearStation != null)
            {
                bool okNear = initialNearStation.InitializeNow();
                if (okNear) return;
            }

            if (InitialConic != null)
                InitialConic.InitializeNow();

            return;
        }

        if (Networking.IsOwner(gameObject))
            RestartToScenarioIndex_Internal(startupScenarioIndex, true);
    }

    void Update()
    {
        if (paused || isRestarting || clock == null) return;
        double Tmission = clock.Now();

        bool hasNetCore = (netCore != null);
        bool isOwner = hasNetCore && Networking.IsOwner(gameObject);

        double backTime = 0.0;
        if (netAtt != null)
            backTime = (double)netAtt.interpBackTimeSeconds;

        // Remote render-time cache is updated once per frame from the chosen back-time.
        if (!isOwner)
            clock.UpdateRemoteRenderTimeCache(backTime);

        double tRenderNet = clock.GetCachedRemoteRenderTime();

        byte presentedMode = isOwner
            ? (netCore != null ? netCore.mode : MODE_RAILS)
            : (netCore != null ? netCore.GetPresentedMode(tRenderNet) : MODE_RAILS);

        // Choose one authoritative time for world/ephemeris/render this frame.
        //
        // Owner:
        // - integrated: use _simT
        // - otherwise : use mission time
        //
        // Remote:
        // - default delayed presentation time for rails/docked
        // - integrated: align to sampled netKin sim timeline
        double Tview = Tmission;

        if (isOwner)
        {
            if (netCore != null && netCore.mode == MODE_INTEGRATED && _simTValid)
                Tview = _simT;
        }
        else
        {
            // Default remote delayed sim-time for rails/docked presentation
            Tview = Tmission - backTime * clock.timeScale;

            // Integrated remote world timing comes from latest RAW kinematic snapshot,
            // not from interpolated kinematic playback.
            if (presentedMode == MODE_INTEGRATED && netKin != null && netKin.rawValid)
            {
                netKin.UpdatePresentedState();   // update separate visual-smoothed output
                Tview = netKin.rawSimT;          // coherent latest packet sim-time
            }
        }

        // 1) Evaluate ephemeris for the chosen presentation time
        if (ephem != null)
            ephem.Evaluate(Tview);

        // 2) Evaluate rails objects against the same presentation time
        TickRailsObjects(Tview);

        // 3) Craft handling:
        //    - owner rails propagation
        //    - owner docked motion
        //    - remote net-driven presentation
        //    - owner integrated does nothing here (FixedUpdate owns it)
        TickCraft_UpdateSide(Tmission, Tview, tRenderNet, isOwner);

        // -----------------------------------------------------------------
        // Docking / undocking transitions
        // -----------------------------------------------------------------

        // Undock release request:
        // DockingComputer has already written the released craft state.
        // Switch directly to rails/conic from the current docked frame state.
        if (dockingComp != null && dockingComp.requestUndock)
        {
            dockingComp.ExecuteUndockRelease(Tmission);
        }

        // Consume leave-docked-to-rails transition request
        if (dockingComp != null && dockingComp.requestLeaveDockedToRails)
        {
            dockingComp.requestLeaveDockedToRails = false;

            _settleAccum = 0f;
            _simTValid = false;
            _accumSim = 0.0;

            EnterRails();

            if (dock != null)
                dock.ResetState();
        }

        // Docking capture check (owner only, gated by dockingAllowed)
        if (DockingAllowedNow() && dockingComp != null && netCore != null && clock != null)
        {
            if (isOwner && netCore.mode != MODE_DOCKED)
            {
                dockingComp.EvaluateLatchAndStart(Tmission);

                if (dockingComp.requestEnterDocked)
                {
                    // DockingComputer is expected to have already populated dock snapshot via netCore.SetDocked(...)
                    byte pid = craft != null ? craft.primaryBodyId : (byte)0;
                    netCore.SetMode(MODE_DOCKED, pid, true);

                    // Immediate attitude publish helps remotes snap cleanly
                    if (netAtt != null)
                        netAtt.ForcePublishAttitude();
                }
            }
        }

        // 4) Contacts snapshot used by render / GC / orrery
        if (contactsComp != null)
        {
            contactsComp.craft = craft;
            contactsComp.craftAtt = craftAtt;
            contactsComp.stations = stations;
            contactsComp.Evaluate();
        }

        // 5) Render / diagnostics
        ApplyRender();
    }

    private void FixedUpdate()
    {
        if (paused || isRestarting || clock == null || netCore == null || craft == null)
            return;

        bool isOwner = Networking.IsOwner(gameObject);
        if (!isOwner) return;

        byte mode = netCore.mode;

        // Integrated mode is stepped only in FixedUpdate
        if (mode != MODE_INTEGRATED)
            return;

        // Integrated mode must run at warp = 1 by policy
        if (clock.timeScale != 1.0)
        {
            if (blockEnterIntegratedDuringWarp)
            {
                EnterRails();
                return;
            }

            if (forceWarpTo1OnIntegrated)
                clock.SetTimeScale(1.0);
        }

        double targetT = clock.Now(); // mission time (warp == 1 by policy)

        // Accumulate mission time since the last integrated step
        double dtMission = targetT - _simT;
        if (dtMission < 0.0) dtMission = 0.0; // guard re-anchors / ownership edges
        _accumSim += dtMission;

        int steps = 0;
        double h = (double)fixedDt;
        if (h <= 0.0) h = 0.02; // sane fallback

        // -----------------------------------------------------------------
        // Whole fixed substeps
        // -----------------------------------------------------------------

        while (_accumSim >= h && steps < maxSubstepsPerFixed)
        {
            _accumSim -= h;
            steps++;

            double t0 = _simT;
            double t1 = t0 + h;
            _simT = t1;

            // Owner attitude / actuation for this substep
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

                // Step expects tNow as the START of the step
                numeric.Step(h, t0);
            }

            // Auto-settle back to rails if force remains low long enough
            if (autoModeSwitch)
            {
                double F = GetNetForceMagN();

                if (F < exitIntegratedForceN) _settleAccum += (float)h;
                else _settleAccum = 0f;

                if (_settleAccum >= settleSeconds)
                {
                    _settleAccum = 0f;
                    EnterRails();
                    return;
                }
            }
        }

        // -----------------------------------------------------------------
        // Remaining fractional step to align _simT with mission time
        // -----------------------------------------------------------------

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

        // If step budget was exhausted, clamp sim time back to mission time
        // so render / ephemeris stay coherent.
        // if (steps == maxSubstepsPerFixed && clampToMissionTimeIfBudgetHit && _accumSim > 0.0)
        // {
        //     _simT = targetT;
        //     _accumSim = 0.0;
        // }

        // Publish once per FixedUpdate; individual net scripts remain internally rate-limited
        if (netCore != null)
            netCore.PublishCore();

        if (netCabinAccel != null && actuation != null && craft != null && craftAtt != null)
        {
            double m = craft.massKg;
            if (m < 1.0) m = 1.0;

            Vector3 aE = actuation.F_E / (float)m;
            Vector3 aB = Quaternion.Inverse(craftAtt.qBE) * aE;

            netCabinAccel.currentOwnerAccelB = aB;
            netCabinAccel.currentOwnerSimT = _simT;
            netCabinAccel.PublishAccel();
        }

        if (netKin != null)
        {
            netKin.currentOwnerSimT = _simT;
            netKin.PublishKinematics();
        }

        if (netAtt != null)
            netAtt.PublishAttitude();
    }

    // -------------------------------------------------------------------------
    // Craft handling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Update-side craft handling:
    /// - Owner rails: rails propagation + rails attitude
    /// - Owner docked: dock kinematics in Update
    /// - Owner integrated: no-op here (handled in FixedUpdate)
    /// - Remote: apply presented network state
    /// </summary>
    private void TickCraft_UpdateSide(double Tmission, double Tview, double tRenderNet, bool isOwner)
    {
        if (craft == null || netCore == null) return;

        byte mode = isOwner ? netCore.mode : netCore.GetPresentedMode(tRenderNet);

        if (isOwner)
        {
            if (mode == MODE_RAILS)
            {
                // Rails propagation uses mission time (warp supported)
                if (craftProp != null)
                    craftProp.Evaluate(Tmission);

                if (soiSwitch != null)
                    TryHandleSOISwitchRails(Tmission);

                // Rails attitude runs on frame dt
                float dtFrame = Mathf.Min(Time.deltaTime, 0.05f);
                TickAttitudeOwner(dtFrame);

                // Auto switch to integrated if force is high enough
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

                        EnterIntegrated(Tmission); // anchors next FixedUpdate
                        return;
                    }
                }

                // Publish rails state
                if (netCore != null)  netCore.PublishCore();
                if (netConic != null) netConic.PublishConic();
                if (netAtt != null)   netAtt.PublishAttitude();
            }
            else if (mode == MODE_DOCKED)
            {
                // Docked is kinematic; evaluate in Update with station/render timing
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
                // Owner integrated mode is stepped in FixedUpdate
            }
        }
        else
        {
            // Remotes apply networked presentation in Update
            if (mode == MODE_RAILS)
            {
                if (craftProp != null)
                    craftProp.Evaluate(Tview);

                if (netAtt != null)
                    netAtt.ApplyRemoteAttitude();
            }
            else if (mode == MODE_INTEGRATED)
            {
                // Integrated translation is currently sampled through netKin render cache;
                // direct ApplyRemoteKinematics() is intentionally not used here.
                if (netAtt != null)
                    netAtt.ApplyRemoteAttitude();
                if (netKin != null)
                    netKin.ApplyRemoteRawToCraft();


            }
            else if (mode == MODE_DOCKED)
            {
                // Do NOT apply netKin while docked; docking is deterministic from station + snapshot
                if (DockingAllowedNow() && dockingComp != null)
                {
                    dockingComp.EvaluateDockedRemote(Tview);
                }
                else
                {
                    // Fallback to remote rails presentation if docking is disabled
                    if (craftProp != null)
                        craftProp.Evaluate(Tview);

                    if (netAtt != null)
                        netAtt.ApplyRemoteAttitude();
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // World helpers
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Mode transitions
    // -------------------------------------------------------------------------

    public void EnterIntegrated(double T)
    {
        if (netCore == null) return;
        if (!Networking.IsOwner(gameObject)) return;

        _settleAccum = 0f;

        // Caller has already evaluated rails at T if needed; just anchor integrated time here
        _simT = T;
        _simTValid = true;
        _accumSim = 0.0;

        netCore.SetMode(MODE_INTEGRATED, craft != null ? craft.primaryBodyId : (byte)0, true);

        if (netCabinAccel != null && actuation != null && craft != null && craftAtt != null)
        {
            double m = craft.massKg;
            if (m < 1.0) m = 1.0;

            Vector3 aE = actuation.F_E / (float)m;
            Vector3 aB = Quaternion.Inverse(craftAtt.qBE) * aE;

            netCabinAccel.currentOwnerAccelB = aB;
            netCabinAccel.currentOwnerSimT = _simT;
            netCabinAccel.ForcePublishAccel();
        }

        if (netKin != null)
        {
            netKin.currentOwnerSimT = _simT;
            netKin.ForcePublishKinematics();
        }

        if (netAtt != null)
            netAtt.ForcePublishAttitude();

        if (netConic != null)
            netConic.ForcePublishConic();
    }

    public void EnterRails()
    {
        if (netCore == null || craft == null) return;
        if (!Networking.IsOwner(gameObject)) return;

        _settleAccum = 0f;

        double T = _simTValid ? _simT : (clock != null ? clock.Now() : 0.0);
        byte pid = craft.primaryBodyId;

        if (conicFitter != null)
            conicFitter.Fit(pid, T);

        if (netConic != null)
            netConic.ForcePublishConic();

        netCore.SetMode(MODE_RAILS, pid, true);

        if (netCabinAccel != null)
            netCabinAccel.ForceZeroAccel(T);

        if (netAtt != null)
            netAtt.ForcePublishAttitude();

        _simTValid = false;
        _accumSim = 0.0;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        string playerName = player != null ? player.displayName : "null";
        string localName = Networking.LocalPlayer != null ? Networking.LocalPlayer.displayName : "null";

        Debug.Log("[SimManager] OnOwnershipTransferred: local=" + localName +
                " newOwner=" + playerName +
                " amOwner=" + Networking.IsOwner(gameObject));

        if (!Networking.IsOwner(gameObject)) return;

        _accumSim = 0.0;
        _settleAccum = 0f;

        if (netCore != null && netCore.mode == MODE_INTEGRATED && netKin != null && netKin.rawValid)
        {
            // adopt the same integrated snapshot timeline the remote view was using
            if (craft != null)
            {
                craft.rx = netKin.rawRx;
                craft.ry = netKin.rawRy;
                craft.rz = netKin.rawRz;

                craft.vx = netKin.rawVx;
                craft.vy = netKin.rawVy;
                craft.vz = netKin.rawVz;
            }

            _simT = netKin.rawSimT;
            _simTValid = true;
        }
        else
        {
            _simTValid = false;
        }


        TransferSubordinateOwnershipsToLocal();
        ForcePublishAuthoritativeState();

        Debug.Log("[SimManager] OnOwnershipTransferred: takeover complete.");
    }

    public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)
    {
        string requesterName = requester != null ? requester.displayName : "null";
        string newOwnerName = newOwner != null ? newOwner.displayName : "null";
        string localName = Networking.LocalPlayer != null ? Networking.LocalPlayer.displayName : "null";

        Debug.Log("[SimManager] OnOwnershipRequest: local=" + localName +
                " requester=" + requesterName +
                " newOwner=" + newOwnerName +
                " amOwner=" + Networking.IsOwner(gameObject));

        if (Networking.IsOwner(gameObject))
        {
            ForcePublishAuthoritativeState();
        }

        Debug.Log("[SimManager] OnOwnershipRequest: approving transfer.");
        return true;
    }

    public bool IsSimOwner()
    {
        return Networking.IsOwner(gameObject);
    }

    /// <summary>
    /// Called by the LOCAL requester to ask for sim ownership.
    /// This does NOT guarantee success; it triggers OnOwnershipRequest on the current owner.
    /// </summary>
    public void BeginOwnershipTransfer()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null)
        {
            Debug.Log("[SimManager] BeginOwnershipTransfer: local player null.");
            return;
        }

        bool alreadyOwner = Networking.IsOwner(gameObject);
        Debug.Log("[SimManager] BeginOwnershipTransfer: local=" + local.displayName +
                " alreadyOwner=" + alreadyOwner +
                " managerOwner=" + Networking.GetOwner(gameObject).displayName);

        if (alreadyOwner) return;

        Networking.SetOwner(local, gameObject);

        Debug.Log("[SimManager] BeginOwnershipTransfer: SetOwner called.");
    }

    /// <summary>
    /// Current manager owner publishes the latest authoritative state before approving handoff.
    /// </summary>
    public void ForcePublishAuthoritativeState()
    {
        if (!Networking.IsOwner(gameObject)) return;

        // if (clock != null)
            // clock.PublishEpochNow();

        if (netCore != null)
            netCore.ForcePublishCore();

        byte mode = (netCore != null) ? netCore.mode : MODE_RAILS;

        if (mode == MODE_INTEGRATED)
        {
            if (netKin != null)
            {
                netKin.currentOwnerSimT = _simT;
                netKin.ForcePublishKinematics();
            }
        }
        else
        {
            if (netConic != null)
                netConic.ForcePublishConic();
        }

        if (netAtt != null)
            netAtt.ForcePublishAttitude();
    }

    /// <summary>
    /// New manager owner pulls subordinate sync objects under the same owner.
    /// </summary>
    private void TransferSubordinateOwnershipsToLocal()
    {
        if (!Networking.IsOwner(gameObject)) return;

        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (ownershipObjects == null) return;

        int n = ownershipObjects.Length;
        for (int i = 0; i < n; i++)
        {
            GameObject go = ownershipObjects[i];
            if (go == null) continue;
            if (go == gameObject) continue;

            if (!Networking.IsOwner(go))
                Networking.SetOwner(local, go);
        }
    }

    // -------------------------------------------------------------------------
    // Rails / SOI helpers
    // -------------------------------------------------------------------------

    private void TryHandleSOISwitchRails(double T)
    {
        if (soiSwitch == null) return;
        if (netCore == null) return;
        if (!Networking.IsOwner(gameObject)) return;

        byte newPrimary;
        double dMoon, rSOI;

        bool wants = soiSwitch.Evaluate(out newPrimary, out dMoon, out rSOI);
        if (!wants) return;

        byte oldPrimary = craft != null ? craft.primaryBodyId : (byte)0;

        if (craft != null)
            craft.primaryBodyId = newPrimary;

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

    // -------------------------------------------------------------------------
    // Restart helpers
    // -------------------------------------------------------------------------

    public void SetResetLockByMaster(bool locked)
    {
        if (!Networking.IsMaster) return;
        resetLockedByMaster = locked;
    }

    public void RestartToScenarioIndex(int scenarioIndex)
    {
        RestartToScenarioIndex_Internal(scenarioIndex, false);
    }

    private void RestartToScenarioIndex_Internal(int scenarioIndex, bool ignoreResetPermission)
    {
        if (!ignoreResetPermission && !CanLocalUserReset()) return;
        if (isRestarting) return;

        isRestarting = true;

        BeginRestartTransaction();

        bool ok = ApplyAuthoredScenario(scenarioIndex);

        EndRestartTransaction(ok);
    }

    private void BeginRestartTransaction()
    {
        paused = true;

        _settleAccum = 0f;
        _simT = 0.0;
        _simTValid = false;
        _accumSim = 0.0;

        if (clock != null)
            clock.ResetScenarioTime(0.0, 1.0);

        if (netAtt != null)
            netAtt.ResetPresentationState();

        if (netCore != null)
            netCore.ResetPresentationState();

        if (netKin != null)
            netKin.ResetPresentationState();

        if (dock != null)
            dock.ResetState();

        if (dockingComp != null)
        {
            dockingComp.requestUndock = false;
            dockingComp.requestLeaveDockedToRails = false;
            dockingComp.requestEnterDocked = false;
        }

        if (gcCore != null)
            gcCore.ResetForScenario(0.0);
        else if (gcRuntime != null)
            gcRuntime.ResetState(0.0);

        if (nodePlan != null)
            nodePlan.ClearAll();

        if (gcRuntimeNet != null)
            gcRuntimeNet.ResetPresentationState();

        if (nodePlanNet != null)
            nodePlanNet.ResetPresentationState();

        if (contactsState != null)
        {
            contactsState.ClearFull();
            contactsState.selectedStationIndex = -1;
            contactsState.selectedStationDockPortIndex = 0;
            contactsState.selectedCraftDockPortIndex = 0;
            contactsState.selValid = false;
        }
    }

    private bool ApplyAuthoredScenario(int scenarioIndex)
    {
        if (scenarioInitializer == null) return false;
        return scenarioInitializer.ApplyScenarioByIndex(scenarioIndex, 0.0);
    }

    private void EndRestartTransaction(bool ok)
    {
        
        if (ok)
        {
            if (netCore != null)
                netCore.ResetSyncedStateFromCurrent();

            if (netAtt != null)
                netAtt.ResetSyncedStateFromCurrent();

            if (gcRuntimeNet != null)
                gcRuntimeNet.ResetSyncedStateFromCurrent();

            if (nodePlanNet != null)
                nodePlanNet.ResetSyncedStateFromCurrent();

            if (gcRuntimeNet != null)
                gcRuntimeNet.ForcePublish();

            if (nodePlanNet != null)
                nodePlanNet.ForcePublish();

            if (netKin != null)
            {
                netKin.currentOwnerSimT = 0.0;
                netKin.ResetSyncedStateFromCurrent();
            }

            ForcePublishAuthoritativeState();
        }

        paused = false;
        isRestarting = false;
    }


    // -------------------------------------------------------------------------
    // Small helpers
    // -------------------------------------------------------------------------

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

    public bool CanLocalUserReset()
    {
        if (!Networking.IsOwner(gameObject)) return false;
        if (resetLockedByMaster && !Networking.IsMaster) return false;
        return true;
    }
    private void ApplyRender()
    {
        if (orbit != null) orbit.Evaluate();
        if (primaryDiag != null) primaryDiag.Evaluate();
        if (orbitline != null) orbitline.Apply();

        if (skyrender != null)
            skyrender.Tick();

        if (stationRender != null)
            stationRender.Tick();

        if (stewartResolver != null)
            stewartResolver.Tick();
    }
}