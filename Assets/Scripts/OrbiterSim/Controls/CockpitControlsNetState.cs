using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// CockpitControlsNetState
///
/// Single-seat synced visual-state carrier.
///
/// IMPORTANT:
/// - Use ONE instance per seat.
/// - This is for cockpit-control VISUALS only.
/// - It does NOT drive craft input.
/// - The player currently manipulating this seat can temporarily own this object
///   and publish quantized visual state for everyone else to see.
///
/// Why single-seat?
/// - Left and right seats may be manipulated by different players at the same time.
/// - A single shared net object cannot support that cleanly because it only has one owner.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CockpitControlsNetState : UdonSharpBehaviour
{
    public const byte FLAG_CLAIMED      = 1 << 0;
    public const byte FLAG_GRABBING_ANY = 1 << 1;
    public const byte FLAG_ACTIVE_SEAT  = 1 << 2;

    [Header("Publish")]
    [Tooltip("Visual-state publish cadence for this seat.")]
    public float publishHz = 15f;

    [Header("Read-only mirrors")]
    public sbyte joyX;
    public sbyte joyY;
    public sbyte joyZ;

    public byte throttle01;

    public sbyte transX;
    public sbyte transY;
    public sbyte transZ;

    [Tooltip("Bitfield using FLAG_* constants above.")]
    public byte flags;

    [UdonSynced] private int _rev;

    [UdonSynced] private sbyte _joyX;
    [UdonSynced] private sbyte _joyY;
    [UdonSynced] private sbyte _joyZ;

    [UdonSynced] private byte _throttle01;

    [UdonSynced] private sbyte _transX;
    [UdonSynced] private sbyte _transY;
    [UdonSynced] private sbyte _transZ;

    [UdonSynced] private byte _flags;

    private float _accum;
    private int _appliedRev = -1;

    private float Period
    {
        get
        {
            if (publishHz <= 0f) return 999999f;
            return 1f / publishHz;
        }
    }

    void Start()
    {
        ApplyMirrorsFromSynced();
    }

    void Update()
    {
        // This script does not invent state on its own.
        // It only rate-limits serialization after another script has written synced values.
        PublishIfDue();
    }

    /// <summary>
    /// Write the latest local visual state into this seat's synced payload.
    /// Caller is responsible for owning this net object first.
    /// </summary>
    public void SetLocalVisualState(
        float inJoyX, float inJoyY, float inJoyZ,
        float inThrottle01,
        float inTransX, float inTransY, float inTransZ,
        bool claimed,
        bool grabbingAny,
        bool activeSeat)
    {
        _joyX = QuantizeSigned01(inJoyX);
        _joyY = QuantizeSigned01(inJoyY);
        _joyZ = QuantizeSigned01(inJoyZ);

        _throttle01 = QuantizeUnsigned01(inThrottle01);

        _transX = QuantizeSigned01(inTransX);
        _transY = QuantizeSigned01(inTransY);
        _transZ = QuantizeSigned01(inTransZ);

        byte f = 0;
        if (claimed)     f |= FLAG_CLAIMED;
        if (grabbingAny) f |= FLAG_GRABBING_ANY;
        if (activeSeat)  f |= FLAG_ACTIVE_SEAT;

        _flags = f;
    }

    /// <summary>
    /// Rate-limited publish. Safe to call every frame.
    /// </summary>
    public void PublishIfDue()
    {
        if (!Networking.IsOwner(gameObject)) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;
        _accum = 0f;

        ForcePublish();
    }

    /// <summary>
    /// Immediate publish for state changes that should be seen quickly.
    /// </summary>
    public void ForcePublish()
    {
        if (!Networking.IsOwner(gameObject)) return;

        _rev++;
        ApplyMirrorsFromSynced();
        RequestSerialization();
        _appliedRev = _rev;
    }

    /// <summary>
    /// True if the current synced payload differs from the last public mirror values.
    /// Used so callers can force-publish only on meaningful state changes.
    /// </summary>
    public bool HasPendingVisualChange()
    {
        if (_joyX != joyX) return true;
        if (_joyY != joyY) return true;
        if (_joyZ != joyZ) return true;

        if (_throttle01 != throttle01) return true;

        if (_transX != transX) return true;
        if (_transY != transY) return true;
        if (_transZ != transZ) return true;

        if (_flags != flags) return true;

        return false;
    }
    public override void OnDeserialization()
    {
        ApplyMirrorsFromSynced();

        if (_rev == _appliedRev) return;
        _appliedRev = _rev;
    }

    /// <summary>
    /// Copies synced storage into inspector-visible mirrors.
    /// These mirrors are also what playback readers use.
    /// </summary>
    private void ApplyMirrorsFromSynced()
    {
        joyX = _joyX;
        joyY = _joyY;
        joyZ = _joyZ;

        throttle01 = _throttle01;

        transX = _transX;
        transY = _transY;
        transZ = _transZ;

        flags = _flags;
    }

    // ----------------------------
    // Playback readers (dequantized)
    // ----------------------------

    public float GetJoyX() { return DequantizeSigned01(joyX); }
    public float GetJoyY() { return DequantizeSigned01(joyY); }
    public float GetJoyZ() { return DequantizeSigned01(joyZ); }

    public float GetThrottle01()
    {
        return DequantizeUnsigned01(throttle01);
    }

    public float GetTransX() { return DequantizeSigned01(transX); }
    public float GetTransY() { return DequantizeSigned01(transY); }
    public float GetTransZ() { return DequantizeSigned01(transZ); }

    public bool IsClaimed()
    {
        return (flags & FLAG_CLAIMED) != 0;
    }

    public bool IsGrabbingAny()
    {
        return (flags & FLAG_GRABBING_ANY) != 0;
    }

    public bool IsActiveSeat()
    {
        return (flags & FLAG_ACTIVE_SEAT) != 0;
    }

    // ----------------------------
    // Quantization helpers
    // ----------------------------

    private static sbyte QuantizeSigned01(float v)
    {
        v = Mathf.Clamp(v, -1f, 1f);
        int q = Mathf.RoundToInt(v * 127f);
        q = Mathf.Clamp(q, -127, 127);
        return (sbyte)q;
    }

    private static float DequantizeSigned01(sbyte q)
    {
        return Mathf.Clamp((float)q / 127f, -1f, 1f);
    }

    private static byte QuantizeUnsigned01(float v)
    {
        v = Mathf.Clamp01(v);
        int q = Mathf.RoundToInt(v * 255f);
        q = Mathf.Clamp(q, 0, 255);
        return (byte)q;
    }

    private static float DequantizeUnsigned01(byte q)
    {
        return (float)q / 255f;
    }
}