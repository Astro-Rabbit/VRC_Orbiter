using UdonSharp;
using UnityEngine;

// Enum is fine here at the top
public enum SpaceCraftProbeShape
{
    Sphere = 0,
    Box = 1
}

public class SpaceCraftContactProbe : UdonSharpBehaviour
{
    [Header("Shape")]
    public SpaceCraftProbeShape shape = SpaceCraftProbeShape.Sphere;
    public SphereCollider sphere;
    public BoxCollider box;

    [Header("Parent Ship")]
    public SpaceCraftState myState;

    [Header("Material")]
    [Range(0f, 2f)] public float mu = 0.5f;
    [Range(0f, 1f)] public float restitution = 0.2f;

    [Header("Filtering")]
    public LayerMask collideMask = -1;

    [HideInInspector] public Vector3 colliderLocalPos;
    [HideInInspector] public Quaternion colliderLocalRot;
    [HideInInspector] public Vector3 colliderLossyScale;

    private void Start()
    {
        // These use typeof because they are built-in Unity types (legal in Udon)
        if (shape == SpaceCraftProbeShape.Sphere && sphere == null)
            sphere = (SphereCollider)GetComponent(typeof(SphereCollider));
        if (shape == SpaceCraftProbeShape.Box && box == null)
            box = (BoxCollider)GetComponent(typeof(BoxCollider));

        // FIXED: Use generics for user-defined types
        if (myState == null)
            myState = GetComponentInParent<SpaceCraftState>();
    }

    public Collider GetCollider()
    {
        return (shape == SpaceCraftProbeShape.Sphere) ? (Collider)sphere : (Collider)box;
    }

    public void CacheLocal(Transform craftRoot)
    {
        Collider c = GetCollider();
        if (c == null || craftRoot == null) return;

        Transform t = c.transform;
        colliderLocalPos = craftRoot.InverseTransformPoint(t.position);
        colliderLocalRot = Quaternion.Inverse(craftRoot.rotation) * t.rotation;
        colliderLossyScale = t.lossyScale;
    }
}