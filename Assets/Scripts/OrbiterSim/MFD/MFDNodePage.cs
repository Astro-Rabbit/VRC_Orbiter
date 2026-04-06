using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MFDNodePage : MFDPage
{
    [Header("References")]
    public GC_Core gc;
    public NodePlanState plan;
    public GC_RuntimeState runtime;
    public GC_RuntimeNetState runtimeNet;
    public GC_ModeParams modeParams;

    [Header("Local page state")]
    [UdonSynced] private byte _cursorIndex = 255;
    public int cursorIndex = 0;

    // Layout
    private const int ROW_LIST_START = 2;
    private const int ROW_DETAIL_START = 12;

    private const int COL_BUTTONS = 2;
    private const int COL_LIST = 8;
    private const int COL_MODE = 34;
    private const int COL_DETAIL = 28;

    void Start()
    {
        ApplyCursorFromSynced();
        ClampCursorToExistingNode();
    }

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
            return;
        }

        if (plan == null) return;

        if (side == ButtonSide.Left) {
            // L1 = cursor up
            if (num == 0) {
                MoveCursor(-1);
                return;
            }

            // L2 = cursor down
            if (num == 1) {
                MoveCursor(+1);
                return;
            }

            // L4 = select highlighted node as GC-selected node
            if (num == 3) {
                if (IsNodeValid(cursorIndex) && gc != null) {
                    gc.SendCustomNetworkEvent(
                        NetworkEventTarget.Owner,
                        nameof(GC_Core.Net_RequestSelectNode),
                        cursorIndex
                    );
                }
                return;
            }

            // L5 = delete highlighted node
            if (num == 4) {
                if (IsNodeValid(cursorIndex) && gc != null) {
                    int deleted = cursorIndex;
                    gc.SendCustomNetworkEvent(
                        NetworkEventTarget.Owner,
                        nameof(GC_Core.Net_RequestDeleteNode),
                        deleted
                    );
                    ClampCursorAfterDelete(deleted);
                }
                return;
            }
        }
    }

    private void Update()
    {
        ClampCursorToExistingNode();
    }

    private void MoveCursor(int dir)
    {
        if (plan == null || plan.maxNodes <= 0) return;

        int start = cursorIndex;
        int n = plan.maxNodes;

        for (int step = 0; step < n; step++) {
            start += dir;

            if (start < 0) start = n - 1;
            if (start >= n) start = 0;

            if (IsNodeValid(start)) {
                SetSyncedCursor(start);
                return;
            }
        }
    }

    private void ClampCursorToExistingNode()
    {
        if (plan == null || plan.maxNodes <= 0) {
            cursorIndex = 0;
            return;
        }

        if (cursorIndex < 0) cursorIndex = 0;
        if (cursorIndex >= plan.maxNodes) cursorIndex = plan.maxNodes - 1;

        if (IsNodeValid(cursorIndex)) return;

        for (int i = 0; i < plan.maxNodes; i++) {
            if (IsNodeValid(i)) {
                cursorIndex = i;
                return;
            }
        }

        cursorIndex = 0;
    }

    private void ClampCursorAfterDelete(int deletedIndex)
    {
        if (plan == null || plan.maxNodes <= 0) {
            cursorIndex = 0;
            return;
        }

        if (IsNodeValid(deletedIndex)) {
            cursorIndex = deletedIndex;
            return;
        }

        for (int i = deletedIndex; i < plan.maxNodes; i++) {
            if (IsNodeValid(i)) {
                cursorIndex = i;
                return;
            }
        }

        for (int i = deletedIndex - 1; i >= 0; i--) {
            if (IsNodeValid(i)) {
                cursorIndex = i;
                return;
            }
        }

        cursorIndex = 0;
    }

    private bool IsNodeValid(int i)
    {
        if (plan == null) return false;
        if (i < 0 || i >= plan.maxNodes) return false;
        if (plan.status == null || i >= plan.status.Length) return false;
        return plan.status[i] != NodePlanState.STATUS_EMPTY;
    }

    private string GetStatusText(int i)
    {
        if (!IsNodeValid(i)) return "---";

        byte st = plan.status[i];
        switch (st)
        {
            default: return "UNK";
            case NodePlanState.STATUS_EMPTY:   return "---";
            case NodePlanState.STATUS_ARMED:   return "ARM";
            case NodePlanState.STATUS_ACTIVE:  return "ACT";
            case NodePlanState.STATUS_DONE:    return "DON";
            case NodePlanState.STATUS_ABORTED: return "ABT";
        }
    }

    private bool IsSelectedNode(int i)
    {
        if (runtimeNet != null)
            return runtimeNet.selectedNodeIndex == i;

        if (modeParams == null) return false;
        return modeParams.selectedNodeIndex == i;
    }

    private bool IsExecutorNode(int i)
    {
        if (runtime == null) return false;
        if (runtime.executorNodeIndex != i) return false;

        return runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_SLEW ||
               runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
               runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST;
    }

    private string GetModeText()
    {
        if (runtime == null) return "MAN";
        return runtime.autoExecuteArmedNodes ? "AUTO" : "MAN";
    }

    private void DrawGlobalIndicators(MFD display)
    {
        display.DrawText("MODE " + GetModeText(), 2, COL_MODE, Color.green);
    }

    private void DrawList(MFD display)
    {
        if (plan == null) return;

        // Left-side button labels
        display.DrawText("UP", 2, COL_BUTTONS, Color.white);
        display.DrawText("DN", 4, COL_BUTTONS, Color.white);
        display.DrawText("SEL", 17, COL_BUTTONS, Color.white);
        display.DrawText("DEL", 20, COL_BUTTONS, Color.white);

        int row = ROW_LIST_START;

        for (int i = 0; i < plan.maxNodes; i++) {
            if (!IsNodeValid(i)) continue;

            string marker = " ";
            if (IsExecutorNode(i)) marker = "!";
            else if (IsSelectedNode(i)) marker = "*";
            if (i == cursorIndex) marker = ">";

            string label = marker + "N" + i + " " + GetStatusText(i);
            Color color = (i == cursorIndex) ? Color.green : Color.white;

            display.DrawText(label, row, COL_LIST, color);
            row++;

            if (row >= ROW_DETAIL_START - 1) break;
        }
    }

    private void DrawDetails(MFD display)
    {
        if (!IsNodeValid(cursorIndex)) {
            display.DrawText("NO NODES", ROW_DETAIL_START, COL_DETAIL, Color.green);
            return;
        }

        double tNode = 0.0;
        bool haveTNode = (gc != null) && gc.API_Node_TryGetTimeToGo(cursorIndex, out tNode);

        double tBurnStart = 0.0;
        bool haveTBurnStart = (gc != null) && gc.API_Node_TryGetTimeToBurnStart(cursorIndex, out tBurnStart);

        float dv = 0f;
        float rem = 0f;
        float burn = 0f;

        if (plan.dVmag_mps != null && cursorIndex < plan.dVmag_mps.Length)
            dv = plan.dVmag_mps[cursorIndex];

        if (plan.remainingDV_mps != null && cursorIndex < plan.remainingDV_mps.Length)
            rem = plan.remainingDV_mps[cursorIndex];

        if (plan.burnDurationSec != null && cursorIndex < plan.burnDurationSec.Length)
            burn = plan.burnDurationSec[cursorIndex];

        string statText = GetStatusText(cursorIndex);

        int row = ROW_DETAIL_START;

        display.DrawText("NODE N" + cursorIndex, row++, COL_DETAIL, Color.green);
        display.DrawText("STAT " + statText, row++, COL_DETAIL, Color.green);

        // TNODE (center of burn / node trigger)
        // if (haveTNode)
        //     display.DrawText(MFD.FormatNumber("TNODE", tNode), row++, COL_DETAIL, Color.green);
        // else
        //     display.DrawText("TNODE ---", row++, COL_DETAIL, Color.green);

        // TGO (burn start)
        if (haveTBurnStart)
            display.DrawText(MFD.FormatNumber("TGO", tBurnStart), row++, COL_DETAIL, Color.green);
        else
            display.DrawText("TGO ---", row++, COL_DETAIL, Color.green);

        display.DrawText(MFD.FormatNumber("DV", dv), row++, COL_DETAIL, Color.green);
        display.DrawText(MFD.FormatNumber("REM", rem), row++, COL_DETAIL, Color.green);
        display.DrawText(MFD.FormatNumber("BURN", burn), row++, COL_DETAIL, Color.green);

        if (IsSelectedNode(cursorIndex))
            display.DrawText("HUD SELECTED", row++, COL_DETAIL, Color.white);

        if (IsExecutorNode(cursorIndex))
            display.DrawText("EXEC ACTIVE", row++, COL_DETAIL, Color.white);
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearGraphics();
        display.ClearText();

        DrawGlobalIndicators(display);
        DrawList(display);
        DrawDetails(display);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    private byte EncodeCursorOrNone(int value)
    {
        if (value < 0 || value >= 255) return 255;
        return (byte)value;
    }

    private int DecodeCursorOrNone(byte value)
    {
        return (value == 255) ? -1 : (int)value;
    }

    private void ApplyCursorFromSynced()
    {
        int decoded = DecodeCursorOrNone(_cursorIndex);
        cursorIndex = (decoded >= 0) ? decoded : 0;
    }

    private void SetSyncedCursor(int idx)
    {
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

        _cursorIndex = EncodeCursorOrNone(idx);
        ApplyCursorFromSynced();
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        ApplyCursorFromSynced();
        ClampCursorToExistingNode();
    }
}