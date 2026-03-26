using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDShipSystemsPage : MFDPage
{
    [Header("References")]
    public GC_RuntimeState runtime;

    [Header("Ship Image Layout (display UV 0..1)")]
    [Tooltip("xmin, ymin, xmax, ymax in MFD UV space.")]
    public Vector4 shipRectUv = new Vector4(0.52f, 0.10f, 0.95f, 0.86f);

    [Header("Image Source UV")]
    [Tooltip("umin, vmin, umax, vmax in source texture UV space.")]
    public Vector4 shipSourceUv = new Vector4(0f, 0f, 1f, 1f);

    [Header("Ship Image")]
    public Texture shipOutline;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2)
        {
            display.SetPage((byte)MFDPageID.Menu);
        }
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearGraphics();
        display.ClearText();

        if (shipOutline != null)
        {
            display.SetImagePanel(
                shipOutline,
                shipRectUv,
                shipSourceUv,
                Color.white
            );
        }
        else
        {
            display.ClearImagePanel();
        }

        DrawGCBlock(display, 1, 1);
        DrawCTRLBlock(display, 7, 1);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    private void DrawGCBlock(MFD display, int row, int col)
    {
        display.DrawText("GC", row + 0, col, Color.green);

        if (runtime == null)
        {
            display.DrawText("ATT : ---", row + 1, col, Color.green);
            display.DrawText("XLAT: ---", row + 2, col, Color.green);
            display.DrawText("EXEC: ---", row + 3, col, Color.green);
            display.DrawText("STAT: ---", row + 4, col, Color.green);
            return;
        }

        display.DrawText("ATT : " + AttModeToString(runtime.activeModeId), row + 1, col, Color.green);
        display.DrawText("XLAT: " + TranslateModeToString(runtime.activeTranslateModeId), row + 2, col, Color.green);
        display.DrawText("EXEC: " + ExecutorToString(runtime.activeExecutorId, runtime.executorPhase), row + 3, col, Color.green);
        display.DrawText("STAT: " + StatusToString(runtime.status), row + 4, col, Color.green);
    }

    private void DrawCTRLBlock(MFD display, int row, int col)
    {
        display.DrawText("CTRL", row + 0, col, Color.green);

        if (runtime == null)
        {
            display.DrawText("ATT: ---", row + 1, col, Color.green);
            display.DrawText("THR: ---", row + 2, col, Color.green);
            return;
        }

        display.DrawText("ATT: " + AttSourceToString(), row + 1, col, Color.green);
        display.DrawText("THR: " + ThrSourceToString(), row + 2, col, Color.green);
    }

    private string AttSourceToString()
    {
        if (runtime == null) return "---";

        if (runtime.activeExecutorId != GC_RuntimeState.EXEC_NONE &&
            (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_SLEW ||
             runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
             runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST))
            return "EXEC";

        if (runtime.activeModeId != GC_RuntimeState.MODE_MANUAL)
            return "MODE";

        return "MAN";
    }

    private string ThrSourceToString()
    {
        if (runtime == null) return "---";

        if (runtime.status == GC_RuntimeState.STATUS_FAULT)
            return "SAFE";

        if (runtime.activeExecutorId != GC_RuntimeState.EXEC_NONE &&
            (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
             runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST))
            return "EXEC";

        return "MAN";
    }

    private string AttModeToString(byte mode)
    {
        switch (mode)
        {
            case GC_RuntimeState.MODE_MANUAL: return "MANUAL";
            case GC_RuntimeState.MODE_HOLD_QUAT: return "HOLD QUAT";
            case GC_RuntimeState.MODE_POINT_DIR_E: return "POINT DIR";
            case GC_RuntimeState.MODE_HOLD_RTN_DIR: return "RTN";
            case GC_RuntimeState.MODE_RATE_TARGET: return "RATE";
            case GC_RuntimeState.MODE_DIRECT_TORQUE: return "TAU";
            case GC_RuntimeState.MODE_HOLD_HORIZON: return "HORIZON";
            case GC_RuntimeState.MODE_POINT_NODE_VECTOR: return "ALIGN DV";
            case GC_RuntimeState.MODE_DOCK_POINT_SHIPZ_TO_PORT: return "DOCK POINT";
            case GC_RuntimeState.MODE_DOCK_ALIGN_PORTS: return "DOCK ALIGN";
            case GC_RuntimeState.MODE_RELVEL_PROGRADE: return "RELVEL +";
            case GC_RuntimeState.MODE_RELVEL_RETROGRADE: return "RELVEL -";
            default: return "UNK";
        }
    }

    private string TranslateModeToString(byte mode)
    {
        switch (mode)
        {
            case GC_RuntimeState.XLAT_MANUAL: return "MANUAL";
            case GC_RuntimeState.XLAT_KILL_RELVEL: return "KILL RELV";
            case GC_RuntimeState.XLAT_HOLD_REL_POS: return "HOLD RPOS";
            case GC_RuntimeState.XLAT_DOCK_HOLD_PORT: return "DOCK";
            default: return "UNK";
        }
    }

    private string ExecutorToString(byte execId, byte phase)
    {
        if (execId == GC_RuntimeState.EXEC_NONE)
            return "NONE";

        string execName = (execId == GC_RuntimeState.EXEC_NODE_SIMPLE) ? "NODE" : "EXEC";
        return execName + " " + ExecPhaseToString(phase);
    }

    private string ExecPhaseToString(byte phase)
    {
        switch (phase)
        {
            case GC_RuntimeState.EXEC_PHASE_WAIT: return "WAIT";
            case GC_RuntimeState.EXEC_PHASE_SLEW: return "SLEW";
            case GC_RuntimeState.EXEC_PHASE_BURN: return "BURN";
            case GC_RuntimeState.EXEC_PHASE_POST: return "POST";
            default: return "NONE";
        }
    }

    private string StatusToString(byte status)
    {
        switch (status)
        {
            case GC_RuntimeState.STATUS_IDLE: return "IDLE";
            case GC_RuntimeState.STATUS_RUNNING: return "RUN";
            case GC_RuntimeState.STATUS_COMPLETED: return "DONE";
            case GC_RuntimeState.STATUS_ABORTED: return "ABORT";
            case GC_RuntimeState.STATUS_FAULT: return "FAULT";
            default: return "UNK";
        }
    }
}