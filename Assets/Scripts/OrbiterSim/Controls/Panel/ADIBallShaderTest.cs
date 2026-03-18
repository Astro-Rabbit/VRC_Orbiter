using UdonSharp;
using UnityEngine;

public class ADIBallShaderTest : UdonSharpBehaviour
{
    public Renderer targetRenderer;
    public string propertyName = "_BallRot";

    [Range(-180f, 180f)] public float testPitch;
    [Range(-180f, 180f)] public float testYaw;
    [Range(-180f, 180f)] public float testRoll;

    private Material _mat;

    private void Start()
    {
        if (targetRenderer != null)
            _mat = targetRenderer.material;
    }

    private void Update()
    {
        if (_mat == null) return;

        Quaternion q = Quaternion.Euler(testYaw, testPitch, testRoll);
        _mat.SetVector(propertyName, new Vector4(q.x, q.y, q.z, q.w));
    }
}