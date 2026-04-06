using UdonSharp;
using UnityEngine;

/// <summary>
/// ADIDriver
///
/// V1:
/// - Drives the three ADI rate needles from CraftAttitudeState angular rates
/// - Uses body-frame angular velocity components wx/wy/wz
/// - Each needle can independently choose:
///     * which rate component it displays
///     * which local rotation axis it animates around
///     * negative / zero / positive display angles
///     * display rate limits
///     * sign inversion
///
/// Intended usage:
/// - Put this on the ADI root or panel object
/// - Assign the three needle transforms
/// - Later this same script can be extended to also drive the attitude globe
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ADIDriver : UdonSharpBehaviour
{
    public const int RATE_WX = 0;
    public const int RATE_WY = 1;
    public const int RATE_WZ = 2;

    public const int AXIS_X = 0;
    public const int AXIS_Y = 1;
    public const int AXIS_Z = 2;

    [Header("Source")]
    public CraftAttitudeState attitude;
    public GuidanceNavCoreState nav;

    [Header("Optional guidance contacts")]
    public GuidanceNavContactsState contacts;


    [Header("Mode Source")]
    public HudDriver_Colimated hudDriver;
    public bool followCopilotHud = false;

    [Header("Mode Source")]
    [Tooltip("0=OFF, 1=GROUND, 2=ORBIT, 3=APPROACH, 4=DOCK")]
    public byte hudMode = 2;

    [Header("ADI Ball Calibration")]
    public Vector3 ballEulerOffset = Vector3.zero;
    public bool invertBallRotation = false;

    [Header("Update")]
    [Tooltip("If true, convert source rad/s to deg/s before mapping to the needle scale.")]
    public bool useDegreesPerSecond = true;

    [Tooltip("If > 0, smooth the visual needle motion. 0 = no smoothing.")]
    public float smoothingSpeed = 12.0f;

    // -------------------------
    // Roll rate needle
    // -------------------------
    [Header("Roll Rate Needle")]
    public Transform rollNeedle;
    [Tooltip("0=wx, 1=wy, 2=wz")]
    public int rollRateSource = RATE_WZ;
    [Tooltip("0=local X, 1=local Y, 2=local Z")]
    public int rollRotationAxis = AXIS_Z;
    public bool invertRoll = false;

    [Tooltip("Displayed negative/full-left rate limit.")]
    public float rollMinRate = -10.0f;
    [Tooltip("Displayed positive/full-right rate limit.")]
    public float rollMaxRate = 10.0f;

    [Tooltip("Local angle at negative limit.")]
    public float rollAngleMin = -30.0f;
    [Tooltip("Local angle at zero rate.")]
    public float rollAngleZero = 0.0f;
    [Tooltip("Local angle at positive limit.")]
    public float rollAngleMax = 30.0f;

    // -------------------------
    // Pitch rate needle
    // -------------------------
    [Header("Pitch Rate Needle")]
    public Transform pitchNeedle;
    [Tooltip("0=wx, 1=wy, 2=wz")]
    public int pitchRateSource = RATE_WX;
    [Tooltip("0=local X, 1=local Y, 2=local Z")]
    public int pitchRotationAxis = AXIS_Z;
    public bool invertPitch = false;

    [Tooltip("Displayed negative/full-down rate limit.")]
    public float pitchMinRate = -10.0f;
    [Tooltip("Displayed positive/full-up rate limit.")]
    public float pitchMaxRate = 10.0f;

    [Tooltip("Local angle at negative limit.")]
    public float pitchAngleMin = -30.0f;
    [Tooltip("Local angle at zero rate.")]
    public float pitchAngleZero = 0.0f;
    [Tooltip("Local angle at positive limit.")]
    public float pitchAngleMax = 30.0f;

    // -------------------------
    // Yaw rate needle
    // -------------------------
    [Header("Yaw Rate Needle")]
    public Transform yawNeedle;
    [Tooltip("0=wx, 1=wy, 2=wz")]
    public int yawRateSource = RATE_WY;
    [Tooltip("0=local X, 1=local Y, 2=local Z")]
    public int yawRotationAxis = AXIS_Z;
    public bool invertYaw = false;

    [Tooltip("Displayed negative/full-left rate limit.")]
    public float yawMinRate = -10.0f;
    [Tooltip("Displayed positive/full-right rate limit.")]
    public float yawMaxRate = 10.0f;

    [Tooltip("Local angle at negative limit.")]
    public float yawAngleMin = -30.0f;
    [Tooltip("Local angle at zero rate.")]
    public float yawAngleZero = 0.0f;
    [Tooltip("Local angle at positive limit.")]

    public float yawAngleMax = 30.0f;



    [Header("Pitch Error Needle")]
    public Transform pitchErrorNeedle;
    public int pitchErrorRotationAxis = AXIS_Z;
    public bool invertPitchError = false;
    public float pitchErrorMinDeg = -30f;
    public float pitchErrorMaxDeg = 30f;
    public float pitchErrorAngleMin = -30f;
    public float pitchErrorAngleZero = 0f;
    public float pitchErrorAngleMax = 30f;

    [Header("Yaw Error Needle")]
    public Transform yawErrorNeedle;
    public int yawErrorRotationAxis = AXIS_Z;
    public bool invertYawError = false;
    public float yawErrorMinDeg = -30f;
    public float yawErrorMaxDeg = 30f;
    public float yawErrorAngleMin = -30f;
    public float yawErrorAngleZero = 0f;
    public float yawErrorAngleMax = 30f;

    [Header("Roll Error Needle")]
    public Transform rollErrorNeedle;
    public int rollErrorRotationAxis = AXIS_Z;
    public bool invertRollError = false;
    public float rollErrorMinDeg = -45f;
    public float rollErrorMaxDeg = 45f;
    public float rollErrorAngleMin = -30f;
    public float rollErrorAngleZero = 0f;
    public float rollErrorAngleMax = 30f;



    [Header("ADI Ball")]
    public Renderer ballRenderer;
    public string ballRotationProperty = "_BallRot";

    private Material _ballMat;

    private void Start()
    {
        if (ballRenderer != null)
            _ballMat = ballRenderer.material;
    }

    private void LateUpdate()
    {
        if (attitude == null) return;

        DriveNeedle(
            rollNeedle,
            GetRateValue(rollRateSource, invertRoll),
            rollMinRate, rollMaxRate,
            rollAngleMin, rollAngleZero, rollAngleMax,
            rollRotationAxis
        );

        DriveNeedle(
            pitchNeedle,
            GetRateValue(pitchRateSource, invertPitch),
            pitchMinRate, pitchMaxRate,
            pitchAngleMin, pitchAngleZero, pitchAngleMax,
            pitchRotationAxis
        );

        DriveNeedle(
            yawNeedle,
            GetRateValue(yawRateSource, invertYaw),
            yawMinRate, yawMaxRate,
            yawAngleMin, yawAngleZero, yawAngleMax,
            yawRotationAxis
        );


        if (_ballMat != null)
        {
            Quaternion q = ComputeBallQuaternion();

            if (invertBallRotation)
                q = Quaternion.Inverse(q);

            Quaternion qOffset = Quaternion.Euler(ballEulerOffset);
            q = q * qOffset;

            _ballMat.SetVector(
                ballRotationProperty,
                new Vector4(q.x, q.y, q.z, q.w)
            );
        }
        DriveErrorNeedles();
    }

    private float GetRateValue(int source, bool invert)
    {
        double raw = 0.0;

        if (source == RATE_WX) raw = attitude.wx;
        else if (source == RATE_WY) raw = attitude.wy;
        else raw = attitude.wz;

        float value = (float)raw;

        if (useDegreesPerSecond)
        {
            value *= Mathf.Rad2Deg;
        }

        if (invert) value = -value;
        return value;
    }

    private void DriveNeedle(
        Transform needle,
        float value,
        float minRate,
        float maxRate,
        float angleMin,
        float angleZero,
        float angleMax,
        int rotationAxis)
    {
        if (needle == null) return;

        float targetAngle = MapBipolar(value, minRate, maxRate, angleMin, angleZero, angleMax);

        Vector3 e = needle.localEulerAngles;
        float currentAngle = GetAxisAngleSigned(e, rotationAxis);
        float nextAngle;

        if (smoothingSpeed > 0.0f)
        {
            float t = 1.0f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);
            nextAngle = Mathf.LerpAngle(currentAngle, targetAngle, t);
        }
        else
        {
            nextAngle = targetAngle;
        }

        SetAxisAngle(ref e, rotationAxis, nextAngle);
        needle.localEulerAngles = e;
    }

    private float MapBipolar(
        float value,
        float minRate,
        float maxRate,
        float angleMin,
        float angleZero,
        float angleMax)
    {
        if (value <= 0.0f)
        {
            float denom = Mathf.Abs(minRate) > 1e-5f ? minRate : -1.0f;
            float t = Mathf.Clamp01(value / denom); // value and denom both negative -> 0..1
            return Mathf.Lerp(angleZero, angleMin, t);
        }
        else
        {
            float denom = Mathf.Abs(maxRate) > 1e-5f ? maxRate : 1.0f;
            float t = Mathf.Clamp01(value / denom);
            return Mathf.Lerp(angleZero, angleMax, t);
        }
    }

    private float GetAxisAngleSigned(Vector3 euler, int axis)
    {
        float a;

        if (axis == AXIS_X) a = euler.x;
        else if (axis == AXIS_Y) a = euler.y;
        else a = euler.z;

        if (a > 180.0f) a -= 360.0f;
        return a;
    }

    private void SetAxisAngle(ref Vector3 euler, int axis, float angle)
    {
        if (axis == AXIS_X) euler.x = angle;
        else if (axis == AXIS_Y) euler.y = angle;
        else euler.z = angle;
    }

    // Optional button/debug hook
    public void SnapNeedlesNow()
    {
        float oldSmoothing = smoothingSpeed;
        smoothingSpeed = 0.0f;
        LateUpdate();
        smoothingSpeed = oldSmoothing;
    }

    Quaternion ComputeBallQuaternion()
    {
        if (nav == null || !nav.valid)
            return Quaternion.identity;

        Quaternion qBE = nav.qBE;
        Quaternion qEB = Quaternion.Inverse(qBE);

        // Keep the center pointing the same as before
        Vector3 T_B = -(qEB * nav.That_E);   // prograde
        Vector3 R_B = -(qEB * nav.Rhat_E);   // radial in
        Vector3 N_B =  (qEB * nav.Nhat_E);   // normal

        if (T_B.sqrMagnitude < 1e-10f || R_B.sqrMagnitude < 1e-10f || N_B.sqrMagnitude < 1e-10f)
            return Quaternion.identity;

        T_B.Normalize();
        R_B.Normalize();
        N_B.Normalize();

        // Rebuild an orthonormal, right-handed basis.
        // We want:
        //   local +Z -> prograde
        //   local +X -> radial in
        //   therefore local +Y must be -normal for a right-handed frame
        //
        // Because in RTN: R x T = N
        // so X x Z = R x T = N
        // but Unity requires X x Y = Z, equivalently Y = Z x X = T x R = -N

        Vector3 up_B = -N_B;

        // Make sure up is orthogonal to forward
        up_B = (up_B - Vector3.Dot(up_B, T_B) * T_B).normalized;

        if (up_B.sqrMagnitude < 1e-10f)
            return Quaternion.identity;

        Quaternion qRefInBody = Quaternion.LookRotation(T_B, up_B);

        // Ball shows reference frame relative to vehicle
        return Quaternion.Inverse(qRefInBody);
    }

    private bool TryGetReferenceFrame_B(out Vector3 refForward_B, out Vector3 refUp_B)
    {
        refForward_B = Vector3.forward;
        refUp_B = Vector3.up;

        if (nav == null || !nav.valid)
            return false;

        Quaternion qEB = Quaternion.Inverse(nav.qBE);
        byte activeHudMode = GetActiveHudMode();

        if (activeHudMode == 2) // ORBIT
        {
            if (!nav.selectedNodeVectorValid) return false;

            refForward_B = qEB * nav.selectedNodeDir_E;
            if (refForward_B.sqrMagnitude < 1e-10f) return false;
            refForward_B.Normalize();

            refUp_B = qEB * nav.Nhat_E;
            refUp_B = (refUp_B - Vector3.Dot(refUp_B, refForward_B) * refForward_B);
            if (refUp_B.sqrMagnitude < 1e-10f) refUp_B = Vector3.up;
            refUp_B.Normalize();
            return true;
        }

        if (activeHudMode == 3) // APPROACH
        {
            if (contacts == null || !contacts.selValid) return false;

            refForward_B = new Vector3(
                (float)contacts.sel_drx_B,
                (float)contacts.sel_dry_B,
                (float)contacts.sel_drz_B
            );
            if (refForward_B.sqrMagnitude < 1e-10f) return false;
            refForward_B.Normalize();

            if (contacts.fullValid0)
            {
                Quaternion qTargetInB = contacts.qTargetInB0;
                refUp_B = qTargetInB * Vector3.up;
                refUp_B = (refUp_B - Vector3.Dot(refUp_B, refForward_B) * refForward_B);
                if (refUp_B.sqrMagnitude < 1e-10f) refUp_B = Vector3.up;
                refUp_B.Normalize();
            }
            else
            {
                refUp_B = Vector3.up;
            }

            return true;
        }

        if (activeHudMode == 4) // DOCK
        {
            if (contacts == null || !contacts.dockValid0) return false;

            Quaternion qTargetPortInB = contacts.qTargetPortInB0;

            refForward_B = -(qTargetPortInB * Vector3.forward);
            if (refForward_B.sqrMagnitude < 1e-10f) return false;
            refForward_B.Normalize();

            refUp_B = qTargetPortInB * Vector3.up;
            refUp_B = (refUp_B - Vector3.Dot(refUp_B, refForward_B) * refForward_B);
            if (refUp_B.sqrMagnitude < 1e-10f) return false;
            refUp_B.Normalize();

            return true;
        }

        return false;
    }


    private void DriveErrorNeedles()
    {
        Vector3 refForward_B, refUp_B;
        if (!TryGetReferenceFrame_B(out refForward_B, out refUp_B))
        {
            DriveNeedle(pitchErrorNeedle, 0f,
                pitchErrorMinDeg, pitchErrorMaxDeg,
                pitchErrorAngleMin, pitchErrorAngleZero, pitchErrorAngleMax,
                pitchErrorRotationAxis);

            DriveNeedle(yawErrorNeedle, 0f,
                yawErrorMinDeg, yawErrorMaxDeg,
                yawErrorAngleMin, yawErrorAngleZero, yawErrorAngleMax,
                yawErrorRotationAxis);

            DriveNeedle(rollErrorNeedle, 0f,
                rollErrorMinDeg, rollErrorMaxDeg,
                rollErrorAngleMin, rollErrorAngleZero, rollErrorAngleMax,
                rollErrorRotationAxis);
            return;
        }
        byte activeHudMode = GetActiveHudMode();

        // Craft boresight frame in body coordinates
        Vector3 curForward_B = Vector3.forward;
        Vector3 curUp_B = Vector3.up;
        Vector3 curRight_B = Vector3.right;

        float yawErrDeg = Mathf.Atan2(refForward_B.x, refForward_B.z) * Mathf.Rad2Deg;
        float pitchErrDeg = Mathf.Atan2(refForward_B.y, refForward_B.z) * Mathf.Rad2Deg;

        Vector3 refRight_B = Vector3.Cross(refUp_B, refForward_B).normalized;
        Vector3 refUpOrtho_B = Vector3.Cross(refForward_B, refRight_B).normalized;

        float rollErrDeg = SignedAngleAroundAxis(curUp_B, refUpOrtho_B, refForward_B);

        if (activeHudMode != 4) // only active in DOCK
        {
            rollErrDeg = 0f;
        }

        if (invertPitchError) pitchErrDeg = -pitchErrDeg;
        if (invertYawError) yawErrDeg = -yawErrDeg;
        if (invertRollError) rollErrDeg = -rollErrDeg;

        DriveNeedle(pitchErrorNeedle, pitchErrDeg,
            pitchErrorMinDeg, pitchErrorMaxDeg,
            pitchErrorAngleMin, pitchErrorAngleZero, pitchErrorAngleMax,
            pitchErrorRotationAxis);

        DriveNeedle(yawErrorNeedle, yawErrDeg,
            yawErrorMinDeg, yawErrorMaxDeg,
            yawErrorAngleMin, yawErrorAngleZero, yawErrorAngleMax,
            yawErrorRotationAxis);

        DriveNeedle(rollErrorNeedle, rollErrDeg,
            rollErrorMinDeg, rollErrorMaxDeg,
            rollErrorAngleMin, rollErrorAngleZero, rollErrorAngleMax,
            rollErrorRotationAxis);
    }

    private Vector3 GetAxisVector(int axis)
    {
        if (axis == AXIS_X) return Vector3.right;
        if (axis == AXIS_Y) return Vector3.up;
        return Vector3.forward;
    }
    private float SignedAngleAroundAxis(Vector3 from, Vector3 to, Vector3 axis)
    {
        Vector3 f = Vector3.ProjectOnPlane(from, axis).normalized;
        Vector3 t = Vector3.ProjectOnPlane(to, axis).normalized;

        if (f.sqrMagnitude < 1e-10f || t.sqrMagnitude < 1e-10f)
            return 0f;

        float ang = Vector3.Angle(f, t);
        float sign = Mathf.Sign(Vector3.Dot(axis, Vector3.Cross(f, t)));
        return ang * sign;
    }
    private byte GetActiveHudMode()
    {
        if (hudDriver == null)
            return hudMode; // fallback to local inspector value if not wired

        return followCopilotHud ? hudDriver.hudMode2 : hudDriver.hudMode;
    }

}