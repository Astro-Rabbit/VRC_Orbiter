using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DesktopSeatInputDriver : UdonSharpBehaviour
{
    [Header("Seat")]
    [Tooltip("0=left, 1=right")]
    public byte seatId = 0;

    [Tooltip("Seat visual net for THIS seat.")]
    public CockpitControlsNetState controlsNet;

    [Tooltip("Seat-local manual draft for THIS seat.")]
    public GC_ManualDraft manualDraft;

    public CockpitAuthorityManager authorityManager;

    [Header("Desktop Session")]
    [Tooltip("Set true while the local desktop user is seated in this seat.")]
    public bool seatSessionActive = false;

    [Header("Desktop Input Inversion")]
    public bool invertPitch = false;
    public bool invertYaw = false;
    public bool invertRoll = false;

    public bool invertTransX = false;
    public bool invertTransY = false;
    public bool invertTransZ = false;

    [Header("Attitude Keys")]
    public KeyCode pitchUpKey = KeyCode.W;
    public KeyCode pitchDownKey = KeyCode.S;
    public KeyCode yawLeftKey = KeyCode.A;
    public KeyCode yawRightKey = KeyCode.D;
    public KeyCode rollLeftKey = KeyCode.Q;
    public KeyCode rollRightKey = KeyCode.E;

    [Header("Throttle Keys")]
    public KeyCode throttleUpKey = KeyCode.R;
    public KeyCode throttleDownKey = KeyCode.F;
    public KeyCode throttleFullKey = KeyCode.Z;
    public KeyCode throttleCutKey = KeyCode.X;

    [Tooltip("Throttle change rate per second while key is held.")]
    public float throttlePerSecond = 0.5f;

    [Header("Translation Keys")]
    public KeyCode translateLeftKey = KeyCode.J;
    public KeyCode translateRightKey = KeyCode.L;
    public KeyCode translateUpKey = KeyCode.I;
    public KeyCode translateDownKey = KeyCode.K;
    public KeyCode translateForwardKey = KeyCode.U;
    public KeyCode translateBackKey = KeyCode.O;

    [Header("Output Mapping")]
    [Tooltip("Rate-control only for V1 desktop.")]
    public float maxPitchRateDeg = 20f;
    public float maxYawRateDeg = 20f;
    public float maxRollRateDeg = 30f;

    [Tooltip("Body-axis translation force mapping.")]
    public Vector3 maxTranslateForce_B = new Vector3(1000f, 1000f, 1000f);

    [Tooltip("Deadzone for considering attitude active.")]
    public float manualDeadzone = 0.02f;

    [Tooltip("Deadzone for considering translation active.")]
    public float translateDeadzone = 0.02f;

    [Header("RCS / Translation")]
    public byte rcsModeWhenTranslating = 0;
    public bool forceRcsModeOnTranslate = true;

    [Header("Debug / Readback")]
    [HideInInspector] public float inputX;
    [HideInInspector] public float inputY;
    [HideInInspector] public float inputZ;
    [HideInInspector] public float ThrottleValue;
    [HideInInspector] public float transX;
    [HideInInspector] public float transY;
    [HideInInspector] public float transZ;

    [HideInInspector] public bool desktopManipulating = false;

    private bool _wasDrivingVisuals = false;
    private bool _lastPublishedClaimed = false;
    private bool _lastPublishedActiveSeat = false;

    void Start()
    {
        if (manualDraft != null)
            manualDraft.Clear();
    }

    void Update()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        if (local.IsUserInVR())
        {
            ForceStopDesktopManipulation();
            return;
        }

        if (!seatSessionActive)
        {
            ForceStopDesktopManipulation();
            return;
        }

        ReadDesktopInputs();

        bool anyInputActive = ComputeAnyInputActive();
        UpdateManipulationState(anyInputActive);

        bool canWrite =
            authorityManager != null &&
            authorityManager.SeatCanWriteInput(seatId);

        if (canWrite)
            WriteManualDraft();
        else if (manualDraft != null)
            manualDraft.Clear();

        PublishOrReleaseVisualState();
    }

    // ---------------------------------------------------------------------
    // Seat session control
    // ---------------------------------------------------------------------

    public void SetSeatSessionActive(bool active)
    {
        if (seatSessionActive == active) return;

        seatSessionActive = active;

        if (!seatSessionActive)
            ForceStopDesktopManipulation();
    }

    public bool IsDrivingSeatVisuals()
    {
        return seatSessionActive && desktopManipulating;
    }

    public bool IsManipulating()
    {
        return desktopManipulating;
    }

    // ---------------------------------------------------------------------
    // Input read
    // ---------------------------------------------------------------------

    private void ReadDesktopInputs()
    {
        float pitch = 0f;
        if (Input.GetKey(pitchUpKey)) pitch += 1f;
        if (Input.GetKey(pitchDownKey)) pitch -= 1f;

        float yaw = 0f;
        if (Input.GetKey(yawLeftKey)) yaw -= 1f;
        if (Input.GetKey(yawRightKey)) yaw += 1f;

        float roll = 0f;
        if (Input.GetKey(rollLeftKey)) roll -= 1f;
        if (Input.GetKey(rollRightKey)) roll += 1f;

        inputZ = Mathf.Clamp(pitch, -1f, 1f);
        inputY = Mathf.Clamp(yaw,   -1f, 1f);
        inputX = Mathf.Clamp(roll,  -1f, 1f);

        if (invertPitch) inputZ = -inputZ;
        if (invertYaw)   inputY = -inputY;
        if (invertRoll)  inputX = -inputX;

        float tx = 0f;
        if (Input.GetKey(translateLeftKey)) tx -= 1f;
        if (Input.GetKey(translateRightKey)) tx += 1f;

        float ty = 0f;
        if (Input.GetKey(translateDownKey)) ty -= 1f;
        if (Input.GetKey(translateUpKey)) ty += 1f;

        float tz = 0f;
        if (Input.GetKey(translateBackKey)) tz -= 1f;
        if (Input.GetKey(translateForwardKey)) tz += 1f;

        transX = Mathf.Clamp(tx, -1f, 1f);
        transY = Mathf.Clamp(ty, -1f, 1f);
        transZ = Mathf.Clamp(tz, -1f, 1f);

        if (invertTransX) transX = -transX;
        if (invertTransY) transY = -transY;
        if (invertTransZ) transZ = -transZ;

        if (Input.GetKey(throttleUpKey))
            ThrottleValue += throttlePerSecond * Time.deltaTime;

        if (Input.GetKey(throttleDownKey))
            ThrottleValue -= throttlePerSecond * Time.deltaTime;

        if (Input.GetKeyDown(throttleFullKey))
            ThrottleValue = 1f;

        if (Input.GetKeyDown(throttleCutKey))
            ThrottleValue = 0f;

        ThrottleValue = Mathf.Clamp01(ThrottleValue);
    }

    private bool ComputeAnyInputActive()
    {
        bool attitudeActive =
            Mathf.Abs(inputX) > manualDeadzone ||
            Mathf.Abs(inputY) > manualDeadzone ||
            Mathf.Abs(inputZ) > manualDeadzone;

        bool translationActive =
            Mathf.Abs(transX) > translateDeadzone ||
            Mathf.Abs(transY) > translateDeadzone ||
            Mathf.Abs(transZ) > translateDeadzone;

        bool throttleKeyActive =
            Input.GetKey(throttleUpKey) ||
            Input.GetKey(throttleDownKey) ||
            Input.GetKeyDown(throttleFullKey) ||
            Input.GetKeyDown(throttleCutKey);

        return attitudeActive || translationActive || throttleKeyActive;
    }

    private void UpdateManipulationState(bool anyInputActive)
    {
        if (anyInputActive && !desktopManipulating)
        {
            desktopManipulating = true;

            if (authorityManager != null)
                authorityManager.NotifySeatManipulationStarted(seatId);
        }
        else if (!anyInputActive && desktopManipulating)
        {
            desktopManipulating = false;

            if (authorityManager != null)
                authorityManager.NotifySeatManipulationEnded(seatId);
        }
    }

    private void ForceStopDesktopManipulation()
    {
        bool hadVisuals = _wasDrivingVisuals;
        bool wasManipulating = desktopManipulating;

        desktopManipulating = false;

        if (wasManipulating && authorityManager != null)
            authorityManager.NotifySeatManipulationEnded(seatId);

        ClearAllLocalInputs();

        if (manualDraft != null)
            manualDraft.Clear();

        if (hadVisuals || wasManipulating)
            PublishOrReleaseVisualState();
    }

    // ---------------------------------------------------------------------
    // Output -> seat-local manual draft
    // ---------------------------------------------------------------------

    private void WriteManualDraft()
    {
        if (manualDraft == null) return;

        bool attActive =
            Mathf.Abs(inputX) > manualDeadzone ||
            Mathf.Abs(inputY) > manualDeadzone ||
            Mathf.Abs(inputZ) > manualDeadzone;

        bool transActive =
            Mathf.Abs(transX) > translateDeadzone ||
            Mathf.Abs(transY) > translateDeadzone ||
            Mathf.Abs(transZ) > translateDeadzone;

        bool thrActive =
            Input.GetKey(throttleUpKey) ||
            Input.GetKey(throttleDownKey) ||
            Input.GetKeyDown(throttleFullKey) ||
            Input.GetKeyDown(throttleCutKey) ||
            ThrottleValue > manualDeadzone;

        manualDraft.manualAttitudeActive = attActive;
        manualDraft.manualThrottleActive = (thrActive || transActive);
        manualDraft.useRateControl = true;

        if (!attActive)
        {
            manualDraft.rateCmd_B = Vector3.zero;
            manualDraft.tauCmd_B = Vector3.zero;
        }
        else
        {
            float p = maxPitchRateDeg * Mathf.Deg2Rad;
            float y = maxYawRateDeg * Mathf.Deg2Rad;
            float r = maxRollRateDeg * Mathf.Deg2Rad;

            manualDraft.rateCmd_B = new Vector3(
                inputZ * p,
                inputY * y,
                -inputX * r
            );
            manualDraft.tauCmd_B = Vector3.zero;
        }

        manualDraft.mainThrottle01 = thrActive ? Mathf.Clamp01(ThrottleValue) : 0f;
        manualDraft.hoverThrottle01 = 0f;

        Vector3 tCmd01 = new Vector3(transX, transY, transZ);
        if (!transActive) tCmd01 = Vector3.zero;

        manualDraft.translateCmd_B = new Vector3(
            tCmd01.x * maxTranslateForce_B.x,
            tCmd01.y * maxTranslateForce_B.y,
            tCmd01.z * maxTranslateForce_B.z
        );

        if (forceRcsModeOnTranslate && transActive)
            manualDraft.rcsMode = rcsModeWhenTranslating;
    }

    // ---------------------------------------------------------------------
    // Local cleanup
    // ---------------------------------------------------------------------

    private void ClearTransientInputsOnly()
    {
        inputX = 0f;
        inputY = 0f;
        inputZ = 0f;
        transX = 0f;
        transY = 0f;
        transZ = 0f;
    }

    private void ClearAllLocalInputs()
    {
        ClearTransientInputsOnly();
        ThrottleValue = 0f;
    }

    // ---------------------------------------------------------------------
    // Visual state
    // ---------------------------------------------------------------------

    private void PublishOrReleaseVisualState()
    {
        bool localDrivingVisuals = IsDrivingSeatVisuals();

        if (localDrivingVisuals)
        {
            EnsureLocalOwnershipOfVisualNet();

            if (controlsNet != null)
            {
                bool activeSeat = (authorityManager != null && authorityManager.SeatHasControl(seatId));

                controlsNet.SetLocalVisualState(
                    inputX, inputY, inputZ,
                    ThrottleValue,
                    transX, transY, transZ,
                    true,
                    true,
                    activeSeat
                );

                bool claimChanged = (_lastPublishedClaimed != true);
                bool activeChanged = (_lastPublishedActiveSeat != activeSeat);
                bool drivingEdge = !_wasDrivingVisuals;

                if (drivingEdge || claimChanged || activeChanged || controlsNet.HasPendingVisualChange())
                    controlsNet.ForcePublish();

                _lastPublishedClaimed = true;
                _lastPublishedActiveSeat = activeSeat;
            }
        }
        else
        {
            if (_wasDrivingVisuals)
            {
                EnsureLocalOwnershipOfVisualNet();

                if (controlsNet != null)
                {
                    bool activeSeat = (authorityManager != null && authorityManager.SeatHasControl(seatId));

                    controlsNet.SetLocalVisualState(
                        0f, 0f, 0f,
                        0f,
                        0f, 0f, 0f,
                        false,
                        false,
                        activeSeat
                    );
                    controlsNet.ForcePublish();

                    _lastPublishedClaimed = false;
                    _lastPublishedActiveSeat = activeSeat;
                }
            }
        }

        _wasDrivingVisuals = localDrivingVisuals;
    }

    private void EnsureLocalOwnershipOfVisualNet()
    {
        if (controlsNet == null) return;

        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        if (!Networking.IsOwner(controlsNet.gameObject))
            Networking.SetOwner(local, controlsNet.gameObject);
    }
}