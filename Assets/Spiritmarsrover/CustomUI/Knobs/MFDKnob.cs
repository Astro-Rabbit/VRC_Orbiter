using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
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
    public Vector3 rotationAxis = new Vector3(0, 1, 0);

    [Header("Interaction Settings")]
    public bool isDiscrete = false;
    public float stepSize = 1f;
    public float desktopSensitivity = 2.0f;

    [UdonSynced] private float _currentValue;
    private float _startValue;
    private int _activePenID = -1;

    private Quaternion _grabRot;
    private TabletPen _activePen;

    void Start()
    {
        // If owner and value is still default zero-ish, initialize from startingValue.
        // This avoids overwriting a networked value on non-owners.
        if (Networking.IsOwner(gameObject) && Mathf.Approximately(_currentValue, 0f))
        {
            _currentValue = Mathf.Clamp(startingValue, minValue, maxValue);
        }

        ApplyValue();
    }

    public override void OnDeserialization()
    {
        ApplyValue();
    }

    public void OnDown(int id, Vector3 hitPoint, Quaternion startRotation, TabletPen pen)
    {
        _activePenID = id;
        _startValue = _currentValue;
        _grabRot = startRotation;
        _activePen = pen;
    }

    public void OnStayVR(int id, Quaternion currentHandRotation)
    {
        if (id != _activePenID) return;

        Quaternion relativeRotation = currentHandRotation * Quaternion.Inverse(_grabRot);
        relativeRotation.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        float projection = Vector3.Dot(axis, transform.up);
        float angleDelta = angle * projection;

        ApplyDelta(angleDelta);
    }

    public void OnStayDesktop(int id, Quaternion currentCameraRotation)
    {
        if (id != _activePenID) return;

        Quaternion relativeRot = Quaternion.Inverse(_grabRot) * currentCameraRotation;
        Vector3 eulers = relativeRot.eulerAngles;

        float pitch = Mathf.DeltaAngle(0, eulers.x);
        float yaw = Mathf.DeltaAngle(0, eulers.y);

        float angleDelta = (yaw - pitch) * desktopSensitivity;
        ApplyDelta(angleDelta);
    }

    private void ApplyDelta(float angleDelta)
    {
        float percentDelta = angleDelta / totalRotationAngle;
        float valueDelta = percentDelta * (maxValue - minValue);

        float newValue = Mathf.Clamp(_startValue + valueDelta, minValue, maxValue);

        if (isDiscrete)
            newValue = Mathf.Round(newValue / stepSize) * stepSize;

        if (Mathf.Approximately(newValue, _currentValue)) return;

        if (_activePen != null)
        {
            if (isDiscrete) _activePen.PlayKnobClip();
            _activePen.TriggerHaptic(0.05f, 0.2f, 1.0f);
        }

        EnsureLocalOwnership();

        _currentValue = newValue;
        ApplyValue();
        RequestSerialization();
    }

    private void ApplyValue()
    {
        UpdateVisuals();
        NotifyTarget();
    }

    private void UpdateVisuals()
    {
        if (knobMesh == null) return;

        float percent = Mathf.InverseLerp(minValue, maxValue, _currentValue);
        float currentDegrees = percent * totalRotationAngle;

        knobMesh.localRotation = Quaternion.AngleAxis(currentDegrees, rotationAxis);
    }

    private void NotifyTarget()
    {
        if (targetScript == null) return;

        if (!string.IsNullOrEmpty(variableName))
            targetScript.SetProgramVariable(variableName, _currentValue);

        if (!string.IsNullOrEmpty(eventName))
            targetScript.SendCustomEvent(eventName);
    }

    public void OnUp(int id)
    {
        if (id == _activePenID)
        {
            _activePenID = -1;
            _activePen = null;
        }
    }

    private void EnsureLocalOwnership()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(local, gameObject);
    }

    public float GetValue()
    {
        return _currentValue;
    }
}