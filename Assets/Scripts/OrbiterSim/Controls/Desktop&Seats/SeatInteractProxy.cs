using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SeatInteractProxy : UdonSharpBehaviour
{
    public SeatStationDriver seatDriver;

    [Header("Visual / Collider")]
    public Collider interactCollider;
    public GameObject visualRoot; // optional (highlight mesh, icon, etc.)

    public override void Interact()
    {
        if (seatDriver == null) return;
        seatDriver.TryUseSeat();
    }

    public void SetInteractEnabled(bool enabled)
    {
        if (interactCollider != null)
            interactCollider.enabled = enabled;

        if (visualRoot != null)
            visualRoot.SetActive(enabled);
    }
}