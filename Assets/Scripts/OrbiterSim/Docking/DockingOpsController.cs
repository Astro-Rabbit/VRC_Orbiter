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
    // ---------------------------------------------------------------------
    // Derived outputs
    // ---------------------------------------------------------------------
    [Header("Derived outputs")]
    public bool allowDockingCapture = false;
    public bool allowStewart = false;




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
        portPos01 = EvaluateMechanismPosition(portState, portStartPosQ, portStartTick, portFullTravelSeconds);
        hatchPos01 = EvaluateMechanismPosition(hatchState, hatchStartPosQ, hatchStartTick, hatchFullTravelSeconds);
        airlockDoorPos01 = EvaluateMechanismPosition(airlockDoorState, airlockDoorStartPosQ, airlockDoorStartTick, airlockDoorFullTravelSeconds);
        ApplyPortTransforms();
        ApplyHatchTransforms();
        ApplyAirlockDoorTransform();


        _lastAppliedPortPos01 = portPos01;
        _lastAppliedHatchPos01 = hatchPos01;
        _lastAppliedAirlockDoorPos01 = airlockDoorPos01;

        UpdateDerivedOutputs();

    }

    void Update()
    {
        // 1) Evaluate current mechanism positions locally from synced state
        portPos01 = EvaluateMechanismPosition(portState, portStartPosQ, portStartTick, portFullTravelSeconds);
        hatchPos01 = EvaluateMechanismPosition(hatchState, hatchStartPosQ, hatchStartTick, hatchFullTravelSeconds);
        airlockDoorPos01 = EvaluateMechanismPosition(airlockDoorState, airlockDoorStartPosQ, airlockDoorStartTick, airlockDoorFullTravelSeconds);
        // 2) Owner finalizes discrete states when travel completes
        if (HasAuthority())
        {
            OwnerFinalizeCompletedMotion();
            UpdateDerivedOutputs();
            PublishOutputsToExternalSystems();
        }
        else
        {
            // Remotes still derive outputs for lamps/UI, but do not write to sim systems.
            UpdateDerivedOutputs();
        }

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
        // 4) Optional debug logging
        LogStateIfChanged();
    }

    public override void OnDeserialization()
    {
        portPos01 = EvaluateMechanismPosition(portState, portStartPosQ, portStartTick, portFullTravelSeconds);
        hatchPos01 = EvaluateMechanismPosition(hatchState, hatchStartPosQ, hatchStartTick, hatchFullTravelSeconds);
        airlockDoorPos01 = EvaluateMechanismPosition(airlockDoorState, airlockDoorStartPosQ, airlockDoorStartTick, airlockDoorFullTravelSeconds);
        UpdateDerivedOutputs();
        ApplyPortTransforms();
        ApplyHatchTransforms();
        ApplyAirlockDoorTransform();

        _lastAppliedPortPos01 = portPos01;
        _lastAppliedHatchPos01 = hatchPos01;
        _lastAppliedAirlockDoorPos01 = airlockDoorPos01;

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
            return Mathf.Lerp(startPos01, 1f, t);
        }

        if (state == MECH_CLOSING)
        {
            float duration = Mathf.Max(0.0001f, startPos01 * fullTravelSeconds);
            float t = Mathf.Clamp01(elapsed / duration);
            return Mathf.Lerp(startPos01, 0f, t);
        }

        return startPos01;
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
        // This is the external "docking system allowed/armed" gate.
        // Do NOT drop it just because dock.active became true, or softlock/retract/hardlock flow breaks.
        allowDockingCapture =
            IsPortOpen() &&
            IsHatchClosed();

        // Stewart probably should still turn off once docking is active.
        allowStewart =
            IsPortOpen() &&
            IsHatchClosed() &&
            !IsAnyDockActive();
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

    // ---------------------------------------------------------------------
    // Utilities
    // ---------------------------------------------------------------------

    private const float NET_TICK_HZ = 20f;
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
        int nowTick = Mathf.FloorToInt(GetSharedTimeSeconds() * NET_TICK_HZ) & 0xFFFF;
        int delta = (nowTick - startTick) & 0xFFFF;
        return delta / NET_TICK_HZ;
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
}