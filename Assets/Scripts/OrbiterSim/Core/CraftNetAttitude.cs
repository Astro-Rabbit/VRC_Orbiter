using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CraftNetAttitude : UdonSharpBehaviour
{
    [Header("Wiring")]
    public SimClock clock;
    public CraftAttitudeState att;

    [Header("Publish rate")]
    [Tooltip("Attitude publish rate (Hz). Works in both rails and integrated.")]
    public float attHz = 10f;

    [Header("Remote apply")]
    [Tooltip("If > 0, slerp toward received attitude at this rate (1/sec). 0 = hard set.")]
    public float slerpRate = 0f;

    // --- synced attitude ---
    [UdonSynced] private int _rev;
    [UdonSynced] private double _epochT;
    [UdonSynced] private float _qX, _qY, _qZ, _qW;
    [UdonSynced] private float _wX, _wY, _wZ;

    private float _accum;
    private int _appliedRev = -1;

    private float Period => (attHz > 0f) ? (1f / attHz) : 999999f;

    /// <summary>Owner: publish attitude at cadence. Safe to call every frame.</summary>
    public void PublishAttitude()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (att == null || clock == null) return;

        _accum += Time.deltaTime;
        if (_accum < Period) return;
        _accum = 0f;

        _epochT = clock.Now();

        Quaternion q = att.qBE;
        _qX = q.x; _qY = q.y; _qZ = q.z; _qW = q.w;

        _wX = (float)att.wx;
        _wY = (float)att.wy;
        _wZ = (float)att.wz;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;
    }

    /// <summary>Remote: apply attitude each frame (or on deserialization if you prefer).</summary>
    public void ApplyRemoteAttitude()
    {
        if (Networking.IsOwner(gameObject)) return;
        if (att == null) return;

        Quaternion target = new Quaternion(_qX, _qY, _qZ, _qW);

        if (slerpRate > 0f)
        {
            float t = 1f - Mathf.Exp(-slerpRate * Time.deltaTime);
            att.qBE = Quaternion.Slerp(att.qBE, target, t);
        }
        else
        {
            att.qBE = target;
        }

        att.wx = _wX;
        att.wy = _wY;
        att.wz = _wZ;
    }

    public void ForcePublishAttitude()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (att == null || clock == null) return;

        _accum = 0f;
        _epochT = clock.Now();

        Quaternion q = att.qBE;
        _qX = q.x; _qY = q.y; _qZ = q.z; _qW = q.w;

        _wX = (float)att.wx;
        _wY = (float)att.wy;
        _wZ = (float)att.wz;

        _rev++;
        RequestSerialization();
        _appliedRev = _rev;
    }

    public override void OnDeserialization()
    {
        // You can apply immediately here, but I prefer SimManager calling ApplyRemoteAttitude()
        _appliedRev = _rev;
    }
}
