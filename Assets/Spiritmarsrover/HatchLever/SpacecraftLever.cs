using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;
using VRC.Udon.Common.Interfaces;
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class SpacecraftLever : UdonSharpBehaviour
{
    public VRCPickup handle;
    public GameObject LeverPivot;
    public GameObject LeverHandlePos;
    public DockingOpsController dockingOps;
    float handleAnlge;
    float angleOut;
    bool isHeld = false;
    public bool isRightHand;
    float lerpspeed;
    [UdonSynced]
    public bool _handlePickable;
    [UdonSynced]
    public bool isLeverOpen;

    void Start()
    {
        lerpspeed = 15f;
    }

    private void Update()
    {

        handleAnlge = Mathf.Atan2(handle.gameObject.transform.localPosition.y, handle.gameObject.transform.localPosition.z) * Mathf.Rad2Deg;
        //Debug.Log("Angle: " + handleAnlge);
        //Limits
        //if (handleAnlge < 0f)
        //{
        //    handleAnlge = 0f;
        //}
        //else if(handleAnlge > 180f)
        //{
        //    handleAnlge = 180f;
        //}
        if (handleAnlge < -90f)
        {
            handleAnlge = 180f;
        }
        else
        {
            handleAnlge = Mathf.Clamp(handleAnlge, 0f, 180f);
        }

        if (!isHeld && Networking.GetOwner(gameObject) == Networking.LocalPlayer)//or not owner?
        {
            gameObject.transform.position = LeverHandlePos.transform.position;

        }
        else
        {
            //not herustic
            //if(handleAnlge < 10f)
            //{
            //    TargetAngle = 0f;
            //}
            //else
            //{
            //    TargetAngle = -handleAnlge;
            //}
            {
                float lastTarget = TargetAngle;

                // Logic for the 0-degree end (Closed)
                if (handleAnlge <= 0f)
                {
                    TargetAngle = 0f;
                }
                else if (handleAnlge < 10f)
                {
                    // If we were already latched at 0, stay at 0.
                    // If we are approaching 0, stay at -10 until handle hits 0.
                    TargetAngle = (lastTarget == 0f) ? 0f : -10f;
                }
                // Logic for the 180-degree end (Open)
                else if (handleAnlge >= 180f)
                {
                    TargetAngle = -180f;
                }
                else if (handleAnlge > 170f)
                {
                    // If we were already latched at 180, stay at 180.
                    // If we are approaching 180, stay at -170 until handle hits 180.
                    TargetAngle = (lastTarget == -180f) ? -180f : -170f;
                }
                // Middle range
                else
                {
                    TargetAngle = -handleAnlge;
                }

                // Check if we just left or entered the 0-degree latch
                bool zeroLatchChanged = (lastTarget == 0f && TargetAngle != 0f) || (lastTarget != 0f && TargetAngle == 0f);

                // Check if we just left or entered the 180-degree latch
                bool endLatchChanged = (lastTarget == -180f && TargetAngle != -180f) || (lastTarget != -180f && TargetAngle == -180f);

                // Check if we just hit or left the resistance points (-10 or -170)
                bool resistanceChanged = (lastTarget != -10f && TargetAngle == -10f) || (lastTarget == -10f && TargetAngle != -10f)
                                      || (lastTarget != -170f && TargetAngle == -170f) || (lastTarget == -170f && TargetAngle != -170f);

                if (zeroLatchChanged || endLatchChanged || resistanceChanged)
                {
                    if (Networking.GetOwner(gameObject) == Networking.LocalPlayer)
                    {
                        TriggerHaptic(0.05f, 0.7f, 1.0f);
                    }
                    
                }
            }
        }
        angleOut = Mathf.LerpAngle(angleOut, TargetAngle, Time.deltaTime * lerpspeed);
        LeverPivot.transform.localRotation = Quaternion.Euler(angleOut, 0f, 0f);
        if (Networking.GetOwner(gameObject) == Networking.LocalPlayer)
        {
            bool prevIsLeverOpen = isLeverOpen;

            // If we push it almost to the end, it's definitely OPEN
            if (angleOut <= -175f)
            {
                isLeverOpen = true;
            }
            // If we pull it almost to the start, it's definitely CLOSED
            else if (angleOut >= -5f)
            {
                isLeverOpen = false;
            }
            // If it's anywhere in the middle, we don't change isLeverOpen. 
            // It stays whatever it was until it hits one of the boundaries above.

            if (isLeverOpen != prevIsLeverOpen)
            {
                RequestSerialization();

                if (dockingOps != null)
                {
                    if (isLeverOpen)
                        dockingOps.SendCustomNetworkEvent(NetworkEventTarget.Owner, "Net_RequestHatchOpenFromLever");
                    else
                        dockingOps.SendCustomNetworkEvent(NetworkEventTarget.Owner, "Net_RequestHatchCloseFromLever");
                }
            }
        }
    }

    public override void OnPickup()
    {
        if (Networking.GetOwner(gameObject) != Networking.LocalPlayer)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        isHeld = true;
        if (handle.currentHand == VRC_Pickup.PickupHand.Right)
        {
            isRightHand = true;
        }
        else
        {
            isRightHand = false;
        }
        lerpspeed = 15f;
    }
    float TargetAngle = 0f;
    public override void OnDrop()
    {
        //float TargetAngle = 0f;
        if (handleAnlge <= 10f)
        {
            //LeverPivot.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            TargetAngle = 0f;
            lerpspeed = 25f;
        }
        else if (handleAnlge > 170f)
        {
            TargetAngle = 180f;
            lerpspeed = 25f;
        }
        else if (handleAnlge > 10f && handleAnlge < 170f)
        {
            TargetAngle = -10f;
            lerpspeed = 1f;
        }

        //gameObject.transform.position = LeverHandlePos.transform.position;
        isHeld = false;
    }

    public void TriggerHaptic(float duration = 0.05f, float amplitude = 0.2f, float frequency = 0.8f)
    {


        VRC_Pickup.PickupHand hand = isRightHand ? VRC_Pickup.PickupHand.Right : VRC_Pickup.PickupHand.Left;
        Networking.LocalPlayer.PlayHapticEventInHand(hand, duration, amplitude, frequency);
    }

    public override void OnDeserialization()
    {
        handle.pickupable = _handlePickable;

        if (!isHeld)
        {
            TargetAngle = isLeverOpen ? -180f : 0f;
        }
    }

    //Lockout
    public void SetPickupOn()
    {
        if (_handlePickable && handle.pickupable) return;

        _handlePickable = true;
        handle.pickupable = true;

        if (Networking.GetOwner(gameObject) == Networking.LocalPlayer)
            RequestSerialization();
    }

    public void SetPickupOff()
    {
        if (!_handlePickable && !handle.pickupable) return;

        _handlePickable = false;
        handle.pickupable = false;

        if (Networking.GetOwner(gameObject) == Networking.LocalPlayer)
            RequestSerialization();
    }
}