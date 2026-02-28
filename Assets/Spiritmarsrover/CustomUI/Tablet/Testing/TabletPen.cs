using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletPen : UdonSharpBehaviour
{
    public float rayDistance = 0.05f;
    public LayerMask tabletLayer;
    private bool isTouching = false;

    //private bool _wasTouching = false; // Track previous state
    private TabletButton _activeButton;
    private bool _wasTouching;
    private TabletButton _lastHoveredButton; // The button the pen is currently over
    public int penID; // 1 for Pen A, 2 for Pen B, etc.

    public bool isRightHand; // Toggle this in the inspector for each pen
    private string _triggerAxis;
    void Start()
    {
        // Set the correct axis string based on hand
        _triggerAxis = isRightHand ? "Oculus_CrossPlatform_SecondaryIndexTrigger" : "Oculus_CrossPlatform_PrimaryIndexTrigger";
    }
    void Update()
    {
        // 1. GET INPUT: Check VR Trigger or Desktop Mouse
        bool triggerHeld = (Input.GetAxisRaw(_triggerAxis) > 0.5f) || Input.GetMouseButton(0);

        RaycastHit hit;
        bool currentlyHitting = false;
        TabletButton hoveredButton = null;

        // 2. RAYCAST: Only active while trigger is held
        if (triggerHeld)
        {
            currentlyHitting = Physics.Raycast(transform.position, -transform.up, out hit, rayDistance, tabletLayer);

            if (currentlyHitting)
            {
                TabletScreen screen = hit.collider.GetComponent<TabletScreen>();
                if (screen != null)
                {
                    hoveredButton = screen.GetButtonAtPoint(hit.point);

                    // HOVER LOGIC: Handle changing buttons while sliding
                    if (hoveredButton != _lastHoveredButton)
                    {
                        if (_lastHoveredButton != null) _lastHoveredButton.OnHoverExit(penID);
                        if (hoveredButton != null) hoveredButton.OnHoverEnter(penID);
                        _lastHoveredButton = hoveredButton;
                    }

                    // TOUCH DOWN: First frame of contact
                    if (!_wasTouching)
                    {
                        _activeButton = hoveredButton;
                        if (_activeButton != null) _activeButton.OnDown(penID);
                    }
                    // TOUCH STAY: Continuous frames
                    else if (_activeButton != null)
                    {
                        _activeButton.OnStay(penID);
                    }
                }
            }
        }

        // 3. RELEASE LOGIC: Runs the moment trigger is released OR ray leaves the screen
        if (!currentlyHitting && _wasTouching)
        {
            if (_activeButton != null)
            {
                // Only trigger "Up" (The Click) if release happened inside the original button
                if (_activeButton == _lastHoveredButton)
                {
                    _activeButton.OnUp(penID);
                }
                else
                {
                    // If we let go outside, ensure the button resets its color
                    _activeButton.OnHoverExit(penID);
                }
                _activeButton = null;
            }

            // Cleanup general hover state
            if (_lastHoveredButton != null)
            {
                _lastHoveredButton.OnHoverExit(penID);
                _lastHoveredButton = null;
            }
        }

        _wasTouching = currentlyHitting;
    }
}