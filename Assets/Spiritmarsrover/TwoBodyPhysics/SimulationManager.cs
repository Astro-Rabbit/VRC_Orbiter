using UdonSharp;
using UnityEngine;

public class SimulationManager : UdonSharpBehaviour
{
    [Header("Core Systems")]
    public SpaceCraftMultiBodySolver solver;
    public SpaceCraftState playerShip;
    public SpaceCraftState[] allShips;

    [Header("Simulation Settings")]
    public bool simPaused = false;

    private void FixedUpdate()
    {
        if (simPaused || playerShip == null || solver == null) return;
        float dt = Time.fixedDeltaTime;

        // PHASE 1: Move everyone in the "Universe"
        IntegrateState(playerShip, dt);
        for (int i = 0; i < allShips.Length; i++)
        {
            if (allShips[i] != null && allShips[i] != playerShip && !allShips[i].isStation)
                IntegrateState(allShips[i], dt);
        }

        // PHASE 2: Move Unity transforms so Colliders are in the right spot for Unity Physics
        SyncUnityTransforms();

        // PHASE 3: Run the Solver
        // Now Physics.OverlapSphere will actually find the ships where they are supposed to be!
        solver.StepPhysics(dt, playerShip);
    }

    //private void UpdateUniverse(float dt)
    //{
    //    // First, integrate the Player Ship so we know the new "Center"
    //    IntegrateState(playerShip, dt);

    //    // Store player's universe position as the reference
    //    double refX = playerShip.px;
    //    double refY = playerShip.py;
    //    double refZ = playerShip.pz;

    //    // Sync Player Visuals: Always at origin, but rotation updates
    //    playerShip.transform.position = Vector3.zero;
    //    playerShip.transform.rotation = new Quaternion(
    //        (float)playerShip.qx, (float)playerShip.qy, (float)playerShip.qz, (float)playerShip.qw
    //    );

    //    // Integrate and Sync all other ships
    //    foreach (SpaceCraftState ship in allShips)
    //    {
    //        if (ship == null || ship == playerShip) continue;

    //        // Update its position in the universe
    //        if (!ship.isStation)
    //        {
    //            IntegrateState(ship, dt);
    //        }

    //        // Calculate relative position for Unity rendering
    //        // (Target Universe Pos - Player Universe Pos) = Offset from Origin
    //        float offsetX = (float)(ship.px - refX);
    //        float offsetY = (float)(ship.py - refY);
    //        float offsetZ = (float)(ship.pz - refZ);

    //        ship.transform.position = new Vector3(offsetX, offsetY, offsetZ);
    //        ship.transform.rotation = new Quaternion(
    //            (float)ship.qx, (float)ship.qy, (float)ship.qz, (float)ship.qw
    //        );
    //    }
    //}

    private void IntegrateState(SpaceCraftState s, float dt)
    {
        // --- Linear Integration ---
        s.px += s.vx * (double)dt;
        s.py += s.vy * (double)dt;
        s.pz += s.vz * (double)dt;

        // --- Angular Integration ---
        // Convert angular velocity vector to a rotation step
        Vector3 w = new Vector3((float)s.wx, (float)s.wy, (float)s.wz);
        float wMag = w.magnitude;
        if (wMag > 1e-6f)
        {
            Quaternion currentQ = new Quaternion((float)s.qx, (float)s.qy, (float)s.qz, (float)s.qw);

            // Create a delta rotation: Axis = w normalized, Angle = wMag * dt
            Quaternion deltaQ = Quaternion.AngleAxis(wMag * dt * Mathf.Rad2Deg, w.normalized);

            // Apply delta (Order: Delta * Current for World-Space Omega)
            Quaternion nextQ = deltaQ * currentQ;

            s.qx = nextQ.x;
            s.qy = nextQ.y;
            s.qz = nextQ.z;
            s.qw = nextQ.w;
        }
    }
    private void SyncUnityTransforms()
    {
        double refX = playerShip.px;
        double refY = playerShip.py;
        double refZ = playerShip.pz;

        for (int i = 0; i < allShips.Length; i++)
        {
            SpaceCraftState ship = allShips[i];
            if (ship == null) continue;

            float offsetX = (float)(ship.px - refX);
            float offsetY = (float)(ship.py - refY);
            float offsetZ = (float)(ship.pz - refZ);

            ship.transform.position = new Vector3(offsetX, offsetY, offsetZ);
            ship.transform.rotation = new Quaternion((float)ship.qx, (float)ship.qy, (float)ship.qz, (float)ship.qw);
        }
    }
}