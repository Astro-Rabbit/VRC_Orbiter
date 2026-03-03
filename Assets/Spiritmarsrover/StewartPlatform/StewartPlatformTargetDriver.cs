using UdonSharp;
using UnityEngine;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StewartPlatformTargetDriver : UdonSharpBehaviour
{
    public Transform targetToMove;
    public Transform centerReference;

    [Header("Movement Settings")]
    public float movementRadius = 1.0f;
    public float maxTargetTilt = 45f;    // Max rotation the target will reach
    public float moveSpeed = 1.0f;
    public float waitTime = 0.5f;

    private Vector3 startPos;
    private Quaternion startRot;
    private Vector3 goalPos;
    private Quaternion goalRot;

    private float lerpProgress = 1.0f;
    private float timer = 0f;

    void Start() => PickNewGoal();

    void Update()
    {
        if (lerpProgress < 1.0f)
        {
            lerpProgress += Time.deltaTime * moveSpeed;

            targetToMove.position = Vector3.Lerp(startPos, goalPos, lerpProgress);
            targetToMove.rotation = Quaternion.Slerp(startRot, goalRot, lerpProgress);
        }
        else
        {
            timer -= Time.deltaTime;
            if (timer <= 0) PickNewGoal();
        }
    }

    void PickNewGoal()
    {
        lerpProgress = 0f;
        timer = waitTime;
        startPos = targetToMove.position;
        startRot = targetToMove.rotation;

        // 1. Pick a random point in the sphere
        goalPos = centerReference.position + (Random.insideUnitSphere * movementRadius);

        // 2. Create a constrained rotation (prevents flipping/snapping)
        float rx = Random.Range(-maxTargetTilt, maxTargetTilt);
        float ry = Random.Range(-maxTargetTilt, maxTargetTilt);
        float rz = Random.Range(-maxTargetTilt, maxTargetTilt);

        // Multiply by centerReference.rotation to stay relative to the platform's "Up"
        goalRot = centerReference.rotation * Quaternion.Euler(rx, ry, rz);
    }
}