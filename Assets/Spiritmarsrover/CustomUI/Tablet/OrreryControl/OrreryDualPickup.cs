using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class OrreryDualPickup : TabletPenPickup
{
    [Header("Pens")]
    private TabletPen pen1;
    private TabletPen pen2;

    [Header("Rotation Settings")]
    public float lerpSpeed = 20f;
   // public Transform handleL; // Local reference points for axis
    //public Transform handleR;

    private Quaternion offsetRot1;
    private bool isTransitioningToOneHand = false;

    [Header("Shell Logic")]
    private bool pen1InInner;
    private bool pen2InInner;

    public GameObject orrery;
    private float initialHandDistance;
    private Vector3 initialOrreryScale;
    public float minScale = 0.1f;
    public float maxScale = 5.0f;

    public override bool CanBeGrabbed()
    {
        // Returns true if there is at least one empty slot
        return pen1 == null || pen2 == null;
    }

    public override void OnGrab(TabletPen pen)
    {
        //Debug.Log("[OrreryDualPickup] OnGrab Starting");
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
       // Debug.Log("[OrreryDualPickup] OnGrab: Owner " + Networking.LocalPlayer);
        isTransitioningToOneHand = false;

        if (pen1 == null)
        {
            pen1 = pen;
            // Capture rotation offset: How is the orrery rotated relative to the pen?
            offsetRot1 = Quaternion.Inverse(pen.transform.rotation) * orrery.transform.rotation;
            // Debug.Log("[OrreryDualPickup] OnGrab pen1 ! null: " + pen1.name);

            initialPenDir = (pen.transform.position - orrery.transform.position).normalized;

            // 2. Capture the starting rotations
            initialOrreryRot = orrery.transform.rotation;
            initialPenRot = pen.transform.rotation;
        }
        else if (pen2 == null && pen != pen1)
        {
            pen2 = pen;

            // --- CAPTURE BASELINES ---
            // 1. Scale
            initialHandDistance = Vector3.Distance(pen1.transform.position, pen2.transform.position);
            initialOrreryScale = orrery.transform.localScale;

            // 2. Combined Rotation (Steering Wheel)
            Vector3 mid = (pen1.transform.position + pen2.transform.position) * 0.5f;
            Vector3 midDir = (mid - orrery.transform.position).normalized;
            Vector3 handlebar = (pen2.transform.position - pen1.transform.position).normalized;

            initialHandRot = Quaternion.LookRotation(midDir, handlebar);
            initialOrreryRot = orrery.transform.rotation; // Capture current state to avoid snapping

            if (initialHandDistance < 0.001f) initialHandDistance = 0.001f;
        }
    }

    //public override void OnRelease(TabletPen pen)
    //{
    //   // Debug.Log("[OrreryDualPickup] OnRelease Starting");
    //    if (pen1 == pen)
    //    {
    //        if (pen2 != null)
    //        {
    //            // Shift pen2 to pen1 slot
    //            offsetRot1 = Quaternion.Inverse(pen2.transform.rotation) * orrery.transform.rotation;
    //            pen1 = pen2;
    //            pen2 = null;
    //            isTransitioningToOneHand = true;
    //            //Debug.Log("[OrreryDualPickup] OnRelease Pen2: " + pen2.name);
    //        }
    //        else pen1 = null;
    //    }
    //    else if (pen2 == pen)
    //    {
    //        if (pen1 != null)
    //        {
    //            offsetRot1 = Quaternion.Inverse(pen1.transform.rotation) * orrery.transform.rotation;
    //            isTransitioningToOneHand = true;
    //            //Debug.Log("[OrreryDualPickup] OnRelease Pen1: " + pen1.name);
    //        }
    //        pen2 = null;
    //    }
    //}
    public override void OnRelease(TabletPen pen)
    {
        if (pen1 == pen)
        {
            if (pen2 != null)
            {
                // Shift pen2 to the primary slot
                pen1 = pen2;
                pen2 = null;

                // --- RE-CAPTURE BASELINE ---
                initialPenDir = (pen1.transform.position - orrery.transform.position).normalized;
                initialOrreryRot = orrery.transform.rotation;
                initialPenRot = pen1.transform.rotation;

                isTransitioningToOneHand = true;
            }
            else pen1 = null;
        }
        else if (pen2 == pen)
        {
            if (pen1 != null)
            {
                // --- RE-CAPTURE BASELINE ---
                // Even though pen1 didn't change, its "start point" must be 
                // reset to the orrery's current orientation.
                initialPenDir = (pen1.transform.position - orrery.transform.position).normalized;
                initialOrreryRot = orrery.transform.rotation;
                initialPenRot = pen1.transform.rotation;

                isTransitioningToOneHand = true;
            }
            pen2 = null;
        }
    }

    public override void PostLateUpdate()
    {
        if (Networking.GetOwner(gameObject) != Networking.LocalPlayer) return;

        if (pen1 != null && pen2 != null)
        {
            HandleTwoHanded();
        }
        else if (pen1 != null)
        {
            HandleOneHanded();
        }
    }
    private Vector3 initialPenDir;     // Direction from center to pen at grab
    private Quaternion initialOrreryRot; // Orrery rotation at grab
    private Quaternion initialPenRot;   // Pen rotation at grab
    //private void HandleOneHanded()
    //{
    //    Quaternion targetRot = pen1.transform.rotation * offsetRot1;

    //    if (isTransitioningToOneHand)
    //    {
    //        orrery.transform.rotation = Quaternion.Slerp(orrery.transform.rotation, targetRot, Time.deltaTime * lerpSpeed);
    //        if (Quaternion.Angle(orrery.transform.rotation, targetRot) < 0.1f) isTransitioningToOneHand = false;
    //    }
    //    else
    //    {
    //        orrery.transform.rotation = targetRot;
    //    }
    //}
    private void HandleOneHanded()
    {
        // --- PART A: TANGENTIAL TRACKING (Position) ---
        // Calculate the current direction from center to pen
        Vector3 currentPenDir = (pen1.transform.position - orrery.transform.position).normalized;

        // Find the rotation required to move from the initial direction to the current direction
        Quaternion trackballRot = Quaternion.FromToRotation(initialPenDir, currentPenDir);

        // --- PART B: RADIAL ROLL (Wrist Twist) ---
        // Calculate how much the pen has rotated since the grab
        Quaternion penDeltaRot = pen1.transform.rotation * Quaternion.Inverse(initialPenRot);

        // Extract only the rotation around the radial axis (the line from center to hand)
        // We project the pen's rotation delta onto the current radial vector
        penDeltaRot.ToAngleAxis(out float angle, out Vector3 axis);
        float twistAngle = angle * Vector3.Dot(axis, currentPenDir);
        Quaternion twistRot = Quaternion.AngleAxis(twistAngle, currentPenDir);

        // --- PART C: FINAL COMBINATION ---
        // Apply the trackball rotation, then the twist, to the initial orrery orientation
        Quaternion targetRot = twistRot * trackballRot * initialOrreryRot;

        // Apply via Lerp for smoothness (prevents high-frequency jitter)
        if (isTransitioningToOneHand)
        {
            orrery.transform.rotation = Quaternion.Slerp(orrery.transform.rotation, targetRot, Time.deltaTime * lerpSpeed);
            if (Quaternion.Angle(orrery.transform.rotation, targetRot) < 0.1f) isTransitioningToOneHand = false;
        }
        else
        {
            // Use a high lerp speed even when not "transitioning" to filter tracking jitter
            orrery.transform.rotation = Quaternion.Slerp(orrery.transform.rotation, targetRot, Time.deltaTime * 35f);
        }
    }
    private Quaternion initialHandRot; // Combined orientation of both hands at grab

    private void HandleTwoHanded()
    {
        // --- SCALING LOGIC ---
        float currentHandDistance = Vector3.Distance(pen1.transform.position, pen2.transform.position);

        // Ratio: current distance / initial distance
        float scaleFactor = currentHandDistance / initialHandDistance;

        // Apply factor to the starting scale
        Vector3 targetScale = initialOrreryScale * scaleFactor;

        // Clamp to prevent the orrery from becoming too small or too huge
        float clampedX = Mathf.Clamp(targetScale.x, minScale, maxScale);
        float clampedY = Mathf.Clamp(targetScale.y, minScale, maxScale);
        float clampedZ = Mathf.Clamp(targetScale.z, minScale, maxScale);

        orrery.transform.localScale = new Vector3(clampedX, clampedY, clampedZ);

        // --- OPTIONAL: TWO-HANDED ROTATION ---
        // --- 2. STEERING (Rotation) ---
        // Calculate current combined hand vectors
        Vector3 currentMid = (pen1.transform.position + pen2.transform.position) * 0.5f;
        Vector3 currentMidDir = (currentMid - orrery.transform.position).normalized;
        Vector3 currentHandlebar = (pen2.transform.position - pen1.transform.position).normalized;

        // Create current "Steering Wheel" orientation
        Quaternion currentHandRot = Quaternion.LookRotation(currentMidDir, currentHandlebar);

        // Calculate how much the "Steering Wheel" has rotated since the second grab
        Quaternion handDelta = currentHandRot * Quaternion.Inverse(initialHandRot);

        // Apply that delta to the orrery's starting rotation
        Quaternion targetRot = handDelta * initialOrreryRot;

        // Apply with high-speed Lerp to filter jitter
        orrery.transform.rotation = Quaternion.Slerp(orrery.transform.rotation, targetRot, Time.deltaTime * 35f);
    }

    // --- SHELL COLLIDER LOGIC ---

    public void SetPenInInner(TabletPen pen, bool inside)
    {
        if (pen == pen1) pen1InInner = inside;
        if (pen == pen2) pen2InInner = inside;

        if (inside)
        {
            // Entering the center: Disable hover
            if (pen._hoveredPickup == (TabletPenPickup)(Component)this)
                pen.SetHoveredPickup(null);
        }
        else
        {
            // Leaving the center: You are now back in the "Shell" 
            // Re-enable hover immediately
            pen.SetHoveredPickup((TabletPenPickup)(Component)this);
        }
    }

    public override void OnTriggerEnter(Collider other)
    {
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null)
        {
            //Debug.Log("[OrreryDualPickup] OnTriggerEnter");
            // Only allow hover if we are NOT in the inner sphere
            bool isDeep = (pen == pen1 && pen1InInner) || (pen == pen2 && pen2InInner);
            if (!isDeep) pen.SetHoveredPickup((TabletPenPickup)(Component)this);
        }
    }

    public override void OnTriggerExit(Collider other)
    {
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null && pen._hoveredPickup == (TabletPenPickup)(Component)this)
        {
           // Debug.Log("[OrreryDualPickup] OnTriggerExit");
            pen.SetHoveredPickup(null);
        }
    }
}