using UdonSharp;
using UnityEngine;

/// <summary>
/// DockingSoundDriver
///
/// Current features:
/// - Soft dock one-shot on transition into DOCK_SOFT
/// - Hard dock one-shot on transition into DOCK_HARD
/// - Decompress one-shot when docking ends after previously being hard docked
/// - Hatch compression one-shot on transition into hatch OPENING
/// - Hatch open one-shot after a configurable delay
/// - Hatch close one-shot on transition into hatch CLOSING
/// - Airlock open one-shot on transition into airlock OPENING
/// - Airlock close one-shot on transition into airlock CLOSING
///
/// Notes:
/// - Local-only playback. Every client plays sounds from their observed state transitions.
/// - Uses PersonalShipSoundState.dockingSound as the gain category.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingSoundDriver : UdonSharpBehaviour
{
    [Header("Core refs")]
    public DockingRuntimeState dock;
    public DockingComputer dockingComp;
    public DockingOpsController dockingOps;
    public PersonalShipSoundState personalSound;

    [Header("Docking audio")]
    public AudioSource softDockAudio;
    public AudioSource hardDockAudio;
    public AudioSource decompressAudio;

    [Header("Hatch audio")]
    public AudioSource hatchCompressionAudio;
    public AudioSource hatchOpenAudio;
    public AudioSource hatchCloseAudio;

    [Header("Airlock audio")]
    public AudioSource airlockOpenAudio;
    public AudioSource airlockCloseAudio;

    [Header("Timing")]
    [Tooltip("Delay after compression before hatch open sound.")]
    public float hatchOpenDelay = 0.4f;

    [Header("Base volumes")]
    [Range(0f, 1f)] public float softDockVolume = 1f;
    [Range(0f, 1f)] public float hardDockVolume = 1f;
    [Range(0f, 1f)] public float decompressVolume = 1f;

    [Range(0f, 1f)] public float hatchCompressionVolume = 1f;
    [Range(0f, 1f)] public float hatchOpenVolume = 1f;
    [Range(0f, 1f)] public float hatchCloseVolume = 1f;

    [Range(0f, 1f)] public float airlockOpenVolume = 1f;
    [Range(0f, 1f)] public float airlockCloseVolume = 1f;

    [Header("Behavior")]
    [Tooltip("If true, suppress transition sounds on the very first frame after scene load.")]
    public bool suppressInitialTransitions = true;

    [Tooltip("If true, stop the decompress sound and restart it if triggered again.")]
    public bool restartDecompressIfAlreadyPlaying = true;

    [Tooltip("If true, stop and restart hatch open if the delayed trigger fires while already playing.")]
    public bool restartHatchOpenIfAlreadyPlaying = false;

    [Header("Debug")]
    public bool logTransitions = false;

    // Cached previous docking state
    private bool _prevDockActive = false;
    private byte _prevDockPhase = DockingRuntimeState.DOCK_NONE;
    private bool _prevWasHardDocked = false;

    // Cached previous mechanism states
    private byte _prevPortState = DockingOpsController.MECH_CLOSED;
    private byte _prevHatchState = DockingOpsController.MECH_CLOSED;
    private byte _prevAirlockDoorState = DockingOpsController.MECH_CLOSED;

    // Delayed hatch-open trigger
    private float _hatchOpenTimer = -1f;

    private bool _initialized = false;

    void Start()
    {
        PrimePreviousState();
    }

    void Update()
    {
        if (!_initialized)
            PrimePreviousState();

        bool dockActive = GetDockActive();
        byte dockPhase = GetDockPhase();
        bool isHardDocked = dockActive && dockPhase == DockingRuntimeState.DOCK_HARD;

        byte hatchState = GetHatchState();
        byte airlockState = GetAirlockDoorState();

        bool allowTransitionSounds = (!suppressInitialTransitions || _initialized);

        // ------------------------------------------------------------
        // Docking phase transition sounds
        // ------------------------------------------------------------
        if (!_prevDockActive || _prevDockPhase != dockPhase)
        {
            // Soft dock
            if (dockActive && dockPhase == DockingRuntimeState.DOCK_SOFT)
            {
                if (allowTransitionSounds)
                {
                    PlayOneShot(softDockAudio, softDockVolume);
                    if (logTransitions) Debug.Log("[DockingSoundDriver] soft dock");
                }
            }

            // Hard dock
            if (dockActive && dockPhase == DockingRuntimeState.DOCK_HARD)
            {
                if (allowTransitionSounds)
                {
                    PlayOneShot(hardDockAudio, hardDockVolume);
                    if (logTransitions) Debug.Log("[DockingSoundDriver] hard dock");
                }
            }
        }

        // Decompress / undock release proxy:
        // Trigger when docking ends after having previously been hard docked.
        if (_prevDockActive && _prevWasHardDocked && !dockActive)
        {
            if (allowTransitionSounds)
            {
                PlayOneShot(decompressAudio, decompressVolume, restartDecompressIfAlreadyPlaying);
                if (logTransitions) Debug.Log("[DockingSoundDriver] decompress");
            }
        }

        // ------------------------------------------------------------
        // Hatch transition sounds
        // ------------------------------------------------------------

        // Hatch opening: play compression immediately, then delayed hatch open
        if (_prevHatchState != DockingOpsController.MECH_OPENING &&
            hatchState == DockingOpsController.MECH_OPENING)
        {
            if (allowTransitionSounds)
            {
                PlayOneShot(hatchCompressionAudio, hatchCompressionVolume);
                _hatchOpenTimer = Mathf.Max(0f, hatchOpenDelay);

                if (logTransitions) Debug.Log("[DockingSoundDriver] hatch compression");
            }
            else
            {
                _hatchOpenTimer = -1f;
            }
        }

        // Hatch closing
        if (_prevHatchState != DockingOpsController.MECH_CLOSING &&
            hatchState == DockingOpsController.MECH_CLOSING)
        {
            if (allowTransitionSounds)
            {
                PlayOneShot(hatchCloseAudio, hatchCloseVolume);
                if (logTransitions) Debug.Log("[DockingSoundDriver] hatch close");
            }

            // Cancel any pending delayed hatch-open if we reversed direction
            _hatchOpenTimer = -1f;
        }

        // Delayed hatch open sound
        if (_hatchOpenTimer >= 0f)
        {
            _hatchOpenTimer -= Time.deltaTime;

            // Only fire if we're still in opening/open path
            if (_hatchOpenTimer <= 0f)
            {
                if (hatchState == DockingOpsController.MECH_OPENING ||
                    hatchState == DockingOpsController.MECH_OPEN)
                {
                    PlayOneShot(hatchOpenAudio, hatchOpenVolume, restartHatchOpenIfAlreadyPlaying);
                    if (logTransitions) Debug.Log("[DockingSoundDriver] hatch open");
                }

                _hatchOpenTimer = -1f;
            }
        }

        // ------------------------------------------------------------
        // Airlock transition sounds
        // ------------------------------------------------------------

        // Airlock opening
        if (_prevAirlockDoorState != DockingOpsController.MECH_OPENING &&
            airlockState == DockingOpsController.MECH_OPENING)
        {
            if (allowTransitionSounds)
            {
                PlayOneShot(airlockOpenAudio, airlockOpenVolume);
                if (logTransitions) Debug.Log("[DockingSoundDriver] airlock open");
            }
        }

        // Airlock closing
        if (_prevAirlockDoorState != DockingOpsController.MECH_CLOSING &&
            airlockState == DockingOpsController.MECH_CLOSING)
        {
            if (allowTransitionSounds)
            {
                PlayOneShot(airlockCloseAudio, airlockCloseVolume);
                if (logTransitions) Debug.Log("[DockingSoundDriver] airlock close");
            }
        }

        // ------------------------------------------------------------
        // Advance previous state
        // ------------------------------------------------------------
        _prevPortState = GetPortState();
        _prevHatchState = hatchState;
        _prevAirlockDoorState = airlockState;

        _prevDockActive = dockActive;
        _prevDockPhase = dockPhase;
        _prevWasHardDocked = isHardDocked;

        _initialized = true;
    }

    private void PrimePreviousState()
    {
        _prevDockActive = GetDockActive();
        _prevDockPhase = GetDockPhase();
        _prevWasHardDocked = _prevDockActive && _prevDockPhase == DockingRuntimeState.DOCK_HARD;

        _prevPortState = GetPortState();
        _prevHatchState = GetHatchState();
        _prevAirlockDoorState = GetAirlockDoorState();

        _hatchOpenTimer = -1f;
        _initialized = true;
    }

    private bool GetDockActive()
    {
        if (dock == null) return false;
        return dock.active;
    }

    private byte GetDockPhase()
    {
        if (dock == null) return DockingRuntimeState.DOCK_NONE;
        return dock.phase;
    }

    private byte GetPortState()
    {
        if (dockingOps == null) return DockingOpsController.MECH_CLOSED;
        return dockingOps.portState;
    }

    private byte GetHatchState()
    {
        if (dockingOps == null) return DockingOpsController.MECH_CLOSED;
        return dockingOps.hatchState;
    }

    private byte GetAirlockDoorState()
    {
        if (dockingOps == null) return DockingOpsController.MECH_CLOSED;
        return dockingOps.airlockDoorState;
    }

    private float GetDockingGain()
    {
        if (personalSound == null) return 1f;
        return Mathf.Clamp01(personalSound.GetEffectiveDockingGain());
    }

    private void PlayOneShot(AudioSource source, float baseVolume)
    {
        PlayOneShot(source, baseVolume, false);
    }

    private void PlayOneShot(AudioSource source, float baseVolume, bool restartIfPlaying)
    {
        if (source == null) return;

        source.volume = Mathf.Clamp01(baseVolume * GetDockingGain());

        if (restartIfPlaying && source.isPlaying)
            source.Stop();

        source.Play();
    }
}