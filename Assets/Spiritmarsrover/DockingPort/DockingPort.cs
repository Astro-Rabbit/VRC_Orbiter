using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingPort : UdonSharpBehaviour
{
    public CraftState craftState;
    public DockingLatch[] latches; // Assign the 3 latches here

    [Header("Hard Capture Target")]
    [Tooltip("A Transform representing where the OTHER craft's port origin should be when docked.")]
    public Transform dockTarget;

    public bool IsFullyLatched()
    {
        if (latches.Length < 3) return false;
        return latches[0].isLatched && latches[1].isLatched && latches[2].isLatched;
    }

    public DockingPort GetTargetPort()
    {
        // If latched, return the port we are connected to via the first latch
        if (latches[0].isLatched && latches[0].targetLatch != null)
        {
            return latches[0].targetLatch.parentPort;
        }
        return null;
    }
}