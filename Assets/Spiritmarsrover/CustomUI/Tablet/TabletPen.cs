using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletPen : UdonSharpBehaviour
{
    public float rayDistance = 0.05f;
    public LayerMask interactionLayers; // Ensure this includes the MFD Button layer!

    private bool _wasTouching;
    private TabletButton _activeTabletBtn;
    private MFDButton _activeMFDBtn;

    private TabletButton _lastHoveredTabletBtn;
    private MFDButton _lastHoveredMFDBtn;

    public int penID;
    public bool isRightHand;
    private string _triggerAxis;
    public float desktopRayDistance = 1.0f;

    public GameObject PenMesh;
    public AudioSource ButtonAudioSource;
    public AudioClip[] ButtonUpClip;
    public AudioClip[] ButtonDownClip;

    void Start()
    {
        _triggerAxis = isRightHand ? "Oculus_CrossPlatform_SecondaryIndexTrigger" : "Oculus_CrossPlatform_PrimaryIndexTrigger";
        ButtonAudioSource = gameObject.GetComponent<AudioSource>();
    }

    void Update()
    {
        if (!Networking.LocalPlayer.IsUserInVR() && !isRightHand)
        {
            return;
        }
        bool triggerHeld = (Input.GetAxisRaw(_triggerAxis) > 0.9f) || Input.GetMouseButton(0);
        RaycastHit hit;
        bool currentlyHitting = false;

        if (triggerHeld)
        {
            Vector3 rayOrigin;
            Vector3 rayDirection;
            float dist;

            if (!Networking.LocalPlayer.IsUserInVR())
            {
                // Get Camera position and rotation from TrackingData
                VRCPlayerApi.TrackingData headData = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                rayOrigin = headData.position;
                rayDirection = headData.rotation * Vector3.forward;
                dist = desktopRayDistance;
            }
            else
            {
                // VR Pen logic
                rayOrigin = transform.position;
                rayDirection = -transform.up;
                dist = rayDistance;
            }

            //currentlyHitting = Physics.Raycast(rayOrigin, rayDirection, out hit, dist, interactionLayers);
            currentlyHitting = Physics.Raycast(rayOrigin, rayDirection, out hit, dist, interactionLayers, QueryTriggerInteraction.Ignore);


            if (currentlyHitting)
            {
                // 1. Check for Tablet (UI Logic)
                TabletScreen screen = hit.collider.GetComponent<TabletScreen>();
                if (screen != null)
                {
                    HandleTabletInteraction(screen.GetButtonAtPoint(hit.point));
                }
                // 2. Check for MFD (Physical Logic)
                else
                {
                    MFDButton mfdBtn = hit.collider.GetComponent<MFDButton>();
                    HandleMFDInteraction(mfdBtn);
                }
            }
        }

        // Release logic
        if (!currentlyHitting && _wasTouching)
        {
            ClearAllInteractions();
        }

        _wasTouching = currentlyHitting;
    }

    private void HandleTabletInteraction(TabletButton hovered)
    {
        if (hovered != _lastHoveredTabletBtn)
        {
            if (_lastHoveredTabletBtn != null) _lastHoveredTabletBtn.OnHoverExit(penID);
            if (hovered != null) hovered.OnHoverEnter(penID);
            _lastHoveredTabletBtn = hovered;
        }

        if (!_wasTouching)
        {
            _activeTabletBtn = hovered;
            if (_activeTabletBtn != null) _activeTabletBtn.OnDown(penID);
        }
        else if (_activeTabletBtn != null)
        {
            _activeTabletBtn.OnStay(penID);
        }
    }

    private void HandleMFDInteraction(MFDButton hovered)
    {
        if (hovered != _lastHoveredMFDBtn)
        {
            //if (_lastHoveredMFDBtn != null) _lastHoveredMFDBtn.OnHoverExit(penID);
            //if (hovered != null) hovered.OnHoverEnter(penID);
            _lastHoveredMFDBtn = hovered;
        }

        if (!_wasTouching)
        {
            _activeMFDBtn = hovered;
            if (_activeMFDBtn != null)
            {
                _activeMFDBtn.OnDown(penID,this);
                //ButtonAudioSource.pitch = Random.Range(.95f, 1.05f);
                //ButtonAudioSource.volume = Random.Range(.9f, 1f);
                //ButtonAudioSource.PlayOneShot(ButtonDownClip[Random.Range(0, ButtonDownClip.Length)]);
            }
        }
        else if (_activeMFDBtn != null)
        {
            _activeMFDBtn.OnStay(penID);
        }
    }

    private void ClearAllInteractions()
    {
        if (_activeTabletBtn != null)
        {
            if (_activeTabletBtn == _lastHoveredTabletBtn)
            {
                _activeTabletBtn.OnUp(penID);
                //ButtonAudioSource.pitch = Random.Range(.95f, 1.05f);
                //ButtonAudioSource.volume = Random.Range(.9f, 1f);
                //ButtonAudioSource.PlayOneShot(ButtonUpClip[Random.Range(0, ButtonUpClip.Length)]);
            }
            else _activeTabletBtn.OnHoverExit(penID);
            _activeTabletBtn = null;
        }
        if (_activeMFDBtn != null)
        {
            if (_activeMFDBtn == _lastHoveredMFDBtn)
            {
                _activeMFDBtn.OnUp(penID,this);
                //ButtonAudioSource.pitch = Random.Range(.95f, 1.05f);
                //ButtonAudioSource.volume = Random.Range(.9f, 1f);
                //ButtonAudioSource.PlayOneShot(ButtonUpClip[Random.Range(0, ButtonUpClip.Length)]);
            }
            //else _activeMFDBtn.OnHoverExit(penID);
            _activeMFDBtn = null;
        }

        if (_lastHoveredTabletBtn != null) { _lastHoveredTabletBtn.OnHoverExit(penID); _lastHoveredTabletBtn = null; }
       // if (_lastHoveredMFDBtn != null) { _lastHoveredMFDBtn.OnHoverExit(penID); _lastHoveredMFDBtn = null; }
    }

    private int _zoneCount = 0;

    public void OnTriggerEnter(Collider other)
    {
        //Prevent Desktoppers pen mesh from enabling
        if (!Networking.LocalPlayer.IsUserInVR())
        {
            return;
        }
        if (other.name == "PenEnable")
        {
            _zoneCount++;
            PenMesh.SetActive(true);
        }
    }

    public void OnTriggerExit(Collider other)
    {
        //Prevent Desktoppers pen mesh from enabling
        if (!Networking.LocalPlayer.IsUserInVR())
        {
            return;
        }
        if (other.name == "PenEnable")
        {
            _zoneCount--;
            // Only turn off if we aren't inside ANY valid zones
            if (_zoneCount <= 0)
            {
                _zoneCount = 0; // Safety reset
                PenMesh.SetActive(false);
            }
        }
    }
    public void PlayButtonUpClip()
    {
        ButtonAudioSource.pitch = Random.Range(.9f, 1.1f);
        ButtonAudioSource.volume = Random.Range(.9f, 1f);
        ButtonAudioSource.PlayOneShot(ButtonUpClip[Random.Range(0, ButtonUpClip.Length)]);
    }
    public void PlayButtonDownClip()
    {
        ButtonAudioSource.pitch = Random.Range(.9f, 1.1f);
        ButtonAudioSource.volume = Random.Range(.9f, 1f);
        ButtonAudioSource.PlayOneShot(ButtonDownClip[Random.Range(0, ButtonDownClip.Length)]);
    }
}