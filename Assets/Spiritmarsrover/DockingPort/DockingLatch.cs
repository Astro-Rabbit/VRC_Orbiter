using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingLatch : UdonSharpBehaviour
{
    public DockingPort parentPort;
    [System.NonSerialized] public bool isLatched = false;
    [System.NonSerialized] public DockingLatch targetLatch;

    private void OnTriggerEnter(Collider foreign)
    {
        //Debug.Log("[DockingLatch] TriggereddWithSomething");
        if (foreign == null) return;

        // Look for another latch
        DockingLatch other = foreign.GetComponent<DockingLatch>();

        // Ensure it's a latch, not ours, and belongs to a different craft 
        //Don't really need to check if its ours because we will not be intersecting any of our own ports. 
        //if (other != null && other.parentPort != parentPort)
        if (other != null)
        {
            isLatched = true;
            targetLatch = other;
            //if (parentPort != null)
            //{
            //    Debug.Log("[DockingLatch] Fully Latched?: " + parentPort.IsFullyLatched());
            //}
            
            
        }
    }

    private void OnTriggerExit(Collider foreign)
    {
        DockingLatch other = foreign.GetComponent<DockingLatch>();
        if (other != null && other == targetLatch)
        {
            isLatched = false;
            targetLatch = null;
        }
    }
}