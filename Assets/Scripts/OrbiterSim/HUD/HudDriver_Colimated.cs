using UdonSharp;
using UnityEngine;

public class HudDriver_Colimated : UdonSharpBehaviour
{
    [Header("References")]
    public Material hudMat;
    public GuidanceNavCoreState nav;

    [Header("HUD config")]
    [Tooltip("0=OFF, 1=GROUND, 2=ORBIT, 3=DOCK")]
    public byte hudMode = 2;

    [Tooltip("Angular half-width of HUD in body-frame radians.")]
    public float hudHalfFovX = 0.25f;

    [Tooltip("Angular half-height of HUD in body-frame radians.")]
    public float hudHalfFovY = 0.18f;

    [Header("Debug / fallback")]
    public bool useFallbackIfInvalid = true;

    [Tooltip("Fallback prograde dir in body frame.")]
    public Vector3 fallbackPrograde_B = new Vector3(0f, 0f, 1f);

    [Tooltip("Fallback radial-out dir in body frame.")]
    public Vector3 fallbackRadialOut_B = new Vector3(1f, 0f, 0f);

    [Tooltip("Fallback normal dir in body frame.")]
    public Vector3 fallbackNormal_B = new Vector3(0f, 1f, 0f);

    private void Update()
    {
        if (hudMat == null) return;

        // Always push basic HUD controls
        hudMat.SetFloat("_HudMode", (float)hudMode);
        hudMat.SetFloat("_HudHalfFovX", hudHalfFovX);
        hudMat.SetFloat("_HudHalfFovY", hudHalfFovY);

        Vector3 prograde_B = fallbackPrograde_B;
        Vector3 radialOut_B = fallbackRadialOut_B;
        Vector3 normal_B = fallbackNormal_B;

        bool haveNav = (nav != null && nav.valid);

        if (haveNav)
        {
            Quaternion qBE = nav.qBE;
            Quaternion qEB = new Quaternion(-qBE.x, -qBE.y, -qBE.z, qBE.w); // inverse for unit quaternion

            Vector3 that_E = nav.That_E;
            Vector3 rhat_E = nav.Rhat_E;
            Vector3 nhat_E = nav.Nhat_E;

            bool thatOk = that_E.sqrMagnitude > 1e-8f;
            bool rhatOk = rhat_E.sqrMagnitude > 1e-8f;
            bool nhatOk = nhat_E.sqrMagnitude > 1e-8f;

            if (thatOk)
            {
                prograde_B = qEB * that_E;
                if (prograde_B.sqrMagnitude > 1e-8f) prograde_B.Normalize();
                else thatOk = false;
            }

            if (rhatOk)
            {
                radialOut_B = qEB * rhat_E;
                if (radialOut_B.sqrMagnitude > 1e-8f) radialOut_B.Normalize();
                else rhatOk = false;
            }

            if (nhatOk)
            {
                normal_B = qEB * nhat_E;
                if (normal_B.sqrMagnitude > 1e-8f) normal_B.Normalize();
                else nhatOk = false;
            }

            // If any direction failed and fallback is disabled, keep previous defaults only if allowed.
            if (!useFallbackIfInvalid)
            {
                if (!thatOk) prograde_B = Vector3.forward;
                if (!rhatOk) radialOut_B = Vector3.right;
                if (!nhatOk) normal_B = Vector3.up;
            }
        }
        else if (useFallbackIfInvalid)
        {
            if (fallbackPrograde_B.sqrMagnitude > 1e-8f) prograde_B = fallbackPrograde_B.normalized;
            else prograde_B = Vector3.forward;

            if (fallbackRadialOut_B.sqrMagnitude > 1e-8f) radialOut_B = fallbackRadialOut_B.normalized;
            else radialOut_B = Vector3.right;

            if (fallbackNormal_B.sqrMagnitude > 1e-8f) normal_B = fallbackNormal_B.normalized;
            else normal_B = Vector3.up;
        }

        hudMat.SetVector("_ProgradeDir_B", new Vector4(prograde_B.x, prograde_B.y, prograde_B.z, 0f));
        hudMat.SetVector("_RadialOutDir_B", new Vector4(radialOut_B.x, radialOut_B.y, radialOut_B.z, 0f));
        hudMat.SetVector("_NormalDir_B", new Vector4(normal_B.x, normal_B.y, normal_B.z, 0f));
    }
}