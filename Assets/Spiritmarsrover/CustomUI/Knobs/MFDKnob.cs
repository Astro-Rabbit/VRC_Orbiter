using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDKnob : UdonSharpBehaviour
{
    [Header("References")]
    public Transform knobMesh;
    public UdonBehaviour targetScript;
    public string eventName;
    public string variableName;

    [Header("Range Settings")]
    public float minValue = 0f;
    public float maxValue = 100f;
    public float startingValue = 0f;

    [Header("Visual Settings")]
    public float totalRotationAngle = 300f;
    public Vector3 rotationAxis = new Vector3(0, 1, 0); // Local axis for mesh rotation

    [Header("Interaction Settings")]
    public bool isDiscrete = false;
    public float stepSize = 1f;
    public float desktopSensitivity = 2.0f; // Ratio of head tilt to knob turn

    private float _currentValue;
    private float _startValue;
    private int _activePenID = -1;

    // Relative Tracker Data (VR)
    private Quaternion _grabRot; // Hand rotation at start
    private float _startAngle;   // Knob angle at start

    void Start()
    {
        _currentValue = startingValue;
        UpdateVisuals();
    }

    //// Updated OnDown signature
    //public void OnDown(int id, Vector3 hitPoint, Quaternion startRotation)
    //{
    //    _activePenID = id;
    //    _startValue = _currentValue;
    //    _grabRot = startRotation; // Store the Pen's starting orientation
    //}

    //public void OnDown(int id, Vector3 hitPoint)
    //{
    //    _activePenID = id;
    //    _startValue = _currentValue;

    //    // Capture starting orientation
    //    VRCPlayerApi.TrackingDataType source = (Networking.LocalPlayer.IsUserInVR()) ?
    //        ((id == 1) ? VRCPlayerApi.TrackingDataType.RightHand : VRCPlayerApi.TrackingDataType.LeftHand) :
    //        VRCPlayerApi.TrackingDataType.Head;

    //    _grabRot = Networking.LocalPlayer.GetTrackingData(source).rotation;
    //}
    // Inside MFDKnob.cs

    //private Quaternion _grabRot; // This stores either the Pen OR the Head rotation

    //public void OnDown(int id, Vector3 hitPoint, Quaternion startRotation)
    //{
    //    _activePenID = id;
    //    _startValue = _currentValue;
    //    _grabRot = startRotation;

    //    // ADD THIS: Calculate where the angle starts based on current value
    //    float percent = (_currentValue - minValue) / (maxValue - minValue);
    //    _startAngle = percent * totalRotationAngle;
    //}

    //public void OnStayVR(int id, Quaternion currentHandRotation)
    //{
    //    if (id != _activePenID) return;

    //    // Use DeltaAngle to find the shortest path twist (prevents 0/360 snapping)
    //    Quaternion rel = Quaternion.Inverse(_grabRot) * currentHandRotation;
    //    rel.ToAngleAxis(out float angle, out Vector3 axis);
    //    if (angle > 180) angle -= 360;

    //    float dot = Vector3.Dot(axis, transform.forward);
    //    float angleDelta = angle * dot;

    //    ApplyDelta(angleDelta);
    //}

    // private Quaternion _grabRot; // Hand/Pen rotation at the moment of interaction
    private TabletPen _activePen;
    public void OnDown(int id, Vector3 hitPoint, Quaternion startRotation, TabletPen pen)
    {
        //Debug.Log("[MFDKnob] OnDown");
        _activePenID = id;
        _startValue = _currentValue;
        _grabRot = startRotation; // Store the initial orientation
        _activePen = pen; // Store the pen for audio
    }

    public void OnStayVR(int id, Quaternion currentHandRotation)
    {
        if (id != _activePenID) return;

        // 1. Calculate the rotation change from the start
        Quaternion relativeRotation = currentHandRotation * Quaternion.Inverse(_grabRot);

        // 2. Convert that rotation into an Axis and an Angle
        relativeRotation.ToAngleAxis(out float angle, out Vector3 axis);

        // 3. Unity returns angles 0-360; convert to -180 to 180 for delta calculation
        if (angle > 180f) angle -= 360f;

        // 4. PROJECT: Only keep the portion of the rotation that matches the knob's axis
        // Use the dot product between the pen's rotation axis and the knob's forward axis
        float projection = Vector3.Dot(axis, transform.up);
        float angleDelta = angle * projection;

        // 5. Apply the isolated rotation
        ApplyDelta(angleDelta);
    }

    private void ApplyDelta(float angleDelta)
    {
        // Calculate what percentage of the total range the movement represents
        float percentDelta = angleDelta / totalRotationAngle;
        float valueDelta = percentDelta * (maxValue - minValue);

        float newValue = Mathf.Clamp(_startValue + valueDelta, minValue, maxValue);

        if (isDiscrete)
        {
            newValue = Mathf.Round(newValue / stepSize) * stepSize;
        }

        if (Mathf.Approximately(newValue, _currentValue)) return;

        if (isDiscrete && _activePen != null) _activePen.PlayKnobClip();
        _activePen.TriggerHaptic(0.05f, 0.2f, 1.0f);

        _currentValue = newValue;
        UpdateVisuals();
        NotifyTarget();
    }

    //public void OnStayDesktop(int id, Quaternion currentCameraRotation)
    //{
    //    if (id != _activePenID) return;

    //    // Pitch and Yaw drag logic
    //    Quaternion relativeRot = Quaternion.Inverse(_grabRot) * currentCameraRotation;
    //    Vector3 eulers = relativeRot.eulerAngles;

    //    float pitch = eulers.x > 180 ? eulers.x - 360 : eulers.x;
    //    float yaw = eulers.y > 180 ? eulers.y - 360 : eulers.y;

    //    // Combine Pitch (Up/Down) and Yaw (Left/Right) for a diagonal "pull" feel
    //    float angleDelta = (yaw - pitch) * desktopSensitivity;

    //    float targetAngle = _startAngle + angleDelta;
    //    ApplyAngle(targetAngle);
    //}

    public void OnStayDesktop(int id, Quaternion currentCameraRotation)
    {
        if (id != _activePenID) return;

        // 1. Calculate the rotation delta since OnDown
        Quaternion relativeRot = Quaternion.Inverse(_grabRot) * currentCameraRotation;
        Vector3 eulers = relativeRot.eulerAngles;

        // 2. Normalize deltas to -180...180
        float pitch = Mathf.DeltaAngle(0, eulers.x);
        float yaw = Mathf.DeltaAngle(0, eulers.y);

        // 3. Combine movement and scale by sensitivity
        float angleDelta = (yaw - pitch) * desktopSensitivity;

        // 4. Use ApplyDelta just like VR does
        ApplyDelta(angleDelta);
    }

    private void ApplyAngle(float targetAngle)
    {
        // Convert angle back to value range
        float percent = Mathf.Clamp(targetAngle / totalRotationAngle, 0f, 1f);
        float rawValue = minValue + (percent * (maxValue - minValue));

        if (isDiscrete)
        {
            rawValue = Mathf.Round(rawValue / stepSize) * stepSize;
        }

        if (Mathf.Approximately(rawValue, _currentValue)) return;

        _currentValue = rawValue;
        UpdateVisuals();
        NotifyTarget();
    }

    private void UpdateVisuals()
    {
        if (knobMesh == null) return;

        // Map value (0.0 - 1.0) to rotation (Offset to Offset + Total)
        float percent = (_currentValue - minValue) / (maxValue - minValue);
        float currentDegrees = (percent * totalRotationAngle);

        knobMesh.localRotation = Quaternion.AngleAxis(currentDegrees, rotationAxis);
    }

    private void NotifyTarget()
    {
        if (targetScript == null) return;
        if (!string.IsNullOrEmpty(variableName)) targetScript.SetProgramVariable(variableName, _currentValue);
        if (!string.IsNullOrEmpty(eventName)) targetScript.SendCustomEvent(eventName);
    }

    public void OnUp(int id)
    {
        if (id == _activePenID)
        {
            _activePenID = -1;
            _activePen = null; // Clear reference
        }
    }
}