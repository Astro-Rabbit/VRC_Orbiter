using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingControls : UdonSharpBehaviour
{
    [Header("References")]
    public VPControls controls;
    public CraftState craft;

    [Header("Movement Settings")]
    public float translationThrust = 5000f; // Force for strafing (X, Y, Z)
    public float mainEngineThrust = 10000f; // Extra force from the Throttle
    public float rotationTorque = 3000f;    // Torque for Pitch, Yaw, Roll
    public PilotSeat pilot;
    private void FixedUpdate()
    {
        if (!pilot._isLocalInStation) return;

        HandleKeyboardInput();
        if (controls == null || craft == null) return;

        // 1. ROTATION (Joystick)
        // inputZ = Pitch (X-axis), inputY = Yaw (Y-axis), inputX = Roll (Z-axis)
        Vector3 torque = new Vector3(
            controls.inputZ * rotationTorque,
            controls.inputY * rotationTorque,
            -controls.inputX * rotationTorque
        );
        craft.AddLocalTorque(torque);

        // 2. TRANSLATION (Translation Handle + Throttle)
        // transX = Strafe Left/Right
        // transY = Strafe Up/Down
        // transZ + ThrottleValue = Forward/Back
        Vector3 force = new Vector3(
            -controls.transX * translationThrust,
            controls.transY * translationThrust,
            (controls.transZ * translationThrust) + (controls.ThrottleValue * mainEngineThrust)
        );
        craft.AddRelativeForce(force);
    }

    [Header("Keyboard Settings")]
    public float kbThrust = 5000f;
    public float kbTorque = 3000f;

    private void HandleKeyboardInput()
    {
        Vector3 torque = Vector3.zero;
        Vector3 force = Vector3.zero;

        // --- ROTATION (WASD + QE) ---
        if (Input.GetKey(KeyCode.W)) torque.x += 1; // Pitch Down
        if (Input.GetKey(KeyCode.S)) torque.x -= 1; // Pitch Up
        if (Input.GetKey(KeyCode.A)) torque.z += 1; // Roll Left
        if (Input.GetKey(KeyCode.D)) torque.z -= 1; // Roll Right
        if (Input.GetKey(KeyCode.Q)) torque.y -= 1; // Yaw Left
        if (Input.GetKey(KeyCode.E)) torque.y += 1; // Yaw Right

        // --- TRANSLATION (IJKL + HN) ---
        if (Input.GetKey(KeyCode.I)) force.y += 1; // Strafe Up
        if (Input.GetKey(KeyCode.K)) force.y -= 1; // Strafe Down
        if (Input.GetKey(KeyCode.J)) force.x -= 1; // Strafe Left
        if (Input.GetKey(KeyCode.L)) force.x += 1; // Strafe Right
        if (Input.GetKey(KeyCode.H)) force.z += 1; // Forward (Thrust)
        if (Input.GetKey(KeyCode.N)) force.z -= 1; // Backward (Retro)

        if (torque != Vector3.zero) craft.AddLocalTorque(torque * kbTorque);
        if (force != Vector3.zero) craft.AddRelativeForce(force * kbThrust);
    }


}