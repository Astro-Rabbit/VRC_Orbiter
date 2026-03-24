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

    [Tooltip("Minimum time between repeated ownership-transfer requests.")]
    public float ownershipRetrySeconds = 0.75f;

    [Header("Ownership handoff gating")]
    [Tooltip("After requesting sim ownership for a seat, block that seat from writing manual input until ownership is actually established.")]
    public bool blockInputWhileOwnershipPending = true;

    [Tooltip("Optional short grace after ownership arrives before enabling input.")]
    public float ownershipAcquireGraceSeconds = 0.05f;

    [Header("Optional joystick status lights")]
    public Renderer leftJoystickLightRenderer;
    public Renderer rightJoystickLightRenderer;
    public int lightMaterialIndex = 0;
    public Color lightAuthorityColor = Color.green;
    public Color lightBlockedColor = Color.red;
    public Color lightOffColor = Color.black;
    public string emissionProperty = "_EmissionColor";

    [Header("Debug")]
    public bool applyContinuously = true;
    public bool logSeatChanges = false;
    public bool logOwnershipRequests = false;

    [Header("Local manipulation mirrors (debug)")]
    public bool leftSeatManipulatingLocal = false;
    public bool rightSeatManipulatingLocal = false;

    private bool _ownershipRequestPending = false;
    private byte _pendingSeat = SEAT_LEFT;
    private float _ownershipAcquireTime = -1f;
    private bool _prevSimOwner = false;
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
    // PUBLIC API - manipulation driven
    // =====================================================================

    public void NotifySeatManipulationStarted(byte seat)
    {
        seat = ClampSeat(seat);

        // If another seat currently has active control and is still being used,
        // do not allow this seat to steal control.
        if (!CanSeatTakeControlNow(seat))
        {
            UpdateSeatStateFlags();
            ApplyActiveSeatToSharedIfOwner();
            UpdateJoystickLights();
            return;
        }

        SetSeatManipulatingLocal(seat, true);

        bool simOwner = (simManager != null && simManager.IsSimOwner());

        if (simOwner)
        {
            SetActiveSeatInternal(seat);
            UpdateSeatStateFlags();
            ApplyActiveSeatToSharedIfOwner();
            UpdateJoystickLights();
            return;
        }

        BeginOwnershipRequestForSeat(seat);

        UpdateSeatStateFlags();
        ApplyActiveSeatToSharedIfOwner();
        UpdateJoystickLights();
    }

    public void NotifySeatManipulationEnded(byte seat)
    {
        seat = ClampSeat(seat);
        SetSeatManipulatingLocal(seat, false);

        bool simOwner = (simManager != null && simManager.IsSimOwner());
        if (simOwner && activeSeat == seat)
        {
            byte other = OppositeSeat(seat);
            if (SeatIsManipulatingLocal(other))
            {
                SetActiveSeatInternal(other);
            }
        }

        UpdateSeatStateFlags();
        ApplyActiveSeatToSharedIfOwner();
        UpdateJoystickLights();
    }

    public bool SeatIsManipulatingLocal(byte seat)
    {
        seat = ClampSeat(seat);
        return (seat == SEAT_RIGHT) ? rightSeatManipulatingLocal : leftSeatManipulatingLocal;
    }

    public bool SeatHasAuthority(byte seat)
    {
        seat = ClampSeat(seat);
        return (simManager != null && simManager.IsSimOwner()) &&
               (activeSeat == seat) &&
               !IsOwnershipInputBlocked();
    }

    public bool SeatCanWriteInput(byte seat)
    {
        return SeatHasAuthority(seat);
    }

    public bool SeatHasControl(byte seat)
    {
        return activeSeat == ClampSeat(seat);
    }

    // =====================================================================
    // MAIN TICK
    // =====================================================================

    public void TickAuthority()
    {
        UpdateOwnershipArrivalState();

        bool simOwner = (simManager != null && simManager.IsSimOwner());
        if (simOwner)
        {
            bool leftManip = SeatIsManipulatingLocal(SEAT_LEFT);
            bool rightManip = SeatIsManipulatingLocal(SEAT_RIGHT);

            if (leftManip && !rightManip && activeSeat != SEAT_LEFT)
                SetActiveSeatInternal(SEAT_LEFT);
            else if (rightManip && !leftManip && activeSeat != SEAT_RIGHT)
                SetActiveSeatInternal(SEAT_RIGHT);
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
    // OWNERSHIP HANDOFF
    // =====================================================================

    private void BeginOwnershipRequestForSeat(byte seat)
    {
        seat = ClampSeat(seat);

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

        bool leftClaimed =
            SeatIsManipulatingLocal(SEAT_LEFT) ||
            (leftControlsNet != null && leftControlsNet.IsClaimed());

        bool rightClaimed =
            SeatIsManipulatingLocal(SEAT_RIGHT) ||
            (rightControlsNet != null && rightControlsNet.IsClaimed());

        bool leftActive = (activeSeat == SEAT_LEFT);
        bool rightActive = (activeSeat == SEAT_RIGHT);

        if (leftHandControls != null)
        {
            leftHandControls.SetSeatClaimed(leftClaimed);
            leftHandControls.SetSeatActiveForVisuals(leftActive);
            leftHandControls.SetSeatAuthority(simOwner && leftActive && !inputBlocked);
        }

        if (rightHandControls != null)
        {
            rightHandControls.SetSeatClaimed(rightClaimed);
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
        }

        if (!simOwner && _prevSimOwner)
        {
            _ownershipAcquireTime = -1f;
        }

        _prevSimOwner = simOwner;
    }

    private bool IsOwnershipInputBlocked()
    {
        if (!blockInputWhileOwnershipPending)
            return false;

        if (_ownershipRequestPending)
            return true;

        if (_ownershipAcquireTime >= 0f &&
            (Time.time - _ownershipAcquireTime) < ownershipAcquireGraceSeconds)
            return true;

        return false;
    }

    // =====================================================================
    // LIGHTS
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

        Color c = lightOffColor;
        if (hc.seatClaimed)
            c = hc.seatHasAuthority ? lightAuthorityColor : lightBlockedColor;

        if (m.HasProperty(emissionProperty))
            m.SetColor(emissionProperty, c);
    }

    // =====================================================================
    // AUTHORITY OBJECT OWNERSHIP
    // =====================================================================

    private void EnsureLocalOwnershipOfAuthority()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(local, gameObject);
    }

    public override void OnDeserialization()
    {
        if (_authorityRev == _lastAppliedAuthorityRev) return;
        _lastAppliedAuthorityRev = _authorityRev;

        UpdateSeatStateFlags();
        ApplyActiveSeatToSharedIfOwner();
        UpdateJoystickLights();
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private void SetSeatManipulatingLocal(byte seat, bool manipulating)
    {
        if (seat == SEAT_RIGHT) rightSeatManipulatingLocal = manipulating;
        else leftSeatManipulatingLocal = manipulating;
    }

    private byte ClampSeat(byte seat)
    {
        return (seat == SEAT_RIGHT) ? SEAT_RIGHT : SEAT_LEFT;
    }

    private byte OppositeSeat(byte seat)
    {
        return (ClampSeat(seat) == SEAT_RIGHT) ? SEAT_LEFT : SEAT_RIGHT;
    }

    private string SeatName(byte seat)
    {
        return (ClampSeat(seat) == SEAT_RIGHT) ? "RIGHT" : "LEFT";
    }
    private bool IsSeatClaimedForTakeoverBlock(byte seat)
    {
        seat = ClampSeat(seat);

        // Local knowledge first.
        if (SeatIsManipulatingLocal(seat))
            return true;

        // Remote/other-client observation for seat occupancy.
        CockpitControlsNetState net =
            (seat == SEAT_RIGHT) ? rightControlsNet : leftControlsNet;

        return (net != null && net.IsClaimed());
    }

    private bool CanSeatTakeControlNow(byte requestedSeat)
    {
        requestedSeat = ClampSeat(requestedSeat);
        byte currentSeat = ClampSeat(activeSeat);

        // Same seat is always allowed to keep/control itself.
        if (requestedSeat == currentSeat)
            return true;

        // If the currently active seat is still actively claimed/manipulating,
        // do NOT allow the other seat to steal control.
        if (IsSeatClaimedForTakeoverBlock(currentSeat))
            return false;

        return true;
    }
    private void CopyManual(GC_ManualDraft src, GC_ManualDraft dst)
    {
        dst.manualAttitudeActive = src.manualAttitudeActive;
        dst.manualThrottleActive = src.manualThrottleActive;
        dst.useRateControl = src.useRateControl;
        dst.rateCmd_B = src.rateCmd_B;
        dst.tauCmd_B = src.tauCmd_B;
        dst.mainThrottle01 = src.mainThrottle01;
        dst.hoverThrottle01 = src.hoverThrottle01;
        dst.translateCmd_B = src.translateCmd_B;
        dst.rcsMode = src.rcsMode;
    }

    private void CopyOverrides(GC_ActuatorOverrideState src, GC_ActuatorOverrideState dst)
    {
        dst.overrideAllowWheels = src.overrideAllowWheels;
        dst.overrideAllowRCS = src.overrideAllowRCS;
        dst.overrideAllowGimbal = src.overrideAllowGimbal;
        dst.overrideAttitudeActuatorMode = src.overrideAttitudeActuatorMode;
        dst.overrideRcsMode = src.overrideRcsMode;
    }
}