using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletPen : UdonSharpBehaviour
{
    public float rayDistance = 0.05f;
    public float desktopRayDistance = 1.5f;
    public LayerMask interactionLayers;

    [Header("References")]
    public GameObject PenMesh;
    public AudioSource ButtonAudioSource;
    public AudioClip[] ButtonUpClip;
    public AudioClip[] ButtonDownClip;
    public AudioClip[] SwitchUpClip;
    public AudioClip[] SwitchDownClip;
    public AudioClip[] KnobClip;


    public int penID;
    public bool isRightHand;

    // Focus Lock (For Knobs/MFD)
    private bool _isLocked;
    private MFDButton _focusedMFDBtn;
    private MFDKnob _focusedKnob;
    private MFDSwitch _focusedSwitch;

    // Previous System (For Tablet)
    private TabletButton _activeTabletBtn;
    private TabletButton _lastHoveredTabletBtn;
    private bool _wasTouchingTablet;

    private string _triggerAxis;
    private VRCPlayerApi _localPlayer;
    private int _zoneCount = 0;


    public bool IsGripping;
    private string _gripAxis;
    public GameObject Pickup;

    // NEW VARIABLES FOR THE CONSTRAINT
    private TabletPenPickup _heldPickup;
    private Vector3 _heldPosOffset;
    private Quaternion _heldRotOffset;
    private bool _wasGripping;
    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        _triggerAxis = isRightHand ? "Oculus_CrossPlatform_SecondaryIndexTrigger" : "Oculus_CrossPlatform_PrimaryIndexTrigger";
        _gripAxis = isRightHand ? "Oculus_CrossPlatform_SecondaryHandTrigger" : "Oculus_CrossPlatform_PrimaryHandTrigger";

        if (ButtonAudioSource == null) ButtonAudioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        



        if (_localPlayer == null) return;
        if (!_localPlayer.IsUserInVR() && !isRightHand) return;

        IsGripping = Input.GetAxisRaw(_gripAxis) > 0.95f;
        bool gripJustPressed = IsGripping && !_wasGripping;
        //HandlePickupConstraint();
        HandlePickupConstraint(IsGripping, gripJustPressed);
        _wasGripping = IsGripping;


        bool isVR = _localPlayer.IsUserInVR();
        bool triggerHeld = (Input.GetAxisRaw(_triggerAxis) > 0.9f) || Input.GetMouseButton(0);

        // 1. IF LOCKED (KNOB/MFD)
        if (_isLocked)
        {
            if (!triggerHeld) ReleaseFocus();
            else StayFocus(isVR);
            return;
        }

        // 2. RAYCAST SEARCH
        RaycastHit hit;
        Vector3 rayOrigin; Vector3 rayDirection; float dist;

        if (!isVR)
        {
            VRCPlayerApi.TrackingData headData = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            rayOrigin = headData.position; rayDirection = headData.rotation * Vector3.forward; dist = desktopRayDistance;
        }
        else
        {
            rayOrigin = transform.position; rayDirection = -transform.up; dist = rayDistance;
        }

        bool currentlyHitting = Physics.Raycast(rayOrigin, rayDirection, out hit, dist, interactionLayers, QueryTriggerInteraction.Ignore);

        if (currentlyHitting)
        {
            MFDKnob knob = hit.collider.GetComponent<MFDKnob>();

            MFDButton mfdBtn = hit.collider.GetComponent<MFDButton>();
            TabletScreen screen = hit.collider.GetComponent<TabletScreen>();
            MFDSwitch mfdSwitch = hit.collider.GetComponent<MFDSwitch>();

            // A. Handle Locked Objects (Knobs/MFD)
            if (triggerHeld && (knob != null || mfdBtn != null || mfdSwitch != null))
            {

                // Inside TabletPen.cs -> AttemptCapture
                if (mfdSwitch != null)
                {
                    _focusedSwitch = mfdSwitch;
                    _focusedSwitch.OnDown(penID, this, isVR ? transform.rotation : _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation);

                }
                else if (knob != null)
                {
                    _focusedKnob = knob;

                    // Decide which rotation to snapshot
                    Quaternion startRot;
                    if (_localPlayer.IsUserInVR())
                    {
                        startRot = transform.rotation; // Snapshot the Pen
                    }
                    else
                    {
                        // Snapshot the Camera
                        startRot = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
                    }

                    // Handshake: Pass ID, HitPoint, and the correct Source Rotation
                    _focusedKnob.OnDown(penID, hit.point, startRot, this);

                    _isLocked = true;
                    return;
                }
                else
                {
                    _focusedMFDBtn = mfdBtn; _focusedMFDBtn.OnDown(penID, this);
                    Debug.Log("[TabletPen] MFD hit");
                }
                _isLocked = true;
                ClearTabletInteraction(); // Ensure tablet state is reset if we switch to physical
                return;
            }

            // B. Handle Tablet (Previous Hover/Slide System)
            if (screen != null)
            {
                HandleTabletInteraction(screen.GetButtonAtPoint(hit.point), triggerHeld);
            }
            else
            {
                ClearTabletInteraction();
            }
        }
        else
        {
            ClearTabletInteraction();
        }
    }

    private void HandleTabletInteraction(TabletButton hovered, bool triggerHeld)
    {
        // Hover Logic
        if (hovered != _lastHoveredTabletBtn)
        {
            if (_lastHoveredTabletBtn != null) _lastHoveredTabletBtn.OnHoverExit(penID);
            if (hovered != null) hovered.OnHoverEnter(penID);
            _lastHoveredTabletBtn = hovered;
        }

        // Press Logic
        if (triggerHeld)
        {
            if (!_wasTouchingTablet)
            {
                _activeTabletBtn = hovered;
                if (_activeTabletBtn != null) _activeTabletBtn.OnDown(penID);
            }
            else if (_activeTabletBtn != null)
            {
                _activeTabletBtn.OnStay(penID);
            }
            _wasTouchingTablet = true;
        }
        else if (_wasTouchingTablet)
        {
            ClearTabletInteraction();
        }
    }

    private void ClearTabletInteraction()
    {
        if (_activeTabletBtn != null)
        {
            if (_activeTabletBtn == _lastHoveredTabletBtn) _activeTabletBtn.OnUp(penID);
            else _activeTabletBtn.OnHoverExit(penID);
            _activeTabletBtn = null;
        }
        if (_lastHoveredTabletBtn != null)
        {
            _lastHoveredTabletBtn.OnHoverExit(penID);
            _lastHoveredTabletBtn = null;
        }
        _wasTouchingTablet = false;
    }

    //private void StayFocus(bool isVR)
    //{
    //    // Determine the tracking source
    //    VRCPlayerApi.TrackingDataType source = isVR ?
    //        (isRightHand ? VRCPlayerApi.TrackingDataType.RightHand : VRCPlayerApi.TrackingDataType.LeftHand) :
    //        VRCPlayerApi.TrackingDataType.Head;

    //    Quaternion currentRot = _localPlayer.GetTrackingData(source).rotation;

    //    if (_focusedKnob != null)
    //    {
    //        if (isVR) _focusedKnob.OnStayVR(penID, currentRot);
    //        else _focusedKnob.OnStayDesktop(penID, currentRot);
    //    }
    //    else if (_focusedMFDBtn != null) _focusedMFDBtn.OnStay(penID);
    //}
    private void StayFocus(bool isVR)
    {
        if (_focusedKnob != null)
        {
            if (isVR)
            {
                // Pass the rotation of THIS Pen object (filtered/pickup rotation)
                _focusedKnob.OnStayVR(penID, transform.rotation);
            }
            else
            {
                // Desktop still uses Head rotation as the "mouse"
                _focusedKnob.OnStayDesktop(penID, _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation);
            }
        }
        else if (_focusedSwitch != null)
        {
            if (isVR) _focusedSwitch.OnStayVR(penID, this);
            else _focusedSwitch.OnStayDesktop(penID, _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation);
        }
        else if (_focusedMFDBtn != null)
        {
            _focusedMFDBtn.OnStay(penID);
        }
    }

    private void ReleaseFocus()
    {
        if (_focusedKnob != null) _focusedKnob.OnUp(penID);
        if (_focusedMFDBtn != null) _focusedMFDBtn.OnUp(penID, this);
        if (_focusedSwitch != null) _focusedSwitch.OnUp(penID);
        _focusedSwitch = null;
        _focusedKnob = null;
        _focusedMFDBtn = null;
        _isLocked = false;
    }

    private Vector3 GetDesktopRayPoint()
    {
        VRCPlayerApi.TrackingData head = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        RaycastHit hit;
        if (Physics.Raycast(head.position, head.rotation * Vector3.forward, out hit, desktopRayDistance, interactionLayers, QueryTriggerInteraction.Ignore)) return hit.point;
        return head.position + (head.rotation * Vector3.forward * desktopRayDistance);
    }

    // Trigger and Audio methods remain the same...
    public void OnTriggerEnter(Collider other) {
        if (!_localPlayer.IsUserInVR()) return; if (other.name == "PenEnable")
        {
            _zoneCount++; if (PenMesh != null) PenMesh.SetActive(true);
        }
    }
    public void OnTriggerExit(Collider other) {
        if (!_localPlayer.IsUserInVR()) return; if (other.name == "PenEnable")
        {
            _zoneCount--; if (_zoneCount <= 0)
            {
                _zoneCount = 0; if (PenMesh != null) PenMesh.SetActive(false);
            }
        }
    }

    public void TriggerHaptic(float duration = 0.05f, float amplitude = 0.2f, float frequency = 0.8f)
    {
        if (_localPlayer == null || !_localPlayer.IsUserInVR()) return;

        VRC_Pickup.PickupHand hand = isRightHand ? VRC_Pickup.PickupHand.Right : VRC_Pickup.PickupHand.Left;
        _localPlayer.PlayHapticEventInHand(hand, duration, amplitude, frequency);
    }
    public void PlayButtonUpClip()
    {
        if (ButtonUpClip.Length == 0) return;
        ButtonAudioSource.pitch = Random.Range(.9f, 1.1f);
        ButtonAudioSource.volume = Random.Range(.9f, 1f);
        ButtonAudioSource.PlayOneShot(ButtonUpClip[Random.Range(0, ButtonUpClip.Length)]);
    }
    public void PlayButtonDownClip()
    {
        //Debug.Log("[TabletPen] Button Down Clip");
        if (ButtonDownClip.Length == 0) return;
        ButtonAudioSource.pitch = Random.Range(.9f, 1.1f);
        ButtonAudioSource.volume = Random.Range(.9f, 1f);
        ButtonAudioSource.PlayOneShot(ButtonDownClip[Random.Range(0, ButtonDownClip.Length)]);
    }
    public void PlaySwitchDownClip()
    {
        if (SwitchDownClip.Length == 0) return;
        ButtonAudioSource.pitch = Random.Range(.9f, 1.1f);
        ButtonAudioSource.volume = Random.Range(.9f, 1f);
        ButtonAudioSource.PlayOneShot(SwitchDownClip[Random.Range(0, SwitchDownClip.Length)]);

    }
    public void PlaySwitchUpClip()
    {
        if (SwitchUpClip.Length == 0) return;
        ButtonAudioSource.pitch = Random.Range(.9f, 1.1f);
        ButtonAudioSource.volume = Random.Range(.9f, 1f);
        ButtonAudioSource.PlayOneShot(SwitchUpClip[Random.Range(0, SwitchUpClip.Length)]);

    }
    public void PlayKnobClip()
    {
        if (SwitchUpClip.Length == 0) return;
        ButtonAudioSource.pitch = Random.Range(.9f, 1.1f);
        ButtonAudioSource.volume = Random.Range(.9f, 1f);
        ButtonAudioSource.PlayOneShot(SwitchUpClip[Random.Range(0, SwitchUpClip.Length)]);

    }

    //private void HandlePickupConstraint()
    //{
    //    if (IsGripping)
    //    {
    //        Debug.Log("[TabletPen] Gripping");
    //        // If we aren't holding anything, try to grab
    //        if (_heldPickup == null && Pickup != null)
    //        {
    //            Debug.Log("[TabletPen] Found Object");
    //            TabletPenPickup pickupScript = Pickup.GetComponent<TabletPenPickup>();

    //            // Only grab if the pen is hovering over the pickup and no one else is holding it
    //            if (pickupScript != null && pickupScript.hoveringPen == this && !pickupScript.isBeingHeld)
    //            {
    //                Debug.Log("[TabletPen] PickingUp");
    //                _heldPickup = pickupScript;
    //                _heldPickup.OnGrab();

    //                // CALCULATE OFFSET (The "Parent-Constraint" setup)
    //                // InverseTransformPoint converts the Pickup's world position into "Pen-local" space
    //                _heldPosOffset = transform.InverseTransformPoint(_heldPickup.transform.position);

    //                // Inverse(PenRotation) * PickupRotation = Local Rotation relative to Pen
    //                _heldRotOffset = Quaternion.Inverse(transform.rotation) * _heldPickup.transform.rotation;

    //                TriggerHaptic(0.05f, 0.3f);
    //            }
    //        }

    //        // APPLY CONSTRAINT (The "Parent-Constraint" update)
    //        if (_heldPickup != null)
    //        {
    //            // Move Pickup to Pen's current position + the recorded offset (rotated by pen's current rot)
    //            _heldPickup.transform.position = transform.TransformPoint(_heldPosOffset);

    //            // Rotate Pickup to Pen's current rotation + the recorded rotation offset
    //            _heldPickup.transform.rotation = transform.rotation * _heldRotOffset;
    //        }
    //    }
    //    else
    //    {
    //        // Release the pickup
    //        if (_heldPickup != null)
    //        {
    //            _heldPickup.OnRelease();
    //            _heldPickup = null;
    //        }
    //    }
    //}
    // ... inside TabletPen class variables ...
    public TabletPenPickup _hoveredPickup; // The handle the pen is currently touching
   // public TabletPenPickup _heldPickup;    // The handle the pen is actually gripping

    // Add this method anywhere in TabletPen.cs
    //public void SetHoveredPickup(TabletPenPickup pickup)
    //{
    //    // If we are already holding something, don't change the hover target
    //    if (_heldPickup != null) return;
    //    _hoveredPickup = pickup;
    //}

    // Update your HandlePickupConstraint method to look like this:
    private void HandlePickupConstraint(bool isGripping, bool gripJustPressed)
    {
        if (isGripping)
        {
            // ATTEMPT TO GRAB: 
            // This ONLY runs on the frame the button is pressed AND if the pen is inside a handle
            if (_heldPickup == null && gripJustPressed && _hoveredPickup != null)
            {
                if (!_hoveredPickup.isBeingHeld)
                {
                    _heldPickup = _hoveredPickup;
                    _heldPickup.OnGrab();

                    _heldPosOffset = transform.InverseTransformPoint(_heldPickup.transform.position);
                    _heldRotOffset = Quaternion.Inverse(transform.rotation) * _heldPickup.transform.rotation;

                    TriggerHaptic(0.05f, 0.3f);
                    Debug.Log("[TabletPen] Successful Grab inside trigger");
                }
            }

            // MAINTAIN GRAB:
            // This runs every frame while gripping, but only if we successfully grabbed something
            if (_heldPickup != null)
            {
                _heldPickup.transform.position = transform.TransformPoint(_heldPosOffset);
                _heldPickup.transform.rotation = transform.rotation * _heldRotOffset;
            }
        }
        else
        {
            // RELEASE: Runs when the grip button is let go
            if (_heldPickup != null)
            {
                _heldPickup.OnRelease();
                _heldPickup = null;
            }
        }
    }
    public void SetHoveredPickup(TabletPenPickup pickup)
    {
        // Don't change hover targets while we are actively holding something
        if (_heldPickup != null) return;
        _hoveredPickup = pickup;
    }
}