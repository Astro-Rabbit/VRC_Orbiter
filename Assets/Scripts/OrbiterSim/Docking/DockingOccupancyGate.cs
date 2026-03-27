using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DockingOccupancyGate : UdonSharpBehaviour
{
    [Header("Optional authority ref")]
    public SimManager simManager;

    [Header("Synced outside-craft masks")]
    [UdonSynced] public ushort outsideMask0 = 0; // playerId 1..16
    [UdonSynced] public ushort outsideMask1 = 0; // playerId 17..32
    [UdonSynced] public ushort outsideMask2 = 0; // playerId 33..48

    [Header("Debug")]
    public bool log = false;

    private ushort _lastLogged0;
    private ushort _lastLogged1;
    private ushort _lastLogged2;

    // ---------------------------------------------------------------------
    // Public queries
    // ---------------------------------------------------------------------
    public bool AnyPlayerOutsideCraft()
    {
        return outsideMask0 != 0 || outsideMask1 != 0 || outsideMask2 != 0;
    }

    public int GetOutsideCount()
    {
        return CountBits16(outsideMask0) + CountBits16(outsideMask1) + CountBits16(outsideMask2);
    }

    public bool IsPlayerOutside(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return false;
        return IsPlayerIdOutside(player.playerId);
    }

    public bool IsPlayerIdOutside(int playerId)
    {
        int maskIndex;
        int bitIndex;

        if (!TryGetMaskBit(playerId, out maskIndex, out bitIndex))
            return false;

        ushort bit = (ushort)(1 << bitIndex);

        if (maskIndex == 0) return (outsideMask0 & bit) != 0;
        if (maskIndex == 1) return (outsideMask1 & bit) != 0;
        return (outsideMask2 & bit) != 0;
    }

    // ---------------------------------------------------------------------
    // Public owner-side state change API
    // ---------------------------------------------------------------------
    public void SetPlayerOutside(VRCPlayerApi player, bool outside)
    {
        if (!Utilities.IsValid(player)) return;
        SetPlayerIdOutside(player.playerId, outside);
    }

    public void SetPlayerIdOutside(int playerId, bool outside)
    {
        if (!HasAuthority()) return;

        int maskIndex;
        int bitIndex;

        if (!TryGetMaskBit(playerId, out maskIndex, out bitIndex))
            return;

        ushort bit = (ushort)(1 << bitIndex);
        bool changed = false;

        if (maskIndex == 0)
        {
            ushort oldVal = outsideMask0;
            outsideMask0 = outside ? (ushort)(outsideMask0 | bit) : (ushort)(outsideMask0 & ~bit);
            changed = outsideMask0 != oldVal;
        }
        else if (maskIndex == 1)
        {
            ushort oldVal = outsideMask1;
            outsideMask1 = outside ? (ushort)(outsideMask1 | bit) : (ushort)(outsideMask1 & ~bit);
            changed = outsideMask1 != oldVal;
        }
        else
        {
            ushort oldVal = outsideMask2;
            outsideMask2 = outside ? (ushort)(outsideMask2 | bit) : (ushort)(outsideMask2 & ~bit);
            changed = outsideMask2 != oldVal;
        }

        if (changed)
        {
            RequestSerialization();

            if (log)
            {
                Debug.Log("[DockingOccupancyGate] Set playerId=" + playerId +
                          " outside=" + outside +
                          " count=" + GetOutsideCount());
            }
        }
    }

    public void ClearAllOutsideFlags()
    {
        if (!HasAuthority()) return;

        if (outsideMask0 == 0 && outsideMask1 == 0 && outsideMask2 == 0)
            return;

        outsideMask0 = 0;
        outsideMask1 = 0;
        outsideMask2 = 0;
        RequestSerialization();

        if (log)
            Debug.Log("[DockingOccupancyGate] Cleared all outside flags.");
    }

    // ---------------------------------------------------------------------
    // Network-routed helpers for trigger scripts
    // These accept a cached playerId set by the local trigger script.
    // ---------------------------------------------------------------------
    [HideInInspector] public int pendingPlayerId = -1;

    public void RouteMarkPendingPlayerOutside()
    {
        SendToOwner(nameof(Owner_MarkPendingPlayerOutside));
    }

    public void RouteMarkPendingPlayerInside()
    {
        SendToOwner(nameof(Owner_MarkPendingPlayerInside));
    }

    [NetworkCallable]
    public void Owner_MarkPendingPlayerOutside()
    {
        if (!HasAuthority()) return;
        if (pendingPlayerId < 1) return;

        SetPlayerIdOutside(pendingPlayerId, true);
    }

    [NetworkCallable]
    public void Owner_MarkPendingPlayerInside()
    {
        if (!HasAuthority()) return;
        if (pendingPlayerId < 1) return;

        SetPlayerIdOutside(pendingPlayerId, false);
    }

    // ---------------------------------------------------------------------
    // Cleanup on leave
    // ---------------------------------------------------------------------
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!HasAuthority()) return;
        if (!Utilities.IsValid(player)) return;

        SetPlayerIdOutside(player.playerId, false);
    }

    public override void OnDeserialization()
    {
        if (!log) return;

        if (_lastLogged0 == outsideMask0 &&
            _lastLogged1 == outsideMask1 &&
            _lastLogged2 == outsideMask2)
        {
            return;
        }

        _lastLogged0 = outsideMask0;
        _lastLogged1 = outsideMask1;
        _lastLogged2 = outsideMask2;

        Debug.Log("[DockingOccupancyGate] Deserialized. count=" + GetOutsideCount());
    }

    // ---------------------------------------------------------------------
    // Internal helpers
    // ---------------------------------------------------------------------
    private bool HasAuthority()
    {
        if (simManager != null) return simManager.IsSimOwner();
        return Networking.IsOwner(gameObject);
    }

    private void SendToOwner(string eventName)
    {
        if (HasAuthority())
        {
            SendCustomEvent(eventName);
            return;
        }

        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, eventName);
    }

    private bool TryGetMaskBit(int playerId, out int maskIndex, out int bitIndex)
    {
        maskIndex = -1;
        bitIndex = -1;

        if (playerId < 1 || playerId > 48)
            return false;

        int zeroBased = playerId - 1;
        maskIndex = zeroBased / 16;
        bitIndex = zeroBased % 16;
        return true;
    }

    private int CountBits16(ushort x)
    {
        int count = 0;
        ushort v = x;

        while (v != 0)
        {
            count += (v & 1);
            v >>= 1;
        }

        return count;
    }
}