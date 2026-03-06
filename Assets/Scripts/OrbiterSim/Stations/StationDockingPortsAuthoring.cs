using UdonSharp;
using UnityEngine;

/// <summary>
/// StationDockingPortsAuthoring
/// Author docking ports as child Transforms on the station prefab, then cache them into StationStateModel.
/// Cached values are in STATION BODY frame:
/// - position: stationBody.InverseTransformPoint(port.position)
/// - rotation: inverse(stationBody.rotation) * port.rotation
///
/// This is used for docking TARGETING only (no latch/capture logic).
/// </summary>
public class StationDockingPortsAuthoring : UdonSharpBehaviour
{
    [Header("Refs")]
    public StationStateModel station;
    public Transform stationBody;            // station origin + body axes
    public Transform[] portTransforms;       // authored port frames
    public Transform[] stewartTargetTransforms;
    [Header("Behavior")]
    public bool cacheOnStart = true;
    public bool log = false;

    private void Start()
    {
        if (cacheOnStart)
            CacheNow();
    }
    void Awake()
    {
        CacheNow();
    }
    public void CacheNow()
    {
        if (station == null || stationBody == null || portTransforms == null)
        {
            if (log) Debug.Log("[StationDockingPortsAuthoring] Missing refs.");
            return;
        }

        int n = portTransforms.Length;
        station.EnsureDockPortSize(n);

        Quaternion qSB = Quaternion.Inverse(stationBody.rotation); // world -> station body rotation

        for (int i = 0; i < n; i++)
        {
            Transform p = portTransforms[i];
            if (p == null)
            {
                station.dock_px_B[i] = station.dock_py_B[i] = station.dock_pz_B[i] = 0.0;
                station.dock_q_B[i] = Quaternion.identity;
                continue;
            }

            // position in station body axes (meters)
            Vector3 pB = stationBody.InverseTransformPoint(p.position);
            station.dock_px_B[i] = (double)pB.x;
            station.dock_py_B[i] = (double)pB.y;
            station.dock_pz_B[i] = (double)pB.z;

            // orientation in station body axes
            station.dock_q_B[i] = qSB * p.rotation;
        }

        if (log) Debug.Log($"[StationDockingPortsAuthoring] Cached {n} ports.");
    }
}