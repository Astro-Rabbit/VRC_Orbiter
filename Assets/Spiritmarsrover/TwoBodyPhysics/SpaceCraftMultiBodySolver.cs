using UdonSharp;
using UnityEngine;

public class SpaceCraftMultiBodySolver : UdonSharpBehaviour
{
    public SpaceCraftState[] allShips; // Registry of all ships in the scene
    public float activationRadius = 100f;
    [Range(1, 12)] public int iterations = 6;
    public float penetrationSlop = 0.01f;
    public float baumgarteBeta = 0.2f;

    private float _sqrActivationDist;
    private Collider[] _overlaps = new Collider[16];

    void Start() { _sqrActivationDist = activationRadius * activationRadius; }

    public void StepPhysics(float dt, SpaceCraftState player)
    {
        for (int it = 0; it < iterations; it++)
        {
            for (int i = 0; i < allShips.Length; i++)
            {
                SpaceCraftState shipA = allShips[i];
                if (shipA == null || !shipA.gameObject.activeInHierarchy) continue;

                // FIX: Check distance relative to player universe position
                double dx = shipA.px - player.px;
                double dy = shipA.py - player.py;
                double dz = shipA.pz - player.pz;
                if ((dx * dx + dy * dy + dz * dz) > (double)_sqrActivationDist) continue;

                // Loop through probes (Use for-loop for Udon performance)
                for (int p = 0; p < shipA.probes.Length; p++)
                {
                    ResolveProbe(shipA.probes[p], dt);
                }
            }
        }
    }

    private void ResolveProbe(SpaceCraftContactProbe probe, float dt)
    {
        Collider colA = probe.GetCollider();
        // Since Player is at 0,0,0, Unity World Position = Simulation Position
        Vector3 posA = colA.transform.position;
        Quaternion rotA = colA.transform.rotation;

        float checkRadius = 1.0f;
        if (probe.shape == SpaceCraftProbeShape.Sphere && probe.sphere != null)
            checkRadius = probe.sphere.radius * Mathf.Max(probe.transform.lossyScale.x, probe.transform.lossyScale.y);
        else if (probe.box != null)
            checkRadius = probe.box.size.magnitude;

        int count = Physics.OverlapSphereNonAlloc(posA, checkRadius, _overlaps, probe.collideMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider colB = _overlaps[i];
            if (colB == colA || colB == null) continue;

            // 1. Geometry Query
            Vector3 n; float pen;
            if (!Physics.ComputePenetration(colA, posA, rotA, colB, colB.transform.position, colB.transform.rotation, out n, out pen)) continue;
            if (pen < penetrationSlop) continue;

            // 2. Identify Body B
            // Check if colB belongs to another AeroCraftState
            SpaceCraftContactProbe probeB = colB.GetComponent<SpaceCraftContactProbe>();
            SpaceCraftState stateB = (probeB != null) ? probeB.myState : null;
            SpaceCraftState stateA = probe.myState;

            // 3. Setup Physics Data
            Vector3 cp = colB.ClosestPoint(posA);
            Vector3 rA = cp - GetCoGWorld(stateA);
            Vector3 rB = (stateB != null) ? (cp - GetCoGWorld(stateB)) : Vector3.zero;

            Vector3 vA = GetPointVel(stateA, rA);
            Vector3 vB = (stateB != null) ? GetPointVel(stateB, rB) : Vector3.zero;
            Vector3 vRel = vA - vB;
            float vn = Vector3.Dot(vRel, n);

            // 4. Calculate Combined Effective Mass (K-Matrix)
            float kA = ComputeK(stateA, n, rA);
            float kB = (stateB != null) ? ComputeK(stateB, n, rB) : 0f; // Station = 0 mass resistance
            float effMass = 1.0f / Mathf.Max(1e-6f, kA + kB);

            // 5. Normal Impulse (with Bounce)
            float bias = (baumgarteBeta / dt) * (pen - penetrationSlop);
            float vBounce = (vn < -0.2f) ? -vn * probe.restitution : 0f;

            float jn = ((vBounce - vn) + bias) * effMass;
            if (jn < 0) jn = 0;

            Vector3 J = n * jn;
            ApplyImpulse(stateA, rA, J);
            if (stateB != null) ApplyImpulse(stateB, rB, -J); // Newton's 3rd Law

            // 6. Friction
            vRel = GetPointVel(stateA, rA) - ((stateB != null) ? GetPointVel(stateB, rB) : Vector3.zero);
            Vector3 vt = vRel - n * Vector3.Dot(vRel, n);
            if (vt.sqrMagnitude > 1e-4f)
            {
                Vector3 tdir = vt.normalized;
                float kTan = ComputeK(stateA, tdir, rA) + ((stateB != null) ? ComputeK(stateB, tdir, rB) : 0f);
                float jt = -vt.magnitude / Mathf.Max(1e-6f, kTan);

                float maxFric = probe.mu * jn;
                jt = Mathf.Clamp(jt, -maxFric, maxFric);

                Vector3 Jt = tdir * jt;
                ApplyImpulse(stateA, rA, Jt);
                if (stateB != null) ApplyImpulse(stateB, rB, -Jt);
            }
        }
    }

    // --- Helper Math ---

    private Vector3 GetCoGWorld(SpaceCraftState s) => new Vector3((float)s.px, (float)s.py, (float)s.pz);

    private Vector3 GetPointVel(SpaceCraftState s, Vector3 r)
    {
        Vector3 v = new Vector3((float)s.vx, (float)s.vy, (float)s.vz);
        Vector3 w = new Vector3((float)s.wx, (float)s.wy, (float)s.wz); // Assumes world-space omega
        return v + Vector3.Cross(w, r);
    }

    private float ComputeK(SpaceCraftState s, Vector3 n, Vector3 r)
    {
        float invM = 1.0f / (float)s.massKg;
        Vector3 rn = Vector3.Cross(r, n);
        // Simplified Body-Space Inertia application
        Vector3 invI = new Vector3(1f / (float)s.Ix, 1f / (float)s.Iy, 1f / (float)s.Iz);
        Vector3 localRn = Quaternion.Inverse(s.transform.rotation) * rn;
        Vector3 localAng = Vector3.Scale(localRn, invI);
        Vector3 worldAng = s.transform.rotation * localAng;
        return invM + Vector3.Dot(n, Vector3.Cross(worldAng, r));
    }

    private void ApplyImpulse(SpaceCraftState s, Vector3 r, Vector3 J)
    {
        // 1. Static/Station check: Stations have infinite mass (invM = 0)
        if (s.isStation) return;

        float invM = 1.0f / (float)s.massKg;
        s.vx += (double)(J.x * invM);
        s.vy += (double)(J.y * invM);
        s.vz += (double)(J.z * invM);

        // 2. Angular Impulse Math
        Vector3 torque = Vector3.Cross(r, J);
        Vector3 invI = new Vector3(1f / (float)s.Ix, 1f / (float)s.Iy, 1f / (float)s.Iz);

        // Rotate world-space torque into ship-local space to use the Inertia Tensor
        Quaternion worldToLocal = Quaternion.Inverse(s.transform.rotation);
        Vector3 localTorque = worldToLocal * torque;

        // Apply local inertia
        Vector3 localDW = Vector3.Scale(localTorque, invI);

        // Rotate the resulting angular velocity change BACK to world space
        Vector3 worldDW = s.transform.rotation * localDW;

        s.wx += (double)worldDW.x;
        s.wy += (double)worldDW.y;
        s.wz += (double)worldDW.z;
    }
}