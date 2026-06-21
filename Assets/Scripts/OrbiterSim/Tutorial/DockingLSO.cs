using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// DockingLSO — Landing Signal Officer callout system.
///
/// Becomes active once RendezvousTutorial.finalApproachReady is true
/// AND the player is inside approachFinal range (~200m / 250m gate).
///
/// Priority order (highest → lowest):
///   1. Capture       (Contact / Capture / Soft-lock)
///   2. Too Fast      (Slow down / Watch that speed!)
///   3. Rotation      (Rotation out of alignment)
///   4. Velocity      (Approach X m/s — periodic timer)
///   5. Distance      (60m … 1m — threshold, repeating on cooldown)
///   6. Directions    (Up / Down / Left / Right — continuous, cooldown)
///   7. Lateral align (Horizontal good / Vertical good)
///   8. Too Slow      (*yawns* you're going too slow)
///
/// Audio clip arrays are assigned in the Inspector.
/// All clips must be assigned in order — see [Header] labels.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DockingLSO : UdonSharpBehaviour
{
    // ──────────────────────────────────────────────────────────────
    //  REFERENCES
    // ──────────────────────────────────────────────────────────────

    [Header("References")]
    public MFDDockingPage dockingPage;
    public DockingController dockingController;
    //public RendezvousTutorial tutorial;
    public AudioSource audioSource;

    // ──────────────────────────────────────────────────────────────
    //  THRESHOLDS  (tweak in Inspector)
    // ──────────────────────────────────────────────────────────────

    [Header("Active Zone")]
    [Tooltip("LSO only speaks once the player is inside this range (meters).")]
    public float activeRangeMeters = 250f;

    [Header("Speed Thresholds")]
    [Tooltip("Closing speed above this triggers Too-Fast warning.")]
    public float tooFastThreshold = 0.5f;
    [Tooltip("Closing speed below this triggers Too-Slow warning.")]
    public float tooSlowThreshold = 0.05f;

    [Header("Alignment Thresholds (meters)")]
    [Tooltip("Lateral X offset above this counts as misaligned horizontally.")]
    public float lateralXThreshold = 0.5f;
    [Tooltip("Lateral Y offset above this counts as misaligned vertically.")]
    public float lateralYThreshold = 0.5f;

    [Header("Rotation Threshold (degrees)")]
    public float rotationThreshold = 2.0f;

    [Header("Cooldowns (seconds)")]
    public float distanceCooldown = 5f;
    public float directionCooldown = 8f;
    public float tooFastCooldown = 10f;
    public float tooSlowCooldown = 7f;
    public float rotationCooldown = 6f;
    public float velocityCalloutInterval = 25f;   // periodic velocity callout

    // ──────────────────────────────────────────────────────────────
    //  AUDIO CLIPS
    //  Assign every slot in the Inspector — order matters!
    // ──────────────────────────────────────────────────────────────

    [Header("Direction Clips  [0=Up  1=Down  2=Right  3=Left]")]
    public AudioClip[] directionClips;   // length 4

    [Header("Too-Fast Clips  [0='Slow down.'  1='Watch that speed!']")]
    public AudioClip[] tooFastClips;     // length 2

    [Header("Too-Slow Clips  [0='*yawns* you're going too slow.']")]
    public AudioClip[] tooSlowClips;     // length 1

    [Header("Distance Clips  [0=60m  1=50m  2=40m  3=30m  4=20m  5=15m  6=10m  7=9m  8=8m  9=7m  10=6m  11=5m  12=4m  13=3m  14=2m  15=1m]")]
    public AudioClip[] distanceClips;    // length 16

    [Header("Lateral Alignment Clips  [0='Horizontal good.'  1='Vertical good.']")]
    public AudioClip[] lateralClips;     // length 2

    [Header("Rotation Alignment Clips  [0='Our rotation is out of alignment!']")]
    public AudioClip[] rotationClips;    // length 1

    [Header("Approach Velocity Clips  [0=0.25  1=0.20  2=0.15  3=0.10  4=0.30!]")]
    public AudioClip[] approachVelocityClips; // length 5

    [Header("Capture Clips  [0='Contact!'  1='Capture!'  2='We have soft-lock!']")]
    public AudioClip[] captureClips;     // length 3

    // ──────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ──────────────────────────────────────────────────────────────

    // Distance thresholds in meters, parallel to distanceClips[]
    private readonly float[] _distanceThresholds = new float[]
    {
        60f, 50f, 40f, 30f, 20f, 15f, 10f, 9f, 8f, 7f, 6f, 5f, 4f, 3f, 2f, 1f
    };

    // Approach velocity buckets (m/s), parallel to approachVelocityClips[]
    // Index 4 (0.30) is the "too fast" advisory, not a danger clip.
    private readonly float[] _velocityBuckets = new float[]
    {
        0.25f, 0.20f, 0.15f, 0.10f, 0.30f
    };

    // Per-distance-band: time when we may fire again
    //public float[] _distanceNextFireTime;

    // Cooldown timers (Time.time when next allowed)
    private float _tooFastNextTime = 0f;
    private float _tooSlowNextTime = 0f;
    private float _rotationNextTime = 0f;
    private float _directionNextTime = 0f;
    private float _velocityNextTime = 0f;

    // One-shot capture flags
    private bool _contactFired = false;
    private bool _captureFired = false;
    private bool _softLockFired = false;

    // Track last lateral-good states so we don't repeat "Horizontal good" forever
    private bool _lastHorizGood = false;
    private bool _lastVertGood = false;

    // Whether the LSO has been activated this session
    private bool _lsoActive = false;
    private float _prevRange = float.MaxValue;
    // ──────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ──────────────────────────────────────────────────────────────

    void Start()
    {
        // _distanceNextFireTime = new float[_distanceThresholds.Length];
        // for (int i = 0; i < _distanceNextFireTime.Length; i++)
        //     _distanceNextFireTime[i] = 0f;
    }

    public bool startLSO;
    void LateUpdate()
    {
        if (!startLSO)
        {
            return;
        }
        // if (dockingPage == null || tutorial == null || audioSource == null)
        //     return;

        // // Only active once tutorial says finalApproachReady
        // // AND the player has hit Continue (continueYellowGate) inside 250 m
        // if (!tutorial.finalApproachReady || !tutorial.continueYellowGate)
        // {
        //     _lsoActive = false;
        //     return;
        // }

        // Compute range the same way SelectDockingClip does
        Vector3 relPos = new Vector3(
            (float)dockingPage.contacts.dockErr_px_B0,
            (float)dockingPage.contacts.dockErr_py_B0,
            (float)dockingPage.contacts.dockErr_pz_B0);
        Vector3 portFacing = dockingPage.contacts.qTargetPortInB0 * Vector3.forward;
        float range = Vector3.Dot(relPos, -portFacing);

        // Only speak inside the active zone
        if (range > activeRangeMeters)
        {
            _lsoActive = false;
            return;
        }

        _lsoActive = true;

        float speed = (float)dockingPage.speed;
        float offsetX = (float)dockingPage.contacts.dockErr_px_B0;
        float offsetY = (float)dockingPage.contacts.dockErr_py_B0;
        float normalizedRoll = Mathf.Atan2(Mathf.Sin(dockingPage.roll), Mathf.Cos(dockingPage.roll));
        float rotErr = Mathf.Max(
            Mathf.Abs(normalizedRoll),
            Mathf.Abs(dockingPage.angleX),
            Mathf.Abs(dockingPage.angleY));

        float now = Time.time;
        // Always track range from frame to frame for threshold crossing detection
        float prevRange = _prevRange;
        _prevRange = range;


        // ── 1. CAPTURE (one-shots, highest priority, always interrupt) ──────
        if (dockingController.state == DockingState.SoftCapture && !_softLockFired)
        {
            _softLockFired = true;
            PlayImmediate(GetClip(captureClips, 2)); // "We have soft-lock!"
            return;
        }
        if (dockingController.state == DockingState.HardCapture && !_captureFired)
        {
            _captureFired = true;
            PlayImmediate(GetClip(captureClips, 1)); // "Capture!"
            startLSO = false;
            return;
        }
        // if (range < 1.5f && !_contactFired)//should ref the docking port
        // {
        //     _contactFired = true;
        //     PlayImmediate(GetClip(captureClips, 0)); // "Contact!"
        //     return;
        // }

        // For everything below, don't interrupt a currently-playing clip
        if (audioSource.isPlaying)
            return;

        // ── 2. TOO FAST ──────────────────────────────────────────────────────
        if (speed > tooFastThreshold && now >= _tooFastNextTime && range < 70f)
        {
            _tooFastNextTime = now + tooFastCooldown;
            // Alternate between "Slow down." and "Watch that speed!" randomly
            int idx = Random.Range(0, 2);
            Play(GetClip(tooFastClips, idx));
            return;
        }

        // ── 3. ROTATION ALIGNMENT ────────────────────────────────────────────
        if (rotErr > rotationThreshold && now >= _rotationNextTime)
        {
            _rotationNextTime = now + rotationCooldown;
            Play(GetClip(rotationClips, 0)); // "Our rotation is out of alignment!"
            return;
        }

        // ── 4. APPROACH VELOCITY (periodic) ─────────────────────────────────
        if (now >= _velocityNextTime && range > 1.5f)
        {
            _velocityNextTime = now + velocityCalloutInterval;
            int velIdx = GetVelocityBucketIndex(speed);
            if (velIdx >= 0)
            {
                Play(GetClip(approachVelocityClips, velIdx));
                return;
            }
        }

        // ── 5. DISTANCE ──────────────────────────────────────────────────────
        // for (int i = 0; i < _distanceThresholds.Length; i++)
        // {
        //     if (range <= _distanceThresholds[i] && now >= _distanceNextFireTime[i])
        //     {
        //         // Only fire the tightest band we're actually inside
        //         // (skip bands we've already passed further below)
        //         if (i < _distanceThresholds.Length - 1 && range <= _distanceThresholds[i + 1])
        //             continue;

        //         _distanceNextFireTime[i] = now + distanceCooldown;
        //         Play(GetClip(distanceClips, i));
        //         return;
        //     }
        // }
        for (int i = 0; i < _distanceThresholds.Length; i++)
        {
            float t = _distanceThresholds[i];
            if (range <= t && prevRange > t)
            {
                Play(GetClip(distanceClips, i));
                return;
            }
        }


        // ── 6. DIRECTIONS (lateral offset) ──────────────────────────────────
        bool xBad = Mathf.Abs(offsetX) > lateralXThreshold;
        bool yBad = Mathf.Abs(offsetY) > lateralYThreshold;

        if ((xBad || yBad) && now >= _directionNextTime)
        {
            _directionNextTime = now + directionCooldown;

            // Pick the worst axis to call first
            if (yBad && Mathf.Abs(offsetY) >= Mathf.Abs(offsetX))
            {
                // Y axis: positive = too high, negative = too low
                Play(offsetY > 0 ? GetClip(directionClips, 0) : GetClip(directionClips, 1));
            }
            else
            {
                // X axis: positive = too far right, negative = too far left
                Play(offsetX > 0 ? GetClip(directionClips, 2) : GetClip(directionClips, 3));
            }
            return;
        }

        // ── 7. LATERAL ALIGNMENT GOOD ────────────────────────────────────────
        bool horizGood = !xBad;
        bool vertGood = !yBad;

        if (horizGood && !_lastHorizGood)
        {
            _lastHorizGood = true;
            Play(GetClip(lateralClips, 0)); // "Horizontal good."
            return;
        }
        if (vertGood && !_lastVertGood)
        {
            _lastVertGood = true;
            Play(GetClip(lateralClips, 1)); // "Vertical good."
            return;
        }

        // Reset good-flags if they drift back out
        if (!horizGood) _lastHorizGood = false;
        if (!vertGood) _lastVertGood = false;

        // ── 8. TOO SLOW ──────────────────────────────────────────────────────
        if (speed < tooSlowThreshold && range > 1.5f && now >= _tooSlowNextTime)
        {
            _tooSlowNextTime = now + tooSlowCooldown;
            Play(GetClip(tooSlowClips, 0)); // "*yawns* you're going too slow."
            return;
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  HELPERS
    // ──────────────────────────────────────────────────────────────

    /// Play a clip without interrupting (caller already checked isPlaying)
    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        audioSource.clip = clip;
        audioSource.Play();
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Net_PlayAudio));
    }

    /// Play immediately, interrupting whatever is current (capture calls)
    private void PlayImmediate(AudioClip clip)
    {
        if (clip == null) return;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Net_PlayAudio));
    }

    /// Network event so all clients hear the currently-set clip
    public void Net_PlayAudio()
    {
        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();
    }

    /// Safe array accessor — returns null and logs a warning if out of range
    private AudioClip GetClip(AudioClip[] arr, int idx)
    {
        if (arr == null || idx < 0 || idx >= arr.Length)
        {
            Debug.LogWarning("[DockingLSO] Missing audio clip at index " + idx);
            return null;
        }
        return arr[idx];
    }

    /// Maps current closing speed to the closest approach-velocity bucket index.
    /// Returns -1 if no good match (speed is way off all buckets).
    private int GetVelocityBucketIndex(float speed)
    {
        // Find the bucket whose value is closest to current speed
        int best = -1;
        float bestDelta = 0.08f; // max acceptable deviation from a bucket (m/s)
        for (int i = 0; i < _velocityBuckets.Length; i++)
        {
            float delta = Mathf.Abs(speed - _velocityBuckets[i]);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }
        return best;
    }

    // ──────────────────────────────────────────────────────────────
    //  PUBLIC API  (call from other scripts / buttons if needed)
    // ──────────────────────────────────────────────────────────────

    /// Hard-reset all LSO state (e.g. when tutorial restarts)
    public void ResetLSO()
    {
        _contactFired = false;
        _captureFired = false;
        _softLockFired = false;
        _lastHorizGood = false;
        _lastVertGood = false;
        _lsoActive = false;

        float now = Time.time;
        _tooFastNextTime = now;
        _tooSlowNextTime = now;
        _rotationNextTime = now;
        _directionNextTime = now;
        _velocityNextTime = now;


        _prevRange = float.MaxValue;
        // if (_distanceNextFireTime != null)
        //     for (int i = 0; i < _distanceNextFireTime.Length; i++)
        //         _distanceNextFireTime[i] = now;

        // if (audioSource != null)
        //     audioSource.Stop();
    }

    /// Returns true if the LSO is currently active and monitoring
    public bool IsActive() => _lsoActive;
}