using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class RcsVfxDriver : UdonSharpBehaviour
{
    [Header("Core references")]
    public ThrusterCatalog catalog;
    public EffectsSyncState effectsSync;
    public ActuationController actuator;
    public PersonalShipSoundState personalSound;

    [Header("Source selection")]
    [Tooltip("If true and local player owns the craft/effectsSync, prefer local actuator values.")]
    public bool preferLocalActuatorWhenOwner = true;

    [Header("Remote linger (RCS masks)")]
    [Tooltip("If true, remote mask updates are held briefly so short pulses are visible/audible.")]
    public bool enableRemoteLinger = true;

    [Tooltip("Seconds to hold last-seen remote state after an update.")]
    public float remoteLingerSeconds = 0.20f;

    // ============================================================
    // RCS VISUAL PORTS
    // ============================================================
    [Header("RCS visual ports")]
    [Tooltip("Each entry is one visual port/jet. Multiple entries may point to the same logical thruster.")]
    public ParticleSystem[] rcsPortParticles;

    [Tooltip("For each visual port, which logical thruster index does it follow?")]
    public int[] rcsPortThrusterIndex;

    [Header("RCS particle tuning")]
    [Tooltip("If true, particle emission rate is scaled by LOW/HIGH value. If false, just Play/Stop.")]
    public bool scaleRcsParticleEmission = true;

    [Tooltip("Emission rate when firing LOW.")]
    public float rcsLowEmissionRate = 10f;

    [Tooltip("Emission rate when firing HIGH.")]
    public float rcsHighEmissionRate = 40f;

    [Tooltip("Color used for low mode particles.")]
    public Color rcsLowColor = new Color(1f, 0.65f, 0.25f, 1f);

    [Tooltip("Color used for high mode particles.")]
    public Color rcsHighColor = Color.white;

    // ============================================================
    // RCS AUDIO
    // ============================================================
    [Header("RCS sustain audio (per logical thruster)")]
    [Tooltip("Looping sustain audio per logical thruster.")]
    public AudioSource[] rcsSustain;

    [Tooltip("Optional one-shot on stop per logical thruster.")]
    public AudioSource[] rcsAttack;

    [Header("RCS low-pass filters (per logical thruster)")]
    [Tooltip("Optional low-pass filter per logical thruster, usually on the sustain source.")]
    public AudioLowPassFilter[] rcsLowPass;

    [Header("RCS audio tuning")]
    [Tooltip("If true, sustain/attack volumes are scaled by LOW/HIGH intensity.")]
    public bool scaleRcsAudioVolume = true;

    [Tooltip("If true, play the attack/cutoff sound on a per-thruster falling edge.")]
    public bool playRcsAttackOnStop = true;

    [Range(0f, 1f)] public float rcsSustainVolLow = 0.35f;
    [Range(0f, 1f)] public float rcsSustainVolHigh = 1.0f;

    [Range(0f, 1f)] public float rcsAttackVolLow = 0.35f;
    [Range(0f, 1f)] public float rcsAttackVolHigh = 1.0f;

    [Tooltip("Low-pass cutoff for low RCS firing.")]
    public float rcsLowPassCutoffLow = 1200f;

    [Tooltip("Low-pass cutoff for high RCS firing.")]
    public float rcsLowPassCutoffHigh = 5000f;

    [Tooltip("Low-pass cutoff when off.")]
    public float rcsLowPassCutoffOff = 600f;

    // ============================================================
    // MAIN ENGINE VFX
    // ============================================================
    [Header("Main engine particle")]
    public ParticleSystem mainParticle;

    [Header("Main engine visual gimbal pivot")]
    [Tooltip("Optional visual-only gimbal pivot. Recommended: do NOT rotate the physics thruster transform.")]
    public Transform mainGimbalPivot;

    [Header("Main engine particle tuning")]
    [Tooltip("Throttle below this => treat main engine VFX as OFF.")]
    public float mainThrottleDeadband = 0.01f;

    [Tooltip("If true, particle emission scales with throttle. If false, Play/Stop only.")]
    public bool scaleMainParticleEmission = true;

    [Tooltip("Emission rate at full throttle.")]
    public float mainHighEmissionRate = 120f;

    [Header("Remote main gimbal smoothing")]
    public bool smoothRemoteGimbal = true;

    [Tooltip("Max degrees/sec to slew remote gimbal angles.")]
    public float remoteGimbalSlewDegPerSec = 120f;




    // ============================================================
    // MAIN ENGINE AUDIO
    // ============================================================
    [Header("Main engine audio")]
    [Tooltip("Startup audio source.")]
    public AudioSource mainStartAudio;

    [Tooltip("Looping sustain audio source.")]
    public AudioSource mainLoopAudio;

    [Tooltip("Shutdown audio source.")]
    public AudioSource mainStopAudio;

    [Header("Main engine low-pass")]
    [Tooltip("Usually placed on the loop source.")]
    public AudioLowPassFilter mainLowPass;

    [Header("Main engine audio thresholds")]
    [Tooltip("Throttle at/above this counts as engine on.")]
    public float mainAudioOnThreshold = 0.03f;

    [Tooltip("If throttle rises slower than this, skip startup and go straight to loop.")]
    public float mainStartupRiseRateThreshold = 0.70f;

    [Tooltip("Minimum throttle reached during onset to allow startup sound.")]
    public float mainStartupMinThrottle = 0.10f;

    [Tooltip("Throttle at/below this counts as engine off.")]
    public float mainAudioOffThreshold = 0.01f;

    [Header("Main engine audio tuning")]
    [Range(0f, 1f)] public float mainBaseVolume = 1.0f;
    [Range(0f, 1f)] public float mainLoopVolumeMin = 0.15f;
    [Range(0f, 1f)] public float mainLoopVolumeMax = 1.0f;
    [Range(0f, 1f)] public float mainStartupVolume = 1.0f;
    [Range(0f, 1f)] public float mainShutdownVolume = 1.0f;

    [Header("Main engine low-pass tuning")]
    public float mainLowPassCutoffMin = 800f;
    public float mainLowPassCutoffMax = 22000f;

    [Header("Main engine audio timing")]
    [Tooltip("How many seconds before startup ends the loop is allowed to begin.")]
    public float mainLoopStartLeadSeconds = 0.10f;

    [Header("Main engine shutdown matching")]
    [Tooltip("Multiplier applied to the latched live engine volume when shutdown begins.")]
    [Range(0f, 2f)] public float mainShutdownVolumeMultiplier = 1.00f;

    // ============================================================
    // DEBUG / OUTPUT
    // ============================================================
    [Header("Resolved RCS output")]
    [Tooltip("Resolved per-thruster visual fire (0=off, lowScale=low, 1=high).")]
    public float[] rcsFire01Visual;

    [Header("Resolved main output")]
    [Tooltip("Resolved main throttle for visuals (0..1).")]
    public float mainThrottleVisual01;

    [Tooltip("Resolved shared main gimbal yaw (deg) for visuals.")]
    public float mainYawVisualDeg;

    [Tooltip("Resolved shared main gimbal pitch (deg) for visuals.")]
    public float mainPitchVisualDeg;

    [Tooltip("Resolved main on state for visuals.")]
    public bool mainIsOnVisual;

    // ============================================================
    // INTERNAL STATE
    // ============================================================
    private uint _lastHi;
    private uint _lastLo;
    private uint _lastSeq;
    private float _lingerT;

    private bool[] _prevRcsFiring;
    private float[] _prevRcsIntensity;

    private float _mainYawCurDeg;
    private float _mainPitchCurDeg;

    private float _mainPrevThrottle;
    private bool _mainWasOn;
    private int _mainAudioState;

    private const int MAIN_AUDIO_OFF = 0;
    private const int MAIN_AUDIO_STARTUP = 1;
    private const int MAIN_AUDIO_LOOP = 2;
    private const int MAIN_AUDIO_SHUTDOWN = 3;

    private float _mainLatchedLoopVolume = 0f;
    private float _mainLatchedLowPassCutoff = 800f;
    private bool _mainLoopHasStartedThisCycle = false;

    void Start()
    {
        EnsureArrays();
        _lingerT = 0f;
        _lastHi = 0u;
        _lastLo = 0u;
        _lastSeq = 0u;
        _mainPrevThrottle = 0f;
        _mainWasOn = false;
        _mainAudioState = MAIN_AUDIO_OFF;
        _mainLoopHasStartedThisCycle = false;

        _mainLatchedLoopVolume = 0f;
        _mainLatchedLowPassCutoff = mainLowPassCutoffMin;

        if (mainLoopAudio != null) mainLoopAudio.loop = true;
    }

    void Update()
    {
        Apply();
    }

    public void Apply()
    {
        if (catalog == null) return;

        bool useLocalActuator = false;
        if (preferLocalActuatorWhenOwner && actuator != null)
        {
            if (effectsSync != null) useLocalActuator = Networking.IsOwner(effectsSync.gameObject);
            else useLocalActuator = Networking.IsOwner(actuator.gameObject);
        }

        ApplyRcs(useLocalActuator);
        ApplyMain(useLocalActuator);
    }

    // ============================================================
    // RCS
    // ============================================================
    private void ApplyRcs(bool useLocalActuator)
    {
        if (catalog.rcsTf == null) return;

        EnsureArrays();

        int n = catalog.rcsTf.Length;
        float lowScale = catalog.rcsLowScale;

        if (useLocalActuator && actuator != null && actuator.rcsFire01 != null)
        {
            for (int i = 0; i < n; i++)
            {
                float f = (i < actuator.rcsFire01.Length) ? actuator.rcsFire01[i] : 0f;
                rcsFire01Visual[i] = QuantizeToOffLowHigh(f, lowScale);
            }
        }
        else
        {
            uint hi = 0u;
            uint lo = 0u;
            uint seq = 0u;

            if (effectsSync != null)
            {
                hi = effectsSync.rcsHiMask;
                lo = effectsSync.rcsLoMask;
                seq = effectsSync.seq;
            }

            lo &= ~hi;

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
                bool low = (useLo & (1u << i)) != 0u;

                rcsFire01Visual[i] = high ? 1f : (low ? lowScale : 0f);
            }
        }

        for (int i = 0; i < n; i++)
        {
            float f = rcsFire01Visual[i];
            bool firing = f > 0f;

            DriveRcsAudio(i, firing, f, lowScale);

            _prevRcsFiring[i] = firing;
            _prevRcsIntensity[i] = f;
        }

        ApplyRcsPorts(lowScale);
    }

    private void ApplyRcsPorts(float lowScale)
    {
        if (rcsPortParticles == null || rcsPortThrusterIndex == null) return;

        int count = rcsPortParticles.Length;
        if (rcsPortThrusterIndex.Length < count) count = rcsPortThrusterIndex.Length;

        for (int i = 0; i < count; i++)
        {
            ParticleSystem ps = rcsPortParticles[i];
            if (ps == null) continue;

            int thrusterIndex = rcsPortThrusterIndex[i];
            if (thrusterIndex < 0 || rcsFire01Visual == null || thrusterIndex >= rcsFire01Visual.Length)
            {
                StopParticle(ps);
                continue;
            }

            float f = rcsFire01Visual[thrusterIndex];
            bool isHigh = f >= 0.999f;
            bool isLow = (!isHigh && f > 0f);

            DriveRcsPortParticle(ps, isHigh, isLow, lowScale);
        }
    }

    private void DriveRcsPortParticle(ParticleSystem ps, bool isHigh, bool isLow, float lowScale)
    {
        if (!scaleRcsParticleEmission)
        {
            bool shouldPlay = isHigh || isLow;
            if (shouldPlay)
            {
                ApplyParticleColor(ps, isHigh ? rcsHighColor : rcsLowColor);
                if (!ps.isPlaying) ps.Play(true);
            }
            else
            {
                StopParticle(ps);
            }
            return;
        }

        ParticleSystem.EmissionModule em = ps.emission;
        float rate = 0f;
        if (isHigh) rate = rcsHighEmissionRate;
        else if (isLow) rate = rcsLowEmissionRate;

        em.rateOverTimeMultiplier = rate;

        if (rate > 0f)
        {
            ApplyParticleColor(ps, isHigh ? rcsHighColor : rcsLowColor);
            if (!ps.isPlaying) ps.Play(true);
        }
        else
        {
            StopParticle(ps);
        }
    }

    private void ApplyParticleColor(ParticleSystem ps, Color c)
    {
        ParticleSystem.MainModule main = ps.main;
        main.startColor = c;
    }

    private void StopParticle(ParticleSystem ps)
    {
        if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void DriveRcsAudio(int i, bool firing, float intensity01, float lowScale)
    {
        bool wasFiring = (_prevRcsFiring != null && i < _prevRcsFiring.Length) ? _prevRcsFiring[i] : false;
        float prevIntensity = (_prevRcsIntensity != null && i < _prevRcsIntensity.Length) ? _prevRcsIntensity[i] : 0f;

        float gain = GetEffectiveRcsGain();

        // Sustain
        if (rcsSustain != null && i < rcsSustain.Length && rcsSustain[i] != null)
        {
            AudioSource a = rcsSustain[i];

            if (firing)
            {
                if (!a.isPlaying) a.Play();

                float baseVol = 1f;
                if (scaleRcsAudioVolume)
                    baseVol = MapIntensityToVolume(intensity01, lowScale, rcsSustainVolLow, rcsSustainVolHigh);

                a.volume = Mathf.Clamp01(baseVol * gain);
            }
            else
            {
                if (a.isPlaying) a.Stop();
                a.volume = 0f;
            }
        }

        // Low-pass
        if (rcsLowPass != null && i < rcsLowPass.Length && rcsLowPass[i] != null)
        {
            AudioLowPassFilter lp = rcsLowPass[i];

            if (!firing) lp.cutoffFrequency = rcsLowPassCutoffOff;
            else if (intensity01 >= 0.999f) lp.cutoffFrequency = rcsLowPassCutoffHigh;
            else lp.cutoffFrequency = rcsLowPassCutoffLow;
        }

        // Attack on stop
        if (playRcsAttackOnStop && wasFiring && !firing)
        {
            if (rcsAttack != null && i < rcsAttack.Length && rcsAttack[i] != null)
            {
                AudioSource a = rcsAttack[i];

                float baseVol = 1f;
                if (scaleRcsAudioVolume)
                    baseVol = MapIntensityToVolume(prevIntensity, lowScale, rcsAttackVolLow, rcsAttackVolHigh);

                a.volume = Mathf.Clamp01(baseVol * gain);
                a.Stop();
                a.Play();
            }
        }
    }

    // ============================================================
    // MAIN ENGINE
    // ============================================================
    private void ApplyMain(bool useLocalActuator)
    {
        float throttle01 = 0f;
        float yawDeg = 0f;
        float pitchDeg = 0f;
        bool engineOn = false;

        if (useLocalActuator && actuator != null)
        {
            if (actuator.cmd != null) throttle01 = Mathf.Clamp01(actuator.cmd.mainThrottle01);

            if (actuator.mainGimbalYawDeg != null && actuator.mainGimbalYawDeg.Length > 0)
                yawDeg = actuator.mainGimbalYawDeg[0];

            if (actuator.mainGimbalPitchDeg != null && actuator.mainGimbalPitchDeg.Length > 0)
                pitchDeg = actuator.mainGimbalPitchDeg[0];

            engineOn = throttle01 > mainThrottleDeadband;
        }
        else
        {
            if (effectsSync != null)
            {
                throttle01 = effectsSync.mainThrottle255 / 255f;
                yawDeg = effectsSync.mainYaw_cdeg / 100f;
                pitchDeg = effectsSync.mainPitch_cdeg / 100f;
            }

            engineOn = throttle01 > mainThrottleDeadband;
        }

        mainThrottleVisual01 = throttle01;
        mainYawVisualDeg = yawDeg;
        mainPitchVisualDeg = pitchDeg;
        mainIsOnVisual = engineOn;

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

            mainYawVisualDeg = yawDeg;
            mainPitchVisualDeg = pitchDeg;
        }
        else
        {
            _mainYawCurDeg = yawDeg;
            _mainPitchCurDeg = pitchDeg;
        }

        DriveMainParticle(engineOn, throttle01);
        DriveMainAudio(throttle01);
        DriveMainGimbalPivot(yawDeg, pitchDeg);
    }

    private void DriveMainParticle(bool on, float throttle01)
    {
        if (mainParticle == null) return;

        if (!scaleMainParticleEmission)
        {
            if (on)
            {
                if (!mainParticle.isPlaying) mainParticle.Play(true);
            }
            else
            {
                if (mainParticle.isPlaying) mainParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            return;
        }

        ParticleSystem.EmissionModule em = mainParticle.emission;
        float rate = on ? Mathf.Lerp(0f, mainHighEmissionRate, Mathf.Clamp01(throttle01)) : 0f;
        em.rateOverTimeMultiplier = rate;

        if (rate > 0f)
        {
            if (!mainParticle.isPlaying) mainParticle.Play(true);
        }
        else
        {
            if (mainParticle.isPlaying) mainParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void DriveMainAudio(float throttle01)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) dt = 0.0001f;

        float gain = GetEffectiveEngineGain();
        bool isOnNow = throttle01 >= mainAudioOnThreshold;
        bool isOffNow = throttle01 <= mainAudioOffThreshold;

        float riseRate = (throttle01 - _mainPrevThrottle) / dt;
        bool shouldPlayStartup = (!_mainWasOn && isOnNow &&
                                riseRate >= mainStartupRiseRateThreshold &&
                                throttle01 >= mainStartupMinThrottle);

        float liveLoopVol = Mathf.Lerp(mainLoopVolumeMin, mainLoopVolumeMax, Mathf.Clamp01(throttle01));
        liveLoopVol = Mathf.Clamp01(liveLoopVol * mainBaseVolume * gain);

        float liveCutoff = Mathf.Lerp(mainLowPassCutoffMin, mainLowPassCutoffMax, Mathf.Clamp01(throttle01));

        // ------------------------------------------------------------
        // Transition ON
        // ------------------------------------------------------------
        if (!_mainWasOn && isOnNow)
        {
            StopIfPlaying(mainStopAudio);

            _mainLoopHasStartedThisCycle = false;

            if (shouldPlayStartup)
            {
                if (mainStartAudio != null)
                {
                    mainStartAudio.volume = Mathf.Clamp01(mainStartupVolume * mainBaseVolume * gain);
                    mainStartAudio.Stop();
                    mainStartAudio.Play();
                }

                _mainAudioState = MAIN_AUDIO_STARTUP;
            }
            else
            {
                StopIfPlaying(mainStartAudio);

                if (mainLoopAudio != null)
                {
                    mainLoopAudio.volume = liveLoopVol;
                    if (!mainLoopAudio.isPlaying) mainLoopAudio.Play();
                }

                _mainLoopHasStartedThisCycle = true;
                _mainAudioState = MAIN_AUDIO_LOOP;
            }
        }

        // ------------------------------------------------------------
        // While ON
        // ------------------------------------------------------------
        if (isOnNow)
        {
            // Keep current live values updated and latched
            _mainLatchedLoopVolume = liveLoopVol;
            _mainLatchedLowPassCutoff = liveCutoff;

            // Low-pass follows live throttle whenever on
            if (mainLowPass != null)
                mainLowPass.cutoffFrequency = liveCutoff;

            if (_mainAudioState == MAIN_AUDIO_STARTUP)
            {
                bool startupStillPlaying = (mainStartAudio != null && mainStartAudio.isPlaying);

                // Start loop only near the end of startup
                if (!_mainLoopHasStartedThisCycle && startupStillPlaying && mainLoopAudio != null)
                {
                    float clipLen = (mainStartAudio.clip != null) ? mainStartAudio.clip.length : 0f;
                    float timeRemaining = clipLen - mainStartAudio.time;

                    if (timeRemaining <= mainLoopStartLeadSeconds)
                    {
                        mainLoopAudio.volume = liveLoopVol;
                        if (!mainLoopAudio.isPlaying) mainLoopAudio.Play();
                        _mainLoopHasStartedThisCycle = true;
                    }
                }

                // If startup finished, ensure loop is running
                if (!startupStillPlaying)
                {
                    if (!_mainLoopHasStartedThisCycle && mainLoopAudio != null)
                    {
                        mainLoopAudio.volume = liveLoopVol;
                        if (!mainLoopAudio.isPlaying) mainLoopAudio.Play();
                        _mainLoopHasStartedThisCycle = true;
                    }

                    _mainAudioState = MAIN_AUDIO_LOOP;
                }
            }
            else
            {
                if (mainLoopAudio != null)
                {
                    mainLoopAudio.volume = liveLoopVol;
                    if (!mainLoopAudio.isPlaying) mainLoopAudio.Play();
                }

                _mainLoopHasStartedThisCycle = true;
                _mainAudioState = MAIN_AUDIO_LOOP;
            }
        }

        // ------------------------------------------------------------
        // Transition OFF
        // ------------------------------------------------------------
        if (_mainWasOn && isOffNow)
        {
            StopIfPlaying(mainStartAudio);
            StopIfPlaying(mainLoopAudio);

            // Shutdown starts from the previously live engine sound state
            if (mainStopAudio != null)
            {
                float stopVol = Mathf.Clamp01(_mainLatchedLoopVolume * mainShutdownVolumeMultiplier);
                mainStopAudio.volume = stopVol;
                mainStopAudio.Stop();
                mainStopAudio.Play();
            }

            // Keep LPF near prior live tone at shutdown onset instead of snapping
            if (mainLowPass != null)
                mainLowPass.cutoffFrequency = _mainLatchedLowPassCutoff;

            _mainLoopHasStartedThisCycle = false;
            _mainAudioState = MAIN_AUDIO_SHUTDOWN;
        }

        // ------------------------------------------------------------
        // Fully OFF
        // ------------------------------------------------------------
        if (!isOnNow && !_mainWasOn)
        {
            StopIfPlaying(mainStartAudio);
            StopIfPlaying(mainLoopAudio);

            if (mainLowPass != null && (mainStopAudio == null || !mainStopAudio.isPlaying))
                mainLowPass.cutoffFrequency = mainLowPassCutoffMin;

            if (_mainAudioState != MAIN_AUDIO_SHUTDOWN || (mainStopAudio != null && !mainStopAudio.isPlaying))
                _mainAudioState = MAIN_AUDIO_OFF;
        }

        _mainWasOn = isOnNow;
        _mainPrevThrottle = throttle01;
    }

    private void DriveMainGimbalPivot(float yawDeg, float pitchDeg)
    {
        if (mainGimbalPivot == null) return;

        Quaternion qYaw = Quaternion.AngleAxis(yawDeg, Vector3.up);
        Quaternion qPitch = Quaternion.AngleAxis(pitchDeg, Vector3.right);

        mainGimbalPivot.localRotation = qYaw * qPitch;
    }

    private void StopIfPlaying(AudioSource a)
    {
        if (a != null && a.isPlaying) a.Stop();
    }

    // ============================================================
    // HELPERS
    // ============================================================
    private float GetEffectiveEngineGain()
    {
        if (personalSound == null) return 1f;
        return Mathf.Clamp01(personalSound.GetEffectiveEngineGain());
    }

    private float GetEffectiveRcsGain()
    {
        if (personalSound == null) return 1f;
        return Mathf.Clamp01(personalSound.GetEffectiveRcsGain());
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

    private void EnsureArrays()
    {
        int n = (catalog != null && catalog.rcsTf != null) ? catalog.rcsTf.Length : 0;
        if (n <= 0)
        {
            rcsFire01Visual = null;
            _prevRcsFiring = null;
            _prevRcsIntensity = null;
            return;
        }

        if (rcsFire01Visual == null || rcsFire01Visual.Length != n)
            rcsFire01Visual = new float[n];

        if (_prevRcsFiring == null || _prevRcsFiring.Length != n)
            _prevRcsFiring = new bool[n];

        if (_prevRcsIntensity == null || _prevRcsIntensity.Length != n)
            _prevRcsIntensity = new float[n];
    }
}