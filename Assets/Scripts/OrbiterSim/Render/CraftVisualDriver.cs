using UdonSharp;
using UnityEngine;

public class CraftVisualDriver : UdonSharpBehaviour
{
    [Header("State refs")]
    public CraftControlState control;
    public CraftAttitudeState att;

    [Header("Visual root (the object to rotate)")]
    public Transform craftRoot;

    [Header("Body axes -> Mesh axes alignment (optional)")]
    public Quaternion bodyToMesh = Quaternion.identity;

    [Header("Main engine visuals")]
    public ParticleSystem[] mainEngineParticles;
    public Light mainEngineLight;
    public float maxEmissionRate = 200f;
    public float maxLightIntensity = 5f;

    [Header("Debug")]
    public bool drawAxes = true;
    public float axisLen = 1.0f;

    void Start()
    {
        if (craftRoot == null) craftRoot = transform;
    }

    void LateUpdate()
    {
        if (control == null || att == null || craftRoot == null) return;

        // Authoritative attitude from sim:
        craftRoot.rotation = att.qBE * bodyToMesh;

        // Throttle -> engine FX
        float t = Mathf.Clamp01(control.throttle01);
        DriveMainEngineFX(t);

        if (drawAxes)
        {
            Vector3 p = craftRoot.position;
            Debug.DrawLine(p, p + craftRoot.right   * axisLen, Color.red);
            Debug.DrawLine(p, p + craftRoot.up      * axisLen, Color.green);
            Debug.DrawLine(p, p + craftRoot.forward * axisLen, Color.blue);
        }
    }

    private void DriveMainEngineFX(float throttle01)
    {
        if (mainEngineParticles != null)
        {
            for (int i = 0; i < mainEngineParticles.Length; i++)
            {
                ParticleSystem ps = mainEngineParticles[i];
                if (ps == null) continue;

                var em = ps.emission;
                em.rateOverTime = maxEmissionRate * throttle01;

                if (throttle01 > 0.01f) { if (!ps.isPlaying) ps.Play(); }
                else { if (ps.isPlaying) ps.Stop(); }
            }
        }

        if (mainEngineLight != null && maxLightIntensity > 0f)
        {
            mainEngineLight.intensity = maxLightIntensity * throttle01;
            mainEngineLight.enabled = throttle01 > 0.01f;
        }
    }
}
