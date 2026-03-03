using UdonSharp;
using UnityEngine;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StewartPlatformController : UdonSharpBehaviour
{
    public Transform target;
    public Transform topPlate;

    [Header("Mechanical Limits")]
    public float maxRadius = 0.5f;
    public float maxTiltAngle = 20f;
    public float minHeight = 0.1f;
    public float restHeight = 0.3f;

    [Header("IDSS Style Logic")]
    public float activationAngle = 15f;
    public float moveSpeed = 0.5f;
    public float rotationSpeed = 45f;
    public float transitionSpeed = 2.0f; // How fast it "grabs" the target (0 to 1)

    private Vector3 localRestPos;
    private float targetWeight = 0f; // 0 = At Rest, 1 = Tracking Target

    [Header("Port")]
    [Range(0.0f,2.0f)]
    public int portnum = 0;//0 is null
    public GameObject DockingTarget1;
    public GameObject DockingTarget2;
    public bool backlimit;
    void Start()
    {
        localRestPos = new Vector3(0, restHeight, 0);
        // Initialize plate at rest to prevent start-up snap
        topPlate.localPosition = localRestPos;
        topPlate.localRotation = Quaternion.identity;
    }

    void Update()
    {
        switch (portnum)
        {
            case 0:
                target = null;
                break;
            case 1:
                target = DockingTarget1.transform;
                break;
            case 2:
                target = DockingTarget2.transform;
                break;
        }
        // 1. EVALUATE TARGET VALIDITY
        bool isTargetValid = false;
        Vector3 clampedLocalTargetPos = localRestPos;
        Quaternion clampedLocalTargetRot = Quaternion.identity;

        if (target != null)
        {
            Quaternion targetLocalRot = Quaternion.Inverse(transform.rotation) * target.rotation;
            float targetTilt = Quaternion.Angle(Quaternion.identity, targetLocalRot);

            if (targetTilt <= activationAngle)
            {
                isTargetValid = true;

                // Calculate where the platform *would* go if fully engaged
                Vector3 targetLocalPos = transform.InverseTransformPoint(target.position);
                targetLocalPos.y = Mathf.Max(targetLocalPos.y, minHeight);

                float t = Mathf.InverseLerp(minHeight, maxRadius, targetLocalPos.y);
                float currentRadius = Mathf.Lerp(0.1f, maxRadius, t);

                Vector2 horiz = Vector2.ClampMagnitude(new Vector2(targetLocalPos.x, targetLocalPos.z), currentRadius);
                targetLocalPos.x = horiz.x; targetLocalPos.z = horiz.y;

                clampedLocalTargetPos = Vector3.ClampMagnitude(targetLocalPos, maxRadius);
                clampedLocalTargetRot = Quaternion.RotateTowards(Quaternion.identity, targetLocalRot, maxTiltAngle);
            }
        }

        // 2. SMOOTH THE INFLUENCE (The Fix for Snapping)
        // Transition targetWeight towards 1 if valid, towards 0 if invalid
        targetWeight = Mathf.MoveTowards(targetWeight, isTargetValid ? 1f : 0f, transitionSpeed * Time.deltaTime);

        // Interpolate the actual Goal between Rest and Clamped Target
        Vector3 finalGoalPos = Vector3.Lerp(localRestPos, clampedLocalTargetPos, targetWeight);
        Quaternion finalGoalRot = Quaternion.Slerp(Quaternion.identity, clampedLocalTargetRot, targetWeight);

        // 3. DRIVE MECHANICS
        float dist = Vector3.Distance(topPlate.localPosition, finalGoalPos);
        float ang = Quaternion.Angle(topPlate.localRotation, finalGoalRot);

        if (dist > 0.001f || ang > 0.01f)
        {
            topPlate.localPosition = Vector3.MoveTowards(topPlate.localPosition, finalGoalPos, moveSpeed * Time.deltaTime);
            topPlate.localRotation = Quaternion.RotateTowards(topPlate.localRotation, finalGoalRot, rotationSpeed * Time.deltaTime);
        }
        backlimit = topPlate.localPosition.y <= (minHeight + 0.001f);
    }
}