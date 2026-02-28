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

    [Header("Throttle Assets")]
    public GameObject ThrottleCol;
    public GameObject ThrottleVis;
    public GameObject ThrottlePositionTransfer;
    public GameObject ThrottlePositoinInit;
    public GameObject ThrottleBackPosition;
    public GameObject ThrottleGripVis;
    public float ThrottleDisplacment = 0.14f;
    public string ThrotString = "Throttle";

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
        }

        if (ThrottleGrabbing)
        {
            float totalDisplacment = Mathf.Clamp((ThrottlePositionTransfer.transform.localPosition.z - ThrottlePositoinInit.transform.localPosition.z) / ThrottleDisplacment, -1f, 1f);
            ThrottleValue = Mathf.Clamp01(ThrottleValueInit + totalDisplacment);
            ThrottleValue = FilterAxis(ThrottleValue, dt, ref ThrottlePrev, ref ThrottlePrevD);

            ThrottleVis.transform.localPosition = new Vector3(0f, ThrottleValue * 0.5f - 0.5f, 0f);
            ThrottleVis.transform.localScale = new Vector3(ThrottleValue, 1, 1);
            ThrottleGripVis.transform.localPosition = new Vector3(0, 0, ThrottleValue * ThrottleDisplacment);
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

            // Store hand's position/rotation RELATIVE to the controller's transform
            TransHandLocalInitPos = TransCol.transform.InverseTransformPoint(hand.position);
            TransHandLocalInitRot = Quaternion.Inverse(TransCol.transform.rotation) * hand.rotation;

            txPrev = 0; tyPrev = 0; tzPrev = 0;
            txPrevD = 0; tyPrevD = 0; tzPrevD = 0;
        }

        if (TransGrabbing)
        {
            VRCPlayerApi.TrackingData hand = GetActiveHandData(TransString);

            // 1. Get current hand pose in Local Space of the controller
            Vector3 currentLocalPos = TransCol.transform.InverseTransformPoint(hand.position);
            Quaternion currentLocalRot = Quaternion.Inverse(TransCol.transform.rotation) * hand.rotation;

            // 2. POSITION (Z-axis / Push-Pull)
            // This is now craft-rotation independent because it's purely local delta
            float rawZ = Mathf.Clamp((currentLocalPos.z - TransHandLocalInitPos.z) / TransSensitivity, -1f, 1f);

            // 3. ROTATION (Pitch/Yaw for X/Y translation)
            // Calculate rotation delta relative to the moment you grabbed the handle
            Quaternion relRot = currentLocalRot * Quaternion.Inverse(TransHandLocalInitRot);

            // Pitch (Up/Down)
            Vector3 localUp = relRot * Vector3.up;
            float pitchAngle = Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg;
            float rawY = Mathf.Clamp(pitchAngle / maxTiltAngle, -1.0f, 1.0f);

            // Yaw (Left/Right)
            Vector3 localForward = relRot * Vector3.forward;
            float yawAngle = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
            float rawX = Mathf.Clamp(yawAngle / maxTwistAngle, -1.0f, 1.0f);

            // 4. FILTERING
            transX = FilterAxis(rawX, dt, ref txPrev, ref txPrevD);
            transY = FilterAxis(rawY, dt, ref tyPrev, ref tyPrevD);
            transZ = FilterAxis(rawZ, dt, ref tzPrev, ref tzPrevD);

            // 5. VISUAL FEEDBACK
            if (TransGripVis != null)
            {
                TransGripVis.transform.localRotation = Quaternion.Euler(transY * maxTiltAngle, transX * maxTwistAngle, 0);
                // Visual feedback uses the calculated transZ for displacement
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
}