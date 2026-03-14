using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

//
// SimClock (network-ready, single-vessel multiplayer friendly)
//
// Fixes in this version:
// 1) PublishEpochNow anchors to the *derived* sim time from the current epoch mapping (not the potentially stale simTime).
// 2) _revision sentinel: starts at -1 meaning "no valid epoch yet".
// 3) Heartbeat rebroadcasts WITHOUT re-anchoring and WITHOUT bumping revision.
// 4) Optional: deterministic Now() vs smoothed NowVisual() (smoothing never affects simulation time).
//
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SimClock : UdonSharpBehaviour
{
    [Header("Mode")]
    [Tooltip("If true, derive simTime from VRChat server time and a synced epoch (recommended for multiplayer).")]
    public bool useNetworkTime = true;

    [Tooltip("If false, simTime advances locally via StepLocal(dt). Useful for offline testing.")]
    public bool allowLocalStepping = false;

    [Header("Warp")]
    [Tooltip("Global time scale (warp). Shared across clients via synced epoch/scale. 1 = realtime.")]
    public double timeScale = 1.0;

    [Header("State (read-only)")]
    [Tooltip("Current mission time T (seconds). In network mode, this is derived each frame from server time.")]
    public double simTime;

    [Tooltip("Last delta in simTime computed by Update().")]
    public double lastSimDt;

    [Header("Sync / Maintenance")]
    [Tooltip("Optional: owner rebroadcasts epoch/scale every N seconds. 0 disables. Does NOT change epoch/revision.")]
    public float heartbeatSeconds = 0f;

    [Tooltip("Optional visual smoothing for non-owners (seconds of correction per second). 0 disables.")]
    public float slewRate = 0f;


    [Header("Render-time cache (read-only)")]
    public double cachedRenderTimeNet;
    public double cachedRenderBackTime;
    public int cachedRenderFrame = -1;

    // --- Synced epoch mapping (late joiners get this automatically) ---
    [UdonSynced] private double _epochServerTime; // server seconds at epoch
    [UdonSynced] private double _epochSimTime;    // sim seconds at epoch
    [UdonSynced] private double _timeScale;       // warp at epoch
    [UdonSynced] private int _revision = -1;      // -1 means "no epoch yet"

    // Local bookkeeping
    private int _appliedRevision = -2;            // distinct from -1 sentinel
    private float _hbAccum = 0f;

    void Start()
    {
        // Initialize local copies from inspector defaults
        _timeScale = timeScale;

        // Owner should publish an initial epoch so late joiners have a stable reference immediately.
        if (useNetworkTime && Networking.IsOwner(gameObject))
        {
            PublishEpochNow(); // will set revision from -1 -> 0
        }
    }

    void Update()
    {
        double prev = simTime;

        if (useNetworkTime)
        {
            // If we haven't received an epoch yet (late joiner before first deserialization),
            // hold at current simTime until we do.
            if (!Networking.IsOwner(gameObject))
            {
                if (_revision >= 0 && _revision != _appliedRevision)
                {
                    ApplySyncedEpoch(hardSet: (slewRate <= 0f));
                }
            }

            if (_revision >= 0) // only derive time if we have a valid epoch mapping
            {
                double serverNow = Networking.GetServerTimeInSeconds();
                double target = _epochSimTime + (serverNow - _epochServerTime) * _timeScale;

                // IMPORTANT:
                // - simTime is used by NowVisual() and by anyone who (incorrectly) reads simTime directly.
                // - Now() returns deterministic derived time (no slew).
                if (!Networking.IsOwner(gameObject) && slewRate > 0f)
                {
                    double dtReal = (double)Time.deltaTime;
                    double maxAdjust = (double)slewRate * dtReal;
                    double err = target - simTime;
                    if (err >  maxAdjust) err =  maxAdjust;
                    if (err < -maxAdjust) err = -maxAdjust;
                    simTime += err;
                }
                else
                {
                    simTime = target;
                }
            }
        }
        else if (allowLocalStepping)
        {
            StepLocal(Time.deltaTime);
        }

        lastSimDt = simTime - prev;

        // Optional heartbeat: rebroadcast the current synced values WITHOUT re-anchoring.
        if (useNetworkTime && heartbeatSeconds > 0f && Networking.IsOwner(gameObject))
        {
            _hbAccum += Time.deltaTime;
            if (_hbAccum >= heartbeatSeconds)
            {
                _hbAccum = 0f;
                Rebroadcast();
            }
        }
    }

    public void UpdateRemoteRenderTimeCache(double backTimeSeconds)
    {
        cachedRenderBackTime = backTimeSeconds;
        cachedRenderTimeNet = Networking.GetServerTimeInSeconds() - backTimeSeconds;
        cachedRenderFrame = Time.frameCount;
    }

    public double GetCachedRemoteRenderTime()
    {
        return cachedRenderTimeNet;
    }

    /// <summary>
    /// Deterministic mission time (seconds). Use this for ephemeris + conics.
    /// Returns derived time directly from the epoch mapping (no slew).
    /// </summary>
    public double Now()
    {
        if (!useNetworkTime) return simTime;

        if (_revision < 0)
        {
            // No epoch yet; best we can do.
            return simTime;
        }

        double serverNow = Networking.GetServerTimeInSeconds();
        return _epochSimTime + (serverNow - _epochServerTime) * _timeScale;
    }

    /// <summary>
    /// Smoothed mission time (seconds), intended for visual-only consumers.
    /// If slewRate == 0, this equals simTime / Now().
    /// </summary>
    public double NowVisual()
    {
        return simTime;
    }

    public double NowNetwork()
    {
        return Networking.GetServerTimeInSeconds();
    }

    public double GetRemoteRenderTime(double backTimeSeconds)
    {
        return Networking.GetServerTimeInSeconds() - backTimeSeconds;
    }

    /// <summary>
    /// Local stepping (offline/testing): simTime += dtReal * timeScale.
    /// </summary>
    public void StepLocal(double dtReal)
    {
        if (dtReal < 0.0) dtReal = 0.0;
        simTime += (double)dtReal * timeScale;
    }

    /// <summary>
    /// Owner-only: set a new global time scale (warp) in a way that does NOT jump time.
    /// This re-anchors the epoch at the current moment and syncs to everyone.
    /// </summary>
    public void SetTimeScale(double newScale)
    {
        if (newScale < 0.0) newScale = 0.0;

        timeScale = newScale;

        if (!useNetworkTime) return;
        if (!Networking.IsOwner(gameObject)) return;

        PublishEpochNow(); // re-anchors using derived sim-now, and syncs
    }

    /// <summary>
    /// Owner-only: publish a fresh epoch mapping so everyone derives identical mission time.
    /// Anchors to the *derived* sim time at serverNow (not the possibly stale simTime).
    /// This bumps revision.
    /// </summary>
    public void PublishEpochNow()
    {
        if (!useNetworkTime) return;
        if (!Networking.IsOwner(gameObject)) return;

        double serverNow = Networking.GetServerTimeInSeconds();

        // Derive sim-now from the CURRENT mapping if we already have one,
        // otherwise use current simTime (e.g. first publish).
        double simNowDerived = simTime;
        if (_revision >= 0)
        {
            simNowDerived = _epochSimTime + (serverNow - _epochServerTime) * _timeScale;
        }

        _epochServerTime = serverNow;
        _epochSimTime = simNowDerived;
        _timeScale = timeScale;

        if (_revision < 0) _revision = 0;
        else _revision++;

        // Apply locally immediately
        _appliedRevision = _revision;

        // Update local simTime immediately (visual)
        simTime = simNowDerived;

        RequestSerialization();
    }

    /// <summary>
    /// Owner-only: rebroadcast current synced values WITHOUT changing epoch mapping or revision.
    /// Useful as a heartbeat against packet loss.
    /// </summary>
    public void Rebroadcast()
    {
        if (!useNetworkTime) return;
        if (!Networking.IsOwner(gameObject)) return;
        if (_revision < 0) return;

        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        if (!useNetworkTime) return;
        if (_revision < 0) return;

        if (_revision != _appliedRevision)
        {
            ApplySyncedEpoch(hardSet: (slewRate <= 0f));
        }
    }

    public double ServerTimeForSimTime(double simT)
    {
        if (!useNetworkTime)
            return Networking.GetServerTimeInSeconds();

        if (_revision < 0)
            return Networking.GetServerTimeInSeconds();

        // Avoid divide-by-zero / nonsense when paused at warp 0.
        if (System.Math.Abs(_timeScale) < 1e-9)
            return _epochServerTime;

        return _epochServerTime + (simT - _epochSimTime) / _timeScale;
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!useNetworkTime) return;

        // New owner becomes authoritative: publish fresh epoch so everyone locks to it.
        if (Networking.IsOwner(gameObject))
        {
            PublishEpochNow();
        }
    }

    private void ApplySyncedEpoch(bool hardSet)
    {
        // Sync local public fields for inspection/debug
        timeScale = _timeScale;

        double serverNow = Networking.GetServerTimeInSeconds();
        double derived = _epochSimTime + (serverNow - _epochServerTime) * _timeScale;

        if (hardSet)
        {
            simTime = derived;
        }
        // else: Update() will slew toward target

        _appliedRevision = _revision;
    }
}
