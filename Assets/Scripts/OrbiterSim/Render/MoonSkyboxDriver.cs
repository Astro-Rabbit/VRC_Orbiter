using UdonSharp;
using UnityEngine;

/// <summary>
/// MoonSkyboxDriver_OptionB
/// OPTION B: Shader rotates the skybox by _SkyQ each frame.
/// 
/// We therefore pass body parameters in a *pre-sky-rotation* Unity-inertial basis ("UInert"):
/// - UInert is Unity world basis but aligned to heliocentric inertial axes via qBasis (H->U).
/// - The shader applies _SkyQ to the view ray (or equivalently rotates the skybox) so the craft stays fixed in Unity.
///
/// Inputs (ALL in heliocentric/ecliptic H frame):
/// - craft position r_craft_H (meters) from CraftStateModel
/// - craft attitude q_C2H (body -> H) from CraftAttitudeState.qBE
/// - moon position r_moon_H (meters) from BodyCatalog/EphemSnapshot
/// - moon attitude q_M2H (body-fixed -> H) from BodyCatalog.GetBodyFixedToInertial(moonId)
///
/// Outputs (Unity coordinates, PRE-sky-rotation):
/// - _SkyQ                : quaternion to rotate skybox/world in shader (Unity WS)
/// - _MoonCenterUInert    : craft->moon vector in UInert (meters)
/// - _MoonBodyToUInertQ   : body-fixed -> UInert rotation (xyzw)
/// - _MoonRadiusWS        : meters (same)
/// - _SunDirUInert        : unit vector "to Sun" in UInert
///
/// Notes:
/// - Constant basis map qBasis = RotX(-90°) converts H(+Z up) -> Unity(+Y up).
/// - Sky rotation: qSky = inverse( qBasis * qC2H * qBasis^-1 ).
/// - We DO NOT apply qSky to moon center/orientation; shader does that by rotating view ray.
/// </summary>
public class MoonSkyboxDriver : UdonSharpBehaviour
{
    [Header("References")]
    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;
    public BodyCatalog bodies;

    [Header("Material to drive (leave null to use RenderSettings.skybox)")]
    public Material skyboxMat;

    [Header("Body IDs")]
    public byte sunBodyId = 0;
    public byte moonBodyId = 2;

    [Header("Update")]
    public bool driveEveryFrame = true;

    // Constant basis: H(+Z up) -> Unity(+Y up)
    // RotX(-90°): X_U = X_H, Y_U = Z_H, Z_U = -Y_H
    private Quaternion qBasis;
    private Quaternion qBasisInv;


    void Start()
    {
        // Constant basis: H(+Z up) -> Unity(+Y up)
        // RotX(-90°): X_U = X_H, Y_U = Z_H, Z_U = -Y_H
        Quaternion qBasis = Quaternion.AngleAxis(-90f, Vector3.up) * Quaternion.AngleAxis(-90f, Vector3.right);

        qBasisInv = Quaternion.Inverse(qBasis);


    }

    void LateUpdate()
    {
        if (!driveEveryFrame) return;
        Drive();
    }

    public void Drive()
    {
        Material m = (skyboxMat != null) ? skyboxMat : RenderSettings.skybox;
        if (m == null || craft == null || craftAtt == null || bodies == null) return;

        // -------------------------
        // Craft state (H)
        // -------------------------
        Vector3 rCraft_H = new Vector3((float)craft.rx, (float)craft.ry, (float)craft.rz);
        Quaternion qC2H  = craftAtt.qBE; // body -> H

        // Craft attitude converted into Unity inertial-basis
        Quaternion qC2U_inert = qBasis * qC2H * qBasisInv;

        // Skybox/world rotation applied in shader
        Quaternion qSky = Quaternion.Inverse(qC2U_inert);

        // -------------------------
        // Moon position (H) -> craft->moon (H) -> UInert
        // -------------------------
        double mx, my, mz;
        bodies.GetBodyPos(moonBodyId, out mx, out my, out mz);
        Vector3 rMoon_H = new Vector3((float)mx, (float)my, (float)mz);

        Vector3 rRel_H = rMoon_H - rCraft_H;        // craft -> moon
        Vector3 rRel_UInert = qBasis * rRel_H;      // H -> UInert (NO qSky)

        // -------------------------
        // Moon orientation (body-fixed -> H) -> UInert (NO qSky)
        // -------------------------
        Quaternion qM2H = bodies.GetBodyFixedToInertial(moonBodyId); // body -> H
        Quaternion qM2U_inert = qBasis * qM2H * qBasisInv;

        // -------------------------
        // Sun direction (to Sun) in UInert (NO qSky)
        // -------------------------
        double sx, sy, sz;
        bodies.GetBodyPos(sunBodyId, out sx, out sy, out sz);
        Vector3 rSun_H = new Vector3((float)sx, (float)sy, (float)sz);

        Vector3 toSun_H = rSun_H - rCraft_H;
        Vector3 toSun_UInert = qBasis * toSun_H;
        Vector3 toSun_UInert_hat = (toSun_UInert.sqrMagnitude > 1e-12f) ? toSun_UInert.normalized : Vector3.up;

        // -------------------------
        // Radius
        // -------------------------
        float moonRadius = (float)bodies.GetRadius(moonBodyId);

        // -------------------------
        // Push to shader
        // -------------------------
        m.SetVector("_SkyQ", new Vector4(qSky.x, qSky.y, qSky.z, qSky.w));
        m.SetVector("_MoonCenterUInert", new Vector4(rRel_UInert.x, rRel_UInert.y, rRel_UInert.z, 1f));
        m.SetFloat("_MoonRadiusWS", moonRadius);
        m.SetVector("_MoonBodyToUInertQ", new Vector4(qM2U_inert.x, qM2U_inert.y, qM2U_inert.z, qM2U_inert.w));
        m.SetVector("_SunDirUInert", new Vector4(toSun_UInert.x, toSun_UInert.y, toSun_UInert.z, 0f));
    }
}