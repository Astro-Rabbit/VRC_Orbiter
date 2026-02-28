using UdonSharp;
using UnityEngine;

public enum DockingState { Ready, SoftCapture, HardCapture }

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingController : UdonSharpBehaviour
{
    public DockingPort localPort;
    public DockingState state = DockingState.Ready;
    public DockingPort activeTargetPort;

    [Header("Transition Thresholds")]
    public float captureVelocityThreshold = 0.05f;

    private void Update()
    {
        if (localPort == null) return;

        bool latched = localPort.IsFullyLatched();

        if (!latched)
        {
            state = DockingState.Ready;
            activeTargetPort = null;
            //Debug.Log("[DockingController] Latched!");
            return;
        }

        if (state == DockingState.Ready && latched)
        {
            state = DockingState.SoftCapture;
            activeTargetPort = localPort.GetTargetPort();
           // Debug.Log("[DockingController] Soft Capture!");
        }

        if (state == DockingState.SoftCapture && activeTargetPort != null)
        {
            // Check if we are slow enough to Hard Capture
            //Debug.Log("[DockingController] Ready for Hard Capture!");
            Vector3 relV = new Vector3(
                (float)(localPort.craftState.vx - activeTargetPort.craftState.vx),
                (float)(localPort.craftState.vy - activeTargetPort.craftState.vy),
                (float)(localPort.craftState.vz - activeTargetPort.craftState.vz)
            );

            if (relV.sqrMagnitude < captureVelocityThreshold* captureVelocityThreshold)
            {
                state = DockingState.HardCapture;
                Debug.Log("[DockingController] Capture!");
            }
        }
    }
}