using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VPControls : UdonSharpBehaviour
{
    [Header("Hierarchy")]
    public Transform joystickHandle;
    public Transform twistGrip;
    public Transform handProxy;

    [Header("Limits")]
    public float maxTiltAngle = 30f;
    public float maxTwistAngle = 45f;
    public bool JoystickisGrabbed = false;

    

    [Header("Translation Config")]
    public GameObject TranlatePointForRotation;
    public bool isTranslation = false;
    public float TranslationScale = 0.25f;
    public GameObject TranslatePoint;
    public GameObject TranslatePointInitial;


    [Header("Manual Translation Mapping")]
    public float translateDeadzone = 0.05f;

    // Optional: invert axes if needed
    public bool invertTransX = false;
    public bool invertTransY = false;
    public bool invertTransZ = false;
    [Header("Manual Translation Force Mapping")]
    public Vector3 maxTranslateForce_B = new Vector3(1000f, 1000f, 1000f);

    // Optional: if you want translation to automatically imply an RCS mode
    // 0=TRANSLATE, 1=ROTATE, 2=BLENDED (your default), adjust to your project constants
    public byte rcsModeWhenTranslating = 0; // TRANSLATE
    public bool forceRcsModeOnTranslate = true;    

    [Header("UI/Output Debug")]
    public GameObject Yout;
    public GameObject XZout;
    public GameObject RotationControl;
    public GameObject InitialRotation;

    [Header("Global Grab Settings")]
    public float gripThreshold = 0.95f;
    public float letgoThreshold = 0.95f;
    public float GripRadius = 0.1f;
    public GameObject LeftGripVis;
    public GameObject RightGripVis;

    [Header("Joystick Assets")]
    public GameObject JoystickCol;
    public string JoyString = "Joystick";

    [Header("Joystick Mode Selector")]
    public float joystickModeKnobValue = 90f; // expected values: 0 or 90

    [Header("Throttle Assets")]
    public GameObject ThrottleCol;
    public GameObject ThrottleVis;
    public GameObject ThrottlePositionTransfer;
    public GameObject ThrottlePositoinInit;
    public GameObject ThrottleBackPosition;
    public GameObject ThrottleGripVis;
    public float ThrottleDisplacment = 0.14f;
    public string ThrotString = "Throttle";
    public bool transIsPureTranslation = true;

    [Header("Translation Controller Assets")]
    public GameObject TransCol;          // The collider for the translation handle
    public GameObject TransGripVis;     // Visual for the handle movement
    public float TransSensitivity = 0.1f; // Distance (meters) to reach 1.0 input
    public string TransString = "Translation";

    // Interaction State
    float LeftGripValue = 0f;
    float RightGripValue = 0f;
    bool LeftGrabbed, LeftGrabbedOld, LeftBusy;
    string LeftObject = "";
    bool RightGrabbed, RightGrabbedOld, RightBusy;
    string RightObject = "";

    [Header("Output -> Guidance Manual Draft")]
    public GC_ManualDraft manualDraft;
    // Manual tuning
    public bool manualUseRateControl = true;
    // Max command magnitudes
    public float maxPitchRateDeg = 20f;
    public float maxYawRateDeg   = 20f;
    public float maxRollRateDeg  = 30f;
    // If you ever want direct-torque mode for testing
    public float maxTauNm = 4000f;
    // Deadzone for deciding “active”
    public float manualDeadzone = 0.02f;

    // Input Values
    [HideInInspector] public float inputX, inputY, inputZ; // Joystick
    [HideInInspector] public float ThrottleValue;         // Throttle
    [HideInInspector] public float transX, transY, transZ; // Translation Output

    // Internal Logic
    public bool JoystickGrabbing, ThrottleGrabbing, TransGrabbing;
    bool JoystickGrabbingOld, ThrottleGrabbingOld, TransGrabbingOld;
    float ThrottleValueInit;
    Vector3 TransHandInitPos;
    public bool oldRotation = false;

    public GameObject translateOutputXZ;
    public GameObject translateOutputY;

    [Header("Haptics")]
    public float hapticAmp = 0.2f;
    public float hapticDur = 0.04f;
    public float hapticFreq = 1.0f;
    public int hapticSegments = 30;

    // Tracking indices to detect crossings
    private int lastIdxX, lastIdxY, lastIdxZ;
    private int lastIdxThrot;
    private int lastIdxTX, lastIdxTY, lastIdxTZ;

    public GameObject ThrottleRotation;
    private void Start()
    {
        ApplyJoystickModeFromKnob();
    }
    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;

        // --- 1. GRAB DETECTION ---
        LeftGripValue = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryHandTrigger");
        RightGripValue = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryHandTrigger");

        UpdateHandGrab(true, LeftGripValue, ref LeftGrabbed, ref LeftGrabbedOld, ref LeftBusy, ref LeftObject, LeftGripVis);
        UpdateHandGrab(false, RightGripValue, ref RightGrabbed, ref RightGrabbedOld, ref RightBusy, ref RightObject, RightGripVis);

        // --- 2. JOYSTICK LOGIC ---
        UpdateJoystickInput();

        // --- 3. THROTTLE LOGIC ---
        UpdateThrottleInput(dt);

        // --- 4. TRANSLATION LOGIC ---
        UpdateTranslationInput(dt);

        // Save old states
        JoystickGrabbingOld = JoystickGrabbing;
        ThrottleGrabbingOld = ThrottleGrabbing;
        TransGrabbingOld = TransGrabbing;
        LeftGrabbedOld = LeftGrabbed;
        RightGrabbedOld = RightGrabbed;

        // Debug Outputs
        Yout.transform.localPosition = new Vector3(inputY / 2.0f, 0f, 0f);
        XZout.transform.localPosition = new Vector3(inputX / 2.0f, inputZ / 2.0f, 0f);

        translateOutputXZ.transform.localPosition = new Vector3(-transX / 2.0f, transY / 2.0f, 0f);
        translateOutputY.transform.localPosition = new Vector3(0f, transZ/2.0f, 0f);

        // --- 5. WRITE TO MANUAL DRAFT ---
        if (manualDraft != null)
        {
            // Attitude active?
            bool attActive =
                JoystickisGrabbed ||
                (Mathf.Abs(inputX) > manualDeadzone) ||
                (Mathf.Abs(inputY) > manualDeadzone) ||
                (Mathf.Abs(inputZ) > manualDeadzone);

            // Translation active?
            float tx = invertTransX ? -transX : transX;
            float ty = invertTransY ? -transY : transY;
            float tz = invertTransZ ? -transZ : transZ;

            bool transActive =
                TransGrabbing ||
                (Mathf.Abs(tx) > translateDeadzone) ||
                (Mathf.Abs(ty) > translateDeadzone) ||
                (Mathf.Abs(tz) > translateDeadzone);

            // Throttle active?
            bool thrActive =
                ThrottleGrabbing ||
                (ThrottleValue > manualDeadzone);

            // Your draft only has attitude + throttle activity flags.
            // Treat translation as part of "manualThrottleActive" (propulsion/RCS channel activity).
            manualDraft.manualAttitudeActive = attActive;
            manualDraft.manualThrottleActive = (thrActive || transActive);

            // Attitude stick mapping (same as before)
            manualDraft.useRateControl = manualUseRateControl;

            if (!attActive)
            {
                manualDraft.rateCmd_B = Vector3.zero;
                manualDraft.tauCmd_B  = Vector3.zero;
            }
            else
            {
                if (manualDraft.useRateControl)
                {
                    float p = maxPitchRateDeg * Mathf.Deg2Rad;
                    float y = maxYawRateDeg   * Mathf.Deg2Rad;
                    float r = maxRollRateDeg  * Mathf.Deg2Rad;

                    manualDraft.rateCmd_B = new Vector3(
                        inputZ * p,  // pitch about +X
                        inputY * y,  // yaw about +Y
                        -inputX * r   // roll about +Z
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

            // Throttle
            manualDraft.mainThrottle01 = thrActive ? Mathf.Clamp01(ThrottleValue) : 0f;
            manualDraft.hoverThrottle01 = 0f; // still unused

            // Translation command in BODY frame: X=right, Y=up, Z=forward
            // Clamp + deadzone
            Vector3 tCmd01 = new Vector3(tx, ty, tz);
            if (!transActive) tCmd01 = Vector3.zero;

            Vector3 tCmdN = new Vector3(
                tCmd01.x * maxTranslateForce_B.x,
                tCmd01.y * maxTranslateForce_B.y,
                tCmd01.z * maxTranslateForce_B.z
            );

            manualDraft.translateCmd_B = tCmdN;

            // Optional: set preferred RCS mode when translating
            if (forceRcsModeOnTranslate && transActive)
            {
                manualDraft.rcsMode = rcsModeWhenTranslating;
            }
            // else: leave manualDraft.rcsMode as whatever UI/pilot last set (or default)
        }

    }

    private void UpdateHandGrab(bool isLeft, float grip, ref bool grabbed, ref bool grabbedOld, ref bool busy, ref string objName, GameObject vis)
    {
        grabbed = grip >= gripThreshold;
        if (grip < letgoThreshold) grabbed = false;
        if (vis != null) vis.SetActive(grabbed);

        VRCPlayerApi.TrackingData hand = Networking.LocalPlayer.GetTrackingData(isLeft ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);

        // Grab Start
        if (grabbed && !grabbedOld && !busy)
        {
            if (CheckDist(hand.position, JoystickCol)) { JoystickGrabbing = true; objName = JoyString; busy = true; }
            else if (CheckDist(hand.position, ThrottleCol)) { ThrottleGrabbing = true; objName = ThrotString; busy = true; }
            else if (CheckDist(hand.position, TransCol)) { TransGrabbing = true; objName = TransString; busy = true; }

            

        }
        // Grab End
        if (!grabbed && grabbedOld && busy)
        {
            if (objName == JoyString) JoystickGrabbing = false;
            if (objName == ThrotString) ThrottleGrabbing = false;
            if (objName == TransString) TransGrabbing = false;
            objName = "";
            busy = false;
        }

        // Apply Tracking
        if (busy)
        {
            if (objName == JoyString)
            {
                RotationControl.transform.rotation = hand.rotation;
                if (isTranslation) TranslatePoint.transform.position = hand.position;
            }
            if (objName == ThrotString) ThrottlePositionTransfer.transform.position = hand.position;
            if (objName == TransString) /* Hand position used directly in Translation logic */ { }
        }
    }

    private bool CheckDist(Vector3 handPos, GameObject target)
    {
        if (target == null) return false;
        return (handPos - target.transform.position).sqrMagnitude < (GripRadius * GripRadius);
    }

    void UpdateJoystickInput()
    {
        TranslatePoint.transform.rotation = RotationControl.transform.rotation;

        if (JoystickGrabbing && !JoystickGrabbingOld)
        {
            JoystickisGrabbed = true;
            TranslatePointInitial.transform.position = TranslatePoint.transform.position;
            TranslatePointInitial.transform.rotation = TranslatePoint.transform.rotation;
            InitialRotation.transform.rotation = RotationControl.transform.rotation;
            xPrev = inputX; yPrev = inputY; zPrev = inputZ;

            // Inside UpdateJoystickInput grab start:
            lastIdxX = Mathf.FloorToInt((inputX + 1f) * 0.5f * hapticSegments);
            lastIdxY = Mathf.FloorToInt((inputY + 1f) * 0.5f * hapticSegments);
            lastIdxZ = Mathf.FloorToInt((inputZ + 1f) * 0.5f * hapticSegments);
        }

        if (!JoystickGrabbing && JoystickGrabbingOld)
        {
            JoystickisGrabbed = false;
            TranslatePointInitial.transform.localPosition = Vector3.zero;
            TranslatePoint.transform.localPosition = Vector3.zero;
            joystickHandle.localRotation = Quaternion.identity;
            twistGrip.localRotation = Quaternion.identity;
        }

        if (JoystickisGrabbed)
        {
            if (!isTranslation) TranslatePoint.transform.position = TranlatePointForRotation.transform.position;
            UpdateJoystick();

            float dt = Time.deltaTime;
            inputX = FilterAxis(inputX, dt, ref xPrev, ref xPrevD);
            inputY = FilterAxis(inputY, dt, ref yPrev, ref yPrevD);
            inputZ = FilterAxis(inputZ, dt, ref zPrev, ref zPrevD);

            // Inside the if (JoystickisGrabbed) block, after inputX, Y, Z are filtered:
            CheckHaptic(JoyString, inputX, ref lastIdxX, true);
            CheckHaptic(JoyString, inputY, ref lastIdxY, true);
            CheckHaptic(JoyString, inputZ, ref lastIdxZ, true);
        }
        else
        {
            inputX = 0; inputY = 0; inputZ = 0;
        }
    }

    void UpdateThrottleInput(float dt)
    {
        if (ThrottleGrabbing && !ThrottleGrabbingOld)
        {
            ThrottlePositoinInit.transform.position = ThrottlePositionTransfer.transform.position;
            ThrottleValueInit = ThrottleValue;
            ThrottlePrev = ThrottleValue;

            // Inside UpdateThrottleInput grab start:
            lastIdxThrot = Mathf.FloorToInt(ThrottleValue * hapticSegments);
        }

        if (ThrottleGrabbing)
        {
            float totalDisplacment = Mathf.Clamp((ThrottlePositionTransfer.transform.localPosition.z - ThrottlePositoinInit.transform.localPosition.z) / ThrottleDisplacment, -1f, 1f);
            ThrottleValue = Mathf.Clamp01(ThrottleValueInit + totalDisplacment);
            ThrottleValue = FilterAxis(ThrottleValue, dt, ref ThrottlePrev, ref ThrottlePrevD);

            // Inside the if (ThrottleGrabbing) block, after ThrottleValue is filtered:
            CheckHaptic(ThrotString, ThrottleValue, ref lastIdxThrot, false);

            ThrottleVis.transform.localPosition = new Vector3(0f, ThrottleValue * 0.5f - 0.5f, 0f);
            ThrottleVis.transform.localScale = new Vector3(ThrottleValue, ThrottleVis.transform.localScale.y, ThrottleVis.transform.localScale.z);
            ThrottleGripVis.transform.localPosition = new Vector3(ThrottleGripVis.transform.localPosition.x, ThrottleGripVis.transform.localPosition.y, ThrottleValue * ThrottleDisplacment);

            ThrottleRotation.transform.localRotation = Quaternion.Euler(new Vector3(0f, 90f, -49.619f - 50.381f * ThrottleValue));
        }
    }

    private Vector3 TransHandLocalInitPos;   // Changed from World to Local
    private Quaternion TransHandLocalInitRot; // Changed from World to Local

    private Quaternion TransHandInitRot; // Store rotation at moment of grab
    void UpdateTranslationInput(float dt)
    {
        if (TransGrabbing && !TransGrabbingOld)
        {
            VRCPlayerApi.TrackingData hand = GetActiveHandData(TransString);

            TransHandLocalInitPos = TransCol.transform.InverseTransformPoint(hand.position);
            TransHandLocalInitRot = Quaternion.Inverse(TransCol.transform.rotation) * hand.rotation;

            txPrev = 0; tyPrev = 0; tzPrev = 0;
            txPrevD = 0; tyPrevD = 0; tzPrevD = 0;

            lastIdxTX = hapticSegments / 2;
            lastIdxTY = hapticSegments / 2;
            lastIdxTZ = hapticSegments / 2;
        }

        if (TransGrabbing)
        {
            VRCPlayerApi.TrackingData hand = GetActiveHandData(TransString);
            Vector3 currentLocalPos = TransCol.transform.InverseTransformPoint(hand.position);

            float rawX, rawY, rawZ;

            // Common Z-axis calculation (Always position based)
            rawZ = Mathf.Clamp((currentLocalPos.z - TransHandLocalInitPos.z) / TransSensitivity, -1f, 1f);

            if (transIsPureTranslation)
            {
                // NEW PURE TRANSLATION LOGIC
                // Calculate X and Y based on hand displacement instead of rotation
                rawX = -Mathf.Clamp((currentLocalPos.x - TransHandLocalInitPos.x) / TransSensitivity, -1f, 1f);
                rawY = Mathf.Clamp((currentLocalPos.y - TransHandLocalInitPos.y) / TransSensitivity, -1f, 1f);
            }
            else
            {
                // ORIGINAL ROTATION-BASED LOGIC
                Quaternion currentLocalRot = Quaternion.Inverse(TransCol.transform.rotation) * hand.rotation;
                Quaternion relRot = currentLocalRot * Quaternion.Inverse(TransHandLocalInitRot);

                // Pitch (Up/Down)
                Vector3 localUp = relRot * Vector3.up;
                float pitchAngle = Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg;
                rawY = Mathf.Clamp(pitchAngle / maxTiltAngle, -1.0f, 1.0f);

                // Yaw (Left/Right)
                Vector3 localForward = relRot * Vector3.forward;
                float yawAngle = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
                rawX = Mathf.Clamp(yawAngle / maxTwistAngle, -1.0f, 1.0f);
            }

            // 4. FILTERING (Same as before)
            transX = FilterAxis(rawX, dt, ref txPrev, ref txPrevD);
            transY = FilterAxis(rawY, dt, ref tyPrev, ref tyPrevD);
            transZ = FilterAxis(rawZ, dt, ref tzPrev, ref tzPrevD);

            CheckHaptic(TransString, transX, ref lastIdxTX, true);
            CheckHaptic(TransString, transY, ref lastIdxTY, true);
            CheckHaptic(TransString, transZ, ref lastIdxTZ, true);

            // 5. VISUAL FEEDBACK (Same as before)
            // Even though we use position for input, the handle will still tilt 
            // visually based on the input values, satisfying your visual requirement.
            if (TransGripVis != null)
            {
                TransGripVis.transform.localRotation = Quaternion.Euler(transY * maxTiltAngle, transX * maxTwistAngle, 0f);
                TransGripVis.transform.localPosition = Vector3.forward * (transZ * TransSensitivity);
            }
        }
        else
        {
            transX = 0; transY = 0; transZ = 0;
            if (TransGripVis != null)
            {
                TransGripVis.transform.localRotation = Quaternion.identity;
                TransGripVis.transform.localPosition = Vector3.zero;
            }
        }
    }

    private VRCPlayerApi.TrackingData GetActiveHandData(string objName)
    {
        if (LeftObject == objName) return Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
        return Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
    }

    void UpdateJoystick()
    {
        if (isTranslation)
        {
            Vector3 InputPos = (TranslatePoint.transform.localPosition - TranslatePointInitial.transform.localPosition) * (1.0f / (TranslationScale * 0.5f));
            inputX = Mathf.Clamp(InputPos.x, -1.0f, 1.0f);
            inputZ = Mathf.Clamp(InputPos.z, -1.0f, 1.0f);
            joystickHandle.localRotation = Quaternion.Euler(inputZ * maxTiltAngle, 0, -inputX * maxTiltAngle);

            Quaternion relativeRot = TranslatePoint.transform.localRotation * Quaternion.Inverse(TranslatePointInitial.transform.localRotation);
            inputY = Mathf.Clamp(NormalizeAngle(relativeRot.eulerAngles.y) / maxTwistAngle, -1f, 1f);
            twistGrip.localRotation = Quaternion.Euler(0, inputY * maxTwistAngle, 0);
        }
        else
        {
            Quaternion diff = RotationControl.transform.localRotation * Quaternion.Inverse(InitialRotation.transform.localRotation);
            Vector3 localUp = diff * Vector3.up;
            float pitchAngle = Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg;
            float rollAngle = Mathf.Atan2(-localUp.x, localUp.y) * Mathf.Rad2Deg;

            inputZ = Mathf.Clamp(pitchAngle / maxTiltAngle, -1.0f, 1.0f);
            inputX = -Mathf.Clamp(rollAngle / maxTiltAngle, -1.0f, 1.0f);

            Vector3 localForward = diff * Vector3.forward;
            float twistAngle = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
            inputY = Mathf.Clamp(twistAngle / maxTwistAngle, -1.0f, 1.0f);

            joystickHandle.localRotation = Quaternion.Euler(inputZ * maxTiltAngle, 0, -inputX * maxTiltAngle);
            twistGrip.localRotation = Quaternion.Euler(0, inputY * maxTwistAngle, 0);
        }
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    [Header("1 Euro Filter Settings")]
    public float minCutoff = 1.0f;
    public float beta = 0.05f;
    public float dCutoff = 1.0f;

    private float xPrev, xPrevD, yPrev, yPrevD, zPrev, zPrevD;
    private float ThrottlePrev, ThrottlePrevD;
    private float txPrev, txPrevD, tyPrev, tyPrevD, tzPrev, tzPrevD; // Translation Filters

    float Alpha(float dt, float cutoff)
    {
        float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
        return 1.0f / (1.0f + tau / dt);
    }

    float FilterAxis(float value, float dt, ref float prev, ref float prevD)
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
        // Map -1 to 1 range to 0 to 1 if bi-directional (Joystick/Translation)
        float normalized = isBiDirectional ? (value + 1f) * 0.5f : value;
        int currentIdx = Mathf.Clamp(Mathf.FloorToInt(normalized * hapticSegments), 0, hapticSegments);

        if (currentIdx != lastIdx)
        {
            lastIdx = currentIdx;
            VRC_Pickup.PickupHand hand = (LeftObject == objName) ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right;
            Networking.LocalPlayer.PlayHapticEventInHand(hand, hapticDur, hapticAmp, hapticFreq);
        }
    }

    public void ApplyJoystickModeFromKnob()
    {
        // 0 = torque, 90 = rate
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