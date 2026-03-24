using UdonSharp;
using UnityEngine;

/// <summary>
/// GC_RuntimeState
/// Small, explicit guidance-computer runtime bookkeeping.
/// Data-only container: GC_Core is responsible for updating it.
/// 
/// Holds:
/// - Active continuous "mode" (attitude hold, manual, etc.)
/// - Active executor slot (future node execution) and its phase/status
/// - Policy selection and resume behavior
/// </summary>
public class GC_RuntimeState : UdonSharpBehaviour
{
    // --------------------
    // Status
    // --------------------
    public const byte STATUS_IDLE      = 0;
    public const byte STATUS_RUNNING   = 1;
    public const byte STATUS_COMPLETED = 2;
    public const byte STATUS_ABORTED   = 3;
    public const byte STATUS_FAULT     = 4;

    // --------------------
    // Active continuous modes (V1)
    // --------------------
    public const byte MODE_MANUAL         = 0;
    public const byte MODE_HOLD_QUAT      = 1;
    public const byte MODE_POINT_DIR_E    = 2;
    public const byte MODE_HOLD_RTN_DIR   = 3;
    public const byte MODE_RATE_TARGET    = 4;
    public const byte MODE_DIRECT_TORQUE  = 5;

    public const byte MODE_DOCK_POINT_SHIPZ_TO_PORT = 6;  // continuous
    public const byte MODE_DOCK_ALIGN_PORTS        = 7;   // continuous

    public const byte MODE_RELVEL_PROGRADE  = 8;  // continuous: point at +dv (station - craft)
    public const byte MODE_RELVEL_RETROGRADE= 9;  // continuous: point at -dv

    public const byte MODE_HOLD_HORIZON        = 10; // continuous: local horizon attitude hold
    public const byte MODE_POINT_NODE_VECTOR   = 11; // continuous: point selected body axis along selected node dV



    // --------------------
    // Executor slot (future: node execution)
    // --------------------
    public const byte EXEC_NONE            = 0;
    public const byte EXEC_NODE_SIMPLE     = 1; // reserved for later

    public const byte EXEC_PHASE_NONE      = 0;
    public const byte EXEC_PHASE_WAIT      = 1;
    public const byte EXEC_PHASE_SLEW      = 2;
    public const byte EXEC_PHASE_BURN      = 3;
    public const byte EXEC_PHASE_POST      = 4;

    // --------------------
    // Intervention policy (V1 default)
    // --------------------
    public const byte POLICY_V1_DEFAULT    = 0;

    [Header("Active state")]
    public byte status = STATUS_IDLE;

    [Tooltip("Active continuous control mode.")]
    public byte activeModeId = MODE_MANUAL;


    // --------------------
    // Active "program" descriptor (UI-facing)
    // --------------------
    // This is a descriptive label for what GC is doing, independent of control implementation.
    public const byte PROG_NONE          = 0;
    public const byte PROG_MANUAL        = 1;
    public const byte PROG_HOLD_ATT      = 2; // hold quaternion
    public const byte PROG_POINT_DIR_E   = 3; // point body axis at inertial direction
    public const byte PROG_KILL_ROT      = 4; // rate target = 0
    public const byte PROG_HOLD_PROGRADE = 5;
    public const byte PROG_HOLD_RETRO    = 6;
    public const byte PROG_HOLD_RAD_OUT  = 7;
    public const byte PROG_HOLD_RAD_IN   = 8;
    public const byte PROG_HOLD_NORMAL   = 9;
    public const byte PROG_HOLD_ANTINORM = 10;
    public const byte PROG_RELVEL_PRO   = 11;
    public const byte PROG_RELVEL_RETRO= 12;

    public const byte PROG_DOCK_POINT_PORT  = 13;
    public const byte PROG_DOCK_ALIGN_PORTS = 14;
    public const byte PROG_HOLD_HORIZON       = 15;
    public const byte PROG_POINT_NODE_VECTOR  = 16;

    public const byte PROG_EXEC_NODE     = 20; // executor is actively controlling


    // --------------------
    // Translation assist modes (V1 docking helpers)
    // --------------------
    public const byte XLAT_MANUAL          = 0;
    public const byte XLAT_KILL_RELVEL     = 1;   // root dv damping vs selected station
    public const byte XLAT_HOLD_REL_POS    = 2;   // optional: root dr position hold (later)
    public const byte XLAT_DOCK_HOLD_PORT  = 3;   // optional: fine docking using port error (later)

    [Tooltip("Active translation control mode (independent of attitude mode).")]
    public byte activeTranslateModeId = XLAT_MANUAL;

    [Header("UI program indicator")]
    public byte activeProgramId = PROG_NONE;


    [Tooltip("Executor program currently active (future).")]
    public byte activeExecutorId = EXEC_NONE;

    [Tooltip("Executor phase (future).")]
    public byte executorPhase = EXEC_PHASE_NONE;

    [Header("Policy")]
    public byte policyId = POLICY_V1_DEFAULT;


    [Header("Node execution policy")]
    [Tooltip("If true, armed nodes may auto-enter executor wait/slew/burn flow. If false, nodes remain planned only and must be flown manually.")]
    public bool autoExecuteArmedNodes = true;


    [Header("Resume behavior (used by executor later)")]
    [Tooltip("Mode to restore after executor completes (if configured).")]
    public byte resumeModeId = MODE_MANUAL;

    [Tooltip("If true, executor should resume resumeModeId on completion.")]
    public bool resumeModeOnExecutorDone = false;

    [Header("Timestamps (seconds, mission time)")]
    public double modeStartTime;
    public double executorStartTime;
    public double lastAbortTime;

    [Header("Diagnostics")]
    public int lastAbortReason;   // app-defined
    public int lastFaultCode;     // app-defined

    [Header("Manual takeover policy")]
    public bool autoSwitchToManualOnInput = true;
    public bool latchManualTakeover = true;
    public float manualTakeoverDeadzone = 0.05f;
    public float manualReleaseTimeoutSec = 0.5f;


    [Header("Manual translation takeover policy")]
    public bool autoSwitchTranslateToManualOnInput = true;
    public bool latchTranslateTakeover = true;
    public float translateReleaseTimeoutSec = 0.5f;

    [Header("Translation runtime bookkeeping")]
    public byte lastNonManualTranslateModeId = XLAT_MANUAL;
    public double lastManualTranslateInputTime = 0;


    [Header("Runtime bookkeeping")]
    public byte lastNonManualModeId = 0;   // set when we leave a mode due to manual takeover
    public double lastManualInputTime = 0; // nav.t when last manual activity seen


    [Header("Executor runtime (V1 nodes)")]
    public int executorNodeIndex = -1;     // which node plan index is active
    public byte cachedModeBeforeExec = MODE_MANUAL;
    public bool abortExecOnManualInput = true;





    [TextArea] public string lastFaultMessage;

    public void ResetState(double nowT)
    {
        status = STATUS_IDLE;

        activeModeId = MODE_MANUAL;
        activeProgramId = PROG_MANUAL;

        activeTranslateModeId = XLAT_MANUAL;

        activeExecutorId = EXEC_NONE;
        executorPhase = EXEC_PHASE_NONE;
        executorNodeIndex = -1;
        cachedModeBeforeExec = MODE_MANUAL;

        policyId = POLICY_V1_DEFAULT;
        autoExecuteArmedNodes = true;

        resumeModeId = MODE_MANUAL;
        resumeModeOnExecutorDone = true;

        modeStartTime = nowT;
        executorStartTime = 0.0;
        lastAbortTime = 0.0;

        lastAbortReason = 0;
        lastFaultCode = 0;
        lastFaultMessage = "";

        lastNonManualModeId = MODE_MANUAL;
        lastManualInputTime = nowT;

        lastNonManualTranslateModeId = XLAT_MANUAL;
        lastManualTranslateInputTime = nowT;
    }
}