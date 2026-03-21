using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CockpitAuthorityManager : UdonSharpBehaviour
{
    public const byte SEAT_LEFT = 0;
    public const byte SEAT_RIGHT = 1;

    [Header("Sim / policy")]
    public SimManager simManager;

    [Header("Seat-local hand controls")]
    public HandControls leftHandControls;
    public HandControls rightHandControls;

    [Header("Seat visual net states (one object per seat)")]
    public CockpitControlsNetState leftControlsNet;
    public CockpitControlsNetState rightControlsNet;

    [Header("Seat-local manual states")]
    public GC_ManualDraft leftManualLocal;
    public GC_ManualDraft rightManualLocal;

    [Header("Seat-local override states")]
    public GC_ActuatorOverrideState leftOverrideLocal;
    public GC_ActuatorOverrideState rightOverrideLocal;

    [Header("Live shared states read by GC")]
    public GC_ManualDraft sharedManualLive;
    public GC_ActuatorOverrideState sharedOverridesLive;

    [Header("Authority state")]
    [Tooltip("Which seat is currently the active control seat. 0=left, 1=right.")]
    [UdonSynced] public byte activeSeat = SEAT_LEFT;

    [UdonSynced] private ushort _authorityRev = 0;
    private ushort _lastAppliedAuthorityRev = 0;

    [Tooltip("If the active seat releases and the opposite seat is requesting, wait this long before handoff.")]
    public float seatTransferDelaySeconds = 0.5f;

    [Tooltip("Minimum time between repeated ownership-transfer requests.")]
    public float ownershipRetrySeconds = 0.75f;

    [Header("Optional joystick status lights")]
    public Renderer leftJoystickLightRenderer;
    public Renderer rightJoystickLightRenderer;
    public int lightMaterialIndex = 0;
    public Color lightAuthorityColor = Color.green;
    public Color lightBlockedColor = Color.red;
    public Color lightOffColor = Color.black;
    public string emissionProperty = "_EmissionColor";

    [Header("Ownership handoff gating")]
    [Tooltip("After requesting sim ownership for a seat, block that seat from writing manual input until ownership is actually established.")]
    public bool blockInputWhileOwnershipPending = true;

    [Tooltip("Optional short grace after ownership arrives before enabling input.")]
    public float ownershipAcquireGraceSeconds = 0.05f;

    [Header("Debug")]
    public bool applyContinuously = true;
    public bool logSeatChanges = false;
    public bool logOwnershipRequests = false;

    // ---------------------------------------------------------------------
    // Explicit local request state
    // ---------------------------------------------------------------------

    [Header("Local request mirrors (debug)")]
    public bool leftSeatRequestLocal = false;
    public bool rightSeatRequestLocal = false;

    // ---------------------------------------------------------------------
    // Internal
    // ---------------------------------------------------------------------

    private bool _ownershipRequestPending = false;
    private byte _pendingSeat = SEAT_LEFT;
    private float _ownershipAcquireTime = -1f;
    private bool _prevSimOwner = false;

    private float _activeSeatReleaseStartTime = -1f;
    private float _lastOwnershipRequestTime = -999f;
    private byte _lastLoggedActiveSeat = 255;

    void Start()
    {
        _prevSimOwner = (simManager != null && simManager.IsSimOwner());
        _lastAppliedAuthorityRev = _authorityRev;
        UpdateSeatStateFlags();
        ApplyActiveSeatToSharedIfOwner();
        UpdateJoystickLights();
    }

    void Update()
    {
        if (!applyContinuously) return;
        TickAuthority();
    }

    // =====================================================================
    // PUBLIC API - seat request / release
    // =====================================================================

    public void SetSeatRequesting(byte seat, bool requesting)
    {
        seat = ClampSeat(seat);

        if (seat == SEAT_RIGHT) rightSeatRequestLocal = requesting;
        else leftSeatRequestLocal = requesting;

        // If this seat is already active and it just released, start the release timer now.
        if (activeSeat == seat && !requesting)
        {
            if (_activeSeatReleaseStartTime < 0f)
                _activeSeatReleaseStartTime = Time.time;
        }
    }

    public void RequestTakeControl(byte seat)
    {
        SetSeatRequesting(seat, true);
    }

    public void ReleaseControl(byte seat)
    {
        SetSeatRequesting(seat, false);
    }

    public bool SeatHasControl(byte seat)
    {
        return activeSeat == ClampSeat(seat);
    }

    public bool SeatIsRequesting(byte seat)
    {
        return IsSeatRequestingObserved(ClampSeat(seat));
    }

    public bool SeatIsRequestingLocal(byte seat)
    {
        seat = ClampSeat(seat);
        return (seat == SEAT_RIGHT) ? rightSeatRequestLocal : leftSeatRequestLocal;
    }

    public bool SeatIsContested(byte seat)
    {
        seat = ClampSeat(seat);
        if (activeSeat != seat) return false;
        return IsSeatRequestingObserved(OppositeSeat(seat));
    }

    public bool SeatCanTakeControl(byte seat)
    {
        seat = ClampSeat(seat);

        if (activeSeat == seat) return false;

        // "Can take now" = current active seat is no longer requesting.
        // Actual handoff still respects delay / ownership rules.
        return !IsSeatRequestingObserved(activeSeat);
    }

    // =====================================================================
    // MAIN TICK
    // =====================================================================

    public void TickAuthority()
    {
        UpdateOwnershipArrivalState();

        bool activeSeatStillRequesting = IsSeatRequestingObserved(activeSeat);
        byte otherSeat = OppositeSeat(activeSeat);
        bool otherSeatRequesting = IsSeatRequestingObserved(otherSeat);

        // Active seat keeps control while still requesting.
        if (activeSeatStillRequesting)
        {
            _activeSeatReleaseStartTime = -1f;
        }
        else
        {
            if (_activeSeatReleaseStartTime < 0f)
                _activeSeatReleaseStartTime = Time.time;

            if (otherSeatRequesting)
            {
                float dtReleased = Time.time - _activeSeatReleaseStartTime;
                if (dtReleased >= seatTransferDelaySeconds)
                {
                    TryHandoffToSeat(otherSeat);
                }
            }
        }

        UpdateSeatStateFlags();
        ApplyActiveSeatToSharedIfOwner();
        UpdateJoystickLights();

        if (_lastLoggedActiveSeat != activeSeat)
        {
            if (logSeatChanges)
                Debug.Log("[CockpitAuthorityManager] Active seat -> " + SeatName(activeSeat));

            _lastLoggedActiveSeat = activeSeat;
        }
    }

    // =====================================================================
    // REQUEST OBSERVATION
    // =====================================================================

    /// <summary>
    /// Final observed request state for a seat.
    ///
    /// Sources:
    /// - explicit local request flag (desktop toggle / future explicit VR request)
    /// - immediate local VR grab fallback
    /// - synced seat visual-net claimed flag (for remote observers / remote request visibility)
    /// </summary>
    private bool IsSeatRequestingObserved(byte seat)
    {
        seat = ClampSeat(seat);

        // Explicit local request state.
        bool localRequest = (seat == SEAT_RIGHT) ? rightSeatRequestLocal : leftSeatRequestLocal;
        if (localRequest) return true;

        // Immediate local VR fallback so grabs count instantly even before other scripts are updated.
        HandControls hc = GetSeatHandControls(seat);
        if (hc != null && hc.IsAnyPrimaryControlGrabbed()) return true;

        // Remote / network-observed request state.
        CockpitControlsNetState net = GetSeatControlsNet(seat);
        if (net != null && net.IsClaimed()) return true;

        return false;
    }

    private CockpitControlsNetState GetSeatControlsNet(byte seat)
    {
        return (seat == SEAT_RIGHT) ? rightControlsNet : leftControlsNet;
    }

    // =====================================================================
    // HANDOFF
    // =====================================================================

    private void TryHandoffToSeat(byte seat)
    {
        seat = ClampSeat(seat);

        // Only the local requester should initiate ownership handoff for its seat.
        if (!SeatIsRequestingLocal(seat))
            return;

        bool simOwner = (simManager != null && simManager.IsSimOwner());

        if (simOwner)
        {
            SetActiveSeatInternal(seat);
            return;
        }

        if (simManager != null && !simManager.CanApproveOwnershipTransfer())
            return;

        if ((Time.time - _lastOwnershipRequestTime) < ownershipRetrySeconds)
            return;

        _lastOwnershipRequestTime = Time.time;

        _ownershipRequestPending = true;
        _pendingSeat = seat;
        _ownershipAcquireTime = -1f;

        if (logOwnershipRequests)
            Debug.Log("[CockpitAuthorityManager] Requesting sim ownership for seat " + SeatName(seat));

        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null || simManager == null) return;

        simManager.SendCustomNetworkEvent(
            VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner,
            nameof(SimManager.Evt_RequestHandoff),
            local.playerId
        );
    }

    private void SetActiveSeatInternal(byte seat)
    {
        seat = ClampSeat(seat);
        if (activeSeat == seat) return;

        bool simOwner = (simManager != null && simManager.IsSimOwner());
        if (!simOwner) return;

        EnsureLocalOwnershipOfAuthority();

        activeSeat = seat;
        _authorityRev++;
        _activeSeatReleaseStartTime = -1f;

        RequestSerialization();
    }
    public void SetActiveSeatLeft()
    {
        SetActiveSeatInternal(SEAT_LEFT);
    }

    public void SetActiveSeatRight()
    {
        SetActiveSeatInternal(SEAT_RIGHT);
    }

    // =====================================================================
    // SEAT FLAGS -> INPUT SOURCES / VISUALS
    // =====================================================================

    private void UpdateSeatStateFlags()
    {
        bool simOwner = (simManager != null && simManager.IsSimOwner());
        bool inputBlocked = IsOwnershipInputBlocked();

        bool leftRequested = IsSeatRequestingObserved(SEAT_LEFT);
        bool rightRequested = IsSeatRequestingObserved(SEAT_RIGHT);

        bool leftActive = (activeSeat == SEAT_LEFT);
        bool rightActive = (activeSeat == SEAT_RIGHT);

        if (leftHandControls != null)
        {
            leftHandControls.SetSeatClaimed(leftRequested);
            leftHandControls.SetSeatActiveForVisuals(leftActive);
            leftHandControls.SetSeatAuthority(simOwner && leftActive && !inputBlocked);
        }

        if (rightHandControls != null)
        {
            rightHandControls.SetSeatClaimed(rightRequested);
            rightHandControls.SetSeatActiveForVisuals(rightActive);
            rightHandControls.SetSeatAuthority(simOwner && rightActive && !inputBlocked);
        }
    }

    // =====================================================================
    // LIVE SHARED GC WRITE
    // =====================================================================

    private void ApplyActiveSeatToSharedIfOwner()
    {
        if (sharedManualLive == null || sharedOverridesLive == null)
            return;

        if (simManager == null || !simManager.IsSimOwner())
            return;

        if (IsOwnershipInputBlocked())
        {
            sharedManualLive.Clear();
            sharedOverridesLive.Clear();
            return;
        }

        GC_ManualDraft srcManual = GetActiveManualSource();
        GC_ActuatorOverrideState srcOverrides = GetActiveOverrideSource();

        if (srcManual != null) CopyManual(srcManual, sharedManualLive);
        else sharedManualLive.Clear();

        if (srcOverrides != null) CopyOverrides(srcOverrides, sharedOverridesLive);
        else sharedOverridesLive.Clear();
    }

    private GC_ManualDraft GetActiveManualSource()
    {
        return (activeSeat == SEAT_RIGHT) ? rightManualLocal : leftManualLocal;
    }

    private GC_ActuatorOverrideState GetActiveOverrideSource()
    {
        return (activeSeat == SEAT_RIGHT) ? rightOverrideLocal : leftOverrideLocal;
    }

    // =====================================================================
    // JOYSTICK LIGHTS
    // =====================================================================

    private void UpdateJoystickLights()
    {
        UpdateSingleJoystickLight(leftJoystickLightRenderer, leftHandControls);
        UpdateSingleJoystickLight(rightJoystickLightRenderer, rightHandControls);
    }

    private void UpdateSingleJoystickLight(Renderer r, HandControls hc)
    {
        if (r == null || hc == null) return;
        if (lightMaterialIndex < 0 || lightMaterialIndex >= r.materials.Length) return;

        Material m = r.materials[lightMaterialIndex];
        if (m == null) return;

        bool grabbedLocalJoystick = hc.JoystickGrabbing;
        bool seatAuthority = hc.seatHasAuthority;

        Color c = lightOffColor;

        if (grabbedLocalJoystick)
            c = seatAuthority ? lightAuthorityColor : lightBlockedColor;

        m.SetColor(emissionProperty, c);

        if (c.maxColorComponent > 0.0001f) m.EnableKeyword("_EMISSION");
        else m.DisableKeyword("_EMISSION");
    }

    // =====================================================================
    // COPY HELPERS
    // =====================================================================

    private void CopyManual(GC_ManualDraft src, GC_ManualDraft dst)
    {
        if (src == null || dst == null) return;

        dst.manualAttitudeActive = src.manualAttitudeActive;
        dst.manualThrottleActive = src.manualThrottleActive;

        dst.tauCmd_B = src.tauCmd_B;
        dst.rateCmd_B = src.rateCmd_B;
        dst.useRateControl = src.useRateControl;

        dst.mainThrottle01 = src.mainThrottle01;
        dst.hoverThrottle01 = src.hoverThrottle01;

        dst.translateCmd_B = src.translateCmd_B;
        dst.rcsMode = src.rcsMode;

        dst.attitudeActuatorMode = src.attitudeActuatorMode;
        dst.allowWheels = src.allowWheels;
        dst.allowRCS = src.allowRCS;
        dst.allowGimbal = src.allowGimbal;
    }

    private void CopyOverrides(GC_ActuatorOverrideState src, GC_ActuatorOverrideState dst)
    {
        if (src == null || dst == null) return;

        dst.overrideAllowWheels = src.overrideAllowWheels;
        dst.overrideAllowRCS = src.overrideAllowRCS;
        dst.overrideAllowGimbal = src.overrideAllowGimbal;

        dst.overrideAttitudeActuatorMode = src.overrideAttitudeActuatorMode;
        dst.overrideRcsMode = src.overrideRcsMode;
    }

    // =====================================================================
    // OWNERSHIP ARRIVAL / BLOCKING
    // =====================================================================

    private void UpdateOwnershipArrivalState()
    {
        bool simOwner = (simManager != null && simManager.IsSimOwner());

        if (simOwner && !_prevSimOwner)
        {
            _ownershipAcquireTime = Time.time;

            if (_ownershipRequestPending)
            {
                SetActiveSeatInternal(_pendingSeat);
                _ownershipRequestPending = false;
            }
            else
            {
                bool leftLocal = SeatIsRequestingLocal(SEAT_LEFT);
                bool rightLocal = SeatIsRequestingLocal(SEAT_RIGHT);

                if (leftLocal && !rightLocal) SetActiveSeatInternal(SEAT_LEFT);
                else if (rightLocal && !leftLocal) SetActiveSeatInternal(SEAT_RIGHT);
            }
        }

        if (!simOwner)
            _ownershipAcquireTime = -1f;

        _prevSimOwner = simOwner;
    }

    private bool IsOwnershipInputBlocked()
    {
        if (!blockInputWhileOwnershipPending) return false;

        bool simOwner = (simManager != null && simManager.IsSimOwner());

        if (_ownershipRequestPending && !simOwner)
            return true;

        if (simOwner && _ownershipAcquireTime >= 0f)
        {
            if ((Time.time - _ownershipAcquireTime) < ownershipAcquireGraceSeconds)
                return true;
        }

        return false;
    }

    private void EnsureLocalOwnershipOfAuthority()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        if (!Networking.IsOwner(local, gameObject))
            Networking.SetOwner(local, gameObject);
    }

    public override void OnDeserialization()
    {
        if (_authorityRev == _lastAppliedAuthorityRev) return;
        _lastAppliedAuthorityRev = _authorityRev;

        _activeSeatReleaseStartTime = -1f;

        // Refresh local derived state immediately.
        UpdateSeatStateFlags();
        UpdateJoystickLights();
    }

    // =====================================================================
    // UTILITY
    // =====================================================================

    private HandControls GetSeatHandControls(byte seat)
    {
        return (seat == SEAT_RIGHT) ? rightHandControls : leftHandControls;
    }

    public bool IsLeftSeatActive()
    {
        return activeSeat == SEAT_LEFT;
    }

    public bool IsRightSeatActive()
    {
        return activeSeat == SEAT_RIGHT;
    }

    private static byte ClampSeat(byte seat)
    {
        return (seat == SEAT_RIGHT) ? SEAT_RIGHT : SEAT_LEFT;
    }

    private static byte OppositeSeat(byte seat)
    {
        return (seat == SEAT_RIGHT) ? SEAT_LEFT : SEAT_RIGHT;
    }

    private static string SeatName(byte seat)
    {
        return (seat == SEAT_RIGHT) ? "RIGHT" : "LEFT";
    }
}