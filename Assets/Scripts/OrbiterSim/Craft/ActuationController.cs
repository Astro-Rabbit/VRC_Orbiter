using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// ActuationController (V3.0 - RCS smart allocator + anti-spam)
/// - Wheels (torque clamp)
/// - Main engines: thrust + fuel mdot
/// - Gimbal:
///     * MANUAL: applies pilot gimbal to thrust direction, does NOT participate in attitude allocation
///     * AUTO:   uses attitude torque request to compute a SINGLE symmetric yaw/pitch for all running gimballed engines
/// - RCS:
///     * Unified wrench allocator (force+torque cross-control), top-M jets per tick
///     * COLD/HOT latch with hysteresis based on combined cold capability
///     * Anti-spam:
///         - Channel engagement hysteresis + minimum dwell time
///         - Per-jet min ON time (hold) + per-jet min OFF time (cooldown), separate cold vs hot
///
/// Outputs (unchanged):
/// - F_E (N, inertial)
/// - Tau_B (Nm, body)
/// - rcsFire01[] for VFX
/// - mainMdot_kgps (kg/s) for SimManager to subtract prop
/// - mainGimbalYawDeg[] / mainGimbalPitchDeg[] for anim/VFX/debug
///
/// Requirements:
/// - ThrusterCatalog must cache:
///     rcsDir_B[], rcsPosRelCg_B[], rcsCached[]
///     rcsTauPerNewton_B[] and rcsTauPerNewtonMag[]   (tauPerN = r x dir; Nm per N)
/// </summary>
public class ActuationController : UdonSharpBehaviour
{
    [Header("References")]
    public CraftCommandState cmd;
    public CraftAttitudeState attState;
    public AttitudeControllerPD attController;   // provides tauCmd_B
    public ThrusterCatalog catalog;

    [Tooltip("Optional: used to gate fuel-consuming engines when prop is empty.")]
    public CraftStateModel craft;

    [Header("Effects Sync (optional)")]
    public EffectsSyncState effectsSync;

    // NOTE: translateCmd_B is now interpreted as Newtons in BODY frame (already scaled by UI/GC).
    // Keeping this field for backwards compatibility / inspector sanity, but it is NOT used.
    [Header("Translation scaling (legacy; unused)")]
    public float maxTranslateForceN = 50f;

    [Header("Deadbands (legacy; still used for basic 'wanted' booleans)")]
    [Tooltip("If |forceCmd_B| < this, translation RCS will not engage (subject to engage hysteresis).")]
    public float forceDeadbandN = 0.25f;

    [Tooltip("If |tauCmd_B| < this, attitude RCS will not engage (subject to engage hysteresis).")]
    public float torqueDeadbandNm = 0.25f;

    [Header("Gimbal AUTO stability")]
    [Tooltip("If |tauRemaining_B| below this, AUTO gimbal outputs stay at 0 (prevents chasing tiny residuals).")]
    public float gimbalTorqueDeadbandNm = 5f;

    [Tooltip("Reject summed gimbal bases whose magnitude is below (basisMinFrac * sum(rF)). Prevents divide-by-tiny.")]
    [Range(0f, 1f)] public float gimbalBasisMinFrac = 1e-4f;

    [Tooltip("Optional: limit how fast gimbal angles can change (deg/s). 0 = no rate limit.")]
    public float gimbalMaxRateDegPerSec = 60f;

    // -----------------------------
    // NEW: RCS anti-spam + policy
    // -----------------------------

    [Header("RCS Blend Policy (NEW)")]
    [Tooltip("0=ATT_FIRST, 1=TRANS_FIRST, 2=BALANCED (used only when cmd.rcsMode == BLENDED).")]
    public byte rcsBlendPolicy = 0;

    [Header("RCS Engage Hysteresis (NEW)")]
    [Tooltip("Attitude engage threshold (Nm). If |tauRemaining| exceeds this, engage attitude RCS.")]
    public float attEngageHiNm = 10f;

    [Tooltip("Attitude disengage threshold (Nm). Must be < engageHi.")]
    public float attEngageLoNm = 5f;

    [Tooltip("Translation engage threshold (N). If |forceCmd| exceeds this, engage translation RCS.")]
    public float transEngageHiN = 1.0f;

    [Tooltip("Translation disengage threshold (N). Must be < engageHi.")]
    public float transEngageLoN = 0.5f;

    [Tooltip("Minimum time to stay engaged once engaged (seconds).")]
    public float rcsMinEngageTimeAtt = 0.30f;

    [Tooltip("Minimum time to stay engaged once engaged (seconds).")]
    public float rcsMinEngageTimeTrans = 0.18f;

    [Header("Cold/Hot hysteresis (NEW)")]
    [Tooltip("Switch COLD->HOT if request > coldCapability * this.")]
    [Range(0.5f, 1.5f)] public float coldToHotHiFrac = 0.95f;

    [Tooltip("Switch HOT->COLD if request < coldCapability * this.")]
    [Range(0.0f, 1.0f)] public float hotToColdLoFrac = 0.75f;

    [Header("Cold/Hot timing (NEW)")]
    public float minRcsOffTimeCold = 0.02f;
    public float minRcsOffTimeHot  = 0.06f;

    [Tooltip("Minimum ON time once a jet starts firing (prevents flicker).")]
    public float minRcsOnTimeCold = 0.05f;

    [Tooltip("Minimum ON time once a jet starts firing (prevents flicker).")]
    public float minRcsOnTimeHot = 0.08f;

    [Header("RCS Selection (NEW)")]
    [Tooltip("Max RCS jets to fire per tick (hard cap).")]
    public int rcsMaxJetsPerTick = 8;

    [Tooltip("Eligibility alignment threshold for translation (0..1).")]
    [Range(0f, 1f)] public float transAlignElig = 0.30f;

    [Tooltip("Eligibility alignment threshold for attitude via torque axis (0..1).")]
    [Range(0f, 1f)] public float attAlignElig = 0.20f;

    [Header("Optional PWM toggles (GC use; not wired yet)")]
    public bool rcsPwmEnableAtt = false;
    public bool rcsPwmEnableTrans = false;

    // -----------------------------
    // Outputs
    // -----------------------------
    [Header("Outputs")]
    [Tooltip("Net force in INERTIAL frame (N).")]
    public Vector3 F_E = Vector3.zero;

    [Tooltip("Net torque in BODY frame (Nm).")]
    public Vector3 Tau_B = Vector3.zero;

    // Convenience doubles for integrator / mode switching
    public double Fx, Fy, Fz;

    [Header("Fuel flow (output)")]
    [Tooltip("Main-engine propellant mass flow (kg/s). SimManager should subtract mdot * dt from craft.propMassKg.")]
    public double mainMdot_kgps = 0.0;

    [Header("Per-thruster outputs (for animation/VFX)")]
    [Tooltip("RCS fire level per jet: 0=OFF, coldScale=COLD, 1=HOT.")]
    public float[] rcsFire01;

    [Header("Main gimbal outputs (for animation/VFX/debug)")]
    public float[] mainGimbalYawDeg;
    public float[] mainGimbalPitchDeg;

    [Tooltip("If true, owner packs rcsFire01[] into bitmasks for remote VFX.")]
    public bool syncRcsMasks = true;

    [Tooltip("Treat values >= this as HIGH.")]
    public float rcsHighThreshold = 0.95f;

    [Tooltip("Treat values > 0 and < highThreshold as LOW.")]
    public float rcsLowThreshold = 1e-4f;

    // -----------------------------
    // Internal scratch
    // -----------------------------
    private Vector3 _forceCmd_B;
    private Vector3 _tauReq_B;

    // Per-jet gating
    private float[] _rcsCooldownUntil; // earliest time we may START firing (if not held-on)
    private float[] _rcsHoldOnUntil;   // if now < holdUntil and prevLevel>0, we MUST keep firing (min ON)
    private float[] _rcsPrevLevel;     // 0, coldScale, or 1

    // Channel engagement latches
    private bool _attRcsEngaged = false;
    private bool _transRcsEngaged = false;
    private float _attEngagedUntil = 0f;
    private float _transEngagedUntil = 0f;

    // Cold/Hot latches
    private bool _attHotLatched = false;
    private bool _transHotLatched = false;

    // Gimbal rate limiting state (single shared yaw/pitch for symmetric AUTO)
    private float _autoYawDegPrev = 0f;
    private float _autoPitchDegPrev = 0f;

    private const float G0 = 9.80665f;

    public void Evaluate()
    {
        F_E = Vector3.zero;
        Tau_B = Vector3.zero;
        Fx = Fy = Fz = 0.0;
        mainMdot_kgps = 0.0;

        if (cmd == null || attState == null || catalog == null) return;

        EnsureRcsArray();
        EnsureRcsGateArrays();
        ClearRcsFires();
        EnsureMainGimbalArrays();

        // ---- attitude torque request source ----
        // Safety: honor TORQUE_DIRECT explicitly so the actuator can't be "surprised".
        if (cmd.attitudeCmdMode == CraftCommandState.ATT_CMD_TORQUE_DIRECT)
        {
            _tauReq_B = cmd.tauDirect_B;
        }
        else
        {
            _tauReq_B = Vector3.zero;
            if (attController != null) _tauReq_B = attController.tauCmd_B;
        }

        // ---- translation force request (BODY, Newtons) ----
        _forceCmd_B = cmd.translateCmd_B;

        // ---- is any attitude torque actually desired? ----
        bool attitudeTorqueWanted = _tauReq_B.sqrMagnitude > (torqueDeadbandNm * torqueDeadbandNm);

        // -----------------------------
        // Attitude torque allocation
        // -----------------------------
        Vector3 tauRemaining_B = _tauReq_B;

        // --- wheels ---
        bool wheelsAllowed = attitudeTorqueWanted &&
                             cmd.allowWheels &&
                             (catalog.wheelMaxTorqueNm > 0f) &&
                             (cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_WHEELS_ONLY ||
                              cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_AUTO);

        if (wheelsAllowed)
        {
            Vector3 tauWheel_B = ClampMagnitude(tauRemaining_B, catalog.wheelMaxTorqueNm);
            Tau_B += tauWheel_B;
            tauRemaining_B -= tauWheel_B;
        }

        // -----------------------------
        // Mains: thrust always; gimbal may steer thrust
        // Gimbal-for-attitude only in AUTO (and only if attitude torque wanted and mode allows it)
        // -----------------------------
        float throttle01 = Mathf.Clamp01(cmd.mainThrottle01);
        bool enginesRunningGlobal = throttle01 > 1e-6f;

        bool gimbalEnabled = cmd.allowGimbal; // physical gimbal allowed at all

        bool gimbalAllowedByActuatorMode =
            (cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_GIMBAL_ONLY ||
             cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_AUTO);

        bool autoMode = (cmd.gimbalMode == CraftCommandState.GIMBAL_MODE_AUTO_TORQUE);
        bool manualMode = (cmd.gimbalMode == CraftCommandState.GIMBAL_MODE_MANUAL_INPUT);

        bool useGimbalForAttitude = enginesRunningGlobal &&
                                    gimbalEnabled &&
                                    gimbalAllowedByActuatorMode &&
                                    autoMode &&
                                    attitudeTorqueWanted;

        if (enginesRunningGlobal)
        {
            Vector3 tauFromMains_B = AllocateMains(
                throttle01,
                useGimbalForAttitude ? tauRemaining_B : Vector3.zero,
                gimbalEnabled,
                useGimbalForAttitude,
                manualMode
            );

            // Only subtract mains torque from remaining if we were using AUTO gimbal as an attitude actuator.
            if (useGimbalForAttitude)
                tauRemaining_B -= tauFromMains_B;
        }
        else
        {
            ClearMainGimbals();
            _autoYawDegPrev = 0f;
            _autoPitchDegPrev = 0f;
        }

        // -----------------------------
        // RCS allocation (unified wrench allocator)
        // -----------------------------
        int nRcs = (catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;

        bool rcsAvail =
            cmd.allowRCS &&
            (nRcs > 0) &&
            (catalog.rcsCached != null) &&
            (catalog.rcsDir_B != null) &&
            (catalog.rcsPosRelCg_B != null) &&
            (catalog.rcsTauPerNewton_B != null) &&
            (catalog.rcsTauPerNewtonMag != null);

        // Attitude RCS is only used if (a) attitude torque is desired, and (b) actuator policy allows it.
        bool allowRcsForAtt = rcsAvail &&
                              attitudeTorqueWanted &&
                              (cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_RCS_ONLY ||
                               cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_AUTO);

        // If user selected GIMBAL_ONLY, we do NOT use RCS for attitude torque.
        if (cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_GIMBAL_ONLY)
            allowRcsForAtt = false;

        // Translation RCS remains independent of attitudeActuatorMode
        bool allowRcsForTrans = rcsAvail;

        if (rcsAvail)
        {
            AllocateRcsUnified(_forceCmd_B, tauRemaining_B, cmd.rcsMode, allowRcsForTrans, allowRcsForAtt);
        }

        Fx = (double)F_E.x;
        Fy = (double)F_E.y;
        Fz = (double)F_E.z;

        if (syncRcsMasks && effectsSync != null && Networking.IsOwner(effectsSync.gameObject))
        {
            uint hi, lo;
            PackRcsMasks(out hi, out lo);
            effectsSync.SetRcsMasks(hi, lo);
        }

        // ---------------- MAIN VFX SYNC (owner-only) ----------------
        if (effectsSync != null && Networking.IsOwner(effectsSync.gameObject))
        {
            float t01 = Mathf.Clamp01(cmd != null ? cmd.mainThrottle01 : 0f);

            // Shared yaw/pitch: symmetric gimbal, use engine[0] if available.
            float yawDeg = 0f;
            float pitchDeg = 0f;
            if (mainGimbalYawDeg != null && mainGimbalYawDeg.Length > 0) yawDeg = mainGimbalYawDeg[0];
            if (mainGimbalPitchDeg != null && mainGimbalPitchDeg.Length > 0) pitchDeg = mainGimbalPitchDeg[0];

            uint onMask = 0u;

            int nMain = (catalog != null && catalog.mainTf != null) ? catalog.mainTf.Length : 0;
            int limit = (nMain > 32) ? 32 : nMain;

            bool anyThrottle = t01 > 1e-6f;

            if (anyThrottle && catalog != null)
            {
                for (int i = 0; i < limit; i++)
                {
                    float maxF = catalog.GetMainMaxForceN(i);
                    if (maxF <= 0f) continue;

                    float isp = catalog.GetMainIspSec(i);
                    bool consumesProp = isp > 1e-6f;

                    if (consumesProp && craft != null && craft.propMassKg <= 0.0) continue;

                    onMask |= (1u << i);
                }
            }

            effectsSync.SetMainVfx(t01, yawDeg, pitchDeg, onMask);

            effectsSync.SetCommandReadout(_tauReq_B, _forceCmd_B);

        }
    }

    /// <summary>
    /// Unified RCS allocator with anti-spam gating and cross-control.
    /// Selects up to rcsMaxJetsPerTick jets, and fires them at either COLD (rcsLowScale) or HOT (1.0).
    /// </summary>
    private void AllocateRcsUnified(Vector3 forceCmd_B, Vector3 tauCmd_B, byte rcsMode, bool allowTrans, bool allowAtt)
    {
        int n = (catalog != null && catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (n <= 0) return;

        float now = Time.time;

        // -------------
        // Build residuals subject to mode + allow flags
        // -------------
        Vector3 fRes_B = Vector3.zero;
        Vector3 tRes_B = Vector3.zero;

        if (allowTrans && (rcsMode == CraftCommandState.RCS_MODE_TRANSLATE || rcsMode == CraftCommandState.RCS_MODE_BLENDED))
            fRes_B = forceCmd_B;

        if (allowAtt && (rcsMode == CraftCommandState.RCS_MODE_ROTATE || rcsMode == CraftCommandState.RCS_MODE_BLENDED))
            tRes_B = tauCmd_B;

        float fMag = fRes_B.magnitude;
        float tMag = tRes_B.magnitude;

        // Basic deadband guard (still useful), but engagement hysteresis dominates.
        bool transWanted = fMag >= forceDeadbandN;
        bool attWanted = tMag >= torqueDeadbandNm;

        // -------------
        // Channel engagement latches (anti-spam)
        // -------------
        // Translation engage/disengage
        if (!_transRcsEngaged)
        {
            if (transWanted && fMag >= transEngageHiN)
            {
                _transRcsEngaged = true;
                _transEngagedUntil = now + rcsMinEngageTimeTrans;
            }
        }
        else
        {
            if (now >= _transEngagedUntil && (!transWanted || fMag <= transEngageLoN))
                _transRcsEngaged = false;
        }

        // Attitude engage/disengage
        if (!_attRcsEngaged)
        {
            if (attWanted && tMag >= attEngageHiNm)
            {
                _attRcsEngaged = true;
                _attEngagedUntil = now + rcsMinEngageTimeAtt;
            }
        }
        else
        {
            if (now >= _attEngagedUntil && (!attWanted || tMag <= attEngageLoNm))
                _attRcsEngaged = false;
        }

        // Apply engagement results
        if (!_transRcsEngaged) fRes_B = Vector3.zero;
        if (!_attRcsEngaged)   tRes_B = Vector3.zero;

        fMag = fRes_B.magnitude;
        tMag = tRes_B.magnitude;

        if (fMag < 1e-6f && tMag < 1e-6f)
        {
            // Nothing to do. We intentionally do NOT force jets off here;
            // per-jet hold will expire and then cooldown will apply naturally.
            UpdateJetTurnOffs(now);
            return;
        }

        Vector3 fHat = (fMag > 1e-6f) ? (fRes_B / fMag) : Vector3.forward;
        Vector3 tHat = (tMag > 1e-6f) ? (tRes_B / tMag) : Vector3.up;

        // -------------
        // Cold capability estimates (ignore cooldown to prevent mode-flip due to a cooling jet)
        // -------------
        bool hasCold = catalog.rcsHasLowMode && (catalog.rcsLowScale > 1e-6f);
        float coldScale = hasCold ? catalog.rcsLowScale : 1f;

        float capColdForce = 0f; // N along fHat
        float capColdTau   = 0f; // Nm along tHat

        if (hasCold)
        {
            for (int i = 0; i < n; i++)
            {
                if (!catalog.rcsCached[i]) continue;

                float maxFhot = catalog.GetRcsMaxForceN(i);
                if (maxFhot <= 0f) continue;

                float Fcold = maxFhot * coldScale;

                if (fMag > 1e-6f)
                {
                    float a = Vector3.Dot(catalog.rcsDir_B[i], fHat);
                    if (a >= transAlignElig) capColdForce += (Fcold * a);
                }

                if (tMag > 1e-6f)
                {
                    float projPerN = Vector3.Dot(catalog.rcsTauPerNewton_B[i], tHat); // Nm per N projected
                    if (projPerN > 0f)
                    {
                        float alignTau = projPerN / Mathf.Max(1e-6f, catalog.rcsTauPerNewtonMag[i]);
                        if (alignTau >= attAlignElig) capColdTau += (projPerN * Fcold);
                    }
                }
            }

            // Translation latch
            if (!_transHotLatched)
            {
                if (fMag > 1e-6f && capColdForce > 1e-6f && fMag > capColdForce * coldToHotHiFrac)
                    _transHotLatched = true;
            }
            else
            {
                if (fMag < 1e-6f || capColdForce < 1e-6f || fMag < capColdForce * hotToColdLoFrac)
                    _transHotLatched = false;
            }

            // Attitude latch
            if (!_attHotLatched)
            {
                if (tMag > 1e-6f && capColdTau > 1e-6f && tMag > capColdTau * coldToHotHiFrac)
                    _attHotLatched = true;
            }
            else
            {
                if (tMag < 1e-6f || capColdTau < 1e-6f || tMag < capColdTau * hotToColdLoFrac)
                    _attHotLatched = false;
            }
        }
        else
        {
            // No cold mode available => always hot
            _transHotLatched = true;
            _attHotLatched = true;
        }

        // If either channel needs HOT this tick, run jets HOT (simple + stable).
        bool useHotThisTick = _transHotLatched || _attHotLatched;
        float levelBase = useHotThisTick ? 1f : coldScale;

        // -------------
        // Blend weights (att-first/trans-first/balanced)
        // -------------
        float wf = 0f, wt = 0f;
        if (rcsMode == CraftCommandState.RCS_MODE_BLENDED)
        {
            if (rcsBlendPolicy == 0) { wt = 1.0f; wf = 0.35f; }      // ATT_FIRST
            else if (rcsBlendPolicy == 1) { wf = 1.0f; wt = 0.35f; } // TRANS_FIRST
            else { wf = 0.7f; wt = 0.7f; }                           // BALANCED
        }
        else if (rcsMode == CraftCommandState.RCS_MODE_TRANSLATE)
        {
            wf = 1f; wt = 0f;
        }
        else // ROTATE
        {
            wf = 0f; wt = 1f;
        }

        int M = Mathf.Clamp(rcsMaxJetsPerTick, 1, 32);

        // Buffers for top-M
        int[] bestIdx = new int[32];     // Udon note: fixed size avoids realloc risk; we only use [0..M)
        float[] bestScore = new float[32];
        for (int k = 0; k < 32; k++) { bestIdx[k] = -1; bestScore[k] = 0f; }

        // -------------
        // 1) Enforce per-jet min ON holds: if a jet is within its hold window, KEEP IT FIRING.
        //    This is the most important anti-flicker piece.
        // -------------
        Quaternion qBE = attState.qBE;

        int forcedCount = 0;
        for (int i = 0; i < n && forcedCount < M; i++)
        {
            float prev = (_rcsPrevLevel != null && i < _rcsPrevLevel.Length) ? _rcsPrevLevel[i] : 0f;
            if (prev <= 0f) continue;

            float holdUntil = (_rcsHoldOnUntil != null && i < _rcsHoldOnUntil.Length) ? _rcsHoldOnUntil[i] : 0f;
            if (now >= holdUntil) continue;

            // Keep firing during hold. Use the max of (previous level, current global base level).
            float level = (prev >= 0.95f || levelBase >= 0.95f) ? 1f : coldScale;

            // Fire it
            FireJet(i, level, qBE, now);

            bestIdx[forcedCount] = i;  // mark selected to avoid duplicates
            bestScore[forcedCount] = float.PositiveInfinity;
            forcedCount++;
        }

        // -------------
        // 2) Score remaining jets and fill remaining slots up to M
        // -------------
        for (int i = 0; i < n; i++)
        {
            if (!catalog.rcsCached[i]) continue;

            // Skip if already selected (forced or previously inserted)
            bool already = false;
            for (int k = 0; k < forcedCount; k++) { if (bestIdx[k] == i) { already = true; break; } }
            if (already) continue;

            // Must be allowed to start (cooldown) if not held
            if (!CanStartJet(i, now)) continue;

            float maxFhot = catalog.GetRcsMaxForceN(i);
            if (maxFhot <= 0f) continue;

            float F = maxFhot * levelBase;

            float sf = 0f, st = 0f;

            // Force help (either)
            if (fMag > 1e-6f && wf > 0f)
            {
                float a = Vector3.Dot(catalog.rcsDir_B[i], fHat);
                if (a > 0f && a >= transAlignElig) sf = a * F; // N projected
            }

            // Torque help (either)
            if (tMag > 1e-6f && wt > 0f)
            {
                float projPerN = Vector3.Dot(catalog.rcsTauPerNewton_B[i], tHat); // Nm per N projected
                if (projPerN > 0f)
                {
                    float alignTau = projPerN / Mathf.Max(1e-6f, catalog.rcsTauPerNewtonMag[i]);
                    if (alignTau >= attAlignElig) st = projPerN * F; // Nm projected
                }
            }

            float score = wf * sf + wt * st;
            if (score <= 0f) continue;

            // Insert into best list starting at forcedCount (top-M insertion)
            for (int k = forcedCount; k < M; k++)
            {
                if (score > bestScore[k])
                {
                    for (int s = M - 1; s > k; s--)
                    {
                        bestScore[s] = bestScore[s - 1];
                        bestIdx[s] = bestIdx[s - 1];
                    }
                    bestScore[k] = score;
                    bestIdx[k] = i;
                    break;
                }
            }
        }

        // -------------
        // 3) Fire selected (excluding forced already fired)
        // -------------
        for (int k = forcedCount; k < M; k++)
        {
            int i = bestIdx[k];
            if (i < 0) continue;

            // Might have become "not startable" due to earlier selections? (shouldn't)
            if (!CanStartJet(i, now)) continue;

            float level = levelBase;
            FireJet(i, level, qBE, now);
        }

        // -------------
        // 4) Turn-off bookkeeping (min ON + cooldown scheduling)
        // -------------
        UpdateJetTurnOffs(now);
    }

    private void FireJet(int i, float level, Quaternion qBE, float now)
    {
        int n = (catalog != null && catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (i < 0 || i >= n) return;

        float maxFhot = catalog.GetRcsMaxForceN(i);
        if (maxFhot <= 0f) return;

        float F = maxFhot * level;

        Vector3 f_B = catalog.rcsDir_B[i] * F;
        Vector3 f_E = qBE * f_B;

        F_E += f_E;
        Tau_B += catalog.rcsTauPerNewton_B[i] * F;

        if (rcsFire01 != null && i < rcsFire01.Length)
            rcsFire01[i] = level;

        MarkJetFired(i, level, now);
    }

    private bool CanStartJet(int i, float now)
    {
        if (_rcsCooldownUntil == null || i < 0 || i >= _rcsCooldownUntil.Length) return true;
        return now >= _rcsCooldownUntil[i];
    }

    private void MarkJetFired(int i, float level, float now)
    {
        if (_rcsPrevLevel == null || _rcsHoldOnUntil == null) return;
        if (i < 0 || i >= _rcsPrevLevel.Length || i >= _rcsHoldOnUntil.Length) return;

        bool hot = level >= 0.95f;

        bool wasOn = _rcsPrevLevel[i] > 0f;

        // If starting from OFF, set min ON hold window
        if (!wasOn)
        {
            float minOn = hot ? minRcsOnTimeHot : minRcsOnTimeCold;
            if (minOn > 0f) _rcsHoldOnUntil[i] = now + minOn;
        }
        else
        {
            // If already on, keep extending hold slightly? NO.
            // Do not extend; prevents "stuck on" under noisy commands.
        }

        _rcsPrevLevel[i] = hot ? 1f : catalog.rcsLowScale;
    }

    private void UpdateJetTurnOffs(float now)
    {
        if (rcsFire01 == null || _rcsPrevLevel == null || _rcsCooldownUntil == null || _rcsHoldOnUntil == null) return;

        int n = rcsFire01.Length;

        for (int i = 0; i < n; i++)
        {
            bool firedThisTick = rcsFire01[i] > 0f;
            bool wasOn = _rcsPrevLevel[i] > 0f;

            if (wasOn && !firedThisTick)
            {
                // If still within mandatory ON hold window, we should have fired it.
                // But if we didn't (e.g., M too small), refuse to schedule cooldown or drop state yet.
                if (now < _rcsHoldOnUntil[i])
                {
                    // Keep previous level "on" so we continue forcing it next tick.
                    continue;
                }

                // Hold expired and it is not firing now => turn off and schedule cooldown based on previous level
                bool hotWas = _rcsPrevLevel[i] >= 0.95f;

                float off = hotWas ? minRcsOffTimeHot : minRcsOffTimeCold;
                if (off > 0f) _rcsCooldownUntil[i] = now + off;

                _rcsPrevLevel[i] = 0f;
            }
            else if (!wasOn && firedThisTick)
            {
                // Should not happen because MarkJetFired sets prevLevel, but be safe:
                _rcsPrevLevel[i] = (rcsFire01[i] >= 0.95f) ? 1f : catalog.rcsLowScale;
            }
            else if (wasOn && firedThisTick)
            {
                // keep state consistent
                _rcsPrevLevel[i] = (rcsFire01[i] >= 0.95f) ? 1f : catalog.rcsLowScale;
            }
        }
    }

    /// <summary>
    /// Apply main-engine thrust to F_E and Tau_B.
    /// - Thrust is always applied when running.
    /// - If gimbalEnabled:
    ///     * MANUAL: uses cmd.gimbalPitchYawCmd for all gimballed engines.
    ///     * AUTO: computes ONE symmetric yaw/pitch from summed bases (only if useGimbalForAttitude).
    /// Returns torque (BODY) from mains as actually applied.
    /// </summary>
    private Vector3 AllocateMains(float throttle01, Vector3 tauTarget_B,
                                 bool gimbalEnabled, bool useGimbalForAttitude, bool manualMode)
    {
        Vector3 tauFromMains_B = Vector3.zero;

        if (catalog.mainTf == null || catalog.mainTf.Length == 0) return Vector3.zero;
        if (catalog.mainCached == null || catalog.mainDir_B == null || catalog.mainPosRelCg_B == null) return Vector3.zero;

        Quaternion qBE = attState.qBE;
        int n = catalog.mainTf.Length;

        float sharedYawDeg = 0f;
        float sharedPitchDeg = 0f;

        if (gimbalEnabled && useGimbalForAttitude)
        {
            if (tauTarget_B.sqrMagnitude >= (gimbalTorqueDeadbandNm * gimbalTorqueDeadbandNm))
            {
                Vector3 bPitchSum = Vector3.zero;
                Vector3 bYawSum = Vector3.zero;
                float expectedSum = 0f;
                float maxGimbalDegMin = 1e9f;

                for (int i = 0; i < n; i++)
                {
                    if (catalog.mainCached == null || i >= catalog.mainCached.Length || !catalog.mainCached[i]) continue;

                    float maxF = catalog.GetMainMaxForceN(i);
                    if (maxF <= 0f) continue;

                    float F = maxF * throttle01;
                    if (F <= 0f) continue;

                    bool hasGimbal = catalog.GetMainHasGimbal(i);
                    float maxGimbalDeg = catalog.GetMainMaxGimbalDeg(i);
                    if (!hasGimbal || maxGimbalDeg <= 1e-4f) continue;

                    float isp = catalog.GetMainIspSec(i);
                    bool consumesProp = isp > 1e-6f;
                    if (consumesProp && craft != null && craft.propMassKg <= 0.0) continue;

                    Vector3 r_B = catalog.mainPosRelCg_B[i];
                    Vector3 d0_B = catalog.mainDir_B[i];

                    Vector3 aPitch = catalog.mainGimbalPitchAxis_B[i];
                    Vector3 aYaw = catalog.mainGimbalYawAxis_B[i];

                    Vector3 vPitch = Vector3.Cross(aPitch, d0_B);
                    Vector3 vYaw = Vector3.Cross(aYaw, d0_B);

                    Vector3 bPitch = Vector3.Cross(r_B, vPitch * F);
                    Vector3 bYaw = Vector3.Cross(r_B, vYaw * F);

                    bPitchSum += bPitch;
                    bYawSum += bYaw;

                    expectedSum += (r_B.magnitude * F);

                    if (maxGimbalDeg < maxGimbalDegMin) maxGimbalDegMin = maxGimbalDeg;
                }

                if (expectedSum > 1e-3f && maxGimbalDegMin < 1e8f)
                {
                    float minBasis = expectedSum * gimbalBasisMinFrac;

                    float bpMag = bPitchSum.magnitude;
                    float byMag = bYawSum.magnitude;

                    float maxRad = maxGimbalDegMin * Mathf.Deg2Rad;

                    float pitchRad = 0f;
                    float yawRad = 0f;

                    if (bpMag >= minBasis)
                    {
                        float bp2 = bpMag * bpMag;
                        pitchRad = Mathf.Clamp(Vector3.Dot(tauTarget_B, bPitchSum) / bp2, -maxRad, maxRad);
                    }

                    if (byMag >= minBasis)
                    {
                        float by2 = byMag * byMag;
                        yawRad = Mathf.Clamp(Vector3.Dot(tauTarget_B, bYawSum) / by2, -maxRad, maxRad);
                    }

                    sharedPitchDeg = pitchRad * Mathf.Rad2Deg;
                    sharedYawDeg = yawRad * Mathf.Rad2Deg;

                    if (gimbalMaxRateDegPerSec > 0f)
                    {
                        float dt = Time.deltaTime;
                        if (dt < 0f) dt = 0f;

                        float maxStep = gimbalMaxRateDegPerSec * dt;

                        sharedYawDeg = Mathf.MoveTowards(_autoYawDegPrev, sharedYawDeg, maxStep);
                        sharedPitchDeg = Mathf.MoveTowards(_autoPitchDegPrev, sharedPitchDeg, maxStep);

                        _autoYawDegPrev = sharedYawDeg;
                        _autoPitchDegPrev = sharedPitchDeg;
                    }
                }
            }
        }
        else
        {
            _autoYawDegPrev = 0f;
            _autoPitchDegPrev = 0f;
        }

        float manualYawDeg = 0f;
        float manualPitchDeg = 0f;
        if (gimbalEnabled && manualMode)
        {
            manualYawDeg = Mathf.Clamp(cmd.gimbalPitchYawCmd.x, -1f, 1f);
            manualPitchDeg = Mathf.Clamp(cmd.gimbalPitchYawCmd.y, -1f, 1f);
        }

        for (int i = 0; i < n; i++)
        {
            if (catalog.mainCached == null || i >= catalog.mainCached.Length || !catalog.mainCached[i]) continue;

            float maxF = catalog.GetMainMaxForceN(i);
            if (maxF <= 0f) continue;

            float isp = catalog.GetMainIspSec(i);
            bool consumesProp = isp > 1e-6f;

            if (consumesProp && craft != null && craft.propMassKg <= 0.0)
            {
                mainGimbalYawDeg[i] = 0f;
                mainGimbalPitchDeg[i] = 0f;
                continue;
            }

            float F = maxF * throttle01;
            if (F <= 0f)
            {
                mainGimbalYawDeg[i] = 0f;
                mainGimbalPitchDeg[i] = 0f;
                continue;
            }

            if (consumesProp)
            {
                double mdot = (double)F / ((double)isp * (double)G0);
                mainMdot_kgps += mdot;
            }

            Vector3 r_B = catalog.mainPosRelCg_B[i];
            Vector3 d0_B = catalog.mainDir_B[i];

            bool hasGimbal = catalog.GetMainHasGimbal(i);
            float maxGimbalDeg = catalog.GetMainMaxGimbalDeg(i);

            float yawDeg = 0f;
            float pitchDeg = 0f;

            Vector3 d_B = d0_B;

            if (gimbalEnabled && hasGimbal && maxGimbalDeg > 1e-4f)
            {
                if (useGimbalForAttitude)
                {
                    yawDeg = Mathf.Clamp(sharedYawDeg, -maxGimbalDeg, maxGimbalDeg);
                    pitchDeg = Mathf.Clamp(sharedPitchDeg, -maxGimbalDeg, maxGimbalDeg);
                }
                else if (manualMode)
                {
                    yawDeg = manualYawDeg * maxGimbalDeg;
                    pitchDeg = manualPitchDeg * maxGimbalDeg;
                }

                Vector3 aYawB = catalog.mainGimbalYawAxis_B[i];
                Vector3 aPitchB = catalog.mainGimbalPitchAxis_B[i];

                Quaternion qYaw = Quaternion.AngleAxis(yawDeg, aYawB);
                Quaternion qPitch = Quaternion.AngleAxis(pitchDeg, aPitchB);

                d_B = qYaw * (qPitch * d0_B);
                if (d_B.sqrMagnitude > 1e-12f) d_B.Normalize();
                else d_B = d0_B;
            }

            mainGimbalYawDeg[i] = yawDeg;
            mainGimbalPitchDeg[i] = pitchDeg;

            Vector3 f_B = d_B * F;

            Vector3 f_E = qBE * f_B;
            F_E += f_E;

            Vector3 tau_i_B = Vector3.Cross(r_B, f_B);
            Tau_B += tau_i_B;
            tauFromMains_B += tau_i_B;
        }

        return tauFromMains_B;
    }

    // ------------------ Arrays / Utilities ------------------

    private void EnsureRcsArray()
    {
        int n = (catalog != null && catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (n <= 0) { rcsFire01 = null; return; }
        if (rcsFire01 == null || rcsFire01.Length != n) rcsFire01 = new float[n];
    }

    private void EnsureRcsGateArrays()
    {
        int n = (catalog != null && catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (n <= 0)
        {
            _rcsCooldownUntil = null;
            _rcsHoldOnUntil = null;
            _rcsPrevLevel = null;
            return;
        }

        if (_rcsCooldownUntil == null || _rcsCooldownUntil.Length != n) _rcsCooldownUntil = new float[n];
        if (_rcsHoldOnUntil == null || _rcsHoldOnUntil.Length != n) _rcsHoldOnUntil = new float[n];
        if (_rcsPrevLevel == null || _rcsPrevLevel.Length != n) _rcsPrevLevel = new float[n];
    }

    private void ClearRcsFires()
    {
        if (rcsFire01 == null) return;
        for (int i = 0; i < rcsFire01.Length; i++) rcsFire01[i] = 0f;
    }

    private void EnsureMainGimbalArrays()
    {
        int n = (catalog != null && catalog.mainTf != null) ? catalog.mainTf.Length : 0;
        if (n <= 0) { mainGimbalYawDeg = null; mainGimbalPitchDeg = null; return; }

        if (mainGimbalYawDeg == null || mainGimbalYawDeg.Length != n) mainGimbalYawDeg = new float[n];
        if (mainGimbalPitchDeg == null || mainGimbalPitchDeg.Length != n) mainGimbalPitchDeg = new float[n];

        for (int i = 0; i < n; i++) { mainGimbalYawDeg[i] = 0f; mainGimbalPitchDeg[i] = 0f; }
    }

    private void ClearMainGimbals()
    {
        if (mainGimbalYawDeg == null || mainGimbalPitchDeg == null) return;
        for (int i = 0; i < mainGimbalYawDeg.Length; i++) { mainGimbalYawDeg[i] = 0f; mainGimbalPitchDeg[i] = 0f; }
    }

    private static Vector3 ClampMagnitude(Vector3 v, float maxMag)
    {
        if (maxMag <= 0f) return Vector3.zero;
        float mag = v.magnitude;
        if (mag <= maxMag) return v;
        if (mag < 1e-9f) return Vector3.zero;
        return v * (maxMag / mag);
    }

    private void PackRcsMasks(out uint hiMask, out uint loMask)
    {
        hiMask = 0u;
        loMask = 0u;

        if (rcsFire01 == null) return;

        int n = rcsFire01.Length;
        int limit = (n > 32) ? 32 : n;

        for (int i = 0; i < limit; i++)
        {
            float f = rcsFire01[i];

            if (f >= rcsHighThreshold)
                hiMask |= (1u << i);
            else if (f > rcsLowThreshold)
                loMask |= (1u << i);
        }

        loMask &= ~hiMask;
    }
}