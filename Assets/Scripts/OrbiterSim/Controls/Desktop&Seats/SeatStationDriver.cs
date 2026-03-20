using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SeatStationDriver : UdonSharpBehaviour
{
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

    [Header("Seat Adjustment")]
    [Tooltip("Transform that moves for local seat adjustment. Usually a parent of the station enter point / camera anchor.")]
    public Transform seatAdjustRoot;

    [Tooltip("Local position used as the neutral/default seat position.")]
    public Vector3 adjustBaseLocalPosition;

    [Tooltip("Meters per button press forward/back.")]
    public float forwardStep = 0.02f;

    [Tooltip("Meters per button press up/down.")]
    public float verticalStep = 0.02f;

    [Tooltip("Local Z offset limits relative to adjustBaseLocalPosition.")]
    public float minForwardOffset = -0.10f;
    public float maxForwardOffset =  0.10f;

    [Tooltip("Local Y offset limits relative to adjustBaseLocalPosition.")]
    public float minVerticalOffset = -0.08f;
    public float maxVerticalOffset =  0.08f;

    [Tooltip("Apply base local position on Start.")]
    public bool snapAdjustRootToBaseOnStart = true;

    private bool _deployed;
    private bool _localInStation;

    // local-only seat adjustment state
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
    }

    public override void OnStationExited(VRCPlayerApi player)
    {
        if (player == null) return;
        if (!player.isLocal) return;

        _localInStation = false;
    }

    public void ExitSeat()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (!_localInStation) return;

        station.ExitStation(local);
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
    // Seat adjustment buttons
    // Local only for now
    // ---------------------------------------------------------------------

    public void SeatUp()
    {
        if (!_localInStation) return;
        _verticalOffset = Mathf.Clamp(_verticalOffset + verticalStep, minVerticalOffset, maxVerticalOffset);
        ApplySeatAdjustment();
    }

    public void SeatDown()
    {
        if (!_localInStation) return;
        _verticalOffset = Mathf.Clamp(_verticalOffset - verticalStep, minVerticalOffset, maxVerticalOffset);
        ApplySeatAdjustment();
    }

    public void SeatForward()
    {
        if (!_localInStation) return;
        _forwardOffset = Mathf.Clamp(_forwardOffset + forwardStep, minForwardOffset, maxForwardOffset);
        ApplySeatAdjustment();
    }

    public void SeatBack()
    {
        if (!_localInStation) return;
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
        if (interactProxy != null)
        {
            interactProxy.SetInteractEnabled(_deployed);
        }
    }

    private void ApplySeatAdjustment()
    {
        if (seatAdjustRoot == null) return;

        Vector3 p = adjustBaseLocalPosition;
        p.y += _verticalOffset;
        p.z += _forwardOffset;
        seatAdjustRoot.localPosition = p;
    }
}