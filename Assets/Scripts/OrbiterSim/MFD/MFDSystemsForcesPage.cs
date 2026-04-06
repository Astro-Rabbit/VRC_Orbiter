using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDSystemsForcesPage : MFDPage
{
    [Header("References")]
    public CraftAttitudeState attState;
    public EffectsSyncState effectsSync;
    public GC_RuntimeState runtime;

    [Header("Ship Image Layout (display UV 0..1)")]
    [Tooltip("xmin, ymin, xmax, ymax in MFD UV space.")]
    public Vector4 shipRectUv = new Vector4(0.58f, 0.10f, 0.95f, 0.86f);

    [Header("Image Source UV")]
    [Tooltip("umin, vmin, umax, vmax in source texture UV space.")]
    public Vector4 shipSourceUv = new Vector4(0f, 0f, 1f, 1f);

    [Header("Ship Image")]
    public Texture shipOutline;

    [Header("Force/Torque display scaling")]
    public float maxDisplayForceN = 5000f;
    public float maxDisplayTorqueNm = 5000f;
    public float arrowHalfLenUv = 0.05f;

    [Header("Fuel hooks (temporary until real propellant source is wired)")]
    public bool hasFuelData = false;
    [Range(0f, 1f)] public float mainFuel01 = 0f;
    [Range(0f, 1f)] public float rcsFuel01 = 0f;

    public const int RCS_GLYPH_LINE = 0;
    public const int RCS_GLYPH_CIRCLE = 1;
    public const int RCS_GLYPH_X = 2;

    [Header("RCS Indicator Layout")]
    [Tooltip("Indicator center positions in display UV space. Put nearby indicators together to form a pack.")]
    public Vector2[] rcsIndicatorPosUv;

    [Tooltip("Indicator line directions in display UV space. Used for LINE glyphs. Example: (1,0), (-1,0), (0,1), (0,-1).")]
    public Vector2[] rcsIndicatorDirUv;

    [Tooltip("Glyph type per indicator: 0 = LINE, 1 = CIRCLE, 2 = X.")]
    public int[] rcsIndicatorGlyphType;



    [Header("RCS Indicator Styling")]
    public float rcsIndicatorLenUv = 0.018f;
    public float rcsIndicatorGapUv = 0.004f;
    public float rcsIndicatorStubUv = 0.007f;
    public float rcsZGlyphHalfSizeUv = 0.007f;

    public Color rcsOffColor = new Color(0f, 0.35f, 0f, 1f);
    public Color rcsLoColor = Color.yellow;
    public Color rcsHiColor = Color.red;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 0)
        {
            display.SetPage((byte)MFDPageID.SystemsMenu);
            return;
        }

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

        display.DrawText("SYSTEMS / FORCES", 0, 14, Color.green);

        DrawRatesBlock(display, 2, 1);
        DrawCmdBlock(display, 7, 1);
        DrawFuelBlock(display, 16, 1);

        DrawGCBlock(display, 2, 20);
        DrawCTRLBlock(display, 8, 20);

        DrawShipOverlay(display);
        DrawRcsIndicators(display);

        display.DrawText("SYS",  MFD.TEXT_ROWS - 1, 2, Color.white);
        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    private void DrawRatesBlock(MFD display, int row, int col)
    {
        display.DrawText("RATES", row + 0, col, Color.green);

        if (attState == null)
        {
            display.DrawText("WX  ---", row + 1, col, Color.green);
            display.DrawText("WY  ---", row + 2, col, Color.green);
            display.DrawText("WZ  ---", row + 3, col, Color.green);
            return;
        }

        display.DrawText(FormatSigned("WX", attState.wx), row + 1, col, Color.green);
        display.DrawText(FormatSigned("WY", attState.wy), row + 2, col, Color.green);
        display.DrawText(FormatSigned("WZ", attState.wz), row + 3, col, Color.green);
    }

    private void DrawCmdBlock(MFD display, int row, int col)
    {
        display.DrawText("CMD", row + 0, col, Color.green);

        if (effectsSync == null)
        {
            display.DrawText("TX ---", row + 1, col, Color.green);
            display.DrawText("TY ---", row + 2, col, Color.green);
            display.DrawText("TZ ---", row + 3, col, Color.green);
            display.DrawText("FX ---", row + 4, col, Color.green);
            display.DrawText("FY ---", row + 5, col, Color.green);
            display.DrawText("FZ ---", row + 6, col, Color.green);
            return;
        }

        float tx = effectsSync.cmdTauX_dNm * 0.1f;
        float ty = effectsSync.cmdTauY_dNm * 0.1f;
        float tz = effectsSync.cmdTauZ_dNm * 0.1f;

        float fx = effectsSync.cmdTransX_dN * 0.1f;
        float fy = effectsSync.cmdTransY_dN * 0.1f;
        float fz = effectsSync.cmdTransZ_dN * 0.1f;

        display.DrawText(FormatSigned("TX", tx), row + 1, col, Color.green);
        display.DrawText(FormatSigned("TY", ty), row + 2, col, Color.green);
        display.DrawText(FormatSigned("TZ", tz), row + 3, col, Color.green);
        display.DrawText(FormatSigned("FX", fx), row + 4, col, Color.green);
        display.DrawText(FormatSigned("FY", fy), row + 5, col, Color.green);
        display.DrawText(FormatSigned("FZ", fz), row + 6, col, Color.green);
    }

    private void DrawFuelBlock(MFD display, int row, int col)
    {
        display.DrawText("FUEL", row + 0, col, Color.green);

        if (!hasFuelData)
        {
            display.DrawText("MAIN ---", row + 1, col, Color.green);
            display.DrawText("RCS  ---", row + 2, col, Color.green);
            return;
        }

        display.DrawText(MFD.FormatPercent("MAIN", mainFuel01), row + 1, col, Color.green);
        display.DrawText(MFD.FormatPercent("RCS", rcsFuel01), row + 2, col, Color.green);
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
        {
            return "EXEC";
        }

        if (runtime.activeModeId != GC_RuntimeState.MODE_MANUAL)
        {
            return "MODE";
        }

        return "MAN";
    }

    private string ThrSourceToString()
    {
        if (runtime == null) return "---";

        if (runtime.status == GC_RuntimeState.STATUS_FAULT)
        {
            return "SAFE";
        }

        if (runtime.activeExecutorId != GC_RuntimeState.EXEC_NONE &&
            (runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_BURN ||
             runtime.executorPhase == GC_RuntimeState.EXEC_PHASE_POST))
        {
            return "EXEC";
        }

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

    private void DrawShipOverlay(MFD display)
    {
        if (effectsSync == null) return;

        float tx = effectsSync.cmdTauX_dNm * 0.1f;
        float ty = effectsSync.cmdTauY_dNm * 0.1f;
        float tz = effectsSync.cmdTauZ_dNm * 0.1f;

        float fx = effectsSync.cmdTransX_dN * 0.1f;
        float fy = effectsSync.cmdTransY_dN * 0.1f;
        float fz = effectsSync.cmdTransZ_dN * 0.1f;

        Vector2 c = CenterOf(shipRectUv);

        Vector2 top = new Vector2(c.x, shipRectUv.w - 0.03f);
        Vector2 bot = new Vector2(c.x, shipRectUv.y + 0.03f);
        Vector2 lft = new Vector2(shipRectUv.x + 0.03f, c.y);
        Vector2 rgt = new Vector2(shipRectUv.z - 0.03f, c.y);

        DrawAxisArrow(display, rgt, new Vector2(1f, 0f), fx, maxDisplayForceN, Color.cyan);
        DrawAxisArrow(display, lft, new Vector2(-1f, 0f), fx, maxDisplayForceN, Color.cyan);

        DrawAxisArrow(display, top, new Vector2(0f, 1f), fy, maxDisplayForceN, Color.yellow);
        DrawAxisArrow(display, bot, new Vector2(0f, -1f), fy, maxDisplayForceN, Color.yellow);

        DrawAxisArrow(display, c + new Vector2(0.07f, 0f), new Vector2(0f, 1f), fz, maxDisplayForceN, Color.green);

        // DrawTorqueTick(display, c + new Vector2(-0.09f, 0.10f), tx, maxDisplayTorqueNm, true, Color.magenta);
        // DrawTorqueTick(display, c + new Vector2( 0.09f, 0.10f), ty, maxDisplayTorqueNm, false, Color.magenta);
        // DrawTorqueTick(display, c + new Vector2( 0.00f,-0.11f), tz, maxDisplayTorqueNm, true, Color.magenta);
    }

    private void DrawAxisArrow(MFD display, Vector2 anchor, Vector2 dir, float value, float fullScale, Color color)
    {
        float mag = Mathf.Abs(value);
        if (mag < 0.001f) return;

        float s = (fullScale > 0f) ? Mathf.Clamp01(mag / fullScale) : 0f;
        float len = s * arrowHalfLenUv;

        Vector2 actualDir = (value >= 0f) ? dir : -dir;
        display.DrawLine(anchor, anchor + actualDir * len, color);
    }

    private void DrawTorqueTick(MFD display, Vector2 center, float value, float fullScale, bool horizontal, Color color)
    {
        float mag = Mathf.Abs(value);
        if (mag < 0.001f) return;

        float s = (fullScale > 0f) ? Mathf.Clamp01(mag / fullScale) : 0f;
        float len = Mathf.Lerp(0.01f, 0.04f, s);

        if (horizontal)
        {
            display.DrawLine(center - new Vector2(len, 0f), center + new Vector2(len, 0f), color);
        }
        else
        {
            display.DrawLine(center - new Vector2(0f, len), center + new Vector2(0f, len), color);
        }
    }

    private Vector2 CenterOf(Vector4 rectUv)
    {
        return new Vector2(
            0.5f * (rectUv.x + rectUv.z),
            0.5f * (rectUv.y + rectUv.w)
        );
    }
    private void DrawRcsIndicators(MFD display)
    {
        if (effectsSync == null) return;
        if (rcsIndicatorPosUv == null) return;
        if (rcsIndicatorDirUv == null) return;
        if (rcsIndicatorGlyphType == null) return;

        int count = rcsIndicatorPosUv.Length;

        if (rcsIndicatorDirUv.Length < count) count = rcsIndicatorDirUv.Length;
        if (rcsIndicatorGlyphType.Length < count) count = rcsIndicatorGlyphType.Length;
        if (count > 32) count = 32;

        uint hiMask = effectsSync.rcsHiMask;
        uint loMask = effectsSync.rcsLoMask;

        for (int i = 0; i < count; i++)
        {
            Vector2 p = rcsIndicatorPosUv[i];
            Vector2 d = rcsIndicatorDirUv[i];
            int glyphType = rcsIndicatorGlyphType[i];

            bool hiOn = IsBitOn(hiMask, i);
            bool loOn = IsBitOn(loMask, i);

            Color c = rcsOffColor;
            if (hiOn) c = rcsHiColor;
            else if (loOn) c = rcsLoColor;

            DrawRcsIndicatorGlyph(display, p, d, glyphType, c);
        }
    }

    private bool IsBitOn(uint mask, int bitIndex)
    {
        if (bitIndex < 0 || bitIndex > 31) return false;
        uint bit = 1u << bitIndex;
        return (mask & bit) != 0u;
    }

    private void DrawRcsIndicatorGlyph(MFD display, Vector2 center, Vector2 dir, int glyphType, Color color)
    {
        if (glyphType == RCS_GLYPH_CIRCLE)
        {
            DrawRcsCircleGlyph(display, center, color);
            return;
        }

        if (glyphType == RCS_GLYPH_X)
        {
            DrawRcsXGlyph(display, center, color);
            return;
        }

        DrawRcsLineGlyph(display, center, dir, color);
    }

    private void DrawRcsLineGlyph(MFD display, Vector2 center, Vector2 dir, Color color)
    {
        float mag = dir.magnitude;
        if (mag < 1e-5f) return;
        dir /= mag;

        Vector2 n = new Vector2(-dir.y, dir.x);

        // Small transverse marker
        display.DrawLine(
            center - n * rcsIndicatorStubUv,
            center + n * rcsIndicatorStubUv,
            color
        );

        // Firing direction stroke
        Vector2 a = center + dir * rcsIndicatorGapUv;
        Vector2 b = a + dir * rcsIndicatorLenUv;

        display.DrawLine(a, b, color);
    }

    private void DrawRcsCircleGlyph(MFD display, Vector2 center, Color color)
    {
        float s = rcsZGlyphHalfSizeUv;

        Vector2 top = center + new Vector2(0f, s);
        Vector2 bot = center + new Vector2(0f, -s);
        Vector2 lft = center + new Vector2(-s, 0f);
        Vector2 rgt = center + new Vector2(s, 0f);

        display.DrawLine(top, rgt, color);
        display.DrawLine(rgt, bot, color);
        display.DrawLine(bot, lft, color);
        display.DrawLine(lft, top, color);
    }

    private void DrawRcsXGlyph(MFD display, Vector2 center, Color color)
    {
        float s = rcsZGlyphHalfSizeUv;

        display.DrawLine(
            center + new Vector2(-s, -s),
            center + new Vector2(s, s),
            color
        );

        display.DrawLine(
            center + new Vector2(-s, s),
            center + new Vector2(s, -s),
            color
        );
    }
    private string FormatSigned(string title, double value)
    {
        return title + " " + value.ToString("+0.00;-0.00; 0.00");
    }
}