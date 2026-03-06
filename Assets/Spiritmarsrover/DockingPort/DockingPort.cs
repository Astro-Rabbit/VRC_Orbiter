using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingPort : UdonSharpBehaviour
{
    [Header("Role")]
    [Tooltip("True if this docking port belongs to a station; false if it belongs to the active craft.")]
    public bool isStationPort = false;

    [Header("Kinematics source (E frame)")]
    [Tooltip("Set when isStationPort=false")]
    public CraftStateModel craftModel;

    [Tooltip("Set when isStationPort=true")]
    public StationStateModel stationModel;

    [Header("Latch geometry")]
    public DockingLatch[] latches; // Assign the 3 latches here

    [Header("Hard Capture Target (optional legacy)")]
    [Tooltip("A Transform representing where the OTHER craft's port origin should be when docked.")]
    public Transform dockTarget;

    public bool IsFullyLatched()
    {
        if (latches == null || latches.Length < 3) return false;
        return latches[0].isLatched && latches[1].isLatched && latches[2].isLatched;
    }

    public DockingPort GetTargetPort()
    {
        // If latched, return the port we are connected to via the first latch
        if (latches != null && latches.Length > 0 && latches[0].isLatched && latches[0].targetLatch != null)
            return latches[0].targetLatch.parentPort;

        return null;
    }

    // Unified velocity getter in solver inertial E frame
    public Vector3 GetVelocityE()
    {
        if (!isStationPort)
        {
            if (craftModel == null) return Vector3.zero;
            return new Vector3((float)craftModel.vx, (float)craftModel.vy, (float)craftModel.vz);
        }
        else
        {
            if (stationModel == null || !stationModel.valid) return Vector3.zero;
            return new Vector3((float)stationModel.vx, (float)stationModel.vy, (float)stationModel.vz);
        }
    }
}