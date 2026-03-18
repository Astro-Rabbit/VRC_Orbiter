using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PenUpdater : UdonSharpBehaviour
{
    public GameObject PenHolderL;
    public GameObject PenHolderR;
    public VRC_Pickup PenLPickup;
    public VRC_Pickup PenRPickup;
    public GameObject PenLMesh;
    public GameObject PenRMesh;
    public TabletPen PenLScript;
    public TabletPen PenRScript;

    public bool PickupableL;
    public bool PickupableR;

    [Header("Use Filter")]
    public bool useFilter;

    [Header("Position Filter Settings")]
    public float posMinCutoff = 0.1f;
    public float posBeta = 0.05f;
    public float posDCutoff = 1.0f;

    [Header("Rotation Filter Settings")]
    public float rotMinCutoff = 0.5f;
    public float rotBeta = 0.02f;
    public float rotDCutoff = 1.0f;

    //public float TestValue = 0f;

    private VRCPlayerApi _localPlayer;

    // Filter states - Position
    private Vector3 _prevRawPosL, _prevRawPosR;
    private Vector3 _filtPosL, _filtPosR;
    private Vector3 _filtDPosL, _filtDPosR;

    // Filter states - Rotation
    private Quaternion _prevRawRotL, _prevRawRotR;
    private Quaternion _filtRotL, _filtRotR;
    private Quaternion _filtDRotL, _filtDRotR;

    private bool _firstFrame = true;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        if (_localPlayer == null) return;

        if (!_localPlayer.IsUserInVR())
        {
            PenLMesh.SetActive(false);
            PenRMesh.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (_localPlayer == null) return;

        

        if (useFilter)
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            // 1. Get Tracking Data
            VRCPlayerApi.TrackingData leftHand = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
            VRCPlayerApi.TrackingData rightHand = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
            VRCPlayerApi.TrackingData origin = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);

            Vector3 originPos = origin.position;
            Quaternion originRot = origin.rotation;
            Quaternion invOrigin = Quaternion.Inverse(originRot);

            // 2. Convert to Local Space (Relative to Player Origin)
            Vector3 relPosL = invOrigin * (leftHand.position - originPos);
            Quaternion relRotL = invOrigin * leftHand.rotation;
            Vector3 relPosR = invOrigin * (rightHand.position - originPos);
            Quaternion relRotR = invOrigin * rightHand.rotation;

            if (_firstFrame)
            {
                _filtPosL = relPosL; _filtPosR = relPosR;
                _filtRotL = relRotL; _filtRotR = relRotR;
                _prevRawPosL = relPosL; _prevRawPosR = relPosR;
                _prevRawRotL = relRotL; _prevRawRotR = relRotR;
                _firstFrame = false;
            }

            // 3. Filter Position and Rotation using their respective tunings
            _filtPosL = FilterVector3(relPosL, ref _prevRawPosL, ref _filtPosL, ref _filtDPosL, dt, posMinCutoff, posBeta, posDCutoff);
            _filtPosR = FilterVector3(relPosR, ref _prevRawPosR, ref _filtPosR, ref _filtDPosR, dt, posMinCutoff, posBeta, posDCutoff);

            _filtRotL = FilterQuaternion(relRotL, ref _prevRawRotL, ref _filtRotL, ref _filtDRotL, dt, rotMinCutoff, rotBeta, rotDCutoff);
            _filtRotR = FilterQuaternion(relRotR, ref _prevRawRotR, ref _filtRotR, ref _filtDRotR, dt, rotMinCutoff, rotBeta, rotDCutoff);

            // 4. Transform back to World Space and Apply
            PenHolderL.transform.position = originPos + (originRot * _filtPosL);
            PenHolderL.transform.rotation = originRot * _filtRotL;

            PenHolderR.transform.position = originPos + (originRot * _filtPosR);
            PenHolderR.transform.rotation = originRot * _filtRotR;
        }
        else
        {
            VRCPlayerApi.TrackingData leftHand = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
            VRCPlayerApi.TrackingData rightHand = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);

            PenHolderL.transform.position = leftHand.position;
            PenHolderL.transform.rotation = leftHand.rotation;

            PenHolderR.transform.position = rightHand.position;
            PenHolderR.transform.rotation = rightHand.rotation;
        }

        
    }

    // --- Filter Logic with Parameter Arguments ---

    private Vector3 FilterVector3(Vector3 raw, ref Vector3 prevRaw, ref Vector3 filt, ref Vector3 filtD, float dt, float minCut, float beta, float dCut)
    {
        Vector3 dValue = (raw - prevRaw) / dt;
        prevRaw = raw;

        Vector3 dFilt = LowPassVector3(dValue, filtD, Alpha(dCut, dt));
        filtD = dFilt;

        float cutoff = minCut + beta * dFilt.magnitude;
        return LowPassVector3(raw, filt, Alpha(cutoff, dt));
    }

    private Quaternion FilterQuaternion(Quaternion raw, ref Quaternion prevRaw, ref Quaternion filt, ref Quaternion filtD, float dt, float minCut, float beta, float dCut)
    {
        if (Quaternion.Dot(raw, prevRaw) < 0) raw = new Quaternion(-raw.x, -raw.y, -raw.z, -raw.w);

        Quaternion dValue = new Quaternion((raw.x - prevRaw.x) / dt, (raw.y - prevRaw.y) / dt, (raw.z - prevRaw.z) / dt, (raw.w - prevRaw.w) / dt);
        prevRaw = raw;

        Quaternion dFilt = LowPassQuat(dValue, filtD, Alpha(dCut, dt));
        filtD = dFilt;

        float velocity = Mathf.Sqrt(dFilt.x * dFilt.x + dFilt.y * dFilt.y + dFilt.z * dFilt.z + dFilt.w * dFilt.w);
        float cutoff = minCut + beta * velocity;

        Quaternion result = LowPassQuat(raw, filt, Alpha(cutoff, dt));
        float mag = Mathf.Sqrt(result.x * result.x + result.y * result.y + result.z * result.z + result.w * result.w);
        return new Quaternion(result.x / mag, result.y / mag, result.z / mag, result.w / mag);
    }

    private float Alpha(float cutoff, float dt)
    {
        float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
        return 1.0f / (1.0f + tau / dt);
    }

    private Vector3 LowPassVector3(Vector3 current, Vector3 prev, float alpha) => Vector3.Lerp(prev, current, alpha);

    private Quaternion LowPassQuat(Quaternion current, Quaternion prev, float alpha)
    {
        return new Quaternion(
            Mathf.Lerp(prev.x, current.x, alpha),
            Mathf.Lerp(prev.y, current.y, alpha),
            Mathf.Lerp(prev.z, current.z, alpha),
            Mathf.Lerp(prev.w, current.w, alpha)
        );
    }
    // --- Existing UI / Pickup Methods ---
    public void TogglePickup(VRC_Pickup pen)
    {
        pen.pickupable = !pen.pickupable;
        PickupableL = pen.pickupable;
        PickupableR = pen.pickupable;
    }
    public void EnterPickUpable(VRC_Pickup pen)
    {
        pen.pickupable = true;
        PickupableL = pen.pickupable;
        PickupableR = pen.pickupable;
    }
    public void ExitPickUpable(VRC_Pickup pen)
    {
        pen.pickupable = false;
        PickupableL = pen.pickupable;
        PickupableR = pen.pickupable;
    }
    public void EnterLeft()
    {
        EnterPickUpable(PenLPickup);
    }
    public void ExitLeft()
    {
        ExitPickUpable(PenLPickup);
    }

    public void EnterRight()
    {
        EnterPickUpable(PenRPickup);
    }
    public void ExitRight()
    {
        ExitPickUpable(PenRPickup);
    }

    public void ToggleLeft()
    {
        TogglePickup(PenLPickup);
    }
    public void ToggleRight()
    {
        TogglePickup(PenRPickup);
    }

    public void toggleFilter()
    {
        useFilter = !useFilter;
    }

    public void ResetLeftStylus()
    {
        PenLPickup.transform.localPosition = Vector3.zero;
        PenLPickup.transform.localRotation =Quaternion.Euler( Vector3.zero);
    }
    public void ResetRightStylus()
    {
        PenRPickup.transform.localPosition = Vector3.zero;
        PenRPickup.transform.localRotation =Quaternion.Euler( Vector3.zero);
    }
    public bool TriggerTouch;
    public void ToggleTriggerTouch()
    {
        TriggerTouch = !PenLScript.TriggerRequiredForTablet;
        SendCustomEventDelayedSeconds("ToggleTriggerDelayed",1.5f);
    }
    public void ToggleTriggerDelayed()
    {
        PenLScript.TriggerRequiredForTablet = TriggerTouch;
        PenRScript.TriggerRequiredForTablet = TriggerTouch;
        
    }
    public bool OverrideMesh;
    public void OverrideMeshToggle()
    {
        OverrideMesh = !OverrideMesh;
        PenLScript.OverRideMeshToggle(OverrideMesh);
        PenRScript.OverRideMeshToggle(OverrideMesh);
    }
    public float Amp = 0.2f;
    public void SetPenAmp()
    {
        PenLScript.HapticAmplitude = Amp;
        PenRScript.HapticAmplitude = Amp;
    }
}

