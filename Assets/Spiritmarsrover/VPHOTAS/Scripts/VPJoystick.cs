
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

public class VPJoystick : UdonSharpBehaviour
{
    [Header("Hierarchy")]
    public Transform joystickHandle; // Child of Base (Pitch/Roll)
    public Transform twistGrip;      // Child of Handle (Twist)
    public Transform handProxy;

    [Header("Limits")]
    public float maxTiltAngle = 30f;
    public float maxTwistAngle = 45f;
    public bool JoystickisGrabbed = false;

    //private Quaternion initialHandRotation;
    //private Quaternion initialHandleRotation;
    //private Quaternion initialGripRotation;

    //public GameObject RotationInitial;
    //public GameObject OnlyXZ;

    public GameObject TranlatePointForRotation;

    public bool isTranslation = false;
    public float TranslationScale = 0.25f;
    public GameObject TranslatePoint;
    public GameObject TranslatePointInitial;
    public GameObject Yout;
    public GameObject XZout;
    public GameObject RotationControl;
    public GameObject InitialRotation;
    private void Start()
    {
        ////Debug in editor
        //if (Networking.LocalPlayer.IsUserInVR())
        //{

        //}
        //else
        //{
        //    isGrabbed = !isGrabbed;
        //    if (isGrabbed)
        //    {
        //        TranslatePointInitial.transform.position = TranslatePoint.transform.position;
        //        TranslatePointInitial.transform.rotation = TranslatePoint.transform.rotation;
        //        InitialRotation.transform.rotation = RotationControl.transform.rotation;
        //    }
        //}
        
    }
    float LeftGripValue = 0f;
    float RightGripValue = 0f;
    public float gripThreshold = 0.95f;
    public float letgoThreshold = 0.95f;
    public GameObject LeftGripVis;
    public GameObject RightGripVis;
    bool LeftGrabbed = false;
    bool LeftGrabbedOld = false;
    bool LeftBusy = false;
    string LeftObject = "";
    bool RightGrabbed = false;
    bool RightGrabbedOld = false;
    bool RightBusy = false;
    string RightObject = "";

    string JoyString = "Joystick";
    string ThrotString = "Throttle";

    public GameObject JoystickCol;
    public GameObject ThrottleCol;

    public float GripRadius;

    public GameObject ThrottleVis;
    public float ThrottleValue;
    public GameObject ThrottlePositionTransfer;
    public GameObject ThrottlePositoinInit;
    public GameObject ThrottleBackPosition;
    public GameObject ThrottleGripVis;
    public float ThrottleDisplacment = 0.14f;
    float ThrottleValueInit;
    public bool oldRotation = false;
    void Update()
    {
        //if (Input.GetKeyDown(KeyCode.Space))
        //{
        //    isGrabbed = !isGrabbed;
        //    if (isGrabbed)
        //    {
        //        TranslatePointInitial.transform.position = TranslatePoint.transform.position;
        //        TranslatePointInitial.transform.rotation = TranslatePoint.transform.rotation;
        //    }
        //}
        LeftGripValue = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryHandTrigger");
        RightGripValue = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryHandTrigger");
        if(LeftGripValue >= gripThreshold)
        {
            LeftGripVis.SetActive(true);
            LeftGrabbed = true;
        }
        if(LeftGripValue<letgoThreshold)
        {
            LeftGripVis.SetActive(false);
            LeftGrabbed = false;
        }

        if(LeftGrabbed == true && LeftGrabbedOld == false)
        {
            if (!LeftBusy)
            {
                if((Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position - JoystickCol.transform.position).sqrMagnitude < (GripRadius * GripRadius))
                {
                    JoystickGrabbing = true;
                    LeftObject = JoyString;//The left hand has the joystick
                    LeftBusy = true;
                }
                if ((Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position - ThrottleCol.transform.position).sqrMagnitude < (GripRadius * GripRadius))
                {
                    ThrottleGrabbing = true;
                    LeftObject = ThrotString;
                    LeftBusy = true;
                }
            }
        }
        if(LeftGrabbed == false && LeftGrabbedOld == true)
        {
            if (LeftBusy)
            {
                if(LeftObject == JoyString)
                {
                    JoystickGrabbing = false;
                    LeftObject = "";
                    LeftBusy = false;
                }
                if (LeftObject == ThrotString)
                {
                    ThrottleGrabbing = false;
                    LeftObject = "";
                    LeftBusy = false;
                }
            }
        }

        if (RightGripValue >= gripThreshold)
        {
            RightGripVis.SetActive(true);
            RightGrabbed = true;
        }
        if(RightGripValue<letgoThreshold)
        {
            RightGripVis.SetActive(false);
            RightGrabbed = false;
        }

        if (RightGrabbed == true && RightGrabbedOld == false)
        {
            if (!RightBusy)
            {
                if ((Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position - JoystickCol.transform.position).sqrMagnitude < (GripRadius * GripRadius))
                {
                    JoystickGrabbing = true;
                    RightObject = JoyString;//The right hand has the joystick
                    RightBusy = true;
                }
                if ((Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position - ThrottleCol.transform.position).sqrMagnitude < (GripRadius * GripRadius))
                {
                    ThrottleGrabbing = true;
                    RightObject = ThrotString;
                    RightBusy = true;
                }
            }
        }
        if (RightGrabbed == false && RightGrabbedOld == true)
        {
            if (RightBusy)
            {
                if (RightObject == JoyString)
                {
                    JoystickGrabbing = false;
                    RightObject = "";
                    RightBusy = false;
                }

                if (RightObject == ThrotString)
                {
                    ThrottleGrabbing = false;
                    RightObject = "";
                    RightBusy = false;
                }
            }
        }

        if(LeftObject == JoyString)
        {
            RotationControl.transform.rotation = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation;
            
            if (isTranslation)
            {
                TranslatePoint.transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
            }
        }

        if (RightObject == JoyString)
        {
            RotationControl.transform.rotation = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation;
            if (isTranslation)
            {
                TranslatePoint.transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
            }
            
        }
        //RotationControl.transform.rotation = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation;
        //TranslatePoint.transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
        TranslatePoint.transform.rotation = RotationControl.transform.rotation;
        if (JoystickGrabbing == true && JoystickGrabbingOld == false)
        {
            JoystickisGrabbed = true;
            if (JoystickisGrabbed)
            {
                TranslatePointInitial.transform.position = TranslatePoint.transform.position;
                TranslatePointInitial.transform.rotation = TranslatePoint.transform.rotation;
                InitialRotation.transform.rotation = RotationControl.transform.rotation;
                // Reset filter states to current raw input
                xPrev = inputX; yPrev = inputY; zPrev = inputZ;
                xPrevD = 0; yPrevD = 0; zPrevD = 0;
                // ... existing logic
            }
        }
        if (JoystickGrabbing == false && JoystickGrabbingOld == true)
        {
            JoystickisGrabbed = false;
            TranslatePointInitial.transform.localPosition = Vector3.zero;
            TranslatePoint.transform.localPosition = Vector3.zero;
            joystickHandle.localRotation = Quaternion.Euler(Vector3.zero);
            twistGrip.localRotation = Quaternion.Euler(Vector3.zero);
            handProxy.transform.rotation = Quaternion.Euler(Vector3.zero);
        }
        if (JoystickisGrabbed)
        {
            if (!isTranslation)
            {
                TranslatePoint.transform.position = TranlatePointForRotation.transform.position;
            }
            
            UpdateJoystick();
            
        }
        else
        {
            inputX = 0f;
            inputY = 0f;
            inputZ = 0f;

            
        }
        

        float dt = Time.deltaTime;
        if (dt <= 0) return;

        inputX = FilterAxis(inputX, dt, ref xPrev, ref xPrevD);
        inputY = FilterAxis(inputY, dt, ref yPrev, ref yPrevD);
        inputZ = FilterAxis(inputZ, dt, ref zPrev, ref zPrevD);

        //Throttle Logic
        if (LeftObject == ThrotString)
        {
            ThrottlePositionTransfer.transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
        }
        if (RightObject == ThrotString)
        {
            ThrottlePositionTransfer.transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
        }
        if (ThrottleGrabbing == true && ThrottleGrabbingOld == false)
        {
            ThrottlePositoinInit.transform.position = ThrottlePositionTransfer.transform.position;
            ThrottleValueInit = ThrottleValue;
            Debug.Log("[VPJoystick] " + "ThrottleGrabbed");
            ThrottlePrev = ThrottleValue;
            ThrottlePrevD = 0f;


        }
        if (ThrottleGrabbing == false && ThrottleGrabbingOld == true)
        {

        }
        if (ThrottleGrabbing)
        {
            float totalDisplacment = Mathf.Clamp((ThrottlePositionTransfer.transform.localPosition.z - ThrottlePositoinInit.transform.localPosition.z)/ ThrottleDisplacment,-1f,1f);
            //Debug.Log("[VPJoystick] " + "totalDisplacment: " + totalDisplacment.ToString("F2"));
            //ThrottleValue = Mathf.Clamp01((ThrottlePositionTransfer.transform.localPosition.z - ThrottleBackPosition.transform.localPosition.z)/ ThrottleDisplacment);
            ThrottleValue = Mathf.Clamp01(ThrottleValueInit + totalDisplacment);

            //1 Euro Filter for Throttle
            ThrottleValue = FilterAxis(ThrottleValue, dt, ref ThrottlePrev, ref ThrottlePrevD);

            ThrottleVis.transform.localPosition = new Vector3(0f, ThrottleValue * 0.5f - 0.5f, 0f);
            ThrottleVis.transform.localScale = new Vector3(ThrottleValue, ThrottleVis.transform.localScale.y, ThrottleVis.transform.localScale.z);
            ThrottleGripVis.transform.localPosition = new Vector3(ThrottleGripVis.transform.localPosition.x, ThrottleGripVis.transform.localPosition.y, ThrottleValue * ThrottleDisplacment);
        }
        


        JoystickGrabbingOld = JoystickGrabbing;
        RightGrabbedOld = RightGrabbed;
        LeftGrabbedOld = LeftGrabbed;
        ThrottleGrabbingOld = ThrottleGrabbing;
        //Debug.Log($"X: {inputX:F2} | Y: {inputY:F2}");
        //Debug.Log($"X: {inputX:F2} | Y: {inputY:F2} | Z: {inputZ:F2}");
        Yout.transform.localPosition = new Vector3(inputY / 2.0f, 0f, 0f);
        XZout.transform.localPosition = new Vector3(inputX / 2.0f, inputZ / 2.0f, 0f);
    }
    public float inputX = 0f;
    public float inputY = 0f;
    public float inputZ = 0f;
    void UpdateJoystick()
    {
        
        if (isTranslation)
        {
            Vector3 Input = (TranslatePoint.transform.localPosition - TranslatePointInitial.transform.localPosition)*(1.0f/(TranslationScale*0.5f));
            //Debug.Log("[VPJoystick] " + "Input.x: " + Input.x.ToString("F2") + " Input.z: " + Input.z.ToString("F2"));
            inputX = Mathf.Clamp(Input.x,-1.0f,1.0f);
            inputZ = Mathf.Clamp(Input.z, -1.0f, 1.0f);

            //float inputZ = Mathf.Clamp((NormalizeAngle(TranslatePoint.transform.localRotation.eulerAngles.y) / maxTwistAngle),-1f,1f);


            
            joystickHandle.localRotation = Quaternion.Euler(inputZ * maxTiltAngle, 0, -inputX  * maxTiltAngle);

            // Get relative rotation
            Quaternion relativeRot = TranslatePoint.transform.localRotation * Quaternion.Inverse(TranslatePointInitial.transform.localRotation);

            // Extract and normalize the Y twist
            float relativeY = NormalizeAngle(relativeRot.eulerAngles.y);
            inputY = Mathf.Clamp(relativeY / maxTwistAngle, -1f, 1f);

            twistGrip.localRotation = Quaternion.Euler(0, inputY * maxTwistAngle, 0);

            
        }
        else
        {
            //Quaternion handInHandleSpace = Quaternion.identity;
            //Quaternion initialHandInHandleSpace = Quaternion.identity;
            //Quaternion twistDelta = Quaternion.identity;


            //// --- 1. PITCH & ROLL (The Stick) ---
            //Quaternion handDelta = handProxy.rotation * Quaternion.Inverse(initialHandRotation);
            //Vector3 targetWorldUp = handDelta * (transform.rotation * initialHandleRotation * Vector3.up);
            //Vector3 localUp = transform.InverseTransformDirection(targetWorldUp);

            //float pitch = Mathf.Clamp(Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg, -maxTiltAngle, maxTiltAngle);
            //float roll = Mathf.Clamp(Mathf.Atan2(-localUp.x, localUp.y) * Mathf.Rad2Deg, -maxTiltAngle, maxTiltAngle);

            //// Apply Pitch/Roll to the Handle
            //joystickHandle.localRotation = Quaternion.Euler(pitch, 0, roll);

            //// --- 2. TWIST (The Grip) ---
            //// Get hand rotation relative to the CURRENT rotation of the handle
            //handInHandleSpace = Quaternion.Inverse(joystickHandle.rotation) * handProxy.rotation;
            //// Get the difference between current hand orientation and the starting orientation
            //// (This isolates the twist movement of the wrist)
            //initialHandInHandleSpace = Quaternion.Inverse(initialHandleRotation) * initialHandRotation;
            //twistDelta = handInHandleSpace * Quaternion.Inverse(initialHandInHandleSpace);


            //float twistYaw = NormalizeAngle(twistDelta.eulerAngles.y);
            //twistYaw = Mathf.Clamp(twistYaw, -maxTwistAngle, maxTwistAngle);

            //// Apply Twist to the child Grip object
            //twistGrip.localRotation = Quaternion.Euler(0, twistYaw, 0);

            //// --- 3. OUTPUT ---
            //float inputX = roll / maxTiltAngle;
            //float inputY = pitch / maxTiltAngle;
            //float inputZ = twistYaw / maxTwistAngle;
            //Quaternion relativeRot = handProxy.transform.localRotation * Quaternion.Inverse(RotationInitial.transform.localRotation);

            ////float relativeX = NormalizeAngle(relativeRot.eulerAngles.x);
            ////float inputX = Mathf.Clamp(relativeX / maxTiltAngle, -1f, 1f);

            ////float relativeZ = NormalizeAngle(relativeRot.eulerAngles.z);
            ////float inputZ = Mathf.Clamp(relativeZ / maxTiltAngle, -1f, 1f);
            //Vector3 tiltDir = relativeRot * Vector3.up;
            //float maxRange = Mathf.Sin(maxTiltAngle * Mathf.Deg2Rad);
            //float inputX = Mathf.Clamp(tiltDir.x / maxRange, -1f, 1f);
            //float inputZ = Mathf.Clamp(tiltDir.z / maxRange, -1f, 1f);


            //Quaternion OnlyXZRotation = Quaternion.Euler(relativeRot.eulerAngles.x, 0.0f, relativeRot.eulerAngles.z);

            //OnlyXZ.transform.localRotation = OnlyXZRotation;

            //Quaternion relativeTwist = handProxy.transform.localRotation * Quaternion.Inverse(OnlyXZRotation);

            //float relativeY = NormalizeAngle(relativeTwist.eulerAngles.y);
            //float inputY = Mathf.Clamp(relativeY / maxTwistAngle, -1f, 1f);

            //Debug.Log($"X: {inputX:F2} | Y: {inputY:F2} | Z: {inputZ:F2}");
            //joystickHandle.localRotation = Quaternion.Euler(inputZ * maxTiltAngle, 0, inputX * maxTiltAngle);
            //twistGrip.localRotation = Quaternion.Euler(0, inputY * maxTwistAngle, 0);
            if (oldRotation)
            {
                Quaternion diff = RotationControl.transform.localRotation * Quaternion.Inverse(InitialRotation.transform.localRotation);
                handProxy.transform.localRotation = diff;


                Vector3 Input = (TranslatePoint.transform.localPosition - TranslatePointInitial.transform.localPosition) * (1.0f / (TranslationScale * 0.5f));

                inputX = Mathf.Clamp(Input.x, -1.0f, 1.0f);
                inputZ = Mathf.Clamp(Input.z, -1.0f, 1.0f);

                //float inputZ = Mathf.Clamp((NormalizeAngle(TranslatePoint.transform.localRotation.eulerAngles.y) / maxTwistAngle),-1f,1f);



                joystickHandle.localRotation = Quaternion.Euler(inputZ * maxTiltAngle, 0, -inputX * maxTiltAngle);

                // Get relative rotation
                //Quaternion relativeRot = handProxy.transform.localRotation * Quaternion.Inverse(TranslatePointInitial.transform.localRotation);

                // Extract and normalize the Y twist
                float relativeY = NormalizeAngle(diff.eulerAngles.y);
                inputY = Mathf.Clamp(relativeY / maxTwistAngle, -1f, 1f);

                twistGrip.localRotation = Quaternion.Euler(0, inputY * maxTwistAngle, 0);

                //Debug.Log($"X: {inputX:F2} | Y: {inputY:F2}");
            }
            else
            {
                //Quaternion diff = RotationControl.transform.localRotation * Quaternion.Inverse(InitialRotation.transform.localRotation);

                //inputX = Mathf.Clamp(diff.eulerAngles.x / maxTiltAngle, -1.0f, 1.0f);
                //inputZ = Mathf.Clamp(diff.eulerAngles.z / maxTiltAngle, -1.0f, 1.0f);
                //inputY = Mathf.Clamp(diff.eulerAngles.y / maxTiltAngle, -1.0f, 1.0f);

                //joystickHandle.localRotation = Quaternion.Euler(inputX * maxTiltAngle, inputY * maxTiltAngle, inputZ * maxTiltAngle);

                // 1. Get the rotation difference from the moment of grabbing
                Quaternion diff = RotationControl.transform.localRotation * Quaternion.Inverse(InitialRotation.transform.localRotation);

                // 2. CALCULATE PITCH AND ROLL (The Swing)
                // We see where the Hand's "Up" axis is pointing relative to the base
                Vector3 localUp = diff * Vector3.up;

                // Atan2 gives us the angle in radians, then convert to degrees
                // Pitch: How far the 'Up' vector leans toward the Z axis (forward/back)
                float pitchAngle = Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg;
                // Roll: How far the 'Up' vector leans toward the X axis (left/right)
                float rollAngle = Mathf.Atan2(-localUp.x, localUp.y) * Mathf.Rad2Deg;

                inputZ = Mathf.Clamp(pitchAngle / maxTiltAngle, -1.0f, 1.0f);
                inputX = -Mathf.Clamp(rollAngle / maxTiltAngle, -1.0f, 1.0f);

                // 3. CALCULATE TWIST (The Yaw)
                // We look at where the Hand's "Forward" axis is pointing
                Vector3 localForward = diff * Vector3.forward;

                // Twist: Rotation around the stick's vertical axis
                float twistAngle = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;

                inputY = Mathf.Clamp(twistAngle / maxTwistAngle, -1.0f, 1.0f);

                // 4. APPLY TO VISUALS
                // Apply Pitch and Roll to the main handle
                joystickHandle.localRotation = Quaternion.Euler(inputZ * maxTiltAngle, 0, -inputX * maxTiltAngle);

                // Apply Twist to the grip (child of the handle)
                twistGrip.localRotation = Quaternion.Euler(0, inputY * maxTwistAngle, 0);
            }



        }
        

    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }
    public bool JoystickGrabbing = false;
    bool JoystickGrabbingOld = false;
    public bool ThrottleGrabbing = false;
    public bool ThrottleGrabbingOld = false;
    //public float grabRadius;
    //private VRCPlayerApi.TrackingDataType activeHand;
    //public override void InputGrab(bool value, UdonInputEventArgs args)
    //{
    //    if (value) // Pressed
    //    {
    //        VRCPlayerApi.TrackingDataType hand = (args.handType == HandType.LEFT) ?
    //            VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;

    //        Vector3 handPos = Networking.LocalPlayer.GetTrackingData(hand).position;
    //        // Check squared distance for performance
    //        if ((handPos - transform.position).sqrMagnitude < (grabRadius * grabRadius))
    //        {
    //            activeHand = hand;
    //            Grabbing = true;
    //            // Reset 1 Euro filter prev values here...
    //        }
    //        if(args.handType == HandType.LEFT)
    //        {

    //        }
    //        else
    //        {

    //        }
    //        if(value && args.handType == HandType.RIGHT)
    //        {

    //        }
    //    }
    //    else if (Grabbing) // Released
    //    {
    //        Grabbing = false;
    //    }
    //}

    [Header("1 Euro Filter Settings")]
    public float minCutoff = 1.0f; // Decrease to reduce jitter
    public float beta = 0.05f;      // Increase to reduce lag
    public float dCutoff = 1.0f;   // Usually 1.0

    private float xPrev, xPrevD, yPrev, yPrevD, zPrev, zPrevD;

    private float ThrottlePrev, ThrottlePrevD;

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
