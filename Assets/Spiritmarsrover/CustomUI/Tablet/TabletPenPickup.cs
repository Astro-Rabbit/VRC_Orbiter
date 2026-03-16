using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletPenPickup : UdonSharpBehaviour
{
    [Header("Base Pickup Settings")]
    public bool isBeingHeld;

    [HideInInspector] public TabletPen currentPen;

    protected Vector3 heldPosOffset;
    protected Quaternion heldRotOffset;

    /// <summary>
    /// TabletPen calls this to check if it's allowed to grab this object.
    /// DualHandPickup overrides this to allow two hands.
    /// </summary>
    public virtual bool CanBeGrabbed()
    {
        return !isBeingHeld;
    }

    /// <summary>
    /// Called by TabletPen when the grip is pressed.
    /// </summary>
    public virtual void OnGrab(TabletPen pen)
    {
        isBeingHeld = true;
        currentPen = pen;

        // Calculate the "Parent Constraint" style offsets
        heldPosOffset = pen.transform.InverseTransformPoint(transform.position);
        heldRotOffset = Quaternion.Inverse(pen.transform.rotation) * transform.rotation;
    }

    /// <summary>
    /// Called by TabletPen when the grip is released.
    /// </summary>
    public virtual void OnRelease(TabletPen pen)
    {
        isBeingHeld = false;
        currentPen = null;
    }

    /// <summary>
    /// Standard single-hand movement logic. 
    /// Overridden in DualHandPickup for multi-hand logic.
    /// </summary>
    public virtual void LateUpdate()
    {
        if (isBeingHeld && currentPen != null)
        {
            transform.position = currentPen.transform.TransformPoint(heldPosOffset);
            transform.rotation = currentPen.transform.rotation * heldRotOffset;
        }
    }

    // --- TRIGGER SYSTEM ---
    // This tells the pen "I am a pickup you can interact with" 
    // when the pen's collider enters this object's trigger.

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Check if the thing entering our trigger is a TabletPen
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null)
        {
            pen.SetHoveredPickup(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;

        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null)
        {
            // Only clear the hover if we are the one currently being hovered
            if (pen._hoveredPickup == this)
            {
                pen.SetHoveredPickup(null);
            }
        }
    }
}