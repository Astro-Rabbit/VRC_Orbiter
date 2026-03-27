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


}