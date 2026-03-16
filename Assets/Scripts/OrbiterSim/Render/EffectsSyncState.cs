using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class EffectsSyncState : UdonSharpBehaviour
{
    [Header("Owner write policy")]
    public bool ownerOnlyWrite = true;

    [Header("Sync rate limiting")]
    [Tooltip("Min seconds between RequestSerialization calls (0.1 = 10 Hz).")]
    public float minSendInterval = 0.15f;

    [Header("Synced masks")]
    [UdonSynced] public uint rcsHiMask;
    [UdonSynced] public uint rcsLoMask;
    [UdonSynced] public uint fxMask;
    [UdonSynced] public uint seq;

    // -------- NEW: Main engine VFX sync --------
    [Header("Main engine VFX (synced)")]
    [UdonSynced] public byte mainThrottle255;   // 0..255
    [UdonSynced] public short mainYaw_cdeg;     // centi-deg
    [UdonSynced] public short mainPitch_cdeg;   // centi-deg
    [UdonSynced] public uint mainOnMask;        // bit i => engine i on

    [UdonSynced] public short cmdTauX_dNm;
    [UdonSynced] public short cmdTauY_dNm;
    [UdonSynced] public short cmdTauZ_dNm;

    [UdonSynced] public short cmdTransX_dN;
    [UdonSynced] public short cmdTransY_dN;
    [UdonSynced] public short cmdTransZ_dN;

    private float _lastSendTime = -999f;

    public bool CanWrite()
    {
        if (!ownerOnlyWrite) return true;
        return Networking.IsOwner(gameObject);
    }

    public void SetRcsMasks(uint hi, uint lo)
    {
        if (!CanWrite()) return;

        lo = (uint)(lo & ~hi);

        bool changed = (hi != rcsHiMask) || (lo != rcsLoMask);
        if (!changed) return;

        rcsHiMask = hi;
        rcsLoMask = lo;
        seq++;

        TrySend();
    }

    public void SetFxMask(uint fx)
    {
        if (!CanWrite()) return;

        if (fx == fxMask) return;
        fxMask = fx;
        seq++;

        TrySend();
    }

    // NEW
    public void SetMainVfx(float throttle01, float yawDeg, float pitchDeg, uint onMask)
    {
        if (!CanWrite()) return;

        byte t = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(throttle01) * 255f), 0, 255);
        short yaw = (short)Mathf.Clamp(Mathf.RoundToInt(yawDeg * 100f), short.MinValue, short.MaxValue);
        short pit = (short)Mathf.Clamp(Mathf.RoundToInt(pitchDeg * 100f), short.MinValue, short.MaxValue);

        bool changed =
            t != mainThrottle255 ||
            yaw != mainYaw_cdeg ||
            pit != mainPitch_cdeg ||
            onMask != mainOnMask;

        if (!changed) return;

        mainThrottle255 = t;
        mainYaw_cdeg = yaw;
        mainPitch_cdeg = pit;
        mainOnMask = onMask;
        seq++;

        TrySend();
    }


    public void SetCommandReadout(Vector3 tauCmd_B, Vector3 transCmd_B)
    {
        if (!CanWrite()) return;

        short tx = (short)Mathf.Clamp(Mathf.RoundToInt(tauCmd_B.x * 10f), short.MinValue, short.MaxValue);
        short ty = (short)Mathf.Clamp(Mathf.RoundToInt(tauCmd_B.y * 10f), short.MinValue, short.MaxValue);
        short tz = (short)Mathf.Clamp(Mathf.RoundToInt(tauCmd_B.z * 10f), short.MinValue, short.MaxValue);

        short fx = (short)Mathf.Clamp(Mathf.RoundToInt(transCmd_B.x * 10f), short.MinValue, short.MaxValue);
        short fy = (short)Mathf.Clamp(Mathf.RoundToInt(transCmd_B.y * 10f), short.MinValue, short.MaxValue);
        short fz = (short)Mathf.Clamp(Mathf.RoundToInt(transCmd_B.z * 10f), short.MinValue, short.MaxValue);

        bool changed =
            tx != cmdTauX_dNm ||
            ty != cmdTauY_dNm ||
            tz != cmdTauZ_dNm ||
            fx != cmdTransX_dN ||
            fy != cmdTransY_dN ||
            fz != cmdTransZ_dN;

        if (!changed) return;

        cmdTauX_dNm = tx;
        cmdTauY_dNm = ty;
        cmdTauZ_dNm = tz;

        cmdTransX_dN = fx;
        cmdTransY_dN = fy;
        cmdTransZ_dN = fz;

        seq++;
        TrySend();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!CanWrite()) return;

        // New owner republishes current state immediately.
        _lastSendTime = -999f;
        RequestSerialization();
    }


    private void TrySend()
    {
        float now = Time.time;
        if (minSendInterval <= 0f || (now - _lastSendTime) >= minSendInterval)
        {
            _lastSendTime = now;
            RequestSerialization();
        }
    }
}