using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingOccupancyBoundaryTrigger : UdonSharpBehaviour
{
    public const byte MARK_OUTSIDE = 0;
    public const byte MARK_INSIDE = 1;

    [Header("Refs")]
    public DockingOccupancyGate occupancyGate;

    [Header("Mode")]
    [Tooltip("0 = mark player outside craft, 1 = mark player inside craft")]
    public byte mode = MARK_OUTSIDE;

    [Header("Filter")]
    public bool localPlayerOnly = true;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (occupancyGate == null) return;
        if (!Utilities.IsValid(player)) return;

        if (localPlayerOnly && !player.isLocal)
            return;

        occupancyGate.pendingPlayerId = player.playerId;

        if (mode == MARK_OUTSIDE)
            occupancyGate.RouteMarkPendingPlayerOutside();
        else
            occupancyGate.RouteMarkPendingPlayerInside();
    }
}