using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletPenPickup : UdonSharpBehaviour
{
    [HideInInspector] public bool isBeingHeld;
    [HideInInspector] public TabletPen hoveringPen;

    public void OnGrab()
    {
        isBeingHeld = true;
    }

    public void OnRelease()
    {
        isBeingHeld = false;
    }

    //public void OnTriggerEnter(Collider other)
    //{
    //    // Check if the thing entering the trigger is a TabletPen
    //    TabletPen pen = other.GetComponent<TabletPen>();
    //    if (pen != null)
    //    {
    //        hoveringPen = pen;
    //        // Tell the pen that it is hovering over THIS handle
    //        pen.SetHoveredPickup(this);
    //        Debug.Log("[TabletPenPickup] Pen entered handle trigger");
    //    }
    //}

    //public void OnTriggerExit(Collider other)
    //{
    //    if (hoveringPen != null && other.gameObject == hoveringPen.gameObject)
    //    {
    //        // Tell the pen it is no longer hovering over us
    //        hoveringPen.SetHoveredPickup(null);
    //        hoveringPen = null;
    //        Debug.Log("[TabletPenPickup] Pen exited handle trigger");
    //    }
    //}

    public void OnTriggerEnter(Collider other)
    {
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null)
        {
            hoveringPen = pen;
            pen.SetHoveredPickup(this); // Tell the pen "I am here"
            Debug.Log("[TabletPenPickup] Pen inside handle");
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (hoveringPen != null && other.gameObject == hoveringPen.gameObject)
        {
            hoveringPen.SetHoveredPickup(null); // Tell the pen "I am gone"
            hoveringPen = null;
        }
    }
}