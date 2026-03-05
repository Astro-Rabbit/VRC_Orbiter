using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// ActuationController (V2.2)
/// - Wheels (torque clamp)
/// - Main engines: thrust + fuel mdot
/// - Gimbal:
///     * MANUAL: applies pilot gimbal to thrust direction, but does NOT participate in attitude allocation
///              (RCS will NOT "fight" manual gimbal when no attitude torque requested)
///     * AUTO:   uses attitude torque request to compute a SINGLE symmetric yaw/pitch for all running gimballed engines
/// - RCS bang-bang (optional LOW)
///
/// Outputs:
/// - F_E (N, inertial)
/// - Tau_B (Nm, body)
/// - rcsFire01[] for VFX
/// - mainMdot_kgps (kg/s) for SimManager to subtract prop
/// - mainGimbalYawDeg[] / mainGimbalPitchDeg[] for anim/VFX/debug
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

    [Header("Translation scaling (V1)")]
    [Tooltip("Desired force command in BODY frame (N) = translateCmd_B * maxTranslateForceN.")]
    public float maxTranslateForceN = 50f;

    [Header("RCS firing thresholds (V1 heuristic)")]
    [Tooltip("If |forceCmd_B| < this, do not fire translation RCS.")]
    public float forceDeadbandN = 0.25f;

    [Tooltip("If |tauCmd_B| < this, do not fire rotational RCS.")]
    public float torqueDeadbandNm = 0.25f;

    [Tooltip("Alignment threshold for selecting thrusters (0..1). Higher = fewer jets fire.")]
    [Range(0f, 1f)] public float alignOnHigh = 0.6f;

    [Tooltip("Alignment threshold for LOW mode selection (0..1). Must be <= alignOnHigh.")]
    [Range(0f, 1f)] public float alignOnLow = 0.3f;

    [Header("RCS anti-chatter")]
    [Tooltip("Minimum OFF time (seconds) after a jet turns off before it can fire again.")]
    public float minRcsOffTime = 0.06f;

    [Header("Gimbal AUTO stability")]
    [Tooltip("If |tauRemaining_B| below this, AUTO gimbal outputs stay at 0 (prevents chasing tiny residuals).")]
    public float gimbalTorqueDeadbandNm = 5f;

    [Tooltip("Reject summed gimbal bases whose magnitude is below (basisMinFrac * sum(rF)). Prevents divide-by-tiny.")]
    [Range(0f, 1f)] public float gimbalBasisMinFrac = 1e-4f;

    [Tooltip("Optional: limit how fast gimbal angles can change (deg/s). 0 = no rate limit.")]
    public float gimbalMaxRateDegPerSec = 60f;

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
    [Tooltip("RCS fire level per jet: 0=OFF, lowScale=LOW, 1=HIGH.")]
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

    [Header("Translation LOW/HIGH (no PWM)")]
    [Tooltip("If requested force is within (lowModeMarginFrac) of LOW capability, stay in LOW.")]
    [Range(0f, 0.5f)] public float lowModeMarginFrac = 0.05f;

    // Internal scratch
    private Vector3 _forceCmd_B;
    private Vector3 _tauReq_B;

    // Anti-chatter bookkeeping (per thruster)
    private float[] _rcsCooldownUntil;
    private bool[] _rcsPrevOn;

    // Gimbal rate limiting state (single shared yaw/pitch for symmetric AUTO)
    private float _autoYawDegPrev = 0f;
    private float _autoPitchDegPrev = 0f;

    private const float G0 = 9.80665f;
    // Translation PWM accumulator (sigma-delta)
    // Keeps average translation force proportional to request.
    private float _transPwmAcc = 0f;
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

        // ---- translation force request (BODY) ----
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
            Vector3 tauFromMains_B = AllocateMains(throttle01, useGimbalForAttitude ? tauRemaining_B : Vector3.zero,
                                                  gimbalEnabled, useGimbalForAttitude, manualMode);

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
        // RCS allocation (attitude + translation)
        // -----------------------------
        int nRcs = (catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        bool rcsAvail = cmd.allowRCS &&
                        (nRcs > 0) &&
                        (catalog.rcsCached != null) &&
                        (catalog.rcsDir_B != null) &&
                        (catalog.rcsPosRelCg_B != null);

        // Attitude RCS is only used if (a) attitude torque is desired, and (b) actuator policy allows it.
        bool allowRcsForAtt = rcsAvail &&
                              attitudeTorqueWanted &&
                              (cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_RCS_ONLY ||
                               cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_AUTO);

        // If user selected GIMBAL_ONLY, we do NOT use RCS for attitude torque.
        if (cmd.attitudeActuatorMode == CraftCommandState.ATT_ACT_GIMBAL_ONLY)
            allowRcsForAtt = false;

        // Translation RCS remains independent of attitudeActuatorMode (as before)
        bool allowRcsForTrans = rcsAvail;

        byte rcsMode = cmd.rcsMode;

        if (rcsMode == CraftCommandState.RCS_MODE_ROTATE)
        {
            if (allowRcsForAtt) AllocateRcsForTorque(tauRemaining_B);
        }
        else if (rcsMode == CraftCommandState.RCS_MODE_TRANSLATE)
        {
            if (allowRcsForTrans) AllocateRcsForForce(_forceCmd_B);
        }
        else // BLENDED
        {
            if (allowRcsForAtt) AllocateRcsForTorque(tauRemaining_B);
            if (allowRcsForTrans) AllocateRcsForForce(_forceCmd_B);
        }

        ApplyRcsCooldowns();

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
        // Paste this right AFTER the existing RCS mask sync block in Evaluate().
        // It will:
        // - send main throttle (0..1)
        // - send a SINGLE shared yaw/pitch (deg) (symmetric gimbal design)
        // - send an onMask bitfield (bit i = engine i producing thrust this tick)
        //
        // Requires EffectsSyncState to have: SetMainVfx(float throttle01, float yawDeg, float pitchDeg, uint onMask)

        if (effectsSync != null && Networking.IsOwner(effectsSync.gameObject))
        {
            float t01 = Mathf.Clamp01(cmd != null ? cmd.mainThrottle01 : 0f);

            // Shared yaw/pitch: actuator is symmetric, so take engine[0] if available.
            float yawDeg = 0f;
            float pitchDeg = 0f;
            if (mainGimbalYawDeg != null && mainGimbalYawDeg.Length > 0) yawDeg = mainGimbalYawDeg[0];
            if (mainGimbalPitchDeg != null && mainGimbalPitchDeg.Length > 0) pitchDeg = mainGimbalPitchDeg[0];

            // Build onMask based on "this engine actually produced thrust this tick"
            // (respects: throttle deadband, per-engine maxF, and prop starvation for consuming engines).
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

                    // Per-engine starvation gate only for consuming engines
                    float isp = catalog.GetMainIspSec(i);
                    bool consumesProp = isp > 1e-6f;

                    if (consumesProp && craft != null && craft.propMassKg <= 0.0) continue;

                    // If we got here, AllocateMains would have applied thrust this tick
                    onMask |= (1u << i);
                }
            }

            // If you haven't added SetMainVfx yet, add it to EffectsSyncState (see earlier plan).
            effectsSync.SetMainVfx(t01, yawDeg, pitchDeg, onMask);
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

        // Determine shared symmetric gimbal angles (AUTO) once per tick.
        float sharedYawDeg = 0f;
        float sharedPitchDeg = 0f;

        if (gimbalEnabled && useGimbalForAttitude)
        {
            // AUTO symmetric solve across all running gimballed engines
            if (tauTarget_B.sqrMagnitude >= (gimbalTorqueDeadbandNm * gimbalTorqueDeadbandNm))
            {
                Vector3 bPitchSum = Vector3.zero;
                Vector3 bYawSum = Vector3.zero;
                float expectedSum = 0f;
                float maxGimbalDegMin = 1e9f;

                // Sum bases across engines
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

                    // Rate limit the shared command
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
            // Not using AUTO attitude gimbal => keep shared history tame
            _autoYawDegPrev = 0f;
            _autoPitchDegPrev = 0f;
        }

        // Manual shared command (applied to all gimballed engines)
        float manualYawDeg = 0f;
        float manualPitchDeg = 0f;
        if (gimbalEnabled && manualMode)
        {
            // NOTE: manual cmd is normalized [-1..1], scaled per-engine later by each engine max gimbal
            manualYawDeg = Mathf.Clamp(cmd.gimbalPitchYawCmd.x, -1f, 1f);
            manualPitchDeg = Mathf.Clamp(cmd.gimbalPitchYawCmd.y, -1f, 1f);
        }

        // Now apply per-engine thrust + torque
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
                    // AUTO symmetric
                    yawDeg = Mathf.Clamp(sharedYawDeg, -maxGimbalDeg, maxGimbalDeg);
                    pitchDeg = Mathf.Clamp(sharedPitchDeg, -maxGimbalDeg, maxGimbalDeg);
                }
                else if (manualMode)
                {
                    // MANUAL shared normalized command, scaled by each engine max
                    yawDeg = manualYawDeg * maxGimbalDeg;
                    pitchDeg = manualPitchDeg * maxGimbalDeg;
                }

                // Apply yaw then pitch about BODY-frame axes
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

    // ------------------ RCS allocation (unchanged) ------------------

    private void AllocateRcsForForce(Vector3 forceCmd_B)
    {
        float mag = forceCmd_B.magnitude;
        if (mag < forceDeadbandN) return;

        Vector3 dirCmd_B = forceCmd_B / mag;
        Quaternion qBE = attState.qBE;

        int n = (catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (n <= 0) return;

        bool hasLow = catalog.rcsHasLowMode && (catalog.rcsLowScale > 1e-6f);

        // 1) Estimate LOW capability along dirCmd using the LOW selection set
        float capLow = 0f; // effective along command direction before lowScale
        if (hasLow)
        {
            for (int i = 0; i < n; i++)
            {
                if (catalog.rcsCached == null || i >= catalog.rcsCached.Length || !catalog.rcsCached[i]) continue;
                if (!CanFireRcs(i)) continue;

                float maxF = catalog.GetRcsMaxForceN(i);
                if (maxF <= 0f) continue;

                Vector3 dir_B = catalog.rcsDir_B[i];
                float align = Vector3.Dot(dir_B, dirCmd_B);
                if (align < alignOnLow) continue;   // LOW set
                capLow += maxF * align;
            }
        }

        float capLowEff = hasLow ? (capLow * catalog.rcsLowScale) : 0f;

        // 2) Choose LOW if LOW can cover requested magnitude (with a small margin)
        bool useLowMode = false;
        if (hasLow && capLowEff > 1e-6f)
        {
            float margin = 1f - Mathf.Clamp01(lowModeMarginFrac); // e.g. 0.95
            useLowMode = mag <= (capLowEff * margin);
        }

        float scale = useLowMode ? catalog.rcsLowScale : 1f;
        float alignThresh = useLowMode ? (hasLow ? alignOnLow : alignOnHigh) : alignOnHigh;

        // 3) Fire jets continuously using chosen mode
        for (int i = 0; i < n; i++)
        {
            if (catalog.rcsCached == null || i >= catalog.rcsCached.Length || !catalog.rcsCached[i]) continue;
            if (!CanFireRcs(i)) continue;

            float maxF = catalog.GetRcsMaxForceN(i);
            if (maxF <= 0f) continue;

            Vector3 dir_B = catalog.rcsDir_B[i];
            float align = Vector3.Dot(dir_B, dirCmd_B);
            if (align <= 0f) continue;
            if (align < alignThresh) continue;

            Vector3 f_B = dir_B * (maxF * scale);

            Vector3 f_E = qBE * f_B;
            F_E += f_E;

            Vector3 r_B = catalog.rcsPosRelCg_B[i];
            Tau_B += Vector3.Cross(r_B, f_B);

            if (rcsFire01 != null && i < rcsFire01.Length)
                rcsFire01[i] = scale; // LOW=lowScale, HIGH=1
        }
    }

    private void AllocateRcsForTorque(Vector3 tauCmd_B)
    {
        float mag = tauCmd_B.magnitude;
        if (mag < torqueDeadbandNm) return;

        Vector3 tauHat_B = tauCmd_B / mag;
        Quaternion qBE = attState.qBE;

        int n = (catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;

        for (int i = 0; i < n; i++)
        {
            if (catalog.rcsCached == null || i >= catalog.rcsCached.Length || !catalog.rcsCached[i]) continue;
            if (!CanFireRcs(i)) continue;

            float maxF = catalog.GetRcsMaxForceN(i);
            if (maxF <= 0f) continue;

            Vector3 r_B = catalog.rcsPosRelCg_B[i];
            Vector3 dir_B = catalog.rcsDir_B[i];

            Vector3 f_B_high = dir_B * maxF;
            Vector3 tau_B_high = Vector3.Cross(r_B, f_B_high);

            float tauMag = tau_B_high.magnitude;
            if (tauMag < 1e-6f) continue;

            Vector3 tauHat_i = tau_B_high / tauMag;

            float align = Vector3.Dot(tauHat_i, tauHat_B);
            if (align <= 0f) continue;

            float fireSel = SelectBangBangLevel(align);
            if (fireSel <= 0f) continue;

            float scale = (fireSel >= 1f) ? 1f : catalog.rcsLowScale;

            Vector3 f_B = dir_B * (maxF * scale);

            Vector3 f_E = qBE * f_B;
            F_E += f_E;

            Tau_B += Vector3.Cross(r_B, f_B);

            if (rcsFire01 != null && i < rcsFire01.Length)
                rcsFire01[i] = (fireSel >= 1f) ? 1f : catalog.rcsLowScale;
        }
    }

    private bool CanFireRcs(int i)
    {
        if (minRcsOffTime <= 0f) return true;
        if (_rcsCooldownUntil == null || i < 0 || i >= _rcsCooldownUntil.Length) return true;
        return Time.time >= _rcsCooldownUntil[i];
    }

    private void ApplyRcsCooldowns()
    {
        if (minRcsOffTime <= 0f) return;
        if (rcsFire01 == null || _rcsPrevOn == null || _rcsCooldownUntil == null) return;

        int n = rcsFire01.Length;
        float now = Time.time;

        for (int i = 0; i < n; i++)
        {
            bool onNow = rcsFire01[i] > 0f;
            bool wasOn = _rcsPrevOn[i];

            if (wasOn && !onNow)
                _rcsCooldownUntil[i] = now + minRcsOffTime;

            _rcsPrevOn[i] = onNow;
        }
    }

    private float SelectBangBangLevel(float align)
    {
        if (align >= alignOnHigh) return 1f;
        if (catalog.rcsHasLowMode && align >= alignOnLow) return 0.5f;
        return 0f;
    }

    private void EnsureRcsArray()
    {
        int n = (catalog != null && catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (n <= 0)
        {
            rcsFire01 = null;
            return;
        }

        if (rcsFire01 == null || rcsFire01.Length != n)
            rcsFire01 = new float[n];
    }

    private void EnsureRcsGateArrays()
    {
        int n = (catalog != null && catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (n <= 0)
        {
            _rcsCooldownUntil = null;
            _rcsPrevOn = null;
            return;
        }

        if (_rcsCooldownUntil == null || _rcsCooldownUntil.Length != n) _rcsCooldownUntil = new float[n];
        if (_rcsPrevOn == null || _rcsPrevOn.Length != n) _rcsPrevOn = new bool[n];
    }

    private void ClearRcsFires()
    {
        if (rcsFire01 == null) return;
        for (int i = 0; i < rcsFire01.Length; i++)
            rcsFire01[i] = 0f;
    }

    private void EnsureMainGimbalArrays()
    {
        int n = (catalog != null && catalog.mainTf != null) ? catalog.mainTf.Length : 0;
        if (n <= 0)
        {
            mainGimbalYawDeg = null;
            mainGimbalPitchDeg = null;
            return;
        }

        if (mainGimbalYawDeg == null || mainGimbalYawDeg.Length != n) mainGimbalYawDeg = new float[n];
        if (mainGimbalPitchDeg == null || mainGimbalPitchDeg.Length != n) mainGimbalPitchDeg = new float[n];

        for (int i = 0; i < n; i++)
        {
            mainGimbalYawDeg[i] = 0f;
            mainGimbalPitchDeg[i] = 0f;
        }
    }

    private void ClearMainGimbals()
    {
        if (mainGimbalYawDeg == null || mainGimbalPitchDeg == null) return;
        for (int i = 0; i < mainGimbalYawDeg.Length; i++)
        {
            mainGimbalYawDeg[i] = 0f;
            mainGimbalPitchDeg[i] = 0f;
        }
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