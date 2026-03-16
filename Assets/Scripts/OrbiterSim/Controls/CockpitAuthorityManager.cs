using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// CockpitAuthorityManager
///
/// Purpose
/// -------
///— Maintains which cockpit seat is the ACTIVE control seat.
/// - Copies the ACTIVE seat's local manual draft into the shared GC manual draft.
/// - Copies the ACTIVE seat's local override state into the shared live override state.
/// - Coordinates automatic seat handoff and sim-ownership requests.
///
/// Core policy
/// -----------
/// - Left and right seats each have their own local HandControls and their own single-seat visual net state.
/// - A seat is considered "claimed" while its controls are actively being grabbed.
/// - Only ONE seat is active at a time.
/// - The currently active seat keeps control until it fully releases.
/// - If the other seat is waiting, authority transfers after a short delay.
/// - If the waiting seat belongs to a different player, that player's client requests sim ownership
///   after the release delay, unless SimManager's hard ownership-transfer lock is enabled.
/// - Only the LOCAL sim owner writes into the shared live GC states.
///
/// Notes
/// -----
/// - This manager is intentionally seat-wide, not per-control.
/// - This script uses the owner of each CockpitControlsNetState object as the current seat holder.
/// - Joystick status lights are LOCAL feedback:
///     off   = joystick not grabbed locally
///     green = joystick grabbed locally and this seat currently has authority
///     red   = joystick grabbed locally but this seat does not currently have authority
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CockpitAuthorityManager : UdonSharpBehaviour
{
    public const byte SEAT_LEFT = 0;
    public const byte SEAT_RIGHT = 1;

    // ---------------------------------------------------------------------
    // References: sim + seat-local input sources
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // Authority state
    // ---------------------------------------------------------------------

    [Header("Authority state")]
    [Tooltip("Which seat is currently the active control seat. 0=left, 1=right.")]
    public byte activeSeat = SEAT_LEFT;

    [Tooltip("If the currently active seat releases and the opposite seat is waiting, wait this long before handoff.")]
    public float seatTransferDelaySeconds = 0.5f;

    [Tooltip("Minimum time between repeated ownership-transfer requests.")]
    public float ownershipRetrySeconds = 0.75f;

    // ---------------------------------------------------------------------
    // Optional joystick status lights
    // ---------------------------------------------------------------------

    [Header("Optional joystick status lights")]
    [Tooltip("Renderer for left joystick-column light. Leave null to disable.")]
    public Renderer leftJoystickLightRenderer;

    [Tooltip("Renderer for right joystick-column light. Leave null to disable.")]
    public Renderer rightJoystickLightRenderer;

    [Tooltip("Material slot index on the light renderer.")]
    public int lightMaterialIndex = 0;

    [Tooltip("Emission color when grabbed locally and authoritative.")]
    public Color lightAuthorityColor = Color.green;

    [Tooltip("Emission color when grabbed locally but not authoritative.")]
    public Color lightBlockedColor = Color.red;

    [Tooltip("Emission color when idle.")]
    public Color lightOffColor = Color.black;

    [Tooltip("Shader emission property name. Most Unity shaders use _EmissionColor.")]
    public string emissionProperty = "_EmissionColor";

    // ---------------------------------------------------------------------
    // Debug
    // ---------------------------------------------------------------------

    [Header("Debug")]
    public bool applyContinuously = true;
    public bool logSeatChanges = false;
    public bool logOwnershipRequests = false;

    // ---------------------------------------------------------------------
    // Internal timers/state
    // ---------------------------------------------------------------------

    // When the currently active seat first became fully released.
    private float _activeSeatReleaseStartTime = -1f;

    // Last time this client requested sim ownership.
    private float _lastOwnershipRequestTime = -999f;

    // Cache for change logging.
    private byte _lastLoggedActiveSeat = 255;

    void Start()
    {
        // Push initial local seat flags and write shared state if we already own the sim.
        UpdateSeatStateFlags();
        ApplyActiveSeatToSharedIfOwner();
        UpdateJoystickLights();
    }

    void Update()
    {
        if (!applyContinuously) return;
        TickAuthority();
    }

    /// <summary>
    /// Main authority tick. Safe to call every frame.
    /// </summary>
    public void TickAuthority()
    {
        // 1) Observe current seat state from local hand scripts + synced seat net objects.
        bool leftClaimed = IsSeatClaimed(SEAT_LEFT);
        bool rightClaimed = IsSeatClaimed(SEAT_RIGHT);

        bool activeSeatStillClaimed = IsSeatClaimed(activeSeat);
        byte otherSeat = OppositeSeat(activeSeat);
        bool otherSeatClaimed = IsSeatClaimed(otherSeat);

        // 2) If the active seat is still claimed, it keeps authority and release timer resets.
        if (activeSeatStillClaimed)
        {
            _activeSeatReleaseStartTime = -1f;
        }
        else
        {
            // Active seat has released.
            // Start release timer the first frame we notice the release.
            if (_activeSeatReleaseStartTime < 0f)
                _activeSeatReleaseStartTime = Time.time;

            // If the other seat is waiting, and release delay has elapsed, try handoff.
            if (otherSeatClaimed)
            {
                float dtReleased = Time.time - _activeSeatReleaseStartTime;
                if (dtReleased >= seatTransferDelaySeconds)
                {
                    TryHandoffToSeat(otherSeat);
                }
            }
        }

        // 3) If nothing is currently claimed, just keep whichever seat was active last.
        //    No automatic reversion is done here.

        // 4) Push seat flags into the HandControls scripts.
        UpdateSeatStateFlags();

        // 5) Only the local sim owner copies active-seat state into the shared GC live objects.
        ApplyActiveSeatToSharedIfOwner();

        // 6) Update local joystick-column status lights.
        UpdateJoystickLights();

        // 7) Optional logging of seat changes.
        if (_lastLoggedActiveSeat != activeSeat)
        {
            if (logSeatChanges)
                Debug.Log("[CockpitAuthorityManager] Active seat -> " + SeatName(activeSeat));

            _lastLoggedActiveSeat = activeSeat;
        }
    }

    // ---------------------------------------------------------------------
    // Seat claim / holder observation
    // ---------------------------------------------------------------------

    /// <summary>
    /// Returns true if the given seat is currently claimed by anyone.
    ///
    /// Policy:
    /// - Local grab counts immediately.
    /// - Otherwise fall back to the seat's synced visual-net grabbing flag.
    /// </summary>
    private bool IsSeatClaimed(byte seat)
    {
        if (seat == SEAT_RIGHT)
        {
            if (rightHandControls != null && rightHandControls.IsAnyPrimaryControlGrabbed()) return true;
            if (rightControlsNet != null && rightControlsNet.IsGrabbingAny()) return true;
            return false;
        }

        if (leftHandControls != null && leftHandControls.IsAnyPrimaryControlGrabbed()) return true;
        if (leftControlsNet != null && leftControlsNet.IsGrabbingAny()) return true;
        return false;
    }

    /// <summary>
    /// Returns the player who currently appears to hold the seat.
    ///
    /// We use ownership of the seat's single-seat visual net object as the holder identity.
    /// This is the best current proxy for "who is manipulating that seat".
    /// </summary>
    private VRCPlayerApi GetSeatHolder(byte seat)
    {
        CockpitControlsNetState net = (seat == SEAT_RIGHT) ? rightControlsNet : leftControlsNet;
        if (net == null) return null;
        return Networking.GetOwner(net.gameObject);
    }

    /// <summary>
    /// True if the local player appears to be the current holder of the seat.
    /// </summary>
    private bool IsLocalSeatHolder(byte seat)
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return false;

        VRCPlayerApi holder = GetSeatHolder(seat);
        if (holder == null) return false;

        return holder.playerId == local.playerId;
    }

    // ---------------------------------------------------------------------
    // Handoff logic
    // ---------------------------------------------------------------------

    /// <summary>
    /// Attempt to hand active control to the requested seat.
    ///
    /// Cases:
    /// - If local player is already sim owner and also holds that seat -> switch immediately.
    /// - If local player holds that seat but is NOT sim owner -> request sim ownership (if allowed).
    /// - If local player does not hold that seat -> do nothing locally; the holder's own client should request.
    /// </summary>
    private void TryHandoffToSeat(byte seat)
    {
        seat = ClampSeat(seat);

        // If local client is not the seat holder, we do not initiate anything.
        // The player actually holding that seat should do it from their own client.
        if (!IsLocalSeatHolder(seat))
            return;

        bool simOwner = (simManager != null && simManager.IsSimOwner());

        // If we already own the sim, seat switch is purely local.
        if (simOwner)
        {
            SetActiveSeatInternal(seat);
            return;
        }

        // We are the waiting seat holder but do not own the sim.
        // Respect hard transfer lock.
        if (simManager != null && !simManager.CanApproveOwnershipTransfer())
            return;

        // Rate-limit ownership-transfer requests.
        if ((Time.time - _lastOwnershipRequestTime) < ownershipRetrySeconds)
            return;

        _lastOwnershipRequestTime = Time.time;

        if (logOwnershipRequests)
            Debug.Log("[CockpitAuthorityManager] Requesting sim ownership for seat " + SeatName(seat));

        if (simManager != null)
            simManager.BeginOwnershipTransfer();

        // Do NOT switch activeSeat locally yet.
        // Wait until ownership is actually transferred to this client.
    }

    /// <summary>
    /// Direct internal active-seat switch.
    /// Use only once policy has already decided the handoff is valid.
    /// </summary>
    private void SetActiveSeatInternal(byte seat)
    {
        seat = ClampSeat(seat);
        if (activeSeat == seat) return;

        activeSeat = seat;
        _activeSeatReleaseStartTime = -1f;
    }

    public void SetActiveSeatLeft()
    {
        SetActiveSeatInternal(SEAT_LEFT);
    }

    public void SetActiveSeatRight()
    {
        SetActiveSeatInternal(SEAT_RIGHT);
    }

    // ---------------------------------------------------------------------
    // Push seat flags into HandControls
    // ---------------------------------------------------------------------

    /// <summary>
    /// Updates each HandControls instance with:
    /// - whether the seat is currently claimed
    /// - whether the seat is currently the active seat
    /// - whether this local client is allowed to write manual input from that seat
    ///
    /// seatHasAuthority is intentionally stricter than activeSeat:
    /// the local seat only has authority if THIS client is also the sim owner.
    /// </summary>
    private void UpdateSeatStateFlags()
    {
        bool simOwner = (simManager != null && simManager.IsSimOwner());

        bool leftClaimed = IsSeatClaimed(SEAT_LEFT);
        bool rightClaimed = IsSeatClaimed(SEAT_RIGHT);

        bool leftActive = (activeSeat == SEAT_LEFT);
        bool rightActive = (activeSeat == SEAT_RIGHT);

        if (leftHandControls != null)
        {
            leftHandControls.SetSeatClaimed(leftClaimed);
            leftHandControls.SetSeatActiveForVisuals(leftActive);
            leftHandControls.SetSeatAuthority(simOwner && leftActive);
        }

        if (rightHandControls != null)
        {
            rightHandControls.SetSeatClaimed(rightClaimed);
            rightHandControls.SetSeatActiveForVisuals(rightActive);
            rightHandControls.SetSeatAuthority(simOwner && rightActive);
        }
    }

    // ---------------------------------------------------------------------
    // Shared GC live-state write path
    // ---------------------------------------------------------------------

    /// <summary>
    /// Copies the current ACTIVE seat's local manual/override state into the shared live GC state,
    /// but ONLY if this local client is currently the sim owner.
    ///
    /// This prevents non-owners from writing stale local state into the live flight computer inputs.
    /// </summary>
    private void ApplyActiveSeatToSharedIfOwner()
    {
        if (sharedManualLive == null || sharedOverridesLive == null)
            return;

        if (simManager == null || !simManager.IsSimOwner())
            return;

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

    // ---------------------------------------------------------------------
    // Joystick status lights
    // ---------------------------------------------------------------------

    /// <summary>
    /// Updates local joystick-column light emission.
    ///
    /// LOCAL feedback policy:
    /// - off   = local joystick not grabbed
    /// - green = local joystick grabbed and this seat currently has authority
    /// - red   = local joystick grabbed but this seat does not currently have authority
    ///
    /// This is intentionally based on local joystick grab state, because the light is meant to tell
    /// the current local player whether their attempted control input is live or blocked.
    /// </summary>
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

        // Standard Unity emission toggle pattern.
        if (c.maxColorComponent > 0.0001f) m.EnableKeyword("_EMISSION");
        else m.DisableKeyword("_EMISSION");
    }

    // ---------------------------------------------------------------------
    // Utility / copy helpers
    // ---------------------------------------------------------------------

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
}