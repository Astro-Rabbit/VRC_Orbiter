using UdonSharp;
using UnityEngine;

/// <summary>
/// NodePlanState
/// Data-only container for scheduled maneuver nodes.
/// V1: nodes drive ATTITUDE ONLY (pointing along burn direction). No burn/throttle yet.
/// Δv is expressed in E (solver inertial).
///
/// Trigger types:
/// - TIME: trigger at mission time t
/// - TRUE_ANOMALY: trigger when current true anomaly nu reaches nuTarget (elliptic-only timing for now)
/// </summary>
public class NodePlanState : UdonSharpBehaviour
{
    // Status
    public const byte STATUS_EMPTY   = 0;
    public const byte STATUS_ARMED   = 1;
    public const byte STATUS_ACTIVE  = 2;
    public const byte STATUS_DONE    = 3;
    public const byte STATUS_ABORTED = 4;

    // Trigger
    public const byte TRIG_TIME = 0;
    public const byte TRIG_TRUE_ANOMALY = 1;

    [Header("Capacity")]
    public int maxNodes = 8;

    [Header("Node arrays (length must be maxNodes)")]
    public byte[] status;         // STATUS_*
    public byte[] trigType;       // TRIG_*

    // Time trigger (mission seconds)
    public double[] triggerTime;

    // True anomaly trigger (radians, in current osculating orbit about primary)
    public double[] triggerNuRad;

    [Header("Attitude pointing")]
    public Vector3[] dV_E;        // desired burn direction+magnitude in E
    public byte[] bodyAxisToPoint;// 0=+X,1=+Y,2=+Z

    [Header("Timing (seconds)")]
    public float[] preSlewLeadSec; // how early to start pointing before trigger
    public float[] postHoldSec;    // how long to keep pointing after trigger

    [Header("Burn execution (V2)")]
    public float[] burnDurationSec;   // computed at creation
    public float[] burnThrottle01;    // computed at creation (V1 fixed 1.0)

    [Header("Runtime bookkeeping")]
    public int activeIndex = -1;

    void Start()
    {
        EnsureArrays();
        ClearAll();
    }

    public void EnsureArrays()
    {
        int n = (maxNodes < 1) ? 1 : maxNodes;

        if (status == null || status.Length != n) status = new byte[n];
        if (trigType == null || trigType.Length != n) trigType = new byte[n];

        if (triggerTime == null || triggerTime.Length != n) triggerTime = new double[n];
        if (triggerNuRad == null || triggerNuRad.Length != n) triggerNuRad = new double[n];

        if (dV_E == null || dV_E.Length != n) dV_E = new Vector3[n];
        if (bodyAxisToPoint == null || bodyAxisToPoint.Length != n) bodyAxisToPoint = new byte[n];

        if (preSlewLeadSec == null || preSlewLeadSec.Length != n) preSlewLeadSec = new float[n];
        if (postHoldSec == null || postHoldSec.Length != n) postHoldSec = new float[n];

        if (burnDurationSec == null || burnDurationSec.Length != n) burnDurationSec = new float[n];
        if (burnThrottle01 == null || burnThrottle01.Length != n) burnThrottle01 = new float[n];

    }

    public void ClearAll()
    {
        EnsureArrays();
        activeIndex = -1;

        for (int i = 0; i < maxNodes; i++)
        {
            status[i] = STATUS_EMPTY;
            trigType[i] = TRIG_TIME;

            triggerTime[i] = 0.0;
            triggerNuRad[i] = 0.0;

            dV_E[i] = Vector3.zero;
            bodyAxisToPoint[i] = 2;

            preSlewLeadSec[i] = 30f;
            postHoldSec[i] = 5f;

            burnDurationSec[i] = 0f;
            burnThrottle01[i] = 0f;

        }
    }

    public int FindFirstFree()
    {
        EnsureArrays();
        for (int i = 0; i < maxNodes; i++)
            if (status[i] == STATUS_EMPTY) return i;
        return -1;
    }

    // Simple: pick earliest time-triggered node.
    // True-anomaly nodes will be compared using the computed ETA in GC_Core.
    // So this just returns all ARMED candidates; GC_Core picks best.
    public bool IsArmed(int i)
    {
        if (i < 0 || i >= maxNodes) return false;
        return status[i] == STATUS_ARMED;
    }

    // --- UI-facing create APIs (your requested shape) ---

    public int API_CreateNode_Time(Vector3 dvE, double tTrigger, byte axis012)
    {
        EnsureArrays();
        int i = FindFirstFree();
        if (i < 0) return -1;

        status[i] = STATUS_ARMED;
        trigType[i] = TRIG_TIME;

        dV_E[i] = dvE;
        triggerTime[i] = tTrigger;
        bodyAxisToPoint[i] = axis012;

        return i;
    }

    public int AllocNode(byte trig, byte axis012)
    {
        EnsureArrays();
        int i = FindFirstFree();
        if (i < 0) return -1;

        status[i] = STATUS_ARMED;
        trigType[i] = trig;
        bodyAxisToPoint[i] = axis012;
        return i;
    }

    public int API_CreateNode_TrueAnomaly(Vector3 dvE, double nuTargetRad, byte axis012)
    {
        EnsureArrays();
        int i = FindFirstFree();
        if (i < 0) return -1;

        status[i] = STATUS_ARMED;
        trigType[i] = TRIG_TRUE_ANOMALY;

        dV_E[i] = dvE;
        triggerNuRad[i] = nuTargetRad;
        bodyAxisToPoint[i] = axis012;

        return i;
    }

    public void API_DeleteNode(int i)
    {
        if (i < 0 || i >= maxNodes) return;

        status[i] = STATUS_EMPTY;
        trigType[i] = TRIG_TIME;

        triggerTime[i] = 0.0;
        triggerNuRad[i] = 0.0;

        burnDurationSec[i] = 0f;
        burnThrottle01[i] = 0f;

        dV_E[i] = Vector3.zero;
        bodyAxisToPoint[i] = 2;

        preSlewLeadSec[i] = 30f;
        postHoldSec[i] = 5f;

        if (activeIndex == i) activeIndex = -1;
    }



    
}