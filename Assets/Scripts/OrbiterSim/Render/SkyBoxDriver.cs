using UdonSharp;
using UnityEngine;
using System;
using VRC.SDKBase;


/// <summary>
/// SkyBoxDriver
/// - Drives skybox orientation from CraftAttitudeState (already validated).
/// - Drives Sun uniforms for the skybox Sun disk (Sun direction is ECLIPTIC frame directly).
/// - Also drives a DirectionalLight to match the same Sun direction.
/// 
/// Coordinate contracts:
/// - Solver/ephemeris frame: SSB / Heliocentric ECLIPTIC inertial
///     +Z = ecliptic north, +X = vernal equinox, +Y completes RHS.
/// - craftAtt.qBE is BODY -> ECLIPTIC inertial.
/// - Stars are RA/Dec baked, and any equatorial↔ecliptic correction for stars is handled IN THE SHADER.
/// - Sun position/direction is computed in ECLIPTIC frame and sent to the shader as _SunDirEcl (unit vector).
/// 
/// Notes:
/// - No Shader.PropertyToID usage (per request).
/// - Uses BodyCatalog.GetCraftToBodyVector(...) as requested.
/// - Uses doubles for ephemeris math, converts to float for shader/unity APIs.
/// </summary>
public class SkyBoxDriver : UdonSharpBehaviour
{
    [Header("Refs")]
    public CraftAttitudeState craftAtt;
    public CraftStateModel craft;
    public BodyCatalog bodies;


    public SimClock clock;
    public CraftNetState netCore;
    public CraftNetAttitude netAtt;

    [Header("Target skybox material (RenderSettings.skybox)")]
    public Material skyboxMat;

    [Header("Sun (shader + light)")]
    public bool driveSun = true;

    [Tooltip("Directional light to aim FROM the sun toward the craft (i.e., light direction = -sunDir).")]
    public Light sunLight;

    [Tooltip("If true, also updates the sun angular radius uniform each tick from distance.")]
    public bool driveSunAngularSize = true;

    [Tooltip("Solar radius in meters (for angular size).")]
    public double sunRadiusM = 6.9634e8; // meters

    [Header("Moon (sphere intersect + later texture orientation)")]
    public bool driveMoon = true;

    [Tooltip("Override Moon radius (m). If <= 0, uses BodyCatalog moonRadiusM.")]
    public double moonRadiusOverrideM = 0.0;

    

    [Header("Debug")]
    public bool writeEveryFrame = true;

    private void Start()
    {
        if (skyboxMat == null)
            skyboxMat = RenderSettings.skybox;
    }

    // private void Update()
    // {
    //     if (!writeEveryFrame) return;
    //     Tick();
    // }

    public void Tick()
    {
        if (craftAtt == null || skyboxMat == null) return;

        // -------------
        // Sky orientation
        // -------------
        // Shader expects BODY->(whatever it uses internally). We simply pass qBE and let the shader do its own
        // equatorial/ecliptic handling for stars, as per your current setup.
        Quaternion q = craftAtt.qBE;

        // Remote rendering: sample buffered attitude at a presentation time
        if (netCore != null && netAtt != null && clock != null)
        {
            if (!Networking.IsOwner(netCore.gameObject))
            {
                double tRender = clock.Now() - (double)netAtt.interpBackTimeSeconds;
                q = netAtt.SampleRenderQuaternion(tRender);
            }
        }

        skyboxMat.SetVector("_CraftBodyToEq", new Vector4(q.x, q.y, q.z, q.w));

        // -------------
        // Sun direction (ECLIPTIC)
        // -------------
        if (driveSun && bodies != null && craft != null)
        {
            // bodies.GetCraftToBodyVector gives craft->body in SSB/ECL:
            //   dx = craft - body
            // We need direction FROM craft TO sun:
            //   sunDir = (sun - craft) = -(craft - sun) = -(dx,dy,dz)
            double dx, dy, dz;
            bodies.GetCraftToBodyVector(bodies.sunId, craft, out dx, out dy, out dz);

            // craft->sun (ECL) = -(craft->sun from helper which returns craft - sun)
            double sx = -dx;
            double sy = -dy;
            double sz = -dz;

            double d2 = sx * sx + sy * sy + sz * sz;
            if (d2 > 1e-18)
            {
                double invD = 1.0 / Math.Sqrt(d2);
                float ux = (float)(sx * invD);
                float uy = (float)(sy * invD);
                float uz = (float)(sz * invD);

                // Shader hook: Sun direction in ECLIPTIC inertial
                skyboxMat.SetVector("_SunDirEcl", new Vector4(ux, uy, uz, 0f));

                // Optional: angular radius (radians) = atan(R / d)
                if (driveSunAngularSize && sunRadiusM > 0.0)
                {
                    double dist = 1.0 / invD;
                    double angRad = Math.Atan(sunRadiusM / dist);
                    skyboxMat.SetFloat("_SunAngRad", (float)angRad);
                }

                // -------------
                // Directional light
                // -------------
                // Unity directional light points along its -forward axis (by convention, it illuminates opposite forward).
                // We want light rays traveling from Sun -> craft, i.e. along (-sunDirEcl).
                // -------------
                // Directional light (WORLD == BODY in your setup)
                // -------------
                if (sunLight != null)
                {
                    // craft->sun in ECL
                    Vector3 sunDirEcl = new Vector3(ux, uy, uz);

                    // Convert to BODY/WORLD: sunDirBody = qEB * sunDirEcl
                    Quaternion qBEq = q;
                    Quaternion qEB = new Quaternion(-qBEq.x, -qBEq.y, -qBEq.z, qBEq.w); // inverse (conjugate)
                    Vector3 sunDirBody = qEB * sunDirEcl;

                    // Unity directional light emits along -transform.forward.
                    // To get light rays coming from the Sun (sun->craft = -sunDirBody),
                    // set forward to craft->sun (sunDirBody).
                    if (sunDirBody.sqrMagnitude > 1e-10f)
                        sunLight.transform.rotation = Quaternion.LookRotation(-sunDirBody, Vector3.up);
                }
            }
        }


        // -------------
        // Moon (ECLIPTIC sphere inputs)
        // -------------
        if (driveMoon && bodies != null && craft != null)
        {
            // GetCraftToBodyVector returns (craft - body) in SSB/ECL.
            // Shader expects craft->moon center (moon - craft), so NEGATE.
            double dx, dy, dz;
            bodies.GetCraftToBodyVector(bodies.moonId, craft, out dx, out dy, out dz);

            float mx = (float)(-dx);
            float my = (float)(-dy);
            float mz = (float)(-dz);

            skyboxMat.SetVector("_MoonPosEcl", new Vector4(mx, my, mz, 0f));

            double Rm = (moonRadiusOverrideM > 0.0) ? moonRadiusOverrideM : bodies.moonRadiusM;
            skyboxMat.SetFloat("_MoonRadiusM", (float)Rm);

            // Orientation hook for Stage C (albedo UV): body-fixed -> inertial ECL.
            // Do NOT conjugate here. If later the texture appears mirrored/retrograde,
            // we will fix it with ONE controlled inversion (driver OR shader), not both.
            Quaternion qMoon = bodies.GetBodyFixedToInertial(bodies.moonId);
            skyboxMat.SetVector("_MoonBodyToEcl", new Vector4(qMoon.x, qMoon.y, qMoon.z, qMoon.w));
        }



    }
}