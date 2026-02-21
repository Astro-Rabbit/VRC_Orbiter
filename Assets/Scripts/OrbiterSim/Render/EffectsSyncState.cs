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