using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// DockingOpsController
///
/// Owns:
/// - Port mechanism state + animation
/// - Hatch mechanism state + animation
/// - Gating for docking capture and Stewart enable
///
/// Does NOT own:
/// - actual docking capture / retract / hardlock / undock math
///   (that stays in DockingComputer / DockingRuntimeState)
///
/// Networking model:
/// - Only sim authority accepts commands and changes synced state.
/// - Synced state contains discrete state + animation descriptor.
/// - Every client evaluates current 0..1 position locally from synced timing.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DockingOpsController : UdonSharpBehaviour
{
    // ---------------------------------------------------------------------
    // Mechanism states
    // ---------------------------------------------------------------------
    public const byte MECH_CLOSED  = 0;
    public const byte MECH_OPENING = 1;
    public const byte MECH_OPEN    = 2;
    public const byte MECH_CLOSING = 3;

    // ---------------------------------------------------------------------
    // Core refs
    // ---------------------------------------------------------------------
    [Header("Core refs")]
    public SimManager simManager;
    public DockingRuntimeState dock;
    public DockingComputer dockingComp;
    public StewartPlatformController stewart;
    public DockingOccupancyGate occupancyGate;


    // ---------------------------------------------------------------------
    // Port animation refs
    // ---------------------------------------------------------------------
    [Header("Port transforms")]
    public Transform portLeafA;
    public Transform portLeafB;

    [Tooltip("Local Euler angles when the port is fully closed.")]
    public Vector3 portLeafAClosedEuler = Vector3.zero;
    public Vector3 portLeafBClosedEuler = Vector3.zero;

    [Tooltip("Local Euler angles when the port is fully open.")]
    public Vector3 portLeafAOpenEuler = Vector3.zero;
    public Vector3 portLeafBOpenEuler = Vector3.zero;

    [Header("Port timing")]
    [Tooltip("Seconds for full travel from 0 to 1.")]
    public float portFullTravelSeconds = 2.0f;

    // ---------------------------------------------------------------------
    // Hatch animation refs
    // ---------------------------------------------------------------------
    [Header("Hatch transforms")]
    public Transform hatchOuterPivot;
    public Transform hatchDoorPivot;

    [Header("Hatch phase split")]
    [Tooltip("Normalized point where seal pull-out ends and clear-path swing begins.")]
    [Range(0.01f, 0.99f)]
    public float hatchPullOutEnd01 = 0.30f;

    [Header("Hatch outer pivot eulers")]
    [Tooltip("Outer pivot local Euler at fully closed.")]
    public Vector3 hatchOuterClosedEuler = Vector3.zero;

    [Tooltip("Outer pivot local Euler at end of pull-out phase.")]
    public Vector3 hatchOuterPullOutEuler = Vector3.zero;

    [Tooltip("Outer pivot local Euler at fully open.")]
    public Vector3 hatchOuterOpenEuler = Vector3.zero;

    [Header("Hatch door pivot eulers")]
    [Tooltip("Door pivot local Euler at fully closed.")]
    public Vector3 hatchDoorClosedEuler = Vector3.zero;

    [Tooltip("Door pivot local Euler at end of pull-out phase. Usually counters the outer pivot.")]
    public Vector3 hatchDoorPullOutEuler = Vector3.zero;

    [Tooltip("Door pivot local Euler at fully open.")]
    public Vector3 hatchDoorOpenEuler = Vector3.zero;

    [Header("Hatch timing")]
    [Tooltip("Seconds for full travel from 0 to 1.")]
    public float hatchFullTravelSeconds = 2.0f;

    // ---------------------------------------------------------------------
    // Hatch policy
    // ---------------------------------------------------------------------
    [Header("Hatch policy")]
    [Tooltip("Allow hatch opening when hard docked.")]
    public bool allowHatchOpenWhenHardDocked = true;

    [Tooltip("Allow hatch opening after a special override/depress event.")]
    public bool allowHatchOpenOnSpecialDepress = true;

    [UdonSynced]
    [Tooltip("Set true by owner when the special depress/override condition is satisfied.")]
    public bool specialDepressComplete = false;



    // ---------------------------------------------------------------------
    // Airlock door animation refs
    // ---------------------------------------------------------------------
    [Header("Airlock door transform")]
    public Transform airlockDoorTransform;

    [Tooltip("Local position when the airlock door is fully closed.")]
    public Vector3 airlockDoorClosedLocalPos = Vector3.zero;

    [Tooltip("Local position when the airlock door is fully open.")]
    public Vector3 airlockDoorOpenLocalPos = Vector3.zero;

    [Header("Airlock door timing")]
    [Tooltip("Seconds for full travel from 0 to 1.")]
    public float airlockDoorFullTravelSeconds = 2.0f;



    // ---------------------------------------------------------------------
    // Synced mechanism state
    // ---------------------------------------------------------------------
    [Header("Synced state - port")]
    [UdonSynced] public byte portState = MECH_CLOSED;
    [UdonSynced] public byte portStartPosQ = 0;
    [UdonSynced] public ushort portStartTick = 0;

    [Header("Synced state - hatch")]
    [UdonSynced] public byte hatchState = MECH_CLOSED;
    [UdonSynced] public byte hatchStartPosQ = 0;
    [UdonSynced] public ushort hatchStartTick = 0;

    [Header("Synced state - airlock door")]
    [UdonSynced] public byte airlockDoorState = MECH_CLOSED;
    [UdonSynced] public byte airlockDoorStartPosQ = 0;
    [UdonSynced] public ushort airlockDoorStartTick = 0;

    // ---------------------------------------------------------------------
    // Local evaluated positions
    // ---------------------------------------------------------------------
    [Header("Runtime positions")]
    [Range(0f, 1f)] public float portPos01 = 0f;
    [Range(0f, 1f)] public float hatchPos01 = 0f;

    [Range(0f, 1f)] public float airlockDoorPos01 = 0f;


    [Header("Lever integration")]
    public SpacecraftLever hatchLever;

    [Header("Station hatch refs")]
    [Tooltip("Station index key for each station hatch entry.")]
    public int[] stationHatchStationIndex;

    [Tooltip("Station port index key for each station hatch entry.")]
    public int[] stationHatchPortIndex;

    [Tooltip("Single rotating pivot for each station hatch entry.")]
    public Transform[] stationHatchPivot;

    [Header("Station hatch shared motion")]
    [Tooltip("Closed local Euler angles for all station hatches.")]
    public Vector3 stationHatchClosedEuler = Vector3.zero;

    [Tooltip("Open local Euler angles for all station hatches.")]
    public Vector3 stationHatchOpenEuler = Vector3.zero;

    [Tooltip("Seconds for full travel from 0 to 1 for all station hatches.")]
    public float stationHatchFullTravelSeconds = 2.0f;

    [Header("Station hatch policy")]
    [Tooltip("Delay after craft hatch is fully open before opening the mated station hatch.")]
    public float stationHatchOpenDelaySeconds = 0.5f;

    [Tooltip("Force all referenced station hatches closed on Start.")]
    public bool forceCloseAllStationHatchesOnStart = true;

    [Tooltip("Force all referenced station hatches closed if dock is suddenly lost.")]
    public bool forceCloseAllStationHatchesOnDockLoss = true;

    [Header("Synced state - station hatch")]
    [UdonSynced] public byte stationHatchState = MECH_CLOSED;
    [UdonSynced] public byte stationHatchStartPosQ = 0;
    [UdonSynced] public ushort stationHatchStartTick = 0;

    [Header("Runtime position - station hatch")]
    [Range(0f, 1f)] public float stationHatchPos01 = 0f;

    
    // ---------------------------------------------------------------------
    // Derived outputs
    // ---------------------------------------------------------------------
    [Header("Derived outputs")]
    public bool allowDockingCapture = false;
    public bool allowStewart = false;


    private float _stationHatchLocalFrom01 = 0f;
    private float _stationHatchLocalTo01 = 0f;
    private float _stationHatchLocalStartTime = 0f;
    private float _stationHatchLocalDuration = 0f;

    private byte _lastLocalStationHatchState = 255;
    private byte _lastLocalStationHatchStartPosQ = 255;
    private ushort _lastLocalStationHatchStartTick = 65535;

    private float _lastAppliedStationHatchPos01 = -1f;

    private int _activeStationHatchIndex = -1;
    private int _lastResolvedStationIndex = -999;
    private int _lastResolvedStationPortIndex = -999;

    private bool _pendingDelayedStationOpen = false;
    private float _pendingDelayedStationOpenStartTime = 0f;

    private bool _lastDockWasHard = false;

    // ---------------------------------------------------------------------
    // Local animation anchors
    // These are NOT synced. They are rebuilt from synced state changes.
    // ---------------------------------------------------------------------
    private float _portLocalFrom01 = 0f;
    private float _portLocalTo01 = 0f;
    private float _portLocalStartTime = 0f;
    private float _portLocalDuration = 0f;
    private byte _lastLocalPortState = 255;
    private byte _lastLocalPortStartPosQ = 255;
    private ushort _lastLocalPortStartTick = 65535;

    private float _hatchLocalFrom01 = 0f;
    private float _hatchLocalTo01 = 0f;
    private float _hatchLocalStartTime = 0f;
    private float _hatchLocalDuration = 0f;
    private byte _lastLocalHatchState = 255;
    private byte _lastLocalHatchStartPosQ = 255;
    private ushort _lastLocalHatchStartTick = 65535;

    private float _airlockLocalFrom01 = 0f;
    private float _airlockLocalTo01 = 0f;
    private float _airlockLocalStartTime = 0f;
    private float _airlockLocalDuration = 0f;
    private byte _lastLocalAirlockState = 255;
    private byte _lastLocalAirlockStartPosQ = 255;
    private ushort _lastLocalAirlockStartTick = 65535;

    private bool _lastLeverPickupAllowed = true;

    // ---------------------------------------------------------------------
    // Thresholds / debug
    // ---------------------------------------------------------------------
    [Header("Thresholds")]
    [Tooltip("Used for state tests like 'fully open' and 'fully closed'.")]
    public float poseEpsilon = 0.001f;

    [Header("Debug")]
    public bool logState = false;

    private byte _lastLoggedPortState = 255;
    private byte _lastLoggedHatchState = 255;
    private bool _lastLoggedDockingAllowed = false;
    private bool _lastLoggedStewartAllowed = false;


    private float _lastAppliedPortPos01 = -1f;
    private float _lastAppliedHatchPos01 = -1f;
    private float _lastAppliedAirlockDoorPos01 = -1f;

    // ---------------------------------------------------------------------
    // Unity
    // ---------------------------------------------------------------------
    void Start()
    {
        RebuildLocalPortAnimation();
        RebuildLocalHatchAnimation();
        RebuildLocalAirlockAnimation();
        RebuildLocalStationHatchAnimation();

        portPos01 = EvaluateLocalMechanismPosition(portState, _portLocalFrom01, _portLocalTo01, _portLocalStartTime, _portLocalDuration);
        hatchPos01 = EvaluateLocalMechanismPosition(hatchState, _hatchLocalFrom01, _hatchLocalTo01, _hatchLocalStartTime, _hatchLocalDuration);
        airlockDoorPos01 = EvaluateLocalMechanismPosition(airlockDoorState, _airlockLocalFrom01, _airlockLocalTo01, _airlockLocalStartTime, _airlockLocalDuration);
        stationHatchPos01 = EvaluateLocalMechanismPosition(
            stationHatchState,
            _stationHatchLocalFrom01,
            _stationHatchLocalTo01,
            _stationHatchLocalStartTime,
            _stationHatchLocalDuration
        );
        ApplyPortTransforms();
        ApplyHatchTransforms();
        ApplyAirlockDoorTransform();

        if (forceCloseAllStationHatchesOnStart)
        {
            ForceCloseAllStationHatchesImmediate();
        }
        else
        {
            ApplyActiveStationHatchTransform();
        }

        _lastAppliedPortPos01 = portPos01;
        _lastAppliedHatchPos01 = hatchPos01;
        _lastAppliedAirlockDoorPos01 = airlockDoorPos01;
        _lastAppliedStationHatchPos01 = stationHatchPos01;
        UpdateDerivedOutputs();

        RefreshHatchLeverLockout();

        if (hatchLever != null)
        {
            bool hatchLooksOpen = (hatchState == MECH_OPEN || hatchState == MECH_OPENING || hatchPos01 > 0.5f);
            hatchLever.isLeverOpen = hatchLooksOpen;
        }


    }

    void Update()
    {
        ResolveActiveStationHatchFromDock();
        // 1) Evaluate current mechanism positions locally from synced state
        // 1) Rebuild local animation anchors only when synced state changed
        RefreshLocalAnimationAnchorsIfNeeded();

        // 2) Evaluate current mechanism positions from local realtime anchors
        portPos01 = EvaluateLocalMechanismPosition(portState, _portLocalFrom01, _portLocalTo01, _portLocalStartTime, _portLocalDuration);
        hatchPos01 = EvaluateLocalMechanismPosition(hatchState, _hatchLocalFrom01, _hatchLocalTo01, _hatchLocalStartTime, _hatchLocalDuration);
        airlockDoorPos01 = EvaluateLocalMechanismPosition(airlockDoorState, _airlockLocalFrom01, _airlockLocalTo01, _airlockLocalStartTime, _airlockLocalDuration);
                stationHatchPos01 = EvaluateLocalMechanismPosition(
            stationHatchState,
            _stationHatchLocalFrom01,
            _stationHatchLocalTo01,
            _stationHatchLocalStartTime,
            _stationHatchLocalDuration
        );

        
        // 2) Owner finalizes discrete states when travel completes
        if (HasAuthority())
        {
            // UpdateCraftHatchFromLever();
            UpdateStationHatchAutoLogic();
            UpdateStationHatchDockLossSafety();
            OwnerFinalizeCompletedMotion();
            UpdateDerivedOutputs();
            PublishOutputsToExternalSystems();
        }
        else
        {
            // Remotes still derive outputs for lamps/UI, but do not write to sim systems.
            UpdateDerivedOutputs();
        }

        RefreshHatchLeverLockout();
        // 3) Apply actual transforms on every client
        if (Mathf.Abs(portPos01 - _lastAppliedPortPos01) > poseEpsilon)
        {
            ApplyPortTransforms();
            _lastAppliedPortPos01 = portPos01;
        }

        if (Mathf.Abs(hatchPos01 - _lastAppliedHatchPos01) > poseEpsilon)
        {
            ApplyHatchTransforms();
            _lastAppliedHatchPos01 = hatchPos01;
        }

        if (Mathf.Abs(airlockDoorPos01 - _lastAppliedAirlockDoorPos01) > poseEpsilon)
        {
            ApplyAirlockDoorTransform();
            _lastAppliedAirlockDoorPos01 = airlockDoorPos01;
        }


        if (Mathf.Abs(stationHatchPos01 - _lastAppliedStationHatchPos01) > poseEpsilon)
        {
            ApplyActiveStationHatchTransform();
            _lastAppliedStationHatchPos01 = stationHatchPos01;
        }

        // 4) Optional debug logging
        LogStateIfChanged();
    }

    public override void OnDeserialization()
    {
        RebuildLocalPortAnimation();
        RebuildLocalHatchAnimation();
        RebuildLocalAirlockAnimation();

        portPos01 = EvaluateLocalMechanismPosition(portState, _portLocalFrom01, _portLocalTo01, _portLocalStartTime, _portLocalDuration);
        hatchPos01 = EvaluateLocalMechanismPosition(hatchState, _hatchLocalFrom01, _hatchLocalTo01, _hatchLocalStartTime, _hatchLocalDuration);
        airlockDoorPos01 = EvaluateLocalMechanismPosition(airlockDoorState, _airlockLocalFrom01, _airlockLocalTo01, _airlockLocalStartTime, _airlockLocalDuration);

        UpdateDerivedOutputs();
        ApplyPortTransforms();
        ApplyHatchTransforms();
        ApplyAirlockDoorTransform();

        _lastAppliedPortPos01 = portPos01;
        _lastAppliedHatchPos01 = hatchPos01;
        _lastAppliedAirlockDoorPos01 = airlockDoorPos01;


        RebuildLocalStationHatchAnimation();

        stationHatchPos01 = EvaluateLocalMechanismPosition(
            stationHatchState,
            _stationHatchLocalFrom01,
            _stationHatchLocalTo01,
            _stationHatchLocalStartTime,
            _stationHatchLocalDuration
        );

        ApplyActiveStationHatchTransform();
        _lastAppliedStationHatchPos01 = stationHatchPos01;


        RefreshHatchLeverLockout();

    }


    private float EvaluateLocalMechanismPosition(byte state, float from01, float to01, float localStartTime, float duration)
    {
        if (state == MECH_CLOSED) return 0f;
        if (state == MECH_OPEN) return 1f;

        if (duration <= 0.0001f)
            return to01;

        float elapsed = Time.realtimeSinceStartup - localStartTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // smootherstep
        t = t * t * t * (t * (t * 6f - 15f) + 10f);

        return Mathf.Lerp(from01, to01, t);
    }

    // ---------------------------------------------------------------------
    // Public command entry points
    // These are intended to be called by the panel on the owner.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Port button behavior:
    /// - CLOSED  -> OPENING (if legal)
    /// - OPENING -> CLOSING (reverse from current pos)
    /// - OPEN    -> CLOSING (if legal)
    /// - CLOSING -> OPENING (reverse from current pos, if legal)
    /// </summary>
    public void CommandPortButton()
    {
        if (!HasAuthority()) return;

        portPos01 = EvaluateMechanismPosition(portState, portStartPosQ, portStartTick, portFullTravelSeconds);
        if (portState == MECH_CLOSED)
        {
            if (!CanStartPortOpening()) return;
            BeginPortMotion(MECH_OPENING, portPos01, 1f);
            return;
        }

        if (portState == MECH_OPENING)
        {
            if (!CanStartPortClosing()) return;
            BeginPortMotion(MECH_CLOSING, portPos01, 0f);
            return;
        }

        if (portState == MECH_OPEN)
        {
            if (!CanStartPortClosing()) return;
            BeginPortMotion(MECH_CLOSING, portPos01, 0f);
            return;
        }

        if (portState == MECH_CLOSING)
        {
            if (!CanStartPortOpening()) return;
            BeginPortMotion(MECH_OPENING, portPos01, 1f);
            return;
        }
    }

    /// <summary>
    /// Hatch button behavior:
    /// - CLOSED  -> OPENING (if legal)
    /// - OPENING -> CLOSING
    /// - OPEN    -> CLOSING
    /// - CLOSING -> OPENING (if legal)
    /// </summary>
    public void CommandHatchButton()
    {
        if (!HasAuthority()) return;

        hatchPos01 = EvaluateMechanismPosition(hatchState, hatchStartPosQ, hatchStartTick, hatchFullTravelSeconds);
        if (hatchState == MECH_CLOSED)
        {
            if (!CanStartHatchOpening()) return;
            BeginHatchMotion(MECH_OPENING, hatchPos01, 1f);
            return;
        }

        if (hatchState == MECH_OPENING)
        {
            if (!CanStartHatchClosing()) return;
            BeginHatchMotion(MECH_CLOSING, hatchPos01, 0f);
            return;
        }

        if (hatchState == MECH_OPEN)
        {
            if (!CanStartHatchClosing()) return;
            BeginHatchMotion(MECH_CLOSING, hatchPos01, 0f);
            return;
        }

        if (hatchState == MECH_CLOSING)
        {
            if (!CanStartHatchOpening()) return;
            BeginHatchMotion(MECH_OPENING, hatchPos01, 1f);
            return;
        }
    }

    /// <summary>
    /// Airlock button behavior:
    /// - CLOSED  -> OPENING (if legal)
    /// - OPENING -> CLOSING
    /// - OPEN    -> CLOSING
    /// - CLOSING -> OPENING (if legal)
    /// </summary>
    public void CommandAirlockDoorButton()
    {
        if (!HasAuthority()) return;

        airlockDoorPos01 = EvaluateMechanismPosition(airlockDoorState, airlockDoorStartPosQ, airlockDoorStartTick, airlockDoorFullTravelSeconds);

        if (airlockDoorState == MECH_CLOSED)
        {
            if (!CanStartAirlockOpening()) return;
            BeginAirlockDoorMotion(MECH_OPENING, airlockDoorPos01, 1f);
            return;
        }

        if (airlockDoorState == MECH_OPENING)
        {
            if (!CanStartAirlockClosing()) return;
            BeginAirlockDoorMotion(MECH_CLOSING, airlockDoorPos01, 0f);
            return;
        }

        if (airlockDoorState == MECH_OPEN)
        {
            if (!CanStartAirlockClosing()) return;
            BeginAirlockDoorMotion(MECH_CLOSING, airlockDoorPos01, 0f);
            return;
        }

        if (airlockDoorState == MECH_CLOSING)
        {
            if (!CanStartAirlockOpening()) return;
            BeginAirlockDoorMotion(MECH_OPENING, airlockDoorPos01, 1f);
            return;
        }
    }
    public void SetSpecialDepressComplete(bool value)
    {
        if (!HasAuthority()) return;

        if (specialDepressComplete == value) return;

        specialDepressComplete = value;
        RequestSerialization();
    }

    // ---------------------------------------------------------------------
    // Public helpers for panel/UI
    // ---------------------------------------------------------------------
    public bool IsHardDocked()
    {
        return dock != null && dock.active && dock.phase == DockingRuntimeState.DOCK_HARD;
    }

    public bool IsAnyDockActive()
    {
        return dock != null && dock.active;
    }

    public bool IsPortClosed()
    {
        return portPos01 <= poseEpsilon;
    }

    public bool IsPortOpen()
    {
        return portPos01 >= (1f - poseEpsilon);
    }

    public bool IsHatchClosed()
    {
        return hatchPos01 <= poseEpsilon;
    }

    public bool IsHatchOpen()
    {
        return hatchPos01 >= (1f - poseEpsilon);
    }

    public bool IsPortMoving()
    {
        return portState == MECH_OPENING || portState == MECH_CLOSING;
    }

    public bool IsHatchMoving()
    {
        return hatchState == MECH_OPENING || hatchState == MECH_CLOSING;
    }

    public bool IsAirlockDoorClosed()
    {
        return airlockDoorPos01 <= poseEpsilon;
    }

    public bool IsAirlockDoorOpen()
    {
        return airlockDoorPos01 >= (1f - poseEpsilon);
    }

    public bool IsAirlockDoorMoving()
    {
        return airlockDoorState == MECH_OPENING || airlockDoorState == MECH_CLOSING;
    }

    // ---------------------------------------------------------------------
    // Gate logic
    // ---------------------------------------------------------------------
    private bool CanStartPortOpening()
    {
        // Conservative first pass:
        // - do not move port while any docking sequence is active
        // - do not move port unless hatch is fully closed
        if (IsAnyDockActive()) return false;
        if (!IsHatchClosed()) return false;
        return true;
    }

    private bool CanStartPortClosing()
    {
        // Same conservative policy as opening for now
        if (IsAnyDockActive()) return false;
        if (!IsHatchClosed()) return false;
        return true;
    }

    private bool CanStartHatchOpening()
    {
        if (!IsPortOpen()) return false;

        bool hardDocked = IsHardDocked();

        // New interlock:
        // If the inner airlock door is open, hatch opening is only allowed while hard docked.
        if (!hardDocked && !IsAirlockDoorClosed()) return false;

        if (allowHatchOpenWhenHardDocked && hardDocked)
            return true;

        if (allowHatchOpenOnSpecialDepress && specialDepressComplete)
            return true;

        return false;
    }

    private bool CanStartHatchClosing()
    {
        if (occupancyGate != null && occupancyGate.AnyPlayerOutsideCraft())
            return false;

        return true;
    }

    private bool CanStartAirlockOpening()
    {
        if (IsHatchClosed()) return true;
        if (IsHardDocked()) return true;
        return false;
    }

    private bool CanStartAirlockClosing()
    {
        return true;
    }

    // ---------------------------------------------------------------------
    // Motion start helpers
    // ---------------------------------------------------------------------
    private void BeginPortMotion(byte newState, float from01, float to01)
    {
        portState = newState;
        portStartPosQ = Quantize01ToByte(from01);
        portStartTick = EncodeNetTick();
        RequestSerialization();
    }

    private void BeginHatchMotion(byte newState, float from01, float to01)
    {
        hatchState = newState;
        hatchStartPosQ = Quantize01ToByte(from01);
        hatchStartTick = EncodeNetTick();
        RequestSerialization();
    }

    private void BeginAirlockDoorMotion(byte newState, float from01, float to01)
    {
        airlockDoorState = newState;
        airlockDoorStartPosQ = Quantize01ToByte(from01);
        airlockDoorStartTick = EncodeNetTick();
        RequestSerialization();
    }

    // ---------------------------------------------------------------------
    // Runtime evaluation
    // ---------------------------------------------------------------------
    private float EvaluateMechanismPosition(byte state, byte startPosQ, ushort startTick, float fullTravelSeconds)
    {
        if (state == MECH_CLOSED) return 0f;
        if (state == MECH_OPEN) return 1f;

        float startPos01 = DequantizeByte01(startPosQ);
        float elapsed = SecondsSinceNetTick(startTick);

        if (state == MECH_OPENING)
        {
            float duration = Mathf.Max(0.0001f, (1f - startPos01) * fullTravelSeconds);
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * t * (t * (t * 6f - 15f) + 10f);
            return Mathf.Lerp(startPos01, 1f, t);
        }

        if (state == MECH_CLOSING)
        {
            float duration = Mathf.Max(0.0001f, startPos01 * fullTravelSeconds);
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * t * (t * (t * 6f - 15f) + 10f);
            return Mathf.Lerp(startPos01, 0f, t);
        }

        return startPos01;
    }



    private void RefreshLocalAnimationAnchorsIfNeeded()
    {
        if (_lastLocalPortState != portState ||
            _lastLocalPortStartPosQ != portStartPosQ ||
            _lastLocalPortStartTick != portStartTick)
        {
            RebuildLocalPortAnimation();
        }

        if (_lastLocalHatchState != hatchState ||
            _lastLocalHatchStartPosQ != hatchStartPosQ ||
            _lastLocalHatchStartTick != hatchStartTick)
        {
            RebuildLocalHatchAnimation();
        }

        if (_lastLocalAirlockState != airlockDoorState ||
            _lastLocalAirlockStartPosQ != airlockDoorStartPosQ ||
            _lastLocalAirlockStartTick != airlockDoorStartTick)
        {
            RebuildLocalAirlockAnimation();
        }

        if (_lastLocalStationHatchState != stationHatchState ||
            _lastLocalStationHatchStartPosQ != stationHatchStartPosQ ||
            _lastLocalStationHatchStartTick != stationHatchStartTick)
        {
            RebuildLocalStationHatchAnimation();
        }


    }

    private void RebuildLocalPortAnimation()
    {
        _lastLocalPortState = portState;
        _lastLocalPortStartPosQ = portStartPosQ;
        _lastLocalPortStartTick = portStartTick;

        BuildLocalAnimation(
            portState,
            portStartPosQ,
            portStartTick,
            portFullTravelSeconds,
            ref _portLocalFrom01,
            ref _portLocalTo01,
            ref _portLocalStartTime,
            ref _portLocalDuration
        );
    }

    private void RebuildLocalHatchAnimation()
    {
        _lastLocalHatchState = hatchState;
        _lastLocalHatchStartPosQ = hatchStartPosQ;
        _lastLocalHatchStartTick = hatchStartTick;

        BuildLocalAnimation(
            hatchState,
            hatchStartPosQ,
            hatchStartTick,
            hatchFullTravelSeconds,
            ref _hatchLocalFrom01,
            ref _hatchLocalTo01,
            ref _hatchLocalStartTime,
            ref _hatchLocalDuration
        );
    }

    private void RebuildLocalAirlockAnimation()
    {
        _lastLocalAirlockState = airlockDoorState;
        _lastLocalAirlockStartPosQ = airlockDoorStartPosQ;
        _lastLocalAirlockStartTick = airlockDoorStartTick;

        BuildLocalAnimation(
            airlockDoorState,
            airlockDoorStartPosQ,
            airlockDoorStartTick,
            airlockDoorFullTravelSeconds,
            ref _airlockLocalFrom01,
            ref _airlockLocalTo01,
            ref _airlockLocalStartTime,
            ref _airlockLocalDuration
        );
    }

    private void BuildLocalAnimation(
        byte state,
        byte startPosQ,
        ushort startTick,
        float fullTravelSeconds,
        ref float localFrom01,
        ref float localTo01,
        ref float localStartTime,
        ref float localDuration)
    {
        if (state == MECH_CLOSED)
        {
            localFrom01 = 0f;
            localTo01 = 0f;
            localStartTime = Time.realtimeSinceStartup;
            localDuration = 0f;
            return;
        }

        if (state == MECH_OPEN)
        {
            localFrom01 = 1f;
            localTo01 = 1f;
            localStartTime = Time.realtimeSinceStartup;
            localDuration = 0f;
            return;
        }

        float startPos01 = DequantizeByte01(startPosQ);
        float currentPos01 = EvaluateMechanismPosition(state, startPosQ, startTick, fullTravelSeconds);

        if (state == MECH_OPENING)
        {
            localFrom01 = currentPos01;
            localTo01 = 1f;
            localDuration = Mathf.Max(0.0001f, (1f - currentPos01) * fullTravelSeconds);
            localStartTime = Time.realtimeSinceStartup;
            return;
        }

        // MECH_CLOSING
        localFrom01 = currentPos01;
        localTo01 = 0f;
        localDuration = Mathf.Max(0.0001f, currentPos01 * fullTravelSeconds);
        localStartTime = Time.realtimeSinceStartup;
    }

    private void OwnerFinalizeCompletedMotion()
    {
        if (portState == MECH_OPENING && portPos01 >= (1f - poseEpsilon))
        {
            portState = MECH_OPEN;
            portStartPosQ = 255;
            portStartTick = EncodeNetTick();
            RequestSerialization();
        }
        else if (portState == MECH_CLOSING && portPos01 <= poseEpsilon)
        {
            portState = MECH_CLOSED;
            portStartPosQ = 0;
            portStartTick = EncodeNetTick();
            RequestSerialization();
        }

        if (hatchState == MECH_OPENING && hatchPos01 >= (1f - poseEpsilon))
        {
            hatchState = MECH_OPEN;
            hatchStartPosQ = 255;
            hatchStartTick = EncodeNetTick();
            RequestSerialization();
        }
        else if (hatchState == MECH_CLOSING && hatchPos01 <= poseEpsilon)
        {
            hatchState = MECH_CLOSED;
            hatchStartPosQ = 0;
            hatchStartTick = EncodeNetTick();
            RequestSerialization();
        }

        if (airlockDoorState == MECH_OPENING && airlockDoorPos01 >= (1f - poseEpsilon))
        {
            airlockDoorState = MECH_OPEN;
            airlockDoorStartPosQ = 255;
            airlockDoorStartTick = EncodeNetTick();
            RequestSerialization();
        }
        else if (airlockDoorState == MECH_CLOSING && airlockDoorPos01 <= poseEpsilon)
        {
            airlockDoorState = MECH_CLOSED;
            airlockDoorStartPosQ = 0;
            airlockDoorStartTick = EncodeNetTick();
            RequestSerialization();
        }

        if (stationHatchState == MECH_OPENING && stationHatchPos01 >= (1f - poseEpsilon))
        {
            stationHatchState = MECH_OPEN;
            stationHatchStartPosQ = 255;
            stationHatchStartTick = EncodeNetTick();
            RequestSerialization();
        }
        else if (stationHatchState == MECH_CLOSING && stationHatchPos01 <= poseEpsilon)
        {
            stationHatchState = MECH_CLOSED;
            stationHatchStartPosQ = 0;
            stationHatchStartTick = EncodeNetTick();
            RequestSerialization();
        }

    }

    // ---------------------------------------------------------------------
    // Transform application
    // ---------------------------------------------------------------------
    private void ApplyPortTransforms()
    {
        Quaternion qa = Quaternion.Euler(Vector3.Lerp(portLeafAClosedEuler, portLeafAOpenEuler, portPos01));
        Quaternion qb = Quaternion.Euler(Vector3.Lerp(portLeafBClosedEuler, portLeafBOpenEuler, portPos01));

        if (portLeafA != null) portLeafA.localRotation = qa;
        if (portLeafB != null) portLeafB.localRotation = qb;
    }

    private void ApplyHatchTransforms()
    {
        float split = Mathf.Clamp(hatchPullOutEnd01, 0.01f, 0.99f);
        float p = Mathf.Clamp01(hatchPos01);

        Vector3 outerEuler;
        Vector3 doorEuler;

        if (p <= split)
        {
            float t = p / split;

            outerEuler = Vector3.Lerp(hatchOuterClosedEuler, hatchOuterPullOutEuler, t);
            doorEuler = Vector3.Lerp(hatchDoorClosedEuler, hatchDoorPullOutEuler, t);
        }
        else
        {
            float t = (p - split) / (1f - split);

            outerEuler = Vector3.Lerp(hatchOuterPullOutEuler, hatchOuterOpenEuler, t);
            doorEuler = Vector3.Lerp(hatchDoorPullOutEuler, hatchDoorOpenEuler, t);
        }

        if (hatchOuterPivot != null)
            hatchOuterPivot.localRotation = Quaternion.Euler(outerEuler);

        if (hatchDoorPivot != null)
            hatchDoorPivot.localRotation = Quaternion.Euler(doorEuler);
    }

    private void ApplyAirlockDoorTransform()
    {
        if (airlockDoorTransform == null) return;

        airlockDoorTransform.localPosition =
            Vector3.Lerp(airlockDoorClosedLocalPos, airlockDoorOpenLocalPos, airlockDoorPos01);
    }

    // ---------------------------------------------------------------------
    // Derived outputs
    // ---------------------------------------------------------------------
    private void UpdateDerivedOutputs()
    {
        bool dockActive = IsAnyDockActive();
        bool portOpen = IsPortOpen();

        // Both of these are tied only to:
        // - active dock, or
        // - port open
        allowDockingCapture = dockActive || portOpen;

        bool portFullyOpenDiscrete = (portState == MECH_OPEN);
        allowStewart = dockActive || portFullyOpenDiscrete;
    }

    private void PublishOutputsToExternalSystems()
    {
        // SimManager already uses dockingAllowed as the external policy gate.
        if (simManager != null)
            simManager.dockingAllowed = allowDockingCapture;

        // Stewart enable is also just a bool gate in the current setup.
        if (stewart != null)
            stewart.platformEnabled = allowStewart;
    }


    public void ForcePortOpenInstant()
    {
        portState = MECH_OPEN;
        portStartPosQ = 255;
        portStartTick = EncodeNetTick();
        portPos01 = 1f;

        RebuildLocalPortAnimation();
        ApplyPortTransforms();

        UpdateDerivedOutputs();

        if (HasAuthority())
        {
            PublishOutputsToExternalSystems();
            RequestSerialization();
        }

        _lastAppliedPortPos01 = portPos01;

        if (logState)
        {
            Debug.Log("[DockingOpsController] ForcePortOpenInstant()");
        }
    }


    public void ForcePortClosedInstant()
    {
        portState = MECH_CLOSED;
        portStartPosQ = 0;
        portStartTick = EncodeNetTick();
        portPos01 = 0f;

        RebuildLocalPortAnimation();
        ApplyPortTransforms();

        UpdateDerivedOutputs();

        if (HasAuthority())
        {
            PublishOutputsToExternalSystems();
            RequestSerialization();
        }

        _lastAppliedPortPos01 = portPos01;

        if (logState)
        {
            Debug.Log("[DockingOpsController] ForcePortClosedInstant()");
        }
    }






    private void RebuildLocalStationHatchAnimation()
    {
        _lastLocalStationHatchState = stationHatchState;
        _lastLocalStationHatchStartPosQ = stationHatchStartPosQ;
        _lastLocalStationHatchStartTick = stationHatchStartTick;

        BuildLocalAnimation(
            stationHatchState,
            stationHatchStartPosQ,
            stationHatchStartTick,
            stationHatchFullTravelSeconds,
            ref _stationHatchLocalFrom01,
            ref _stationHatchLocalTo01,
            ref _stationHatchLocalStartTime,
            ref _stationHatchLocalDuration
        );
    }

    private void ApplyActiveStationHatchTransform()
    {
        if (_activeStationHatchIndex < 0) return;
        if (stationHatchPivot == null) return;
        if (_activeStationHatchIndex >= stationHatchPivot.Length) return;

        Transform pivot = stationHatchPivot[_activeStationHatchIndex];
        if (pivot == null) return;

        Vector3 euler = Vector3.Lerp(
            stationHatchClosedEuler,
            stationHatchOpenEuler,
            stationHatchPos01
        );

        pivot.localRotation = Quaternion.Euler(euler);
    }

    private void ForceCloseActiveStationHatchImmediate()
    {
        if (_activeStationHatchIndex < 0) return;
        if (stationHatchPivot == null) return;
        if (_activeStationHatchIndex >= stationHatchPivot.Length) return;

        Transform pivot = stationHatchPivot[_activeStationHatchIndex];
        if (pivot == null) return;

        pivot.localRotation = Quaternion.Euler(stationHatchClosedEuler);
    }

    private void ForceCloseAllStationHatchesImmediate()
    {
        if (stationHatchPivot == null) return;

        int n = stationHatchPivot.Length;
        for (int i = 0; i < n; i++)
        {
            Transform pivot = stationHatchPivot[i];
            if (pivot == null) continue;

            pivot.localRotation = Quaternion.Euler(stationHatchClosedEuler);
        }

        stationHatchPos01 = 0f;
        _lastAppliedStationHatchPos01 = stationHatchPos01;
    }

    private void BeginStationHatchMotion(byte newState, float from01, float to01)
    {
        stationHatchState = newState;
        stationHatchStartPosQ = Quantize01ToByte(from01);
        stationHatchStartTick = EncodeNetTick();
        RequestSerialization();
    }

    private void UpdateStationHatchAutoLogic()
    {
        bool hardDocked = IsHardDocked();
        bool craftHatchFullyOpen = IsHatchOpen();

        if (!hardDocked)
        {
            CancelPendingStationHatchOpen();

            if (stationHatchState == MECH_OPEN || stationHatchState == MECH_OPENING)
            {
                stationHatchPos01 = EvaluateMechanismPosition(
                    stationHatchState,
                    stationHatchStartPosQ,
                    stationHatchStartTick,
                    stationHatchFullTravelSeconds
                );

                BeginStationHatchMotion(MECH_CLOSING, stationHatchPos01, 0f);
            }

            return;
        }

        if (_activeStationHatchIndex < 0)
        {
            CancelPendingStationHatchOpen();
            return;
        }

        if (craftHatchFullyOpen)
        {
            if (stationHatchState == MECH_CLOSED && !_pendingDelayedStationOpen)
            {
                _pendingDelayedStationOpen = true;
                _pendingDelayedStationOpenStartTime = Time.realtimeSinceStartup;
            }

            if (_pendingDelayedStationOpen)
            {
                float elapsed = Time.realtimeSinceStartup - _pendingDelayedStationOpenStartTime;

                if (elapsed >= stationHatchOpenDelaySeconds)
                {
                    _pendingDelayedStationOpen = false;

                    stationHatchPos01 = EvaluateMechanismPosition(
                        stationHatchState,
                        stationHatchStartPosQ,
                        stationHatchStartTick,
                        stationHatchFullTravelSeconds
                    );

                    BeginStationHatchMotion(MECH_OPENING, stationHatchPos01, 1f);
                }
            }
        }
        else
        {
            CancelPendingStationHatchOpen();

            if (stationHatchState == MECH_OPEN || stationHatchState == MECH_OPENING)
            {
                stationHatchPos01 = EvaluateMechanismPosition(
                    stationHatchState,
                    stationHatchStartPosQ,
                    stationHatchStartTick,
                    stationHatchFullTravelSeconds
                );

                BeginStationHatchMotion(MECH_CLOSING, stationHatchPos01, 0f);
            }
        }
    }

    private void CancelPendingStationHatchOpen()
    {
        _pendingDelayedStationOpen = false;
    }

    private void UpdateStationHatchDockLossSafety()
    {
        bool hardDocked = IsHardDocked();

        if (_lastDockWasHard && !hardDocked)
        {
            CancelPendingStationHatchOpen();

            if (forceCloseAllStationHatchesOnDockLoss)
                ForceCloseAllStationHatchesImmediate();
            else
                ForceCloseActiveStationHatchImmediate();

            stationHatchState = MECH_CLOSED;
            stationHatchStartPosQ = 0;
            stationHatchStartTick = EncodeNetTick();
            stationHatchPos01 = 0f;
            RequestSerialization();
        }

        _lastDockWasHard = hardDocked;
        
    }


    private void RefreshHatchLeverLockout()
    {
        if (hatchLever == null) return;

        bool openingAllowed = CanStartHatchOpening();
        bool hatchOpenSideAccessible =
            (hatchState == MECH_OPEN) ||
            (hatchState == MECH_OPENING) ||
            (hatchPos01 > poseEpsilon);

        bool shouldAllowPickup = hatchOpenSideAccessible || openingAllowed;

        if (shouldAllowPickup == _lastLeverPickupAllowed)
            return;

        _lastLeverPickupAllowed = shouldAllowPickup;

        if (shouldAllowPickup)
            hatchLever.SetPickupOn();
        else
            hatchLever.SetPickupOff();

        hatchLever.handle.pickupable = shouldAllowPickup;
    }

    // private void UpdateCraftHatchFromLever()
    // {
    //     if (hatchLever == null) return;

    //     bool wantOpen = hatchLever.isLeverOpen;
    //     float currentPos01 = hatchPos01; // use the smooth local-evaluated position from this frame

    //     if (wantOpen)
    //     {
    //         if (hatchState == MECH_CLOSED || hatchState == MECH_CLOSING)
    //         {
    //             if (!CanStartHatchOpening()) return;
    //             BeginHatchMotion(MECH_OPENING, currentPos01, 1f);
    //         }
    //     }
    //     else
    //     {
    //         if (hatchState == MECH_OPEN || hatchState == MECH_OPENING)
    //         {
    //             if (!CanStartHatchClosing()) return;
    //             BeginHatchMotion(MECH_CLOSING, currentPos01, 0f);
    //         }
    //     }
    // }

    public void Net_RequestHatchOpenFromLever()
    {
        if (!HasAuthority()) return;

        float currentPos01 = hatchPos01;

        if (hatchState == MECH_CLOSED || hatchState == MECH_CLOSING)
        {
            if (!CanStartHatchOpening()) return;
            BeginHatchMotion(MECH_OPENING, currentPos01, 1f);
        }
    }

    public void Net_RequestHatchCloseFromLever()
    {
        if (!HasAuthority()) return;

        float currentPos01 = hatchPos01;

        if (hatchState == MECH_OPEN || hatchState == MECH_OPENING)
        {
            if (!CanStartHatchClosing()) return;
            BeginHatchMotion(MECH_CLOSING, currentPos01, 0f);
        }
    }

    // ---------------------------------------------------------------------
    // Utilities
    // ---------------------------------------------------------------------

    private const float NET_TICK_HZ = 60f;
    private const float INV_255 = 1f / 255f;

    private byte Quantize01ToByte(float x)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(x) * 255f), 0, 255);
    }

    private float DequantizeByte01(byte q)
    {
        return q * INV_255;
    }

    private ushort EncodeNetTick()
    {
        int tick = Mathf.FloorToInt(GetSharedTimeSeconds() * NET_TICK_HZ);
        return (ushort)(tick & 0xFFFF);
    }

    private float SecondsSinceNetTick(ushort startTick)
    {
        float nowTicksFloat = GetSharedTimeSeconds() * NET_TICK_HZ;

        int nowWholeTicks = Mathf.FloorToInt(nowTicksFloat);
        float nowFracTick = nowTicksFloat - nowWholeTicks;

        int nowWrapped = nowWholeTicks & 0xFFFF;
        int deltaWrapped = (nowWrapped - (int)startTick) & 0xFFFF;

        // Interpret as recent forward time difference.
        // For door motion, real deltas should always be small and positive.
        if (deltaWrapped > 32767)
            deltaWrapped -= 65536;

        return (deltaWrapped + nowFracTick) / NET_TICK_HZ;
    }


    private bool HasAuthority()
    {
        if (simManager != null) return simManager.IsSimOwner();
        return Networking.IsOwner(gameObject);
    }

    private float GetSharedTimeSeconds()
    {
        return (float)Networking.GetServerTimeInSeconds();
    }

    private void LogStateIfChanged()
    {
        if (!logState) return;

        if (_lastLoggedPortState == portState &&
            _lastLoggedHatchState == hatchState &&
            _lastLoggedDockingAllowed == allowDockingCapture &&
            _lastLoggedStewartAllowed == allowStewart)
        {
            return;
        }

        _lastLoggedPortState = portState;
        _lastLoggedHatchState = hatchState;
        _lastLoggedDockingAllowed = allowDockingCapture;
        _lastLoggedStewartAllowed = allowStewart;

        Debug.Log(
            "[DockingOpsController] " +
            "portState=" + portState +
            " portPos01=" + portPos01 +
            " hatchState=" + hatchState +
            " hatchPos01=" + hatchPos01 +
            " allowDockingCapture=" + allowDockingCapture +
            " allowStewart=" + allowStewart +
            " hardDocked=" + IsHardDocked() +
            " dockActive=" + IsAnyDockActive() +
            " specialDepressComplete=" + specialDepressComplete
        );
    }

    private void ResolveActiveStationHatchFromDock()
    {
        int stationIndex = -1;
        int stationPortIndex = -1;

        if (dock != null && dock.active && dock.phase == DockingRuntimeState.DOCK_HARD)
        {
            stationIndex = dock.dockedStationIndex;
            stationPortIndex = dock.stationPortIndex;
        }

        if (stationIndex == _lastResolvedStationIndex &&
            stationPortIndex == _lastResolvedStationPortIndex)
        {
            return;
        }

        _lastResolvedStationIndex = stationIndex;
        _lastResolvedStationPortIndex = stationPortIndex;

        _activeStationHatchIndex = FindStationHatchIndex(stationIndex, stationPortIndex);
    }

    private int FindStationHatchIndex(int stationIndex, int stationPortIndex)
    {
        if (stationHatchStationIndex == null) return -1;
        if (stationHatchPortIndex == null) return -1;
        if (stationHatchPivot == null) return -1;

        int n = stationHatchStationIndex.Length;
        if (stationHatchPortIndex.Length < n) n = stationHatchPortIndex.Length;
        if (stationHatchPivot.Length < n) n = stationHatchPivot.Length;

        for (int i = 0; i < n; i++)
        {
            if (stationHatchStationIndex[i] != stationIndex) continue;
            if (stationHatchPortIndex[i] != stationPortIndex) continue;
            if (stationHatchPivot[i] == null) continue;

            return i;
        }

        return -1;
    }


}