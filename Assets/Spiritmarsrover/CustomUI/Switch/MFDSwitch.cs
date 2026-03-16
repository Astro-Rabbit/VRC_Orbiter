using UdonSharp;
using UnityEngine;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDSwitch : UdonSharpBehaviour
{
    [Header("References")]
    public Transform switchMesh;
    public UdonBehaviour targetScript;
    public string eventName;
    public string variableName;

    [Header("Settings")]
    public bool isThreeWay = false;
    public float dragThreshold = 0.02f; // VR: Meters moved
    public float desktopSensitivity = 50f; // Desktop: Degrees tilted
    public float switchAngle = 30f;

    [Header("Current State")]
    public int state = 0;

    private int _activePenID = -1;
    private Vector3 _startLocalTipPos;
    private Quaternion _grabRot;
    private int _startState;
    private TabletPen _activePen;
    void Start()
    {
        UpdateVisuals();
        NotifyTarget();
    }

    public void OnDown(int id, TabletPen pen, Quaternion startRotation)
    {
        //Debug.Log("[MFDSwitch] Ondown");
        _activePenID = id;
        _startState = state;
        _grabRot = startRotation;
        _activePen = pen;

        // Calculate initial tip position for VR
        Vector3 tipWorldPos = pen.transform.position + (pen.transform.up * -pen.rayDistance);
        _startLocalTipPos = transform.InverseTransformPoint(tipWorldPos);
    }

    public void OnStayVR(int id, TabletPen pen)
    {
        if (id != _activePenID) return;

        Vector3 currentTipWorld = pen.transform.position + (pen.transform.up * -pen.rayDistance);
        Vector3 currentLocalTip = transform.InverseTransformPoint(currentTipWorld);

        float deltaY = currentLocalTip.y - _startLocalTipPos.y;
        ProcessDelta(deltaY / dragThreshold);
    }

    public void OnStayDesktop(int id, Quaternion currentCameraRotation)
    {
        if (id != _activePenID) return;

        // Same head-tilt logic as knob, focusing on Pitch (Vertical movement)
        Quaternion relativeRot = Quaternion.Inverse(_grabRot) * currentCameraRotation;
        float pitch = Mathf.DeltaAngle(0, relativeRot.eulerAngles.x);

        // Negative pitch because looking "up" should move switch "up"
        ProcessDelta(-pitch / desktopSensitivity);
    }

    private void ProcessDelta(float stepDelta)
    {
        int stepChange = Mathf.RoundToInt(stepDelta);
        int maxState = isThreeWay ? 2 : 1;
        int newState = Mathf.Clamp(_startState + stepChange, 0, maxState);
        
        if (newState != state)
        {
            if(newState == 0)
            {
                if (_activePen != null) _activePen.PlaySwitchUpClip();
            }else if(newState == 1)
            {
                if (_activePen != null) _activePen.PlaySwitchDownClip();
            }
            _activePen.TriggerHapticEvent();
            state = newState;
            
            UpdateVisuals();
            NotifyTarget();
        }
    }

    public void OnUp(int id)
    {
        if (id == _activePenID)
        {
            _activePenID = -1;
            _activePen = null; // Clear reference
        }
    }

    private void UpdateVisuals()
    {
        if (switchMesh == null) return;
        float targetAngle = isThreeWay ? (state - 1) * switchAngle : (state == 0 ? -switchAngle : switchAngle);
        switchMesh.localRotation = Quaternion.Euler(targetAngle, 0, 0);
    }

    private void NotifyTarget()
    {
        if (targetScript == null) return;
        if (!string.IsNullOrEmpty(variableName)) targetScript.SetProgramVariable(variableName, state);
        if (!string.IsNullOrEmpty(eventName)) targetScript.SendCustomEvent(eventName);
    }
}