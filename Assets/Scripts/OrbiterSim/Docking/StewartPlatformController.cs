using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StewartPlatformController : UdonSharpBehaviour
{
    [Header("External target")]
    [Tooltip("Assigned by another script (e.g. selected station port resolver).")]
    public Transform target;

    [Header("Deployment / activation")]
    [Tooltip("If false, the platform retracts to rest and ignores target tracking.")]
    public bool platformEnabled = false;

    [Header("References")]
    public Transform topPlate;

    [Header("Mechanical Limits")]
    public float maxRadius = 0.5f;
    public float maxTiltAngle = 180;
    public float minHeight = 0.1f;
    public float restHeight = 0.3f;

    [Header("Tracking Logic")]
    [Tooltip("Target must be within this angular tilt relative to the platform base to be considered trackable.")]
    public float activationAngle = 180f;

    [Tooltip("Linear speed of the plate moving toward its goal.")]
    public float moveSpeed = 0.5f;

    [Tooltip("Angular speed of the plate rotating toward its goal (deg/sec).")]
    public float rotationSpeed = 45f;

    [Tooltip("How fast the platform blends between rest and target-tracking (0..1 influence/sec).")]
    public float transitionSpeed = 2.0f;

    [Header("State / outputs")]
    [Tooltip("True when the top plate is effectively at the rear/compressed travel limit.")]
    public bool backlimit;

    [Tooltip("0 = fully at rest, 1 = fully tracking target.")]
    [Range(0f, 1f)]
    public float targetWeight = 0f;

    [Header("Debug")]
    public bool logState = false;

    private Vector3 _localRestPos;
    private bool _lastValid = false;

    void Start()
    {
        _localRestPos = new Vector3(0f, restHeight, 0f);

        if (topPlate != null)
        {
            topPlate.localPosition = _localRestPos;
            topPlate.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        if (topPlate == null) return;

        // 1) Evaluate whether target tracking is currently valid
        bool isTargetValid = false;
        Vector3 clampedLocalTargetPos = _localRestPos;
        Quaternion clampedLocalTargetRot = Quaternion.identity;

        if (platformEnabled && target != null)
        {
            Quaternion targetLocalRot = Quaternion.Inverse(transform.rotation) * target.rotation;
            float targetTilt = Quaternion.Angle(Quaternion.identity, targetLocalRot);

            if (targetTilt <= activationAngle)
            {
                isTargetValid = true;

                // Desired top-plate target in platform local space
                Vector3 targetLocalPos = transform.InverseTransformPoint(target.position);

                // Respect minimum extension height
                targetLocalPos.y = Mathf.Max(targetLocalPos.y, minHeight);

                // Radius can vary with height
                float t = Mathf.InverseLerp(minHeight, maxRadius, targetLocalPos.y);
                float currentRadius = Mathf.Lerp(0.1f, maxRadius, t);

                Vector2 horiz = Vector2.ClampMagnitude(
                    new Vector2(targetLocalPos.x, targetLocalPos.z),
                    currentRadius
                );

                targetLocalPos.x = horiz.x;
                targetLocalPos.z = horiz.y;

                // Final local target pos clamped to overall max radius
                clampedLocalTargetPos = Vector3.ClampMagnitude(targetLocalPos, maxRadius);

                // Clamp rotation relative to platform base
                clampedLocalTargetRot = Quaternion.RotateTowards(
                    Quaternion.identity,
                    targetLocalRot,
                    maxTiltAngle
                );
            }
        }

        // 2) Smooth tracking influence:
        //    enabled+valid -> blend toward 1
        //    disabled/invalid -> blend toward 0
        targetWeight = Mathf.MoveTowards(
            targetWeight,
            isTargetValid ? 1f : 0f,
            transitionSpeed * Time.deltaTime
        );

        // 3) Blend actual goal between rest and tracked target
        Vector3 finalGoalPos = Vector3.Lerp(_localRestPos, clampedLocalTargetPos, targetWeight);
        Quaternion finalGoalRot = Quaternion.Slerp(Quaternion.identity, clampedLocalTargetRot, targetWeight);

        // 4) Drive plate mechanics
        float dist = Vector3.Distance(topPlate.localPosition, finalGoalPos);
        float ang = Quaternion.Angle(topPlate.localRotation, finalGoalRot);

        if (dist > 0.001f || ang > 0.01f)
        {
            topPlate.localPosition = Vector3.MoveTowards(
                topPlate.localPosition,
                finalGoalPos,
                moveSpeed * Time.deltaTime
            );

            topPlate.localRotation = Quaternion.RotateTowards(
                topPlate.localRotation,
                finalGoalRot,
                rotationSpeed * Time.deltaTime
            );
        }

        // 5) Output / convenience state
        backlimit = topPlate.localPosition.y <= (minHeight + 0.001f);

        if (logState && isTargetValid != _lastValid)
        {
            Debug.Log("[StewartPlatformController] target valid = " + isTargetValid + ", enabled = " + platformEnabled);
            _lastValid = isTargetValid;
        }
    }
}