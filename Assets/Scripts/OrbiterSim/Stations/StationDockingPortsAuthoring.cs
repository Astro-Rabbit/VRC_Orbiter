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

    public void CacheNow()
    {
        if (station == null || stationBody == null || portTransforms == null)
        {
            if (log) Debug.Log("[StationDockingPortsAuthoring] Missing refs.");
            return;
        }

        int n = portTransforms.Length;
        station.EnsureDockPortSize(n);

        for (int i = 0; i < n; i++)
        {
            Transform p = portTransforms[i];
            if (p == null)
            {
                station.dock_px_B[i] = station.dock_py_B[i] = station.dock_pz_B[i] = 0.0;
                station.dock_q_B[i] = Quaternion.identity;
                continue;
            }

            // Position in station body frame
            Vector3 pB_render = stationBody.InverseTransformPoint(p.position);
            Quaternion qB_render = Quaternion.Inverse(stationBody.rotation) * p.rotation;

            Vector3 pB = RenderBodyToSimBody(pB_render);
            Quaternion qB = qB_render;

            station.dock_px_B[i] = (double)pB.x;
            station.dock_py_B[i] = (double)pB.y;
            station.dock_pz_B[i] = (double)pB.z;

            station.dock_q_B[i] = qB;
        }

        station.dockingPortCount = n;

        if (log) Debug.Log("[StationDockingPortsAuthoring] Cached " + n + " ports.");
    }

    private Vector3 RenderBodyToSimBody(Vector3 v)
    {
        return new Vector3(-v.x, v.y, v.z);
    }

    private Quaternion RenderBodyRotToSimBodyRot(Quaternion qRender)
    {
        Vector3 x = qRender * Vector3.right;
        Vector3 y = qRender * Vector3.up;
        Vector3 z = qRender * Vector3.forward;

        x = RenderBodyToSimBody(x);
        y = RenderBodyToSimBody(y);
        z = RenderBodyToSimBody(z);

        x.Normalize();
        z = Vector3.Cross(x, y).normalized;
        y = Vector3.Cross(z, x).normalized;

        return Quaternion.LookRotation(z, y);
    }


}