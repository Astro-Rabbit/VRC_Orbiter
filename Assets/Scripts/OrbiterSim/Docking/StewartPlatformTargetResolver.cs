using UdonSharp;
using UnityEngine;

/// <summary>
/// StewartPlatformTargetResolver
///
/// Resolves the Stewart platform's target Transform from the currently selected
/// station docking port.
///
/// Uses existing StationDockingPortsAuthoring references, so the Stewart platform
/// can directly track the live rendered station port transform.
///
/// This is visual-only. It does not affect docking authority.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StewartPlatformTargetResolver : UdonSharpBehaviour
{
    [Header("Inputs")]
    public GuidanceNavContactsState contacts;

    [Tooltip("Index must match SimManager.stations[] / contacts station indexing.")]
    public StationDockingPortsAuthoring[] stationDockingAuthoring;

    [Header("Output")]
    public StewartPlatformController platform;

    [Header("Behavior")]
    [Tooltip("If true, clear target when no valid selection exists.")]
    public bool clearTargetWhenInvalid = true;

    [Tooltip("If true, also require contacts.dockValid0 before assigning target.")]
    public bool requireDockValid0 = true;

    [Header("Debug")]
    public bool logChanges = false;

    private Transform _lastTarget = null;
    private int _lastStation = -1;
    private int _lastPort = -1;

    public void Tick()
    {
        if (platform == null || contacts == null || stationDockingAuthoring == null)
            return;

        // If platform is disabled, don't give it a target
        if (!platform.platformEnabled)
        {
            if (clearTargetWhenInvalid)
                platform.target = null;

            if (_lastTarget != null && logChanges)
                Debug.Log("[StewartPlatformTargetResolver] Platform disabled -> cleared target.");

            _lastTarget = null;
            _lastStation = -1;
            _lastPort = -1;
            return;
        }

        if (requireDockValid0 && !contacts.dockValid0)
        {
            if (clearTargetWhenInvalid)
                platform.target = null;

            if (_lastTarget != null && logChanges)
                Debug.Log("[StewartPlatformTargetResolver] No valid docking target -> cleared target.");

            _lastTarget = null;
            _lastStation = -1;
            _lastPort = -1;
            return;
        }

        int stIdx = contacts.selectedStationIndex;
        int portIdx = contacts.selectedStationDockPortIndex;

        if (stIdx < 0 || stIdx >= stationDockingAuthoring.Length)
        {
            if (clearTargetWhenInvalid)
                platform.target = null;

            if (_lastTarget != null && logChanges)
                Debug.Log("[StewartPlatformTargetResolver] Invalid station index -> cleared target.");

            _lastTarget = null;
            _lastStation = -1;
            _lastPort = -1;
            return;
        }

        StationDockingPortsAuthoring authoring = stationDockingAuthoring[stIdx];
        if (authoring == null || authoring.portTransforms == null)
        {
            if (clearTargetWhenInvalid)
                platform.target = null;

            if (_lastTarget != null && logChanges)
                Debug.Log("[StewartPlatformTargetResolver] Missing station authoring -> cleared target.");

            _lastTarget = null;
            _lastStation = -1;
            _lastPort = -1;
            return;
        }

        if (portIdx < 0 || portIdx >= authoring.portTransforms.Length)
        {
            if (clearTargetWhenInvalid)
                platform.target = null;

            if (_lastTarget != null && logChanges)
                Debug.Log("[StewartPlatformTargetResolver] Invalid port index -> cleared target.");

            _lastTarget = null;
            _lastStation = -1;
            _lastPort = -1;
            return;
        }

        Transform resolved = authoring.stewartTargetTransforms[portIdx];
        platform.target = resolved;

        if (resolved != _lastTarget || stIdx != _lastStation || portIdx != _lastPort)
        {
            if (logChanges)
                Debug.Log("[StewartPlatformTargetResolver] Target -> station " + stIdx + " port " + portIdx);

            _lastTarget = resolved;
            _lastStation = stIdx;
            _lastPort = portIdx;
        }
    }
}