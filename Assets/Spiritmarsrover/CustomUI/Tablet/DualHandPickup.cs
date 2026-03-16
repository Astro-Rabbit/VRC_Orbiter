using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DualHandPickup : UdonSharpBehaviour
{
    private TabletPen pen1;
    private TabletPen pen2;

    // Positional/Rotational offsets for both pens
    private Vector3 offsetPos1;
    private Quaternion offsetRot1;
    private Vector3 offsetPos2;
    private Quaternion offsetRot2;

    public bool CanBeGrabbed()
    {
        // Returns true if there is at least one empty slot
        return pen1 == null || pen2 == null;
    }

    public void OnGrab(TabletPen pen)
    {
        if (pen1 == null)
        {
            pen1 = pen;
            // Calculate local offset relative to pen 1
            offsetPos1 = pen.transform.InverseTransformPoint(transform.position);
            offsetRot1 = Quaternion.Inverse(pen.transform.rotation) * transform.rotation;
        }
        else if (pen2 == null && pen != pen1)
        {
            pen2 = pen;
            // Calculate local offset relative to pen 2
            offsetPos2 = pen.transform.InverseTransformPoint(transform.position);
            offsetRot2 = Quaternion.Inverse(pen.transform.rotation) * transform.rotation;
        }
    }

    public void OnRelease(TabletPen pen)
    {
        if (pen1 == pen)
        {
            pen1 = pen2; // Shift pen 2 to slot 1 if it exists
            offsetPos1 = offsetPos2;
            offsetRot1 = offsetRot2;
            pen2 = null;
        }
        else if (pen2 == pen)
        {
            pen2 = null;
        }
    }

    // LateUpdate is best for following VR trackers to reduce jitter
    void LateUpdate()
    {
        if (pen1 != null && pen2 != null)
        {
            HandleTwoHanded();
        }
        else if (pen1 != null)
        {
            HandleOneHanded();
        }
    }

    private void HandleOneHanded()
    {
        transform.position = pen1.transform.TransformPoint(offsetPos1);
        transform.rotation = pen1.transform.rotation * offsetRot1;
    }

    private void HandleTwoHanded()
    {
        // 1. POSITION: Halfway between the two intended target points
        Vector3 target1 = pen1.transform.TransformPoint(offsetPos1);
        Vector3 target2 = pen2.transform.TransformPoint(offsetPos2);
        transform.position = Vector3.Lerp(target1, target2, 0.5f);

        // 2. ROTATION: Average rotation (Slerp 0.5 is a perfect average)
        // This calculates what the rotation would be for both hands and finds the middle
        Quaternion rot1 = pen1.transform.rotation * offsetRot1;
        Quaternion rot2 = pen2.transform.rotation * offsetRot2;
        transform.rotation = Quaternion.Slerp(rot1, rot2, 0.5f);
    }

    // Inform the pens when they are hovering over this object
    public void OnTriggerEnter(Collider other)
    {
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null)
        {
            pen.SetHoveredPickup((TabletPenPickup)(Component)this);
            // Note: If TabletPenPickup is a base class, ensure this inherits from it
            // or change TabletPen.cs to use 'DualHandPickup' type.
        }
    }

    public void OnTriggerExit(Collider other)
    {
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null)
        {
            if (pen._hoveredPickup == (TabletPenPickup)(Component)this)
            {
                pen.SetHoveredPickup(null);
            }
        }
    }
}