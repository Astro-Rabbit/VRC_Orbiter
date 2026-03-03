using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CraftManager : UdonSharpBehaviour
{
    [Header("References")]
    public CraftState playerCraft;
    public CraftState[] allCrafts;

    [Header("Solver Settings")]
    [Range(1, 10)] public int iterations = 4;
    public float penetrationSlop = 0.01f;
    [Range(0f, 1f)] public float baumgarteBeta = 0.2f;
    public float maxNormalImpulse = 100000f;

    private Collider[] _overlaps = new Collider[16];

    private Collider[] _lookupColliders;
    private AeroContactProbe[] _lookupProbes;

    [Header("Docking Settings")]
    public float softCaptureDamping = 2.0f;
    public float hardCaptureAlignStrength = 5.0f;

    void Start()
    {
        // Count total probes across all crafts
        int totalProbes = 0;
        foreach (CraftState craft in allCrafts)
        {
            if (craft != null && craft.probes != null) totalProbes += craft.probes.Length;
        }

        _lookupColliders = new Collider[totalProbes];
        _lookupProbes = new AeroContactProbe[totalProbes];

        // Fill the lookup table
        int index = 0;
        foreach (CraftState craft in allCrafts)
        {
            if (craft == null || craft.probes == null) continue;
            foreach (AeroContactProbe probe in craft.probes)
            {
                if (probe == null) continue;
                _lookupColliders[index] = probe.GetCollider();
                _lookupProbes[index] = probe;
                index++;
            }
        }
    }

    private void FixedUpdate()
    {
        if (playerCraft == null) return;
        double dt = (double)Time.fixedDeltaTime;

        // 1. PHYSICAL INTEGRATION
        foreach (CraftState craft in allCrafts)
        {
            if (craft == null) continue;
            IntegrateCraft(craft, dt);
        }

        // 2. FLOATING ORIGIN VISUAL SHIFT
        // We MUST shift transforms BEFORE collision so Unity Physics knows where things are
        double ppx = playerCraft.px;
        double ppy = playerCraft.py;
        double ppz = playerCraft.pz;

        foreach (CraftState craft in allCrafts)
        {
            if (craft == null) continue;
            craft.transform.position = new Vector3((float)(craft.px - ppx), (float)(craft.py - ppy), (float)(craft.pz - ppz));
            craft.transform.rotation = new Quaternion((float)craft.qx, (float)craft.qy, (float)craft.qz, (float)craft.qw);
        }

        // 3. COLLISION RESOLUTION (The Impulse System)
        for (int i = 0; i < iterations; i++)
        {
            foreach (CraftState craft in allCrafts)
            {
                if (craft == null || craft.probes == null) continue;
                ResolveCraftCollisions(craft, (float)dt);
            }
        }
        // 4. DOCKING RESOLUTION (Final constraint pass)
        foreach (CraftState craft in allCrafts)
        {
            if (craft == null) continue;
            ResolveDockingConstraints(craft, (float)dt);
        }
    }

    private void IntegrateCraft(CraftState c, double dt)
    {
        // Linear
        c.vx += (c.forceX / c.mass) * dt;
        c.vy += (c.forceY / c.mass) * dt;
        c.vz += (c.forceZ / c.mass) * dt;
        c.px += c.vx * dt;
        c.py += c.vy * dt;
        c.pz += c.vz * dt;

        // Angular (Assuming wx/y/z are Body Space for easier Inertia application)
        c.wx += (c.torqueX / c.Ix) * dt;
        c.wy += (c.torqueY / c.Iy) * dt;
        c.wz += (c.torqueZ / c.Iz) * dt;

        // Quaternion Integration
        double qx = c.qx, qy = c.qy, qz = c.qz, qw = c.qw;
        // Convert body angular velocity to world-relative for dq/dt
        Vector3 wBody = new Vector3((float)c.wx, (float)c.wy, (float)c.wz);
        Vector3 wWorld = new Quaternion((float)qx, (float)qy, (float)qz, (float)qw) * wBody;

        c.qx += 0.5 * dt * (wWorld.x * qw + wWorld.y * qz - wWorld.z * qy);
        c.qy += 0.5 * dt * (-wWorld.x * qz + wWorld.y * qw + wWorld.z * qx);
        c.qz += 0.5 * dt * (wWorld.x * qy - wWorld.y * qx + wWorld.z * qw);
        c.qw += 0.5 * dt * (-wWorld.x * qx - wWorld.y * qy - wWorld.z * qz);

        double mag = System.Math.Sqrt(c.qx * c.qx + c.qy * c.qy + c.qz * c.qz + c.qw * c.qw);
        c.qx /= mag; c.qy /= mag; c.qz /= mag; c.qw /= mag;

        // Clear forces
        c.forceX = 0; c.forceY = 0; c.forceZ = 0;
        c.torqueX = 0; c.torqueY = 0; c.torqueZ = 0;
    }

    private void ResolveCraftCollisions(CraftState craft, float dt)
    {
        Vector3 craftPos = craft.transform.position;
        Quaternion craftRot = craft.transform.rotation;

        foreach (AeroContactProbe probe in craft.probes)
        {
            if (probe == null) continue;
            Collider myCol = probe.GetCollider();

            // Use the probe's logic to find the probe's visual pose
            Vector3 aPos; Quaternion aRot;
            probe.GetWorldColliderPose(craftPos, craftRot, out aPos, out aRot);

            // Broadphase
            int count = OverlapCandidates(probe, aPos, aRot);
            for (int j = 0; j < count; j++)
            {
                Collider other = _overlaps[j];
                if (other == myCol) continue;

                //AeroContactProbe otherProbe = other.GetComponent<AeroContactProbe>();
                //if (otherProbe != null && otherProbe.craftState == craft) continue;

                // Fast lookup instead of GetComponent
                int probeIdx = System.Array.IndexOf(_lookupColliders, other);
                AeroContactProbe otherProbe = (probeIdx != -1) ? _lookupProbes[probeIdx] : null;
                // Self-collision filter
                if (otherProbe != null && otherProbe.craftState == craft) continue;

                Vector3 n;
                float dist;

                bool hit = Physics.ComputePenetration(myCol, aPos, aRot, other, other.transform.position, other.transform.rotation, out n, out dist);

                if (hit && dist > penetrationSlop)
                {
                    // Narrow phase: find point
                    Vector3 cp = (Physics.ClosestPoint(other.bounds.center, myCol, aPos, aRot) + Physics.ClosestPoint(aPos, other, other.transform.position, other.transform.rotation)) * 0.5f;

                    //Debug.Log("[CraftManager] Craft: "+craft.gameObject.name);
                    //ApplyContactImpulse(craft, probe, cp, n, dist, dt);
                    ApplyContactImpulse(craft, probe, otherProbe, cp, n, dist, dt);

                }
            }
        }
    }

    //private void ApplyContactImpulse(CraftState c, AeroContactProbe probe, Vector3 cp, Vector3 n, float pen, float dt)
    //{
    //    Vector3 r = cp - c.transform.position;

    //    // Calculate point velocity: v + w x r
    //    Vector3 wWorld = c.transform.rotation * new Vector3((float)c.wx, (float)c.wy, (float)c.wz);
    //    Vector3 vPoint = new Vector3((float)c.vx, (float)c.vy, (float)c.vz) + Vector3.Cross(wWorld, r);

    //    float vn = Vector3.Dot(vPoint, n);
    //    float bias = (baumgarteBeta / dt) * (pen - penetrationSlop);
    //    float relVel = vn + bias;

    //    if (relVel >= 0) return;

    //    // Effective Mass
    //    float invM = (float)(1.0 / c.mass);
    //    Vector3 rCrossN = Vector3.Cross(r, n);
    //    Vector3 rCrossNBody = Quaternion.Inverse(c.transform.rotation) * rCrossN;

    //    // (I^-1 * rCnBody) dot rCnBody
    //    float angInertia = (rCrossNBody.x * rCrossNBody.x / (float)c.Ix) +
    //                       (rCrossNBody.y * rCrossNBody.y / (float)c.Iy) +
    //                       (rCrossNBody.z * rCrossNBody.z / (float)c.Iz);

    //    float effMassN = 1.0f / (invM + angInertia);
    //    float jn = -(1.0f + probe.restitution) * relVel * effMassN;
    //    jn = Mathf.Clamp(jn, 0, maxNormalImpulse);

    //    // Apply Linear
    //    Vector3 impulse = n * jn;
    //    c.vx += impulse.x * invM;
    //    c.vy += impulse.y * invM;
    //    c.vz += impulse.z * invM;

    //    // Apply Angular (To Body Space)
    //    Vector3 torqueWorld = Vector3.Cross(r, impulse);
    //    Vector3 torqueBody = Quaternion.Inverse(c.transform.rotation) * torqueWorld;
    //    c.wx += torqueBody.x / c.Ix;
    //    c.wy += torqueBody.y / c.Iy;
    //    c.wz += torqueBody.z / c.Iz;

    //    // Positional Nudge
    //    double push = 0.2 * (double)(pen - penetrationSlop);
    //    c.px += (double)n.x * push;
    //    c.py += (double)n.y * push;
    //    c.pz += (double)n.z * push;
    //}

    private void ApplyContactImpulse(CraftState craftA, AeroContactProbe probe, AeroContactProbe otherProbe, Vector3 cp, Vector3 n, float pen, float dt)
    {
        // If otherProbe is null, craftB remains null (environment collision)
        CraftState craftB = (otherProbe != null) ? otherProbe.craftState : null;

        //Debug.Log("[CraftManager] ApplyingImpulse");
        // 1. Identify what we hit
        //CraftState craftB = null;

        // Check if the collider we hit has a proxy script
        AeroContactProbe proxy = _overlaps[0].GetComponent<AeroContactProbe>();
        if (proxy != null) craftB = proxy.craftState;
        //Debug.Log("[CraftManager] CraftB: " + craftB.gameObject.name);

        // 2. Calculate Lever Arms
        Vector3 rA = cp - craftA.transform.position;
        Vector3 rB = (craftB != null) ? (cp - craftB.transform.position) : Vector3.zero;

        // 3. World Velocities at contact point
        Vector3 wWorldA = craftA.transform.rotation * new Vector3((float)craftA.wx, (float)craftA.wy, (float)craftA.wz);
        Vector3 vPointA = new Vector3((float)craftA.vx, (float)craftA.vy, (float)craftA.vz) + Vector3.Cross(wWorldA, rA);

        Vector3 vPointB = Vector3.zero;
        if (craftB != null)
        {
            Vector3 wWorldB = craftB.transform.rotation * new Vector3((float)craftB.wx, (float)craftB.wy, (float)craftB.wz);
            vPointB = new Vector3((float)craftB.vx, (float)craftB.vy, (float)craftB.vz) + Vector3.Cross(wWorldB, rB);
        }

        // 4. Relative Velocity along normal
        Vector3 relativeVel = vPointA - vPointB;

        // --- NEW DRAG LOGIC ---
        if (probe.useRelativeDrag && craftB != null)
        {
            float invMassA2 = (float)(1.0 / craftA.mass);
            float invMassB2 = (float)(1.0 / craftB.mass);

            // We calculate the effective mass for a 3D impulse (simplified)
            // To be perfectly accurate we'd use an impulse matrix, but for VRChat 
            // a scalar approximation using the normal direction or an average is usually sufficient.
            float inertiaA2 = ComputeAngularInertia(craftA, rA, relativeVel.normalized);
            float inertiaB2 = ComputeAngularInertia(craftB, rB, relativeVel.normalized);
            float totalInvMass2 = invMassA2 + invMassB2 + inertiaA2 + inertiaB2;

            if (totalInvMass2 > 0)
            {
                // Calculate impulse needed to bring relative velocity to zero
                // j = deltaV / invMass
                Vector3 dragImpulse = (-relativeVel * probe.dragStrength) / totalInvMass2;

                ApplyImpulse(craftA, rA, dragImpulse, invMassA2);
                ApplyImpulse(craftB, rB, -dragImpulse, invMassB2);
            }
            //Debug.Log("[CraftManager] Dragging");
            return;
        }
        // --- END DRAG LOGIC ---


        float vn = Vector3.Dot(relativeVel, n);

        // Penetration recovery (Baumgarte)
        float bias = (baumgarteBeta / dt) * (pen - penetrationSlop);
        float jVelocity = -(1.0f + probe.restitution) * (vn + bias);

        if (jVelocity <= 0) return; // Moving apart

        // 5. Effective Mass (Two-Body math)
        float invMassA = (float)(1.0 / craftA.mass);
        float invMassB = (craftB != null) ? (float)(1.0 / craftB.mass) : 0;

        float inertiaA = ComputeAngularInertia(craftA, rA, n);
        float inertiaB = (craftB != null) ? ComputeAngularInertia(craftB, rB, n) : 0;

        float totalInvMass = invMassA + invMassB + inertiaA + inertiaB;
        if (totalInvMass <= 0) return;

        float j = jVelocity / totalInvMass;
        j = Mathf.Clamp(j, 0, maxNormalImpulse);
        Vector3 impulseVec = n * j;

        // 6. Apply to Craft A (The one with the probe)
        ApplyImpulse(craftA, rA, impulseVec, invMassA);

        // 7. Apply to Craft B (The one we hit - Equal and Opposite)
        if (craftB != null)
        {
            ApplyImpulse(craftB, rB, -impulseVec, invMassB);
        }

        // 8. Positional Nudge (split between both based on mass)
        double totalMass = craftA.mass + (craftB != null ? craftB.mass : 10000000);
        double ratioA = (craftB != null) ? (craftB.mass / totalMass) : 1.0;

        double push = (double)(pen - penetrationSlop) * 0.2;

        craftA.px += (double)n.x * push * ratioA;
        craftA.py += (double)n.y * push * ratioA;
        craftA.pz += (double)n.z * push * ratioA;

        if (craftB != null)
        {
            double ratioB = 1.0 - ratioA;
            craftB.px -= (double)n.x * push * ratioB;
            craftB.py -= (double)n.y * push * ratioB;
            craftB.pz -= (double)n.z * push * ratioB;
        }
    }

    private float ComputeAngularInertia(CraftState c, Vector3 r, Vector3 n)
    {
        Vector3 rCrossN = Vector3.Cross(r, n);
        Vector3 rCrossNBody = Quaternion.Inverse(c.transform.rotation) * rCrossN;
        return (rCrossNBody.x * rCrossNBody.x / (float)c.Ix) +
               (rCrossNBody.y * rCrossNBody.y / (float)c.Iy) +
               (rCrossNBody.z * rCrossNBody.z / (float)c.Iz);
    }

    private void ApplyImpulse(CraftState c, Vector3 r, Vector3 impulse, float invMass)
    {
        // Linear
        c.vx += impulse.x * invMass;
        c.vy += impulse.y * invMass;
        c.vz += impulse.z * invMass;

        // Angular
        Vector3 torqueWorld = Vector3.Cross(r, impulse);
        Vector3 torqueBody = Quaternion.Inverse(c.transform.rotation) * torqueWorld;
        c.wx += torqueBody.x / c.Ix;
        c.wy += torqueBody.y / c.Iy;
        c.wz += torqueBody.z / c.Iz;
    }

    private int OverlapCandidates(AeroContactProbe probe, Vector3 aPos, Quaternion aRot)
    {
        if (probe.shape == AeroProbeShape.Sphere)
            return Physics.OverlapSphereNonAlloc(aPos + aRot * probe.sphere.center, probe.sphere.radius * probe.GetMaxAbsScale(), _overlaps, probe.collideMask, QueryTriggerInteraction.Ignore);

        Vector3 he = Vector3.Scale(probe.box.size, probe.colliderLossyScale) * 0.5f;
        return Physics.OverlapBoxNonAlloc(aPos + aRot * probe.box.center, he, _overlaps, aRot, probe.collideMask, QueryTriggerInteraction.Ignore);
    }


    private void ResolveDockingConstraints(CraftState craftA, float dt)
    {
        // Try to find a DockingController on this craft
        DockingController ctrl = craftA.GetComponent<DockingController>();
        //Debug.Log("[CraftManager] ctrl?: " + (ctrl == null).ToString() + " ctrl.state.ready: " + (ctrl.state == DockingState.Ready).ToString() + " ctrl.activetargetPort: "+ (ctrl.activeTargetPort == null).ToString());
        //if (ctrl == null || ctrl.state == DockingState.Ready || ctrl.activeTargetPort == null) return;
        if(ctrl == null)
        {
            return;
        }
        //Debug.Log("[CraftManager] ctrl: " + ctrl.ToString());
        if(ctrl.state == DockingState.Ready)
        {
            return;
        }
       // Debug.Log("[CraftManager] ctrl.state.ready: " + (ctrl.state == DockingState.Ready).ToString());
        if (ctrl.activeTargetPort == null)
        {
            return;
        }
        //Debug.Log("[CraftManager] ctrl.activeTargetPort == null: " + (ctrl.activeTargetPort == null).ToString());
       // Debug.Log("[CraftManager] Resolving Docking Constraints");

        CraftState craftB = ctrl.activeTargetPort.craftState;
        if (craftB == null) return;
        //Debug.Log("[CraftManager] CraftB Found");
        if (ctrl.state == DockingState.SoftCapture)
        {
            // --- SOFT CAPTURE DAMPING ---
            // Calculate relative linear and angular velocities
            Vector3 relV = new Vector3((float)(craftA.vx - craftB.vx), (float)(craftA.vy - craftB.vy), (float)(craftA.vz - craftB.vz));
            Vector3 relW = new Vector3((float)(craftA.wx - craftB.wx), (float)(craftA.wy - craftB.wy), (float)(craftA.wz - craftB.wz));

            // Apply damping as a change in velocity (Impulse-based damping)
            float dampingFactor = Mathf.Clamp01(softCaptureDamping * dt);
            craftA.vx -= relV.x * dampingFactor;
            craftA.vy -= relV.y * dampingFactor;
            craftA.vz -= relV.z * dampingFactor;

            craftA.wx -= relW.x * dampingFactor;
            craftA.wy -= relW.y * dampingFactor;
            craftA.wz -= relW.z * dampingFactor;
        }
        else if (ctrl.state == DockingState.HardCapture)
        {
            // --- HARD CAPTURE ALIGNMENT ---
            Transform targetMarker = ctrl.activeTargetPort.dockTarget;
            float step = hardCaptureAlignStrength * dt;

            // 1. Position Nudge
            // Get the world-space offset from CraftB's center to the docking marker
            Vector3 worldOffset = targetMarker.position - craftB.transform.position;

            // Apply nudge to double-precision coordinates
            //craftA.px = Mathf.Lerp((float)craftA.px, (float)(craftB.px + worldOffset.x), step);
            //craftA.py = Mathf.Lerp((float)craftA.py, (float)(craftB.py + worldOffset.y), step);
            //craftA.pz = Mathf.Lerp((float)craftA.pz, (float)(craftB.pz + worldOffset.z), step);
            // 1. Position Nudge (Double Precision)
            //Vector3 worldOffset = targetMarker.position - craftB.transform.position;
            double targetPX = craftB.px + (double)worldOffset.x;
            double targetPY = craftB.py + (double)worldOffset.y;
            double targetPZ = craftB.pz + (double)worldOffset.z;

            craftA.px += (targetPX - craftA.px) * (double)step;
            craftA.py += (targetPY - craftA.py) * (double)step;
            craftA.pz += (targetPZ - craftA.pz) * (double)step;

            //float dis = Mathf.Sqrt((Mathf.Pow((float)(targetPX - craftA.px),2.0f) + Mathf.Pow((float)(targetPY - craftA.py),2.0f) + Mathf.Pow((float)(targetPZ - craftA.pz),2.0f)));

            //Debug.Log("[CraftManager] HardDockPoint: " + dis.ToString("F3"));

            // 2. Rotation Nudge
            // targetMarker.rotation is the absolute visual orientation we want to match
            Quaternion currentQ = new Quaternion((float)craftA.qx, (float)craftA.qy, (float)craftA.qz, (float)craftA.qw);
            Quaternion nextQ = Quaternion.Slerp(currentQ, targetMarker.rotation, step);

            craftA.qx = nextQ.x;
            craftA.qy = nextQ.y;
            craftA.qz = nextQ.z;
            craftA.qw = nextQ.w;

            // 3. Velocity Match
            // To prevent physics "fighting" the snap, we calculate CraftA's point velocity relative to B
            // But for a hard mate, matching center velocities is usually sufficient:
            craftA.vx = craftB.vx;
            craftA.vy = craftB.vy;
            craftA.vz = craftB.vz;

            craftA.wx = craftB.wx;
            craftA.wy = craftB.wy;
            craftA.wz = craftB.wz;
        }
    }
}