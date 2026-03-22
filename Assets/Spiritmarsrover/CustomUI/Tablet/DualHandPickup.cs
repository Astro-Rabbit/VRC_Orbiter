using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class DualHandPickup : UdonSharpBehaviour
{
    private TabletPen pen1;
    private TabletPen pen2;

    // Positional/Rotational offsets for both pens
    private Vector3 offsetPos1;
    private Quaternion offsetRot1;
    private Vector3 offsetPos2;
    private Quaternion offsetRot2;

    public float lerpSpeed = 50f;

    //public GameObject pen1GrabLocalTransform;
   // public GameObject pen2GrabLocalTransform;

    public GameObject TwoHandedObject;

    public GameObject handleL;
    public GameObject handleR;
    public VRC_Pickup pickup;

    [Header("Docking")]
    public bool isDocked = false;
    private Transform targetDockTransform; // The specific point to snap to

    //private int _pen1EnterCount;
    // private int _pen2EnterCount;
    public BoxCollider TabletPickupCol;
    public BoxCollider VRpickupCol;//the pick for this object
    public BoxCollider DesktoppickupCol;//the pick for this object

    private bool isTransitioningToOneHand = false;
    private bool isTransitioningToDock = false;

    [UdonSynced(UdonSyncMode.None)]
    public bool VRPickuped;

    private void Start()
    {
        
        if (!Networking.LocalPlayer.IsUserInVR())
        {
            pickup.pickupable = true;
            TabletPickupCol.center = DesktoppickupCol.center;
            TabletPickupCol.size = DesktoppickupCol.size;
            //TabletPickupCol = DesktoppickupCol;
        }
        else
        {
            pickup.pickupable = false;
            //TabletPickupCol = VRpickupCol;
            TabletPickupCol.center = VRpickupCol.center;
            TabletPickupCol.size = VRpickupCol.size;
        }
    }
    public bool CanBeGrabbed()
    {
        // Returns true if there is at least one empty slot
        return pen1 == null || pen2 == null;
    }

    public void OnGrab(TabletPen pen)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        VRPickuped = true;

        isDocked = false; // Release from dock immediately on grab
        isTransitioningToOneHand = false; // Reset flag so it snaps to hand
        isTransitioningToDock = false;

        if (pen1 == null)
        {
            pen1 = pen;
            // Calculate local offset relative to pen 1
            offsetPos1 = pen.transform.InverseTransformPoint(transform.position);
            offsetRot1 = Quaternion.Inverse(pen.transform.rotation) * transform.rotation;
            //pen1GrabLocalTransform.transform.position = pen.transform.position;
            //pen1GrabLocalTransform.transform.rotation = Quaternion.Euler(Vector3.zero);
        }
        else if (pen2 == null && pen != pen1)
        {
            pen2 = pen;
            // Calculate local offset relative to pen 2
            offsetPos2 = pen.transform.InverseTransformPoint(transform.position);
            offsetRot2 = Quaternion.Inverse(pen.transform.rotation) * transform.rotation;
            //pen2GrabLocalTransform.transform.position = pen.transform.position;
            //pen2GrabLocalTransform.transform.localRotation = Quaternion.Euler(Vector3.zero);
            //HandleTwoHanded();
            
            HandleTwoHanded();
            transform.SetParent(TwoHandedObject.transform, true);
        }
    }

    public void OnRelease(TabletPen pen)
    {
        if (Networking.GetOwner(gameObject) == Networking.LocalPlayer)
        {
            VRPickuped = false;
        }
        
        if (pen1 == pen)
        {
            if (pen2 != null)
            {
                // SAVE ROTATION
                offsetRot1 = Quaternion.Inverse(pen2.transform.rotation) * transform.rotation;
                // SNAP POSITION
                offsetPos1 = offsetPos2;

                pen1 = pen2;
                pen2 = null;
                isTransitioningToOneHand = true; // Start lerp transition
            }
            else
            {
                pen1 = null;
            }
        }
        else if (pen2 == pen)
        {
            if (pen1 != null)
            {
                // SAVE ROTATION
                offsetRot1 = Quaternion.Inverse(pen1.transform.rotation) * transform.rotation;
                isTransitioningToOneHand = true; // Start lerp transition
            }
            pen2 = null;
        }

        if (pen1 != null)
        {

        }
        if(pen2 != null)
        {

        }
        else
        {
            transform.SetParent(null, true);
        }
        // If we are touching a dock when we let go, dock it!
        if (pen1 == null && pen2 == null && targetDockTransform != null)
        {
            isDocked = true;
            transform.SetParent(null, true); // Ensure it's not parented to a hand/rig
            isTransitioningToDock = true;
        }
        // --- THE FIX: Physics Flush (Updated for multiple colliders) ---
        //Collider[] colliders = GetComponentsInChildren<Collider>();
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            if (col != null)
            {
                col.enabled = false;
                col.enabled = true;
            }
        }
    }

    // LateUpdate is best for following VR trackers to reduce jitter
    //void LateUpdate()
    public override void PostLateUpdate()
    {
        if (pickup.pickupable|| Networking.GetOwner(gameObject) != Networking.LocalPlayer) return;

        if (isDocked && targetDockTransform != null)
        {
            if (isTransitioningToDock)
            {
                // Smoothly snap to the dock position and rotation
                transform.position = Vector3.Lerp(transform.position, targetDockTransform.position, Time.deltaTime * lerpSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetDockTransform.rotation, Time.deltaTime * lerpSpeed);
                // Stop lerping once we are close enough to the hand to prevent "floaty" lag
                if ((transform.position - targetDockTransform.position).sqrMagnitude < 0.000001f)
                {
                    isTransitioningToDock = false;
                }
            }
            else
            {
                transform.position = targetDockTransform.position;
                transform.rotation = targetDockTransform.rotation;
            }
            
            return; // Skip hand logic while docked
        }

        if (pen1 != null && pen2 != null )
        {
            HandleTwoHanded();
        }
        else if (pen1 != null)
        {
            HandleOneHanded();
        }

    }

    //private void HandleOneHanded()
    //{
    //    Vector3 targetPos = pen1.transform.TransformPoint(offsetPos1);
    //    Quaternion targetRot = pen1.transform.rotation * offsetRot1;
    //    //transform.position = pen1.transform.TransformPoint(offsetPos1);
    //    //transform.rotation = pen1.transform.rotation * offsetRot1;


    //    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lerpSpeed);
    //    transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * lerpSpeed);
    //}
    private void HandleOneHanded()
    {
        Vector3 targetPos = pen1.transform.TransformPoint(offsetPos1);
        Quaternion targetRot = pen1.transform.rotation * offsetRot1;

        if (isTransitioningToOneHand)
        {
            // Smoothly transition to the new one-handed center
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * lerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lerpSpeed);

            // Stop lerping once we are close enough to the hand to prevent "floaty" lag
            if ((transform.position - targetPos).sqrMagnitude < 0.00001f)
            {
                isTransitioningToOneHand = false;
            }
        }
        else
        {
            // Snappy tracking (No Lerp)
            transform.position = targetPos;
            transform.rotation = targetRot;
        }
    }

    //private void HandleTwoHanded()
    //{
    //    // 1. POSITION: Halfway between the two intended target points
    //    Vector3 target1 = pen1.transform.TransformPoint(offsetPos1);
    //    Vector3 target2 = pen2.transform.TransformPoint(offsetPos2);

    //    //transform.position = Vector3.Lerp(target1, target2, 0.5f);
    //    Vector3 targetPos = Vector3.Lerp(target1, target2, 0.5f);

    //    // 2. ROTATION: Average rotation (Slerp 0.5 is a perfect average)
    //    // This calculates what the rotation would be for both hands and finds the middle
    //    Quaternion rot1 = pen1.transform.rotation * offsetRot1;
    //    Quaternion rot2 = pen2.transform.rotation * offsetRot2;
    //    //transform.rotation = Quaternion.Slerp(rot1, rot2, 0.5f);
    //    Quaternion targetRot = Quaternion.Slerp(rot1, rot2, 0.5f);

    //    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lerpSpeed);
    //    transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * lerpSpeed);
    //}

    void HandleTwoHanded()
    {
        Vector3 pL = pen1.transform.position;
        Vector3 pR = pen2.transform.position;

        // 1. Position: Keep the tablet center at the midpoint of both hands
        //transform.position = Vector3.Lerp(transform.position, (pL + pR) / 2f, Time.deltaTime * lerpSpeed);

        // 2. Rotation: Calculate rotation based on the line between the hands
        Vector3 handDir = (pR - pL).normalized;
        Vector3 handleDir = (handleL.transform.localPosition - handleR.transform.localPosition).normalized;

        // Find the rotation that aligns the handle-vector with the hand-vector
        Quaternion targetRot = Quaternion.FromToRotation(handleDir, handDir);

        // Add tilt/roll based on the average "Up" of the controllers
        Vector3 avgUp = Vector3.Slerp(pen1.transform.up, pen2.transform.up, 0.5f);
        targetRot = Quaternion.LookRotation(targetRot * Vector3.forward, avgUp);

        //transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lerpSpeed);

        TwoHandedObject.transform.position = (pL + pR) / 2f;
        TwoHandedObject.transform.rotation = Quaternion.LookRotation(targetRot * Vector3.forward, avgUp);
    }

    ////// Inform the pens when they are hovering over this object
    //public void OnTriggerEnter(Collider other)
    //{
    //    // Existing Pen logic
    //    TabletPen pen = other.GetComponent<TabletPen>();
    //    if (pen != null)
    //    {
    //        pen.SetHoveredPickup((TabletPenPickup)(Component)this);
    //        return;
    //    }

    //    // NEW: Dock logic
    //    // Check for a "TabletDock" component or a specific tag
    //    if (other.name == "TabletDockTrigger")
    //    {
    //        targetDockTransform = other.transform; // The trigger itself is the dock point

    //    }
    //}

    //public void OnTriggerExit(Collider other)
    //{
    //    // Existing Pen logic
    //    TabletPen pen = other.GetComponent<TabletPen>();
    //    if (pen != null)
    //    {
    //        if (pen._hoveredPickup == (TabletPenPickup)(Component)this)
    //        {
    //            pen.SetHoveredPickup(null);
    //        }
    //        return;
    //    }

    //    // NEW: Dock logic
    //    if (other.transform == targetDockTransform)
    //    {
    //        // If we aren't currently docked, forget this dock point
    //        if (!isDocked)
    //        {
    //            targetDockTransform = null;
    //        }
    //    }
    //}
    //
    public void OnTriggerEnter(Collider other)
    {
        // Existing Pen logic
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null)
        {
            pen.SetHoveredPickup((TabletPenPickup)(Component)this);
            return;
        }

        // NEW: Only look for a dock if the tablet is currently being held
        if (pen1 != null && other.name == "TabletDockTrigger" &&!isDocked)
        {
            targetDockTransform = other.transform;
        }
    }

    public void OnTriggerExit(Collider other)
    {
        // Existing Pen logic
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null)
        {
            if (pen._hoveredPickup == (TabletPenPickup)(Component)this)
            {
                pen.SetHoveredPickup(null);
            }
            return;
        }

        // Only clear the target dock if we are holding the tablet and moving away
        // If we are docked (isDocked == true), we keep the reference
        if (pen1 != null && other.transform == targetDockTransform)
        {
            targetDockTransform = null;
        }
    }

    public override void OnDeserialization()
    {
        if (VRPickuped)
        {
            TabletPickupCol.enabled = false;
        }
        else
        {
            TabletPickupCol.enabled = true;
        }
    }

}