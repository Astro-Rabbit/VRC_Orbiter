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
            return;
        }

        if (state == DockingState.Ready && latched)
        {
            state = DockingState.SoftCapture;
            activeTargetPort = localPort.GetTargetPort();
            return;
        }

        if (state == DockingState.SoftCapture && activeTargetPort != null)
        {
            // Relative velocity in solver inertial frame (E)
            Vector3 vLocalE  = localPort.GetVelocityE();
            Vector3 vTargE   = activeTargetPort.GetVelocityE();
            Vector3 relV     = vLocalE - vTargE;

            float thr2 = captureVelocityThreshold * captureVelocityThreshold;
            if (relV.sqrMagnitude < thr2)
            {
                state = DockingState.HardCapture;
                Debug.Log("[DockingController] Capture!");
            }
        }
    }

    public void ForceResetController()
    {
        state = DockingState.Ready;
        activeTargetPort = null;

        if (localPort != null)
            localPort.ForceClearAllLatches();
    }


}