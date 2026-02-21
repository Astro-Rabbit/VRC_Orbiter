using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class RcsVfxDriver : UdonSharpBehaviour
{
    [Header("References")]
    public ThrusterCatalog catalog;
    public EffectsSyncState effectsSync;         // remote source
    public ActuationController actuator;         // local owner source (more responsive)

    [Header("Local/Remote selection")]
    [Tooltip("If true and local player owns the craft/effectsSync, prefer actuator.rcsFire01[] for visuals.")]
    public bool preferLocalActuatorWhenOwner = true;

    [Tooltip("If true, remote mask updates are held briefly so short pulses are visible.")]
    public bool enableRemoteLinger = true;

    [Tooltip("Seconds to hold last-seen remote state after an update.")]
    public float remoteLingerSeconds = 0.20f;

    [Header("Per-thruster VFX (arrays must match catalog.rcsTf length)")]
    public ParticleSystem[] rcsParticles;

    [Header("Per-thruster Audio (arrays must match catalog.rcsTf length)")]
    [Tooltip("Looping sustain audio per thruster (same clip can be reused on all).")]
    public AudioSource[] rcsSustain;

    [Tooltip("One-shot wind-down/cutoff audio per thruster (same clip can be reused on all).")]
    public AudioSource[] rcsAttack;

    [Header("Audio scaling")]
    [Tooltip("If true, sustain/attack volumes are scaled by LOW/HIGH intensity.")]
    public bool scaleAudioVolume = true;

    [Range(0f, 1f)] public float sustainVolLow = 0.35f;
    [Range(0f, 1f)] public float sustainVolHigh = 1.0f;

    [Range(0f, 1f)] public float attackVolLow = 0.35f;
    [Range(0f, 1f)] public float attackVolHigh = 1.0f;

    [Tooltip("If true, play the attack (wind-down) on a per-thruster falling edge (firing -> off).")]
    public bool playAttackOnStop = true;

    [Header("Drive settings")]
    [Tooltip("If true, particle emission rate is scaled by LOW/HIGH value. If false, just Play/Stop.")]
    public bool scaleParticleEmission = true;

    [Tooltip("Emission rate when firing HIGH (used if scaleParticleEmission=true).")]
    public float highEmissionRate = 40f;

    [Tooltip("Emission rate when firing LOW (used if scaleParticleEmission=true).")]
    public float lowEmissionRate = 10f;

    [Header("Output (optional debug)")]
    [Tooltip("Resolved per-thruster visual fire (0=off, lowScale=low, 1=high).")]
    public float[] rcsFire01Visual;

    // ------------------------------------------------------------
    // NEW: Main engine VFX
    // ------------------------------------------------------------
    [Header("Main engine VFX (arrays must match catalog.mainTf length)")]
    [Tooltip("Engine plume particle systems per main engine.")]
    public ParticleSystem[] mainParticles;

    [Tooltip("Looping sustain audio per main engine.")]
    public AudioSource[] mainSustain;

    [Tooltip("Optional: visual gimbal pivot per engine. Recommended: do NOT rotate physics thruster Transform.")]
    public Transform[] mainGimbalPivots;

    [Header("Main engine VFX tuning")]
    [Tooltip("Throttle below this => treat main engine VFX as OFF.")]
    public float mainThrottleDeadband = 0.01f;

    [Tooltip("If true, particle emission scales with throttle. If false, Play/Stop only.")]
    public bool scaleMainParticleEmission = true;

    [Tooltip("Emission rate at full throttle (scaleMainParticleEmission=true).")]
    public float mainHighEmissionRate = 120f;

    [Header("Remote main gimbal smoothing")]
    public bool smoothRemoteGimbal = true;

    [Tooltip("Max degrees/sec to slew remote gimbal angles.")]
    public float remoteGimbalSlewDegPerSec = 120f;

    [Header("Main engine debug (read-only)")]
    [Tooltip("Resolved main throttle for visuals (0..1).")]
    public float mainThrottleVisual01;

    [Tooltip("Resolved shared main gimbal yaw (deg) for visuals.")]
    public float mainYawVisualDeg;

    [Tooltip("Resolved shared main gimbal pitch (deg) for visuals.")]
    public float mainPitchVisualDeg;

    [Tooltip("Resolved main on-mask (bit i => engine i on).")]
    public uint mainOnMaskVisual;

    // Remote linger bookkeeping (RCS)
    private uint _lastHi, _lastLo, _lastSeq;
    private float _lingerT;

    // Per-thruster edge detection (for attack sound)
    private bool[] _prevFiring;
    private float[] _prevIntensity;

    // Remote smoothing state (main gimbal)
    private float _mainYawCurDeg = 0f;
    private float _mainPitchCurDeg = 0f;

    void Start()
    {
        EnsureArrays();
        _lingerT = 0f;
        _lastHi = _lastLo = _lastSeq = 0u;
    }

    void Update()
    {
        Apply();
    }

    public void Apply()
    {
        if (catalog == null) return;

        // -------------------------
        // Decide data source
        // -------------------------
        bool useLocalActuator = false;
        if (preferLocalActuatorWhenOwner && actuator != null)
        {
            if (effectsSync != null)
                useLocalActuator = Networking.IsOwner(effectsSync.gameObject);
            else
                useLocalActuator = Networking.IsOwner(actuator.gameObject);
        }

        // -------------------------
        // RCS VISUALS (existing)
        // -------------------------
        ApplyRcs(useLocalActuator);

        // -------------------------
        // MAIN ENGINE VISUALS (new)
        // -------------------------
        ApplyMains(useLocalActuator);
    }

    // ============================================================
    // RCS SECTION (unchanged behavior, just moved into a method)
    // ============================================================
    private void ApplyRcs(bool useLocalActuator)
    {
        if (catalog.rcsTf == null) return;

        EnsureArrays();

        int n = catalog.rcsTf.Length;
        float lowScale = catalog.rcsLowScale;

        if (useLocalActuator && actuator != null && actuator.rcsFire01 != null)
        {
            // Direct, most responsive
            for (int i = 0; i < n; i++)
            {
                float f = (i < actuator.rcsFire01.Length) ? actuator.rcsFire01[i] : 0f;
                rcsFire01Visual[i] = QuantizeToOffLowHigh(f, lowScale);
            }
        }
        else
        {
            // Remote: derive from masks, with optional linger
            uint hi = 0u, lo = 0u, seq = 0u;

            if (effectsSync != null)
            {
                hi = effectsSync.rcsHiMask;
                lo = effectsSync.rcsLoMask;
                seq = effectsSync.seq;
            }

            lo &= ~hi; // HI wins

            // Update linger state on change
            if (seq != _lastSeq || hi != _lastHi || lo != _lastLo)
            {
                _lastSeq = seq;
                _lastHi = hi;
                _lastLo = lo;
                if (enableRemoteLinger) _lingerT = remoteLingerSeconds;
            }
            else
            {
                if (enableRemoteLinger && _lingerT > 0f) _lingerT -= Time.deltaTime;
            }

            uint useHi = hi;
            uint useLo = lo;

            if (enableRemoteLinger && _lingerT > 0f)
            {
                useHi = _lastHi;
                useLo = _lastLo;
            }

            int limit = (n > 32) ? 32 : n;

            for (int i = 0; i < n; i++) rcsFire01Visual[i] = 0f;

            for (int i = 0; i < limit; i++)
            {
                bool high = (useHi & (1u << i)) != 0u;
                bool low  = (useLo & (1u << i)) != 0u;

                rcsFire01Visual[i] = high ? 1f : (low ? lowScale : 0f);
            }
        }

        // Drive actual effects per thruster (particle + audio)
        for (int i = 0; i < n; i++)
        {
            float f = rcsFire01Visual[i];
            bool firing = f > 0f;

            bool isHigh = f >= 0.999f;
            bool isLow = (!isHigh && firing);

            DriveParticle(i, isHigh, isLow);
            DriveThrusterAudio(i, firing, f, lowScale);

            _prevFiring[i] = firing;
            _prevIntensity[i] = f;
        }
    }

    private void DriveParticle(int i, bool isHigh, bool isLow)
    {
        if (rcsParticles == null || i >= rcsParticles.Length) return;
        ParticleSystem ps = rcsParticles[i];
        if (ps == null) return;

        if (!scaleParticleEmission)
        {
            bool shouldPlay = isHigh || isLow;
            if (shouldPlay)
            {
                if (!ps.isPlaying) ps.Play(true);
            }
            else
            {
                if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            return;
        }

        var em = ps.emission;
        float rate = 0f;
        if (isHigh) rate = highEmissionRate;
        else if (isLow) rate = lowEmissionRate;

        em.rateOverTimeMultiplier = rate;

        if (rate > 0f)
        {
            if (!ps.isPlaying) ps.Play(true);
        }
        else
        {
            if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void DriveThrusterAudio(int i, bool firing, float intensity01, float lowScale)
    {
        bool wasFiring = (_prevFiring != null && i < _prevFiring.Length) ? _prevFiring[i] : false;
        float prevIntensity = (_prevIntensity != null && i < _prevIntensity.Length) ? _prevIntensity[i] : 0f;

        // Sustain
        if (rcsSustain != null && i < rcsSustain.Length && rcsSustain[i] != null)
        {
            AudioSource a = rcsSustain[i];

            if (firing)
            {
                if (!a.isPlaying) a.Play();

                if (scaleAudioVolume)
                {
                    float v = MapIntensityToVolume(intensity01, lowScale, sustainVolLow, sustainVolHigh);
                    a.volume = v;
                }
            }
            else
            {
                if (a.isPlaying) a.Stop();
            }
        }

        // Attack on stop
        if (playAttackOnStop && wasFiring && !firing)
        {
            if (rcsAttack != null && i < rcsAttack.Length && rcsAttack[i] != null)
            {
                AudioSource a = rcsAttack[i];

                if (scaleAudioVolume)
                {
                    float v = MapIntensityToVolume(prevIntensity, lowScale, attackVolLow, attackVolHigh);
                    a.volume = v;
                }

                a.Stop();
                a.Play();
            }
        }
    }

    private float MapIntensityToVolume(float intensity01, float lowScale, float volAtLow, float volAtHigh)
    {
        if (intensity01 <= 0f) return 0f;

        float t;
        if (lowScale <= 1e-6f)
        {
            t = Mathf.Clamp01(intensity01);
        }
        else
        {
            t = (intensity01 - lowScale) / (1f - lowScale);
            t = Mathf.Clamp01(t);
        }

        return Mathf.Lerp(volAtLow, volAtHigh, t);
    }

    private float QuantizeToOffLowHigh(float f, float lowScale)
    {
        if (f >= 0.999f) return 1f;
        if (f > 0f) return lowScale;
        return 0f;
    }

    // ============================================================
    // MAIN ENGINE SECTION (new)
    // ============================================================
    private void ApplyMains(bool useLocalActuator)
    {
        int nMain = (catalog.mainTf != null) ? catalog.mainTf.Length : 0;
        if (nMain <= 0) return;

        // --- Resolve main throttle + gimbal angles + on-mask ---
        float throttle01 = 0f;
        float yawDeg = 0f;
        float pitchDeg = 0f;
        uint onMask = 0u;

        if (useLocalActuator && actuator != null)
        {
            // Owner-local: most responsive
            if (actuator.cmd != null) throttle01 = Mathf.Clamp01(actuator.cmd.mainThrottle01);

            // Actuator provides per-engine gimbal arrays; symmetric => take [0] if present
            if (actuator.mainGimbalYawDeg != null && actuator.mainGimbalYawDeg.Length > 0)
                yawDeg = actuator.mainGimbalYawDeg[0];
            if (actuator.mainGimbalPitchDeg != null && actuator.mainGimbalPitchDeg.Length > 0)
                pitchDeg = actuator.mainGimbalPitchDeg[0];

            // On mask: if you haven't implemented per-engine starve mask yet,
            // treat all engines as on when throttled.
            if (throttle01 > mainThrottleDeadband)
            {
                int limit = (nMain > 32) ? 32 : nMain;
                for (int i = 0; i < limit; i++) onMask |= (1u << i);
            }
        }
        else
        {
            // Remote: from synced packed fields
            if (effectsSync != null)
            {
                throttle01 = effectsSync.mainThrottle255 / 255f;
                yawDeg = effectsSync.mainYaw_cdeg / 100f;
                pitchDeg = effectsSync.mainPitch_cdeg / 100f;
                onMask = effectsSync.mainOnMask;
            }
        }

        // Save debug outputs
        mainThrottleVisual01 = throttle01;
        mainYawVisualDeg = yawDeg;
        mainPitchVisualDeg = pitchDeg;
        mainOnMaskVisual = onMask;

        // Optional smoothing for remote gimbal
        if (!useLocalActuator && smoothRemoteGimbal)
        {
            float dt = Time.deltaTime;
            if (dt < 0f) dt = 0f;

            float maxStep = remoteGimbalSlewDegPerSec * dt;

            _mainYawCurDeg = Mathf.MoveTowards(_mainYawCurDeg, yawDeg, maxStep);
            _mainPitchCurDeg = Mathf.MoveTowards(_mainPitchCurDeg, pitchDeg, maxStep);

            yawDeg = _mainYawCurDeg;
            pitchDeg = _mainPitchCurDeg;

            // update debug to show smoothed
            mainYawVisualDeg = yawDeg;
            mainPitchVisualDeg = pitchDeg;
        }
        else
        {
            _mainYawCurDeg = yawDeg;
            _mainPitchCurDeg = pitchDeg;
        }

        // --- Drive per-engine particles/audio/gimbal pivots ---
        bool anyThrottle = throttle01 > mainThrottleDeadband;

        int limitMask = (nMain > 32) ? 32 : nMain;

        for (int i = 0; i < nMain; i++)
        {
            bool engineOn;
            if (i < limitMask)
            {
                // If no mask is being sent (0), fall back to throttle-driven on.
                if (onMask == 0u) engineOn = anyThrottle;
                else engineOn = anyThrottle && ((onMask & (1u << i)) != 0u);
            }
            else
            {
                engineOn = anyThrottle; // beyond 32, no bit
            }

            DriveMainParticles(i, engineOn, throttle01);
            DriveMainAudio(i, engineOn, throttle01);
            DriveMainGimbalPivot(i, yawDeg, pitchDeg);
        }
    }

    private void DriveMainParticles(int i, bool on, float throttle01)
    {
        if (mainParticles == null || i >= mainParticles.Length) return;
        ParticleSystem ps = mainParticles[i];
        if (ps == null) return;

        if (!scaleMainParticleEmission)
        {
            if (on)
            {
                if (!ps.isPlaying) ps.Play(true);
            }
            else
            {
                if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            return;
        }

        var em = ps.emission;
        float rate = on ? Mathf.Lerp(0f, mainHighEmissionRate, Mathf.Clamp01(throttle01)) : 0f;
        em.rateOverTimeMultiplier = rate;

        if (rate > 0f)
        {
            if (!ps.isPlaying) ps.Play(true);
        }
        else
        {
            if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void DriveMainAudio(int i, bool on, float throttle01)
    {
        if (mainSustain == null || i >= mainSustain.Length) return;
        AudioSource a = mainSustain[i];
        if (a == null) return;

        if (on)
        {
            if (!a.isPlaying) a.Play();
            // Scale volume by throttle (simple + effective)
            a.volume = Mathf.Clamp01(throttle01);
        }
        else
        {
            if (a.isPlaying) a.Stop();
        }
    }

    private void DriveMainGimbalPivot(int i, float yawDeg, float pitchDeg)
    {
        if (mainGimbalPivots == null || i >= mainGimbalPivots.Length) return;
        Transform piv = mainGimbalPivots[i];
        if (piv == null) return;

        // Visual convention: yaw about local +Y, pitch about local +X.
        // Author the pivot so these match your intended axes.
        Quaternion qYaw = Quaternion.AngleAxis(yawDeg, Vector3.up);
        Quaternion qPitch = Quaternion.AngleAxis(pitchDeg, Vector3.right);

        piv.localRotation = qYaw * qPitch;
    }

    // ============================================================
    // ARRAY MANAGEMENT
    // ============================================================
    private void EnsureArrays()
    {
        int n = (catalog != null && catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (n <= 0)
        {
            rcsFire01Visual = null;
            _prevFiring = null;
            _prevIntensity = null;
            return;
        }

        if (rcsFire01Visual == null || rcsFire01Visual.Length != n)
            rcsFire01Visual = new float[n];

        if (_prevFiring == null || _prevFiring.Length != n)
            _prevFiring = new bool[n];

        if (_prevIntensity == null || _prevIntensity.Length != n)
            _prevIntensity = new float[n];
    }
}