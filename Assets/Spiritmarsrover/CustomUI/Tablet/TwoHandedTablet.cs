using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TwoHandedTablet : UdonSharpBehaviour
{
    [SerializeField] private TabletPenPickup pickupL;
    [SerializeField] private TabletPenPickup pickupR;
    [SerializeField] private float lerpSpeed = 20f;

    private Vector3 _localL;
    private Vector3 _localR;
    private Quaternion _activeRotationOffset = Quaternion.identity;
    private bool _wasTwoHanded;
    private bool _wasOneHanded;

    void Start()
    {
        // Store the handle positions relative to the tablet center
        _localL = transform.InverseTransformPoint(pickupL.transform.position);
        _localR = transform.InverseTransformPoint(pickupR.transform.position);
    }

    void LateUpdate()
    {
        bool isL = pickupL.isBeingHeld;
        bool isR = pickupR.isBeingHeld;

        // Reset if totally dropped
        if (!isL && !isR)
        {
            _wasTwoHanded = false;
            _wasOneHanded = false;
            _activeRotationOffset = Quaternion.identity;
            UpdateHandlePositions(false, false);
            return;
        }

        if (isL && isR)
        {
            HandleTwoHanded();
            _wasTwoHanded = true;
            _wasOneHanded = false;
        }
        else if (isL)
        {
            HandleOneHanded(pickupL.transform, _localL);
        }
        else if (isR)
        {
            HandleOneHanded(pickupR.transform, _localR);
        }

        UpdateHandlePositions(isL, isR);
    }

    void HandleTwoHanded()
    {
        Vector3 pL = pickupL.transform.position;
        Vector3 pR = pickupR.transform.position;

        // 1. Position: Keep the tablet center at the midpoint of both hands
        transform.position = Vector3.Lerp(transform.position, (pL + pR) / 2f, Time.deltaTime * lerpSpeed);

        // 2. Rotation: Calculate rotation based on the line between the hands
        Vector3 handDir = (pR - pL).normalized;
        Vector3 handleDir = (_localR - _localL).normalized;

        // Find the rotation that aligns the handle-vector with the hand-vector
        Quaternion targetRot = Quaternion.FromToRotation(handleDir, handDir);

        // Add tilt/roll based on the average "Up" of the controllers
        Vector3 avgUp = Vector3.Slerp(pickupL.transform.up, pickupR.transform.up, 0.5f);
        targetRot = Quaternion.LookRotation(targetRot * Vector3.forward, avgUp);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lerpSpeed);
    }

    void HandleOneHanded(Transform hand, Vector3 localHandleOffset)
    {
        // CAPTURE MOMENT: If we just switched from 2-hand to 1-hand, 
        // OR if we just picked it up for the first time.
        if (_wasTwoHanded || !_wasOneHanded)
        {
            // Lock the current tablet rotation relative to the controller
            _activeRotationOffset = Quaternion.Inverse(hand.rotation) * transform.rotation;
            _wasTwoHanded = false;
            _wasOneHanded = true;
        }

        // Apply the saved relative rotation
        Quaternion targetRot = hand.rotation * _activeRotationOffset;

        // IMPORTANT: Calculate position so the HANDLE is at the hand, 
        // using the current target rotation.
        Vector3 targetPos = hand.position - (targetRot * localHandleOffset);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lerpSpeed);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * lerpSpeed);
    }

    void UpdateHandlePositions(bool isL, bool isR)
    {
        // Keep the handles attached to the tablet frame when not being held
        if (!isL)
        {
            pickupL.transform.position = transform.TransformPoint(_localL);
            pickupL.transform.rotation = transform.rotation;
        }
        if (!isR)
        {
            pickupR.transform.position = transform.TransformPoint(_localR);
            pickupR.transform.rotation = transform.rotation;
        }
    }
}