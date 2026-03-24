using UdonSharp;
using UnityEngine;

public class GC_AlertState : UdonSharpBehaviour
{
    public const byte ALERT_NONE = 0;
    public const byte ALERT_ADVISORY = 1;
    public const byte ALERT_CAUTION = 2;
    public const byte ALERT_WARNING = 3;

    // ============================================================
    // Thresholds / tuning
    // ============================================================

    [Header("Thresholds: Orbit")]
    public double periapsisWarnAltMeters = 100000.0;
    public double periapsisCriticalAltMeters = 10000.0;

    [Header("Thresholds: Selected target / closure")]
    public double selectedClosureWarnRangeMeters = 5000.0;
    public double selectedClosureWarnMps = 10.0;

    [Header("Thresholds: Node")]
    public double nodeSoonLeadSec = 120.0;

    // ============================================================
    // Raw evaluated conditions (set by GC_Core each tick)
    // ============================================================

    [Header("Orbit raw conditions")]
    public bool cond_periapsisWarn = false;
    public bool cond_periapsisCritical = false;
    public double periapsisRadiusMeters = 0.0;
    public double periapsisAltitudeMeters = 0.0;

    [Header("Selected target raw conditions")]
    public bool cond_selectedTargetValid = false;
    public double selectedTargetRangeMeters = 0.0;
    public double selectedRelSpeedMps = 0.0;
    public double selectedClosureMps = 0.0;
    public bool cond_selectedClosureHigh = false;

    [Header("Node raw conditions")]
    public bool cond_nodeSelectedValid = false;
    public int nodeSelectedIndex = -1;
    public double nodeRemainingDV_mps = 0.0;
    public bool cond_armedNodeExists = false;
    public bool cond_nodeSoon = false;
    public double nodeTimeToGoSec = 0.0;
    public bool cond_nodeAutoExecuteDisabled = false;

    // ============================================================
    // Ignore / inhibit controls
    // ============================================================

    [Header("Ignore / inhibit")]
    public bool ignorePeriapsisWarn = false;
    public bool ignorePeriapsisCritical = false;
    public bool ignoreSelectedClosureHigh = false;
    public bool ignoreNodeSoon = false;
    public bool ignoreNodeAutoExecuteDisabled = false;

    // ============================================================
    // Acknowledge / clear latches
    // ============================================================

    [Header("Acknowledged / cleared")]
    public bool ackPeriapsisWarn = false;
    public bool ackPeriapsisCritical = false;
    public bool ackSelectedClosureHigh = false;
    public bool ackNodeSoon = false;
    public bool ackNodeAutoExecuteDisabled = false;

    // ============================================================
    // Effective annunciated outputs
    // These are what UI/lights/audio should generally read.
    // ============================================================

    [Header("Effective annunciated outputs")]
    public bool outPeriapsisWarn = false;
    public bool outPeriapsisCritical = false;
    public bool outSelectedClosureHigh = false;
    public bool outNodeSoon = false;
    public bool outNodeAutoExecuteDisabled = false;

    [Header("Summary")]
    public byte highestAlertLevel = ALERT_NONE;
    public bool anyCaution = false;
    public bool anyWarning = false;

    // ============================================================
    // Core lifecycle helpers
    // ============================================================

    public void ClearEvaluatedState()
    {
        cond_periapsisWarn = false;
        cond_periapsisCritical = false;
        periapsisRadiusMeters = 0.0;
        periapsisAltitudeMeters = 0.0;

        cond_selectedTargetValid = false;
        selectedTargetRangeMeters = 0.0;
        selectedRelSpeedMps = 0.0;
        selectedClosureMps = 0.0;
        cond_selectedClosureHigh = false;

        cond_nodeSelectedValid = false;
        nodeSelectedIndex = -1;
        nodeRemainingDV_mps = 0.0;
        cond_armedNodeExists = false;
        cond_nodeSoon = false;
        nodeTimeToGoSec = 0.0;
        cond_nodeAutoExecuteDisabled = false;

        outPeriapsisWarn = false;
        outPeriapsisCritical = false;
        outSelectedClosureHigh = false;
        outNodeSoon = false;
        outNodeAutoExecuteDisabled = false;

        highestAlertLevel = ALERT_NONE;
        anyCaution = false;
        anyWarning = false;
    }

    /// <summary>
    /// Rebuild final annunciated outputs from the current conditions,
    /// ignore flags, and acknowledge flags.
    /// GC_Core should call this after updating raw conditions.
    /// </summary>
    public void RebuildOutputs()
    {
        // Warning-level items
        outPeriapsisCritical =
            cond_periapsisCritical &&
            !ignorePeriapsisCritical &&
            !ackPeriapsisCritical;

        // Caution-level items
        outPeriapsisWarn =
            cond_periapsisWarn &&
            !ignorePeriapsisWarn &&
            !ackPeriapsisWarn;

        outSelectedClosureHigh =
            cond_selectedClosureHigh &&
            !ignoreSelectedClosureHigh &&
            !ackSelectedClosureHigh;

        outNodeSoon =
            cond_nodeSoon &&
            !ignoreNodeSoon &&
            !ackNodeSoon;

        outNodeAutoExecuteDisabled =
            cond_nodeAutoExecuteDisabled &&
            !ignoreNodeAutoExecuteDisabled &&
            !ackNodeAutoExecuteDisabled;

        anyWarning =
            outPeriapsisCritical;

        anyCaution =
            outPeriapsisWarn ||
            outSelectedClosureHigh ||
            outNodeSoon ||
            outNodeAutoExecuteDisabled;

        if (anyWarning) highestAlertLevel = ALERT_WARNING;
        else if (anyCaution) highestAlertLevel = ALERT_CAUTION;
        else highestAlertLevel = ALERT_NONE;
    }

    /// <summary>
    /// Called when user presses a master caution/warning clear.
    /// This acknowledges currently active alerts, suppressing annunciation
    /// until the underlying condition clears and later reappears.
    /// </summary>
    public void API_ClearActiveAlerts()
    {
        if (cond_periapsisWarn) ackPeriapsisWarn = true;
        if (cond_periapsisCritical) ackPeriapsisCritical = true;
        if (cond_selectedClosureHigh) ackSelectedClosureHigh = true;
        if (cond_nodeSoon) ackNodeSoon = true;
        if (cond_nodeAutoExecuteDisabled) ackNodeAutoExecuteDisabled = true;

        RebuildOutputs();
    }

    /// <summary>
    /// Clears all acknowledgement latches.
    /// Useful for test/reset.
    /// </summary>
    public void API_ResetAllAcknowledged()
    {
        ackPeriapsisWarn = false;
        ackPeriapsisCritical = false;
        ackSelectedClosureHigh = false;
        ackNodeSoon = false;
        ackNodeAutoExecuteDisabled = false;

        RebuildOutputs();
    }

    /// <summary>
    /// Clears all ignore/inhibit flags.
    /// </summary>
    public void API_ClearAllIgnores()
    {
        ignorePeriapsisWarn = false;
        ignorePeriapsisCritical = false;
        ignoreSelectedClosureHigh = false;
        ignoreNodeSoon = false;
        ignoreNodeAutoExecuteDisabled = false;

        RebuildOutputs();
    }

    /// <summary>
    /// Convenience full reset for panel reset / scenario init.
    /// </summary>
    public void API_ResetAllAlertControls()
    {
        API_ResetAllAcknowledged();
        API_ClearAllIgnores();
        RebuildOutputs();
    }

    // ------------------------------------------------------------
    // Fine-grained ignore toggles
    // ------------------------------------------------------------

    public void API_SetIgnorePeriapsisWarn(bool v) { ignorePeriapsisWarn = v; RebuildOutputs(); }
    public void API_SetIgnorePeriapsisCritical(bool v) { ignorePeriapsisCritical = v; RebuildOutputs(); }
    public void API_SetIgnoreSelectedClosureHigh(bool v) { ignoreSelectedClosureHigh = v; RebuildOutputs(); }
    public void API_SetIgnoreNodeSoon(bool v) { ignoreNodeSoon = v; RebuildOutputs(); }
    public void API_SetIgnoreNodeAutoExecuteDisabled(bool v) { ignoreNodeAutoExecuteDisabled = v; RebuildOutputs(); }

    public void API_ToggleIgnorePeriapsisWarn() { ignorePeriapsisWarn = !ignorePeriapsisWarn; RebuildOutputs(); }
    public void API_ToggleIgnorePeriapsisCritical() { ignorePeriapsisCritical = !ignorePeriapsisCritical; RebuildOutputs(); }
    public void API_ToggleIgnoreSelectedClosureHigh() { ignoreSelectedClosureHigh = !ignoreSelectedClosureHigh; RebuildOutputs(); }
    public void API_ToggleIgnoreNodeSoon() { ignoreNodeSoon = !ignoreNodeSoon; RebuildOutputs(); }
    public void API_ToggleIgnoreNodeAutoExecuteDisabled() { ignoreNodeAutoExecuteDisabled = !ignoreNodeAutoExecuteDisabled; RebuildOutputs(); }

    // ------------------------------------------------------------
    // Per-alert acknowledge clears
    // ------------------------------------------------------------

    public void API_ClearAckPeriapsisWarn() { ackPeriapsisWarn = false; RebuildOutputs(); }
    public void API_ClearAckPeriapsisCritical() { ackPeriapsisCritical = false; RebuildOutputs(); }
    public void API_ClearAckSelectedClosureHigh() { ackSelectedClosureHigh = false; RebuildOutputs(); }
    public void API_ClearAckNodeSoon() { ackNodeSoon = false; RebuildOutputs(); }
    public void API_ClearAckNodeAutoExecuteDisabled() { ackNodeAutoExecuteDisabled = false; RebuildOutputs(); }
}