using UdonSharp;
using UnityEngine;

/// <summary>
/// DockingPortMeta
/// Minimal mapping tag so solver-side docking can identify which station/port
/// a Unity DockingPort corresponds to.
///
/// - For craft ports: isStationPort=false, craftPortIndex set.
/// - For station ports: isStationPort=true, stationIndex + stationPortIndex set.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingPortMeta : UdonSharpBehaviour
{
    [Header("Identity")]
    public bool isStationPort = false;

    [Tooltip("Index into DockingComputer.stations[] list. Only used for station ports.")]
    public int stationIndex = -1;

    [Tooltip("Port index within the station's cached port arrays. Only used for station ports.")]
    public int stationPortIndex = -1;

    [Tooltip("Port index within the craft's cached port arrays. Only used for craft ports.")]
    public int craftPortIndex = -1;
}