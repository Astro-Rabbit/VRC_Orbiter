using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletController : UdonSharpBehaviour
{
    [Header("References")]
    public TabletHandle handleL;
    public TabletHandle handleR;
    public float lerpSpeed = 20f;

    [Header("Configuration")]
    // If your tablet mesh is sideways when holding with two hands, 
    // adjust this. Usually (0, -90, 0) for side-to-side handles.
    public Vector3 twoHandedRotationOffset = new Vector3(0, -90, 0);

    private TabletPen _heldPenL;
    private TabletPen _heldPenR;

    private Vector3 _localL; // Local pos of handle L relative to tablet center
    private Vector3 _localR; // Local pos of handle R relative to tablet center
    private Quaternion _activeRotationOffset = Quaternion.identity;
    private bool _wasTwoHanded;
    private bool _isHoldingAny;

    void Start()
    {
        // Cache the 'Handle-to-Tablet' relationship
        // We use these as the "Intermediary" offsets
        _localL = transform.InverseTransformPoint(handleL.transform.position);
        _localR = transform.InverseTransformPoint(handleR.transform.position);
    }

    void LateUpdate()
    {
        _heldPenL = UpdateHandState(handleL, _heldPenL);
        _heldPenR = UpdateHandState(handleR, _heldPenR);

        bool isL = _heldPenL != null;
        bool isR = _heldPenR != null;

        if (!isL && !isR)
        {
            _isHoldingAny = false;
            _wasTwoHanded = false;
            _activeRotationOffset = Quaternion.identity;
            UpdateHandleVisuals(false, false);
            return;
        }

        // Logic Switcher
        if (isL && isR)
        {
            HandleTwoHanded(_heldPenL.transform, _heldPenR.transform);
            _wasTwoHanded = true;
        }
        else if (isL)
        {
            HandleOneHanded(_heldPenL.transform, _localL);
        }
        else if (isR)
        {
            HandleOneHanded(_heldPenR.transform, _localR);
        }

        _isHoldingAny = true;
        UpdateHandleVisuals(isL, isR);
    }

    void HandleTwoHanded(Transform pL, Transform pR)
    {
        // 1. POSITION: Center of both hands
        Vector3 targetPos = (pL.position + pR.position) / 2f;

        // 2. ROTATION: LookAt + Average Roll
        Vector3 dir = (pR.position - pL.position).normalized;
        // Average the "Up" of both hands for the roll
        Vector3 avgUp = Vector3.Slerp(pL.up, pR.up, 0.5f);

        // Construct rotation: Right vector is dir, Up vector is avgUp
        Quaternion targetRot = Quaternion.LookRotation(dir, avgUp) * Quaternion.Euler(twoHandedRotationOffset);

        ApplyTransform(targetPos, targetRot);
    }

    void HandleOneHanded(Transform pen, Vector3 localHandleOffset)
    {
        // If we just released a hand or just grabbed fresh:
        // Capture how the tablet is rotated relative to the pen
        if (_wasTwoHanded || !_isHoldingAny)
        {
            _activeRotationOffset = Quaternion.Inverse(pen.rotation) * transform.rotation;
            _wasTwoHanded = false;
        }

        // 1. ROTATION: Follow pen + the offset we saved when we let go of the other hand
        Quaternion targetRot = pen.rotation * _activeRotationOffset;

        // 2. POSITION: Move tablet center so the handle mesh aligns with the pen
        // Math: TabletPos = PenPos - (TabletRot * LocalHandlePos)
        Vector3 targetPos = pen.position - (targetRot * localHandleOffset);

        ApplyTransform(targetPos, targetRot);
    }

    void ApplyTransform(Vector3 targetPos, Quaternion targetRot)
    {
        // Smoothly move the tablet to the calculated targets
        float t = Time.deltaTime * lerpSpeed;
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    void UpdateHandleVisuals(bool isL, bool isR)
    {
        // This keeps the triggers/colliders moving with the mesh 
        // when that specific hand isn't holding it.
        if (!isL)
        {
            handleL.transform.position = transform.TransformPoint(_localL);
            handleL.transform.rotation = transform.rotation;
        }
        if (!isR)
        {
            handleR.transform.position = transform.TransformPoint(_localR);
            handleR.transform.rotation = transform.rotation;
        }
    }

    private TabletPen UpdateHandState(TabletHandle handle, TabletPen currentHeld)
    {
        if (currentHeld == null)
        {
            if (handle.hoveringPen != null && handle.hoveringPen.IsGripping) return handle.hoveringPen;
            return null;
        }
        if (!currentHeld.IsGripping) return null;
        return currentHeld;
    }
}