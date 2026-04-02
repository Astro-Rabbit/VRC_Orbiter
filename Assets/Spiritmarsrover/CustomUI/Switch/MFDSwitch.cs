using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MFDSwitch : UdonSharpBehaviour
{
    [Header("References")]
    public Transform switchMesh;
    public UdonBehaviour targetScript;
    public string eventName;
    public string variableName;

    [Header("Settings")]
    public bool isThreeWay = false;
    public float dragThreshold = 0.02f;
    public float desktopSensitivity = 50f;
    public float switchAngle = 30f;

    [Header("Axis Setup")]
    [Tooltip("Which local axis of THIS object the VR pen motion uses. 0=X, 1=Y, 2=Z")]
    public int dragAxis = 2;

    [Tooltip("Invert the VR drag direction.")]
    public bool invertDrag = false;

    [Tooltip("Which local axis of the SWITCH MESH rotates. 0=X, 1=Y, 2=Z")]
    public int rotationAxis = 0;

    [Tooltip("Invert the visual switch rotation.")]
    public bool invertRotation = false;

    [Header("Current State")]
    [UdonSynced] public byte state = 0;

    private int _activePenID = -1;
    private Vector3 _startLocalTipPos;
    private Quaternion _grabRot;
    private byte _startState;
    private TabletPen _activePen;

    void Start()
    {
        UpdateVisuals();
        if (Networking.IsOwner(Networking.LocalPlayer, gameObject))
        {
            NotifyTarget();
        }

    }

    public override void OnDeserialization()
    {
        ApplyState();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi newOwner)
    {
        // Optional: if you want any ownership-dependent UI, handle it here.
    }

    public void OnDown(int id, TabletPen pen, Quaternion startRotation)
    {
        _activePenID = id;
        _startState = state;
        _grabRot = startRotation;
        _activePen = pen;

        Vector3 tipWorldPos = pen.transform.position + (pen.transform.up * -pen.rayDistance);
        _startLocalTipPos = WorldToLocalNoScale(tipWorldPos);
    }

    public void OnStayVR(int id, TabletPen pen)
    {
        if (id != _activePenID) return;

        Vector3 currentTipWorld = pen.transform.position + (pen.transform.up * -pen.rayDistance);
        Vector3 currentLocalTip = WorldToLocalNoScale(currentTipWorld);

        float startValue = GetAxisValue(_startLocalTipPos, dragAxis);
        float currentValue = GetAxisValue(currentLocalTip, dragAxis);

        float delta = currentValue - startValue;
        if (invertDrag) delta = -delta;

        ProcessDelta(delta / dragThreshold);
    }

    public void OnStayDesktop(int id, Quaternion currentCameraRotation)
    {
        if (id != _activePenID) return;

        Quaternion relativeRot = Quaternion.Inverse(_grabRot) * currentCameraRotation;
        float pitch = Mathf.DeltaAngle(0, relativeRot.eulerAngles.x);

        ProcessDelta(-pitch / desktopSensitivity);
    }

    private void ProcessDelta(float stepDelta)
    {
        int maxState = isThreeWay ? 2 : 1;
        int newState = Mathf.Clamp(_startState + Mathf.RoundToInt(stepDelta), 0, maxState);
        byte newStateByte = (byte)newState;
        if (newStateByte == state) return;

        // Local interaction feedback
        if (_activePen != null)
        {
            // New logic: compare newState to current state
            if (newStateByte > state)
            {
                _activePen.PlaySwitchDownClip();
            }
            else
            {
                _activePen.PlaySwitchUpClip();
            }
            _activePen.TriggerHapticEvent();
            //state = newState;
            
            UpdateVisuals();
            NotifyTarget();

        }

        EnsureLocalOwnership();

        state = newStateByte;
        ApplyState();
        RequestSerialization();
    }

    public void OnUp(int id)
    {
        if (id == _activePenID)
        {
            _activePenID = -1;
            _activePen = null;
        }
    }

    private void ApplyState()
    {
        UpdateVisuals();
        NotifyTarget();
    }

    private void UpdateVisuals()
    {
        if (switchMesh == null) return;

        float targetAngle = isThreeWay
            ? (state - 1) * switchAngle
            : (state == 0 ? -switchAngle : switchAngle);

        if (invertRotation) targetAngle = -targetAngle;

        Vector3 e = Vector3.zero;
        if (rotationAxis == 0) e.x = targetAngle;
        else if (rotationAxis == 1) e.y = targetAngle;
        else e.z = targetAngle;

        switchMesh.localRotation = Quaternion.Euler(e);
    }

    private void NotifyTarget()
    {
        if (targetScript == null) return;

        if (!string.IsNullOrEmpty(variableName))
            targetScript.SetProgramVariable(variableName, state);

        if (!string.IsNullOrEmpty(eventName))
            targetScript.SendCustomEvent(eventName);
    }

    private void EnsureLocalOwnership()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (!Networking.IsOwner(local, gameObject))
            Networking.SetOwner(local, gameObject);
    }

    private Vector3 WorldToLocalNoScale(Vector3 worldPoint)
    {
        return Quaternion.Inverse(transform.rotation) * (worldPoint - transform.position);
    }

    private float GetAxisValue(Vector3 v, int axis)
    {
        if (axis == 0) return v.x;
        if (axis == 1) return v.y;
        return v.z;
    }
}