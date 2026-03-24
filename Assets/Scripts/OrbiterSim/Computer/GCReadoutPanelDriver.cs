using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GCReadoutPanelDriver : UdonSharpBehaviour
{
    [Header("Data Sources")]
    public GC_RuntimeState runtime;
    public GC_ModeParams modeParams;
    public GC_ActuatorOverrideState overrides;
    public NodePlanState plan;
    public EffectsSyncState effectsSync;
    public CraftStateModel craft;

    [Header("Text Outputs")]
    public TMP_Text gcStatusText;
    public TMP_Text overrideText;
    public TMP_Text actuatorText;
    public TMP_Text nodeListText;

    [Header("Refresh")]
    public float refreshInterval = 0.25f;

    private float _refreshTimer = 0f;

    void Start()
    {
        RefreshAll();
    }

    void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= refreshInterval)
        {
            _refreshTimer = 0f;
            RefreshAll();
        }
    }

    private void RefreshAll()
    {
        RefreshGCStatusSection();
        RefreshOverrideSection();
        RefreshActuatorSection();
        RefreshNodeListSection();
    }

    private void RefreshGCStatusSection()
    {
        if (gcStatusText == null)
            return;

        if (runtime == null)
        {
            gcStatusText.text =
                "GC STATUS\n" +
                "PROG: ---\n" +
                "ATT : ---\n" +
                "XLAT: ---\n" +
                "EXEC: ---\n" +
                "STAT: ---";
            return;
        }

        gcStatusText.text =
            "GC STATUS\n" +
            "PROG: " + ProgramToString(runtime.activeProgramId) + "\n" +
            "ATT : " + AttModeToString(runtime.activeModeId) + "\n" +
            "XLAT: " + TranslateModeToString(runtime.activeTranslateModeId) + "\n" +
            "EXEC: " + ExecutorToString(runtime.activeExecutorId, runtime.executorPhase) + "\n" +
            "STAT: " + StatusToString(runtime.status);
    }

private void RefreshOverrideSection()
{
    if (overrideText == null)
        return;

    if (overrides == null)
    {
        overrideText.text =
            "OVERRIDES\n" +
            "WHEELS: ---\n" +
            "RCS   : ---\n" +
            "GIMBAL: ---\n" +
            "ATT OV: ---\n" +
            "RCS OV: ---";
        return;
    }

    overrideText.text =
        "OVERRIDES\n" +
        "WHEELS: " + AllowOverrideToString(overrides.overrideAllowWheels) + "\n" +
        "RCS   : " + AllowOverrideToString(overrides.overrideAllowRCS) + "\n" +
        "GIMBAL: " + AllowOverrideToString(overrides.overrideAllowGimbal) + "\n" +
        "ATT OV: " + AttActuatorOverrideToString(overrides.overrideAttitudeActuatorMode) + "\n" +
        "RCS OV: " + RcsModeOverrideToString(overrides.overrideRcsMode);
}


    private void RefreshNodeListSection()
    {
        if (nodeListText == null)
            return;

        nodeListText.text = "NODES\n---";
    }

    private string ProgramToString(byte v)
    {
        switch (v)
        {
            case GC_RuntimeState.PROG_NONE:          return "NONE";
            case GC_RuntimeState.PROG_MANUAL:        return "MANUAL";
            case GC_RuntimeState.PROG_HOLD_ATT:      return "HOLD ATT";
            case GC_RuntimeState.PROG_POINT_DIR_E:   return "POINT DIR";
            case GC_RuntimeState.PROG_KILL_ROT:      return "KILL ROT";
            case GC_RuntimeState.PROG_HOLD_PROGRADE: return "PROGRADE";
            case GC_RuntimeState.PROG_HOLD_RETRO:    return "RETRO";
            case GC_RuntimeState.PROG_HOLD_RAD_OUT:  return "RAD OUT";
            case GC_RuntimeState.PROG_HOLD_RAD_IN:   return "RAD IN";
            case GC_RuntimeState.PROG_HOLD_NORMAL:   return "NORMAL";
            case GC_RuntimeState.PROG_HOLD_ANTINORM: return "ANTI-N";
            case GC_RuntimeState.PROG_RELVEL_PRO:    return "RELVEL +";
            case GC_RuntimeState.PROG_RELVEL_RETRO:  return "RELVEL -";
            case GC_RuntimeState.PROG_EXEC_NODE:     return "EXEC NODE";
            default:                                 return "UNK";
        }
    }

    private string AttModeToString(byte mode)
    {
        switch (mode)
        {
            case GC_RuntimeState.MODE_MANUAL:
                return "MANUAL";

            case GC_RuntimeState.MODE_HOLD_QUAT:
                return "HOLD QUAT";

            case GC_RuntimeState.MODE_POINT_DIR_E:
                return "POINT DIR";

            case GC_RuntimeState.MODE_HOLD_RTN_DIR:
                return "RTN " + RtnDirToString(modeParams != null ? modeParams.rtnDir : (byte)0);

            case GC_RuntimeState.MODE_RATE_TARGET:
                return "RATE TARGET";

            case GC_RuntimeState.MODE_DIRECT_TORQUE:
                return "DIRECT TAU";

            case GC_RuntimeState.MODE_DOCK_POINT_SHIPZ_TO_PORT:
                return "DOCK POINT";

            case GC_RuntimeState.MODE_DOCK_ALIGN_PORTS:
                return "DOCK ALIGN";

            case GC_RuntimeState.MODE_RELVEL_PROGRADE:
                return "RELVEL +";

            case GC_RuntimeState.MODE_RELVEL_RETROGRADE:
                return "RELVEL -";

            default:
                return "UNK";
        }
    }

    private string TranslateModeToString(byte mode)
    {
        switch (mode)
        {
            case GC_RuntimeState.XLAT_MANUAL:         return "MANUAL";
            case GC_RuntimeState.XLAT_KILL_RELVEL:    return "KILL RELV";
            case GC_RuntimeState.XLAT_HOLD_REL_POS:   return "HOLD RPOS";
            case GC_RuntimeState.XLAT_DOCK_HOLD_PORT: return "DOCK PORT";
            default:                                  return "UNK";
        }
    }

    private string ExecutorToString(byte execId, byte phase)
    {
        if (execId == GC_RuntimeState.EXEC_NONE)
            return "NONE";

        string execName = "EXEC";
        if (execId == GC_RuntimeState.EXEC_NODE_SIMPLE)
            execName = "NODE";

        return execName + " " + ExecPhaseToString(phase);
    }

    private string ExecPhaseToString(byte phase)
    {
        switch (phase)
        {
            case GC_RuntimeState.EXEC_PHASE_NONE: return "NONE";
            case GC_RuntimeState.EXEC_PHASE_WAIT: return "WAIT";
            case GC_RuntimeState.EXEC_PHASE_SLEW: return "SLEW";
            case GC_RuntimeState.EXEC_PHASE_BURN: return "BURN";
            case GC_RuntimeState.EXEC_PHASE_POST: return "POST";
            default:                              return "UNK";
        }
    }

    private string StatusToString(byte status)
    {
        switch (status)
        {
            case GC_RuntimeState.STATUS_IDLE:      return "IDLE";
            case GC_RuntimeState.STATUS_RUNNING:   return "RUN";
            case GC_RuntimeState.STATUS_COMPLETED: return "DONE";
            case GC_RuntimeState.STATUS_ABORTED:   return "ABORT";
            case GC_RuntimeState.STATUS_FAULT:     return "FAULT";
            default:                               return "UNK";
        }
    }

private string AttActuatorOverrideToString(byte v)
{
    switch (v)
    {

        case CraftCommandState.ATT_ACT_WHEELS_ONLY:
            return "WHEELS";

        case CraftCommandState.ATT_ACT_RCS_ONLY:
            return "RCS";

        case CraftCommandState.ATT_ACT_GIMBAL_ONLY:
            return "GIMBAL";

        case CraftCommandState.ATT_ACT_AUTO:
            return "AUTO";

        default:
            return "UNK";
    }
}
    private string RcsModeOverrideToString(byte v)
    {
        switch (v)
        {
            case GC_ActuatorOverrideState.RCSMODE_NO_OVERRIDE:
                return "AUTO";

            case GC_ActuatorOverrideState.RCSMODE_FORCE_TRANSLATE:
                return "TRANS";

            case GC_ActuatorOverrideState.RCSMODE_FORCE_ROTATE:
                return "ROT";

            case GC_ActuatorOverrideState.RCSMODE_FORCE_BLENDED:
                return "BLEND";

            default:
                return "UNK";
        }
    }

    private string AllowOverrideToString(byte v)
    {
        switch (v)
        {
            case GC_ActuatorOverrideState.NO_OVERRIDE:
                return "AUTO";

            case GC_ActuatorOverrideState.FORCE_DISABLE:
                return "OFF";

            case GC_ActuatorOverrideState.FORCE_ENABLE:
                return "ON";

            default:
                return "UNK";
        }
    }

    private void RefreshActuatorSection()
    {
        if (actuatorText == null)
            return;

        if (effectsSync == null)
        {
            actuatorText.text =
                "ACTUATORS\n" +
                "TAU : ---\n" +
                "MAIN: ---\n" +
                "XLAT: ---";
            return;
        }

        float tauX = effectsSync.cmdTauX_dNm * 0.1f;
        float tauY = effectsSync.cmdTauY_dNm * 0.1f;
        float tauZ = effectsSync.cmdTauZ_dNm * 0.1f;

        float transX = effectsSync.cmdTransX_dN * 0.1f;
        float transY = effectsSync.cmdTransY_dN * 0.1f;
        float transZ = effectsSync.cmdTransZ_dN * 0.1f;

        float main01 = effectsSync.mainThrottle255 / 255f;

        actuatorText.text =
            "ACTUATORS\n" +
            "TAU : " + FormatVec3Signed1(tauX, tauY, tauZ) + "\n" +
            "MAIN: " + FormatPercent0(main01) + "\n" +
            "XLAT: " + FormatVec3Signed1(transX, transY, transZ)+
            "Mass: "+ craft.massKg;
    }

    private string RtnDirToString(byte rtnDir)
    {
        switch (rtnDir)
        {
            case GC_ModeParams.RTN_T_PLUS:  return "PRO";
            case GC_ModeParams.RTN_T_MINUS: return "RET";
            case GC_ModeParams.RTN_R_PLUS:  return "RAD+";
            case GC_ModeParams.RTN_R_MINUS: return "RAD-";
            case GC_ModeParams.RTN_N_PLUS:  return "NORM";
            case GC_ModeParams.RTN_N_MINUS: return "ANTI";
            default:                        return "UNK";
        }
    }

    private string FormatSigned1(float v)
    {
        return v.ToString("+0.0;-0.0;0.0");
    }

    private string FormatVec3Signed1(float x, float y, float z)
    {
        return
            FormatSigned1(x) + " " +
            FormatSigned1(y) + " " +
            FormatSigned1(z);
    }

    private string FormatPercent0(float v01)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(v01) * 100f).ToString() + "%";
    }

}