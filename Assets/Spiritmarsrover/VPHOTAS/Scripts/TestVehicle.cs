
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class TestVehicle : UdonSharpBehaviour
{
    public VPJoystick VP_Inputs;
    public Rigidbody rb;
    public float engineVert = 20f;
    public float torque = 5f;
    void Start()
    {
        
    }
    private void FixedUpdate()
    {
        rb.AddRelativeForce(new Vector3(0f, VP_Inputs.ThrottleValue* engineVert, 0f));
        rb.AddRelativeTorque(new Vector3(VP_Inputs.inputZ* torque, VP_Inputs.inputY* torque, -VP_Inputs.inputX* torque));
    }
}
