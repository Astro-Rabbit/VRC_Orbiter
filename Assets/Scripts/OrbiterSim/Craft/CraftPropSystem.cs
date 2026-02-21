using UdonSharp;
using UnityEngine;

public class CraftPropSystem : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public ConicState conic;
    public ConicPropagator conicProp;
    public CraftStateModel craft;

    [Header("Debug")]
    public bool logMissing = false;

    public void Evaluate(double t)
    {
        if (bodies == null || conic == null || conicProp == null || craft == null)
        {
            if (logMissing) Debug.Log("[CraftPropSystem] Missing references.");
            return;
        }

        if (!conic.valid)
        {
            if (logMissing) Debug.Log("[CraftPropSystem] ConicState not valid.");
            return;
        }

        byte pid = conic.primaryBodyId;

        // 1) Relative orbit in solver inertial frame (primary-centered)
        conicProp.Evaluate(t);

        // 2) Primary heliocentric state
        double px, py, pz, pvx, pvy, pvz;
        bodies.GetBodyState(pid, out px, out py, out pz, out pvx, out pvy, out pvz);
        // 3) Compose heliocentric craft state
        craft.rx = px + conicProp.rel_rx;
        craft.ry = py + conicProp.rel_ry;
        craft.rz = pz + conicProp.rel_rz;

        craft.vx = pvx + conicProp.rel_vx;
        craft.vy = pvy + conicProp.rel_vy;
        craft.vz = pvz + conicProp.rel_vz;

        craft.primaryBodyId = pid;
    }
}
