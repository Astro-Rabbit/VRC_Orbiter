using UdonSharp;
using UnityEngine;


public enum AeroProbeShape
{
    Sphere = 0,
    Box = 1
}
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AeroContactProbe : UdonSharpBehaviour
{
    [Header("Shape")]
    public AeroProbeShape shape = AeroProbeShape.Sphere;

    [Tooltip("Use for Sphere shape")]
    public SphereCollider sphere;

    [Tooltip("Use for Box shape")]
    public BoxCollider box;

    [Header("Material")]
    [Range(0f, 2f)] public float mu = 0.8f;
    [Range(0f, 1f)] public float restitution = 0.0f;

    [Header("Filtering")]
    public LayerMask collideMask = -1;

    [Header("Debug")]
    public bool debugDraw = false;

    // ---- cached relative-to-craft pose ----
    [HideInInspector] public Vector3 colliderLocalPos;     // craftRoot local
    [HideInInspector] public Quaternion colliderLocalRot;  // craftRoot local
    [HideInInspector] public Vector3 colliderLossyScale;   // cached at authoring pose

    public CraftState craftState;

    [Header("Special Effects")]
    public bool useRelativeDrag = false;
    [Range(0f, 1f)] public float dragStrength = 0.1f; // 1.0 = instant snap, 0.1 = smooth damping

    private void Start()
    {
        // convenience auto-wire
        if (shape == AeroProbeShape.Sphere && sphere == null)
            sphere = (SphereCollider)GetComponent(typeof(SphereCollider));

        if (shape == AeroProbeShape.Box && box == null)
            box = (BoxCollider)GetComponent(typeof(BoxCollider));
    }

    public Collider GetCollider()
    {
        if (shape == AeroProbeShape.Sphere) return sphere;
        return box;
    }

    public void CacheLocal(Transform craftRoot)
    {
        Collider c = GetCollider();
        if (c == null || craftRoot == null) return;

        Transform t = c.transform;

        colliderLocalPos = craftRoot.InverseTransformPoint(t.position);
        colliderLocalRot = Quaternion.Inverse(craftRoot.rotation) * t.rotation;
        colliderLossyScale = t.lossyScale; // capture authoring scale (assume static)
    }

    public void GetWorldColliderPose(Vector3 craftPosWS, Quaternion craftRotWS, out Vector3 posWS, out Quaternion rotWS)
    {
        posWS = craftPosWS + craftRotWS * colliderLocalPos;
        rotWS = craftRotWS * colliderLocalRot;
    }

    public float GetMaxAbsScale()
    {
        float ax = Mathf.Abs(colliderLossyScale.x);
        float ay = Mathf.Abs(colliderLossyScale.y);
        float az = Mathf.Abs(colliderLossyScale.z);
        float m = ax;
        if (ay > m) m = ay;
        if (az > m) m = az;
        return m;
    }
}
