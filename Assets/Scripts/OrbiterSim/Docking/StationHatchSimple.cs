using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class StationHatchSimple : UdonSharpBehaviour
{
    [Header("Hatch")]
    public Transform hatchPivot;

    [Tooltip("Local Euler angles when closed.")]
    public Vector3 closedLocalEuler = Vector3.zero;

    [Tooltip("Local Euler angles when open.")]
    public Vector3 openLocalEuler = new Vector3(0f, 90f, 0f);

    [Header("Interact")]
    public bool allowInteract = true;

    [Header("Animation")]
    [Tooltip("Seconds to fully open or close.")]
    public float moveSeconds = 1.0f;

    [Tooltip("If true, uses smoothstep easing.")]
    public bool smoothMotion = true;

    [Header("Debug")]
    public bool logState = false;

    [Header("Synced State")]
    [UdonSynced] public bool isOpen = false;
    [UdonSynced] private ushort stateTick = 0;

    [Header("Runtime")]
    [Range(0f, 1f)] public float hatchPos01 = 0f;

    private bool _lastIsOpen = false;
    private ushort _lastStateTick = 0;

    private float _animFrom01 = 0f;
    private float _animTo01 = 0f;
    private float _animStartTime = 0f;
    private float _animDuration = 0f;

    private float _lastApplied01 = -1f;

    void Start()
    {
        RebuildLocalAnimation(true);
        hatchPos01 = EvaluateLocalPosition();
        ApplyTransform(true);
    }

    void Update()
    {
        if (_lastIsOpen != isOpen || _lastStateTick != stateTick)
        {
            RebuildLocalAnimation(false);
        }

        hatchPos01 = EvaluateLocalPosition();

        if (Mathf.Abs(hatchPos01 - _lastApplied01) > 0.0005f)
        {
            ApplyTransform(false);
        }
    }

    public override void Interact()
    {
        if (!allowInteract) return;

        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

        ToggleHatch();
    }

    public void ToggleHatch()
    {
        SetHatchOpen(!isOpen);
    }

    public void OpenHatch()
    {
        SetHatchOpen(true);
    }

    public void CloseHatch()
    {
        SetHatchOpen(false);
    }

    public void SetHatchOpen(bool open)
    {
        if (!Networking.IsOwner(gameObject))
            return;

        if (isOpen == open)
            return;

        // Start from current visual position so reversals look clean.
        hatchPos01 = EvaluateLocalPosition();

        _animFrom01 = hatchPos01;
        _animTo01 = open ? 1f : 0f;
        _animStartTime = Time.realtimeSinceStartup;
        _animDuration = Mathf.Max(0.0001f, Mathf.Abs(_animTo01 - _animFrom01) * moveSeconds);

        isOpen = open;
        stateTick = EncodeTick();

        _lastIsOpen = isOpen;
        _lastStateTick = stateTick;

        RequestSerialization();

        if (logState)
            Debug.Log("[StationHatchSimple] SetHatchOpen -> " + (isOpen ? "OPEN" : "CLOSED"));
    }

    public override void OnDeserialization()
    {
        RebuildLocalAnimation(false);
        hatchPos01 = EvaluateLocalPosition();
        ApplyTransform(true);
    }

    // --------------------------------------------------------------------
    // Public interlock API for craft hatch script
    // --------------------------------------------------------------------

    public bool IsStationHatchOpen()
    {
        return hatchPos01 > 0.999f || isOpen;
    }

    public bool IsStationHatchClosed()
    {
        return hatchPos01 < 0.001f && !isOpen;
    }

    public bool IsStationHatchMoving()
    {
        return hatchPos01 > 0.001f && hatchPos01 < 0.999f;
    }

    /// <summary>
    /// Craft hatch can only close when this station hatch is fully closed.
    /// </summary>
    public bool CanCraftHatchClose()
    {
        return IsStationHatchClosed();
    }

    // --------------------------------------------------------------------
    // Local animation
    // --------------------------------------------------------------------

    private void RebuildLocalAnimation(bool instant)
    {
        _lastIsOpen = isOpen;
        _lastStateTick = stateTick;

        float current = instant ? (isOpen ? 1f : 0f) : hatchPos01;
        float target = isOpen ? 1f : 0f;

        _animFrom01 = current;
        _animTo01 = target;

        if (instant)
        {
            _animStartTime = Time.realtimeSinceStartup;
            _animDuration = 0f;
        }
        else
        {
            _animStartTime = Time.realtimeSinceStartup;
            _animDuration = Mathf.Max(0.0001f, Mathf.Abs(_animTo01 - _animFrom01) * moveSeconds);
        }
    }

    private float EvaluateLocalPosition()
    {
        if (_animDuration <= 0.0001f)
            return _animTo01;

        float t = Mathf.Clamp01((Time.realtimeSinceStartup - _animStartTime) / _animDuration);

        if (smoothMotion)
            t = t * t * (3f - 2f * t); // smoothstep

        return Mathf.Lerp(_animFrom01, _animTo01, t);
    }

    private void ApplyTransform(bool force)
    {
        if (hatchPivot == null) return;

        Vector3 eul = Vector3.Lerp(closedLocalEuler, openLocalEuler, hatchPos01);
        hatchPivot.localRotation = Quaternion.Euler(eul);

        if (force || Mathf.Abs(hatchPos01 - _lastApplied01) > 0.0005f)
            _lastApplied01 = hatchPos01;
    }

    private ushort EncodeTick()
    {
        return (ushort)(Time.realtimeSinceStartup * 20f);
    }
}