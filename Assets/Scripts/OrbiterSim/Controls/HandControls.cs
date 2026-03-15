using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HandControls : UdonSharpBehaviour
{
    // =========================================================
    // JOYSTICK
    // =========================================================
    [Header("Joystick")]
    public Transform joystickHandle;   // pitch/roll visual pivot
    public Transform twistGrip;        // yaw/twist visual pivot
    public GameObject JoystickCol;     // grab point
    public GameObject RotationControl; // current hand rotation helper
    public GameObject InitialRotation; // hand rotation at grab start

    [Header("Joystick Limits")]
    public float maxTiltAngle = 30f;
    public float maxTwistAngle = 45f;

    [Header("Joystick Mode Selector")]
    public float joystickModeKnobValue = 90f; // 0 = torque, 90 = rate

    // =========================================================
    // THROTTLE
    // =========================================================
    [Header("Throttle")]
    public GameObject ThrottleCol;                // grab point
    public GameObject ThrottlePositionTransfer;   // current hand position helper
    public GameObject ThrottlePositoinInit;       // hand position at grab start helper
    public Transform ThrottleAxisFrame;           // local +Z = "more throttle"
    public Transform ThrottleRotation;            // lever pivot visual

    [Tooltip("Hand travel along ThrottleAxisFrame +Z needed to move from 0 to 1 throttle.")]
    public float ThrottleDisplacment = 0.14f;

    [Header("Throttle Visual")]
    public Vector3 throttleVisualAxisLocal = new Vector3(0f, 0f, 1f);
    public float throttleVisualAngleMinDeg = -50f;
    public float throttleVisualAngleMaxDeg = 0f;
    public Vector3 throttleVisualBaseLocalEuler = Vector3.zero;

    public string ThrotString = "Throttle";

    // =========================================================
    // TRANSLATION CONTROLLER
    // =========================================================
    [Header("Translation Controller")]
    public GameObject TransCol;          // grab point + local axis frame
    public GameObject TransGripVis;      // visible handle/pivot
    public float TransSensitivity = 0.1f;
    public float TransDepthSensitivity = 0.04f; // Z/depth limit

    public string TransString = "Translation";
    public bool transIsPureTranslation = true;

    [Header("Translation Mapping")]
    public float translateDeadzone = 0.05f;
    public bool invertTransX = false;
    public bool invertTransY = false;
    public bool invertTransZ = false;

    [Header("Manual Translation Force Mapping")]
    public Vector3 maxTranslateForce_B = new Vector3(1000f, 1000f, 1000f);

    [Header("RCS Mode While Translating")]
    public byte rcsModeWhenTranslating = 0;
    public bool forceRcsModeOnTranslate = true;

    // =========================================================
    // GLOBAL GRAB SETTINGS
    // =========================================================
    [Header("Global Grab Settings")]
    public float gripThreshold = 0.95f;
    public float letgoThreshold = 0.95f;
    public float GripRadius = 0.1f;

    [Header("Control Names")]
    public string JoyString = "Joystick";

    // =========================================================
    // GUIDANCE OUTPUT
    // =========================================================
    [Header("Output -> Guidance Manual Draft")]
    public GC_ManualDraft manualDraft;
    public bool manualUseRateControl = true;
    public float maxPitchRateDeg = 20f;
    public float maxYawRateDeg = 20f;
    public float maxRollRateDeg = 30f;
    public float maxTauNm = 4000f;
    public float manualDeadzone = 0.02f;

    // =========================================================
    // HAPTICS
    // =========================================================
    [Header("Haptics")]
    public float hapticAmp = 0.2f;
    public float hapticDur = 0.04f;
    public float hapticFreq = 1.0f;
    public int hapticSegments = 30;

    // =========================================================
    // FILTER
    // =========================================================
    [Header("1 Euro Filter Settings")]
    public float minCutoff = 1.0f;
    public float beta = 0.05f;
    public float dCutoff = 1.0f;

    // =========================================================
    // PUBLIC OUTPUTS
    // =========================================================
    [HideInInspector] public float inputX, inputY, inputZ; // joystick
    [HideInInspector] public float ThrottleValue;          // throttle 0..1
    [HideInInspector] public float transX, transY, transZ; // translation

    // =========================================================
    // INTERNAL STATE
    // =========================================================
    public bool JoystickisGrabbed = false;
    public bool JoystickGrabbing, ThrottleGrabbing, TransGrabbing;

    bool JoystickGrabbingOld, ThrottleGrabbingOld, TransGrabbingOld;

    float LeftGripValue = 0f;
    float RightGripValue = 0f;

    bool LeftGrabbed, LeftGrabbedOld, LeftBusy;
    string LeftObject = "";

    bool RightGrabbed, RightGrabbedOld, RightBusy;
    string RightObject = "";

    float ThrottleValueInit;

    private Vector3 TransHandInitPosW;
    private Quaternion TransHandLocalInitRot;

    private Vector3 transGripBasePosW;
    private Quaternion transGripBaseRotW;
    private bool transGripBaseCaptured = false;

    // haptics bucket tracking
    private int lastIdxX, lastIdxY, lastIdxZ;
    private int lastIdxThrot;
    private int lastIdxTX, lastIdxTY, lastIdxTZ;

    // filter state
    private float xPrev, xPrevD, yPrev, yPrevD, zPrev, zPrevD;
    private float ThrottlePrev, ThrottlePrevD;
    private float txPrev, txPrevD, tyPrev, tyPrevD, tzPrev, tzPrevD;

    private void Start()
    {
        ApplyJoystickModeFromKnob();
        UpdateThrottleVisuals();
        CacheTransGripBasePose();

    }

    private void CacheTransGripBasePose()
    {
        if (TransGripVis == null) return;
        transGripBasePosW = TransGripVis.transform.position;
        transGripBaseRotW = TransGripVis.transform.rotation;
        transGripBaseCaptured = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        LeftGripValue = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryHandTrigger");
        RightGripValue = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryHandTrigger");

        UpdateHandGrab(true, LeftGripValue, ref LeftGrabbed, ref LeftGrabbedOld, ref LeftBusy, ref LeftObject);
        UpdateHandGrab(false, RightGripValue, ref RightGrabbed, ref RightGrabbedOld, ref RightBusy, ref RightObject);

        UpdateJoystickInput(dt);
        UpdateThrottleInput(dt);
        UpdateTranslationInput(dt);

        WriteManualDraft();

        JoystickGrabbingOld = JoystickGrabbing;
        ThrottleGrabbingOld = ThrottleGrabbing;
        TransGrabbingOld = TransGrabbing;
        LeftGrabbedOld = LeftGrabbed;
        RightGrabbedOld = RightGrabbed;
    }

    private void UpdateHandGrab(
        bool isLeft,
        float grip,
        ref bool grabbed,
        ref bool grabbedOld,
        ref bool busy,
        ref string objName)
    {
        grabbed = grip >= gripThreshold;
        if (grip < letgoThreshold) grabbed = false;

        VRCPlayerApi.TrackingData hand = Networking.LocalPlayer.GetTrackingData(
            isLeft ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand
        );

        if (grabbed && !grabbedOld && !busy)
        {
            if (CheckDist(hand.position, JoystickCol))
            {
                JoystickGrabbing = true;
                objName = JoyString;
                busy = true;
            }
            else if (CheckDist(hand.position, ThrottleCol))
            {
                ThrottleGrabbing = true;
                objName = ThrotString;
                busy = true;
            }
            else if (CheckDist(hand.position, TransCol))
            {
                TransGrabbing = true;
                objName = TransString;
                busy = true;
            }
        }

        if (!grabbed && grabbedOld && busy)
        {
            if (objName == JoyString) JoystickGrabbing = false;
            if (objName == ThrotString) ThrottleGrabbing = false;
            if (objName == TransString) TransGrabbing = false;

            objName = "";
            busy = false;
        }

        if (busy)
        {
            if (objName == JoyString && RotationControl != null)
            {
                RotationControl.transform.rotation = hand.rotation;
            }

            if (objName == ThrotString && ThrottlePositionTransfer != null)
            {
                ThrottlePositionTransfer.transform.position = hand.position;
            }
        }
    }

    private bool CheckDist(Vector3 handPos, GameObject target)
    {
        if (target == null) return false;
        return (handPos - target.transform.position).sqrMagnitude < (GripRadius * GripRadius);
    }

    // =========================================================
    // JOYSTICK
    // =========================================================
    private void UpdateJoystickInput(float dt)
    {
        if (JoystickGrabbing && !JoystickGrabbingOld)
        {
            JoystickisGrabbed = true;

            if (InitialRotation != null && RotationControl != null)
            {
                InitialRotation.transform.rotation = RotationControl.transform.rotation;
            }

            xPrev = inputX; yPrev = inputY; zPrev = inputZ;

            lastIdxX = Mathf.FloorToInt((inputX + 1f) * 0.5f * hapticSegments);
            lastIdxY = Mathf.FloorToInt((inputY + 1f) * 0.5f * hapticSegments);
            lastIdxZ = Mathf.FloorToInt((inputZ + 1f) * 0.5f * hapticSegments);
        }

        if (!JoystickGrabbing && JoystickGrabbingOld)
        {
            JoystickisGrabbed = false;
            inputX = 0f;
            inputY = 0f;
            inputZ = 0f;

            if (joystickHandle != null) joystickHandle.localRotation = Quaternion.identity;
            if (twistGrip != null) twistGrip.localRotation = Quaternion.identity;
        }

        if (!JoystickisGrabbed)
        {
            inputX = 0f;
            inputY = 0f;
            inputZ = 0f;
            return;
        }

        UpdateJoystickFromRotation();

        inputX = FilterAxis(inputX, dt, ref xPrev, ref xPrevD);
        inputY = FilterAxis(inputY, dt, ref yPrev, ref yPrevD);
        inputZ = FilterAxis(inputZ, dt, ref zPrev, ref zPrevD);

        CheckHaptic(JoyString, inputX, ref lastIdxX, true);
        CheckHaptic(JoyString, inputY, ref lastIdxY, true);
        CheckHaptic(JoyString, inputZ, ref lastIdxZ, true);
    }

    private void UpdateJoystickFromRotation()
    {
        if (RotationControl == null || InitialRotation == null) return;

        Quaternion diff =
            RotationControl.transform.rotation *
            Quaternion.Inverse(InitialRotation.transform.rotation);

        Vector3 localUp = diff * Vector3.up;
        float pitchAngle = Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg;
        float rollAngle = Mathf.Atan2(-localUp.x, localUp.y) * Mathf.Rad2Deg;

        inputZ = Mathf.Clamp(pitchAngle / maxTiltAngle, -1.0f, 1.0f);
        inputX = -Mathf.Clamp(rollAngle / maxTiltAngle, -1.0f, 1.0f);

        Vector3 localForward = diff * Vector3.forward;
        float twistAngle = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
        inputY = Mathf.Clamp(twistAngle / maxTwistAngle, -1.0f, 1.0f);

        if (joystickHandle != null)
        {
            joystickHandle.localRotation = Quaternion.Euler(
                -inputZ * maxTiltAngle,
                0f,
                inputY * maxTiltAngle
            );
        }

        if (twistGrip != null)
        {
            twistGrip.localRotation = Quaternion.Euler(
                0f,
                -inputX * maxTwistAngle,
                0f
            );
        }
    }

    // =========================================================
    // THROTTLE
    // =========================================================
    private void UpdateThrottleInput(float dt)
    {
        if (ThrottleGrabbing && !ThrottleGrabbingOld)
        {
            if (ThrottlePositoinInit != null && ThrottlePositionTransfer != null)
            {
                ThrottlePositoinInit.transform.position = ThrottlePositionTransfer.transform.position;
            }

            ThrottleValueInit = ThrottleValue;
            ThrottlePrev = ThrottleValue;
            lastIdxThrot = Mathf.FloorToInt(ThrottleValue * hapticSegments);
        }

        if (ThrottleGrabbing &&
            ThrottlePositionTransfer != null &&
            ThrottlePositoinInit != null &&
            ThrottleAxisFrame != null)
        {
            Vector3 deltaW =
                ThrottlePositionTransfer.transform.position -
                ThrottlePositoinInit.transform.position;

            float signedTravel = Vector3.Dot(deltaW, ThrottleAxisFrame.forward);
            float delta01 = Mathf.Clamp(signedTravel / ThrottleDisplacment, -1f, 1f);

            ThrottleValue = Mathf.Clamp01(ThrottleValueInit + delta01);
            ThrottleValue = FilterAxis(ThrottleValue, dt, ref ThrottlePrev, ref ThrottlePrevD);

            CheckHaptic(ThrotString, ThrottleValue, ref lastIdxThrot, false);
        }

        UpdateThrottleVisuals();
    }

    private void UpdateThrottleVisuals()
    {
        if (ThrottleRotation == null) return;

        Vector3 axis = throttleVisualAxisLocal;
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.forward;
        axis.Normalize();

        float angle = Mathf.Lerp(throttleVisualAngleMinDeg, throttleVisualAngleMaxDeg, ThrottleValue);
        Quaternion baseRot = Quaternion.Euler(throttleVisualBaseLocalEuler);
        Quaternion leverRot = Quaternion.AngleAxis(angle, axis);

        ThrottleRotation.localRotation = baseRot * leverRot;
    }

    // =========================================================
    // TRANSLATION
    // =========================================================
    private void UpdateTranslationInput(float dt)
    {
        if (TransGrabbing && !TransGrabbingOld)
        {
            VRCPlayerApi.TrackingData hand = GetActiveHandData(TransString);

            // Store grab-start hand position in WORLD space so scale does not matter.
            TransHandInitPosW = hand.position;

            // Keep this for optional non-pure-translation mode.
            TransHandLocalInitRot = Quaternion.Inverse(TransCol.transform.rotation) * hand.rotation;

            txPrev = 0f; tyPrev = 0f; tzPrev = 0f;
            txPrevD = 0f; tyPrevD = 0f; tzPrevD = 0f;

            lastIdxTX = hapticSegments / 2;
            lastIdxTY = hapticSegments / 2;
            lastIdxTZ = hapticSegments / 2;
        }

        if (TransGrabbing)
        {
            VRCPlayerApi.TrackingData hand = GetActiveHandData(TransString);

            // World-space delta from grab start.
            Vector3 deltaW = hand.position - TransHandInitPosW;

            float rawX, rawY, rawZ;

            // Project world delta onto TransCol's ROTATION axes only.
            // This ignores TransCol scale and parent scale.
            Vector3 axisX_W = TransCol.transform.right;
            Vector3 axisY_W = TransCol.transform.up;
            Vector3 axisZ_W = TransCol.transform.forward;

            rawZ = Mathf.Clamp(Vector3.Dot(deltaW, axisZ_W) / TransDepthSensitivity, -1f, 1f);

            if (transIsPureTranslation)
            {
                rawX = -Mathf.Clamp(Vector3.Dot(deltaW, axisX_W) / TransSensitivity, -1f, 1f);
                rawY =  Mathf.Clamp(Vector3.Dot(deltaW, axisY_W) / TransSensitivity, -1f, 1f);
            }
            else
            {
                Quaternion currentLocalRot =
                    Quaternion.Inverse(TransCol.transform.rotation) * hand.rotation;
                Quaternion relRot = currentLocalRot * Quaternion.Inverse(TransHandLocalInitRot);

                Vector3 localUp = relRot * Vector3.up;
                float pitchAngle = Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg;
                rawY = Mathf.Clamp(pitchAngle / maxTiltAngle, -1.0f, 1.0f);

                Vector3 localForward = relRot * Vector3.forward;
                float yawAngle = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
                rawX = Mathf.Clamp(yawAngle / maxTwistAngle, -1.0f, 1.0f);
            }

            transX = FilterAxis(rawX, dt, ref txPrev, ref txPrevD);
            transY = FilterAxis(rawY, dt, ref tyPrev, ref tyPrevD);
            transZ = FilterAxis(rawZ, dt, ref tzPrev, ref tzPrevD);

            CheckHaptic(TransString, transX, ref lastIdxTX, true);
            CheckHaptic(TransString, transY, ref lastIdxTY, true);
            CheckHaptic(TransString, transZ, ref lastIdxTZ, true);

            if (TransGripVis != null)
            {
                if (!transGripBaseCaptured) CacheTransGripBasePose();

                Quaternion tiltLocal = Quaternion.Euler(
                    transY * maxTiltAngle,
                    transX * maxTwistAngle,
                    0f
                );

                // Apply visual rotation relative to the original world rotation.
                TransGripVis.transform.rotation = transGripBaseRotW * tiltLocal;

                // Move along the controller's world forward axis, not localPosition,
                // so parent scaling does not explode the motion.
                TransGripVis.transform.position =
                    transGripBasePosW + TransCol.transform.forward * (transZ * TransSensitivity);
            }
        }
        else
        {
            transX = 0f;
            transY = 0f;
            transZ = 0f;

            if (TransGripVis != null)
            {
                if (!transGripBaseCaptured) CacheTransGripBasePose();
                TransGripVis.transform.position = transGripBasePosW;
                TransGripVis.transform.rotation = transGripBaseRotW;
            }
        }
    }

    private VRCPlayerApi.TrackingData GetActiveHandData(string objName)
    {
        if (LeftObject == objName)
            return Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);

        return Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
    }

    // =========================================================
    // OUTPUT TO MANUAL DRAFT
    // =========================================================
    private void WriteManualDraft()
    {
        if (manualDraft == null) return;

        bool attActive =
            JoystickisGrabbed ||
            Mathf.Abs(inputX) > manualDeadzone ||
            Mathf.Abs(inputY) > manualDeadzone ||
            Mathf.Abs(inputZ) > manualDeadzone;

        float tx = invertTransX ? -transX : transX;
        float ty = invertTransY ? -transY : transY;
        float tz = invertTransZ ? -transZ : transZ;

        bool transActive =
            TransGrabbing ||
            Mathf.Abs(tx) > translateDeadzone ||
            Mathf.Abs(ty) > translateDeadzone ||
            Mathf.Abs(tz) > translateDeadzone;

        bool thrActive =
            ThrottleGrabbing ||
            ThrottleValue > manualDeadzone;

        manualDraft.manualAttitudeActive = attActive;
        manualDraft.manualThrottleActive = (thrActive || transActive);

        manualDraft.useRateControl = manualUseRateControl;

        if (!attActive)
        {
            manualDraft.rateCmd_B = Vector3.zero;
            manualDraft.tauCmd_B = Vector3.zero;
        }
        else
        {
            if (manualDraft.useRateControl)
            {
                float p = maxPitchRateDeg * Mathf.Deg2Rad;
                float y = maxYawRateDeg * Mathf.Deg2Rad;
                float r = maxRollRateDeg * Mathf.Deg2Rad;

                manualDraft.rateCmd_B = new Vector3(
                    inputZ * p,
                    inputY * y,
                    -inputX * r
                );
                manualDraft.tauCmd_B = Vector3.zero;
            }
            else
            {
                manualDraft.tauCmd_B = new Vector3(
                    inputZ * maxTauNm,
                    inputY * maxTauNm,
                    -inputX * maxTauNm
                );
                manualDraft.rateCmd_B = Vector3.zero;
            }
        }

        manualDraft.mainThrottle01 = thrActive ? Mathf.Clamp01(ThrottleValue) : 0f;
        manualDraft.hoverThrottle01 = 0f;

        Vector3 tCmd01 = new Vector3(tx, ty, tz);
        if (!transActive) tCmd01 = Vector3.zero;

        manualDraft.translateCmd_B = new Vector3(
            tCmd01.x * maxTranslateForce_B.x,
            tCmd01.y * maxTranslateForce_B.y,
            tCmd01.z * maxTranslateForce_B.z
        );

        if (forceRcsModeOnTranslate && transActive)
        {
            manualDraft.rcsMode = rcsModeWhenTranslating;
        }
    }

    // =========================================================
    // FILTER + HAPTICS
    // =========================================================
    private float Alpha(float dt, float cutoff)
    {
        float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
        return 1.0f / (1.0f + tau / dt);
    }

    private float FilterAxis(float value, float dt, ref float prev, ref float prevD)
    {
        float dValue = (value - prev) / dt;
        float dAlpha = Alpha(dt, dCutoff);
        float edValue = prevD + dAlpha * (dValue - prevD);
        prevD = edValue;

        float cutoff = minCutoff + beta * Mathf.Abs(edValue);
        float alpha = Alpha(dt, cutoff);
        float result = prev + alpha * (value - prev);
        prev = result;
        return result;
    }

    private void CheckHaptic(string objName, float value, ref int lastIdx, bool isBiDirectional)
    {
        float normalized = isBiDirectional ? (value + 1f) * 0.5f : value;
        int currentIdx = Mathf.Clamp(Mathf.FloorToInt(normalized * hapticSegments), 0, hapticSegments);

        if (currentIdx != lastIdx)
        {
            lastIdx = currentIdx;
            VRC_Pickup.PickupHand hand =
                (LeftObject == objName) ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right;

            Networking.LocalPlayer.PlayHapticEventInHand(hand, hapticDur, hapticAmp, hapticFreq);
        }
    }

    // =========================================================
    // MODE HELPERS
    // =========================================================
    public void ApplyJoystickModeFromKnob()
    {
        manualUseRateControl = (joystickModeKnobValue >= 45f);
    }

    public void SetJoystickModeRate()
    {
        joystickModeKnobValue = 90f;
        manualUseRateControl = true;
    }

    public void SetJoystickModeTorque()
    {
        joystickModeKnobValue = 0f;
        manualUseRateControl = false;
    }
}