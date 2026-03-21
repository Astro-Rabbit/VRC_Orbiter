using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SeatStationDriver : UdonSharpBehaviour
{
    [Header("Seat Identity")]
    public byte seatId = 0;

    public CockpitAuthorityManager authorityManager;
    public DesktopSeatInputDriver desktopInputDriver;

    [Header("Animator")]
    public Animator seatAnimator;
    public string deployedBoolName = "Deployed";

    [Header("Switch Input")]
    [Tooltip("Written by your already-synced switch. Example: 0=folded, 1=deployed.")]
    public byte deployCommand = 0;

    [Tooltip("Which deployCommand value means deployed.")]
    public byte deployedCommandValue = 1;

    [Tooltip("If true, any nonzero deployCommand means deployed.")]
    public bool nonZeroMeansDeployed = false;

    [Header("Startup")]
    public bool applyOnStart = true;

    [Header("Station")]
    public VRCStation station;
    public bool desktopOnlyInteract = true;
    public bool requireDeployedToSit = true;

    [Header("Interaction Proxy")]
    public SeatInteractProxy interactProxy;

    [Header("Seat Interact Timing")]
    [Tooltip("Delay after deploy before the seat interact collider becomes usable.")]
    public float interactEnableDelaySeconds = 0.6f;

    private float _interactEnableAtTime = -1f;
    private bool _interactPendingEnable = false;

    [Header("Seat Adjustment")]
    public Transform seatAdjustRoot;
    public Vector3 adjustBaseLocalPosition;
    public float forwardStep = 0.02f;
    public float verticalStep = 0.02f;
    public float minForwardOffset = -0.10f;
    public float maxForwardOffset =  0.10f;
    public float minVerticalOffset = -0.08f;
    public float maxVerticalOffset =  0.08f;
    public bool snapAdjustRootToBaseOnStart = true;

    [Header("Take/Release Button Light")]
    public Renderer controlButtonRenderer;
    public int controlButtonMaterialIndex = 0;
    public string emissionProperty = "_EmissionColor";

    public Color controlOffColor = Color.black;
    public Color controlAvailableColor = Color.green;
    public Color controlHeldColor = Color.red;
    public Color controlContestedColor = new Color(1f, 0.5f, 0f);

    public float contestedFlashHz = 2.0f;

    private bool _deployed;
    private bool _localInStation;

    private float _forwardOffset;
    private float _verticalOffset;

    void Start()
    {
        if (seatAdjustRoot != null && snapAdjustRootToBaseOnStart)
        {
            seatAdjustRoot.localPosition = adjustBaseLocalPosition;
        }

        if (applyOnStart)
        {
            _deployed = EvaluateDeployCommand(deployCommand);
            ApplyAnimatorState();
        }
        else
        {
            ApplyInteractAvailability();
        }
    }

    void Update()
    {
        UpdatePendingInteractEnable();
        UpdateControlButtonLight();
    }

    public void TryUseSeat()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (station == null) return;

        if (desktopOnlyInteract && local.IsUserInVR())
            return;

        if (requireDeployedToSit && !_deployed)
            return;

        local.UseAttachedStation();
    }

    public override void OnStationEntered(VRCPlayerApi player)
    {
        if (player == null) return;
        if (!player.isLocal) return;

        _localInStation = true;
        Debug.Log("Station Entered");
        if (desktopInputDriver != null)
            desktopInputDriver.SetSeatSessionActive(true);
    
    }

    public override void OnStationExited(VRCPlayerApi player)
    {
        if (player == null) return;
        if (!player.isLocal) return;

        _localInStation = false;

        if (desktopInputDriver != null)
            desktopInputDriver.SetSeatSessionActive(false);

        if (authorityManager != null)
            authorityManager.ReleaseControl(seatId);
    }

    public void ExitSeat()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (!_localInStation) return;

        if (desktopInputDriver != null)
            desktopInputDriver.ReleaseControls();

        if (authorityManager != null)
            authorityManager.ReleaseControl(seatId);

        station.ExitStation(local);
    }

    // ---------------------------------------------------------------------
    // Take/release button
    // ---------------------------------------------------------------------

    public void ToggleSeatControlRequest()
    {
        if (desktopInputDriver == null) return;
        desktopInputDriver.ToggleControlsEngaged();
    }

    public bool LocalSeatHasControl()
    {
        if (authorityManager == null) return false;
        return authorityManager.SeatHasControl(seatId);
    }

    public bool LocalSeatCanTakeControl()
    {
        if (!_localInStation) return false;
        if (desktopInputDriver == null) return false;
        if (!desktopInputDriver.CanEngageDesktopControls()) return false;
        if (authorityManager == null) return false;
        return authorityManager.SeatCanTakeControl(seatId);
    }

    public bool LocalSeatIsContested()
    {
        if (authorityManager == null) return false;
        return authorityManager.SeatIsContested(seatId);
    }

    public bool LocalSeatIsRequesting()
    {
        if (authorityManager == null) return false;
        return authorityManager.SeatIsRequestingLocal(seatId);
    }

    // ---------------------------------------------------------------------
    // Deploy / stow
    // ---------------------------------------------------------------------

    public void OnDeployCommandChanged()
    {
        _deployed = EvaluateDeployCommand(deployCommand);
        ApplyAnimatorState();
    }

    public void Deploy()
    {
        _deployed = true;
        ApplyAnimatorState();
    }

    public void Fold()
    {
        _deployed = false;
        ApplyAnimatorState();
    }

    public void Toggle()
    {
        _deployed = !_deployed;
        ApplyAnimatorState();
    }

    // ---------------------------------------------------------------------
    // Seat adjustment
    // ---------------------------------------------------------------------

    public void SeatUp()
    {
        _verticalOffset = Mathf.Clamp(_verticalOffset + verticalStep, minVerticalOffset, maxVerticalOffset);
        ApplySeatAdjustment();
    }

    public void SeatDown()
    {
        _verticalOffset = Mathf.Clamp(_verticalOffset - verticalStep, minVerticalOffset, maxVerticalOffset);
        ApplySeatAdjustment();
    }

    public void SeatForward()
    {
        _forwardOffset = Mathf.Clamp(_forwardOffset + forwardStep, minForwardOffset, maxForwardOffset);
        ApplySeatAdjustment();
    }

    public void SeatBack()
    {
        _forwardOffset = Mathf.Clamp(_forwardOffset - forwardStep, minForwardOffset, maxForwardOffset);
        ApplySeatAdjustment();
    }

    public void ResetSeatAdjust()
    {
        _forwardOffset = 0f;
        _verticalOffset = 0f;
        ApplySeatAdjustment();
    }

    // ---------------------------------------------------------------------
    // Queries
    // ---------------------------------------------------------------------

    public bool IsLocalInStation()
    {
        return _localInStation;
    }

    public bool IsDeployed()
    {
        return _deployed;
    }

    // ---------------------------------------------------------------------
    // Internal
    // ---------------------------------------------------------------------

    private bool EvaluateDeployCommand(byte value)
    {
        if (nonZeroMeansDeployed) return value != 0;
        return value == deployedCommandValue;
    }

    private void ApplyAnimatorState()
    {
        if (seatAnimator != null && !string.IsNullOrEmpty(deployedBoolName))
        {
            seatAnimator.SetBool(deployedBoolName, _deployed);
        }

        ApplyInteractAvailability();
    }

    private void ApplyInteractAvailability()
    {
        if (interactProxy == null) return;

        if (!_deployed)
        {
            _interactPendingEnable = false;
            interactProxy.SetInteractEnabled(false);
            return;
        }

        // Deployed: wait a short delay so the seat can finish unfolding first.
        _interactPendingEnable = true;
        _interactEnableAtTime = Time.time + interactEnableDelaySeconds;
        interactProxy.SetInteractEnabled(false);
    }

    private void ApplySeatAdjustment()
    {
        if (seatAdjustRoot == null) return;

        Vector3 p = adjustBaseLocalPosition;
        p.y += _verticalOffset;
        p.z += _forwardOffset;
        seatAdjustRoot.localPosition = p;
    }

    private void UpdatePendingInteractEnable()
    {
        if (!_interactPendingEnable) return;
        if (!_deployed)
        {
            _interactPendingEnable = false;
            return;
        }

        if (Time.time < _interactEnableAtTime) return;

        _interactPendingEnable = false;

        if (interactProxy != null)
            interactProxy.SetInteractEnabled(true);
    }

    public bool LocalDesktopActuallyHoldingControl()
    {
        if (desktopInputDriver == null) return false;
        if (authorityManager == null) return false;

        return desktopInputDriver.IsControlsEngaged() && authorityManager.SeatHasControl(seatId);
    }
    private void UpdateControlButtonLight()
    {
        if (controlButtonRenderer == null) return;
        if (controlButtonMaterialIndex < 0 || controlButtonMaterialIndex >= controlButtonRenderer.materials.Length) return;

        Material m = controlButtonRenderer.materials[controlButtonMaterialIndex];
        if (m == null) return;

        Color c = controlOffColor;

        if (_localInStation)
        {
            bool controlsEngaged = (desktopInputDriver != null && desktopInputDriver.IsControlsEngaged());
            bool actuallyHoldingControl = LocalDesktopActuallyHoldingControl();
            bool contested = LocalSeatIsContested();
            bool canEngage = (desktopInputDriver != null && desktopInputDriver.CanEngageDesktopControls());

            if (actuallyHoldingControl && contested)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * contestedFlashHz * Mathf.PI * 2f);
                c = Color.Lerp(controlOffColor, controlContestedColor, pulse);
            }
            else if (actuallyHoldingControl)
            {
                c = controlHeldColor;
            }
            else if (!controlsEngaged && canEngage)
            {
                c = controlAvailableColor;
            }
            else
            {
                c = controlOffColor;
            }
        }

        m.SetColor(emissionProperty, c);

        if (c.maxColorComponent > 0.0001f) m.EnableKeyword("_EMISSION");
        else m.DisableKeyword("_EMISSION");
    }
}