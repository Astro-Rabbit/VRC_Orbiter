using UdonSharp;
using UnityEngine;

/// <summary>
/// CraftDockingPorts
/// Docking port cache for the craft, relative to the craft CG in craft BODY frame.
///
/// Conventions:
/// - Unity WORLD axes == craft BODY axes in your render model.
/// - craftCG defines the craft origin for docking targeting (center of gravity transform).
/// - Each port Transform's local axes define the port frame:
///     +Z (Transform.forward) points outward along the docking axis (approach direction)
///     +Y is "up" reference for roll alignment
///
/// Cached outputs (craft BODY frame):
/// - dock_p*_B: position relative to craftCG, expressed in craft body axes (meters)
/// - dock_q_B:  orientation of the port frame expressed in craft body axes (Quaternion)
///
/// This script is for TARGETING only (no latch/capture mechanics).
/// </summary>
public class CraftDockingPorts : UdonSharpBehaviour
{
    [Header("Refs")]
    [Tooltip("Craft CG transform. Defines origin + axes for docking targeting.")]
    public Transform craftCG;

    [Tooltip("Authored port frames (Transforms). Orient +Z outward.")]
    public Transform[] portTransforms;

    [Header("Behavior")]
    public bool cacheOnStart = true;
    public bool log = false;

    [Header("Cached ports (craft BODY frame, relative to CG)")]
    public int dockingPortCount = 0;

    public double[] dock_px_B;
    public double[] dock_py_B;
    public double[] dock_pz_B;

    public Quaternion[] dock_q_B;

    private void Start()
    {
        if (cacheOnStart)
            CacheNow();
    }

    public void EnsureSize(int n)
    {
        if (n < 0) n = 0;
        dockingPortCount = n;

        if (dock_px_B == null || dock_px_B.Length != n) dock_px_B = new double[n];
        if (dock_py_B == null || dock_py_B.Length != n) dock_py_B = new double[n];
        if (dock_pz_B == null || dock_pz_B.Length != n) dock_pz_B = new double[n];
        if (dock_q_B  == null || dock_q_B.Length  != n) dock_q_B  = new Quaternion[n];
    }

    public void CacheNow()
    {
        if (craftCG == null || portTransforms == null)
        {
            if (log) Debug.Log("[CraftDockingPorts] Missing craftCG or portTransforms.");
            return;
        }

        if (log)
        {
            Debug.Log("[CraftDockingPorts] craftCG rot = " + craftCG.rotation);
        }

        int n = portTransforms.Length;
        EnsureSize(n);

        // world -> craft body rotation
        Quaternion qCB = Quaternion.Inverse(craftCG.rotation);

        for (int i = 0; i < n; i++)
        {
            Transform p = portTransforms[i];
            if (p == null)
            {
                dock_px_B[i] = dock_py_B[i] = dock_pz_B[i] = 0.0;
                dock_q_B[i] = Quaternion.identity;
                continue;
            }

            Vector3 pB_render = craftCG.InverseTransformPoint(p.position);
            Quaternion qB_render = qCB * p.rotation;

            Vector3 pB = RenderBodyToSimBody(pB_render);
            Quaternion qB = qB_render;

            dock_px_B[i] = (double)pB.x;
            dock_py_B[i] = (double)pB.y;
            dock_pz_B[i] = (double)pB.z;
            dock_q_B[i] = qB;


            if (log)
            {
                Vector3 fwdB = qB * Vector3.forward;
                Vector3 upB  = qB * Vector3.up;

                Debug.Log(
                    "[CraftDockingPorts] port " + i +
                    " worldRot=" + p.rotation +
                    " localPosB=" + pB +
                    " fwdB=" + fwdB +
                    " upB=" + upB
                );
            }

        }


        if (log) Debug.Log($"[CraftDockingPorts] Cached {n} ports.");
    }

    // Convenience getters (optional; avoids repeating casts elsewhere)
    public Vector3 GetPortPosB(int index)
    {
        if (index < 0 || index >= dockingPortCount) return Vector3.zero;
        return new Vector3((float)dock_px_B[index], (float)dock_py_B[index], (float)dock_pz_B[index]);
    }

    public Quaternion GetPortRotB(int index)
    {
        if (index < 0 || index >= dockingPortCount) return Quaternion.identity;
        return dock_q_B[index];
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