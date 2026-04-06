using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class MFDSystemsPdLimitsPage : MFDPage
{
    [Header("References")]
    public AttitudeRateLimitConfigSync rateLimitConfig;

    [Header("Comfort warning")]
    [Tooltip("If limiter is OFF or selected rate exceeds this threshold, require confirmation.")]
    public int comfortWarnThresholdDegPerSec = 25;

    private bool _showConfirmPopup = false;

    private byte _confirmAction = ACTION_NONE;
    private bool _confirmEnableValue = true;
    private int _confirmAdjustDelta = 0;

    private const byte ACTION_NONE = 0;
    private const byte ACTION_SET_ENABLE = 1;
    private const byte ACTION_ADJUST_LIMIT = 2;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (_showConfirmPopup)
        {
            OnPopupButton(display, side, num);
            return;
        }

        bool canEdit = CanLocalEdit();
        bool canToggleLock = CanLocalToggleLock();

        if (side == ButtonSide.Bottom && num == 0)
        {
            display.SetPage((byte)MFDPageID.SystemsMenu);
            return;
        }

        if (side == ButtonSide.Bottom && num == 2)
        {
            display.SetPage((byte)MFDPageID.Menu);
            return;
        }

        if (side == ButtonSide.Right && num == 0)
        {
            if (!canToggleLock || rateLimitConfig == null) return;

            bool nextLock = !rateLimitConfig.GetRestrictToSimOwner();
            rateLimitConfig.SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(AttitudeRateLimitConfigSync.Net_RequestSetRestrictToSimOwner),
                nextLock
            );
            return;
        }

        if (!canEdit || rateLimitConfig == null) return;

        if (side == ButtonSide.Left && num == 0)
        {
            bool nextEnable = !rateLimitConfig.GetLimiterEnabled();

            if (WouldNeedWarningForEnable(nextEnable))
            {
                _confirmAction = ACTION_SET_ENABLE;
                _confirmEnableValue = nextEnable;
                _confirmAdjustDelta = 0;
                _showConfirmPopup = true;
            }
            else
            {
                rateLimitConfig.SendCustomNetworkEvent(
                    NetworkEventTarget.Owner,
                    nameof(AttitudeRateLimitConfigSync.Net_RequestSetLimiterEnabled),
                    nextEnable
                );
            }
            return;
        }

        if (side == ButtonSide.Left && num == 1)
        {
            TryAdjustLimit(-1);
            return;
        }

        if (side == ButtonSide.Left && num == 2)
        {
            TryAdjustLimit(+1);
            return;
        }
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearGraphics();
        display.ClearText();
        display.ClearImagePanel();

        display.DrawText("SYSTEMS / PD LIMITS", 0, 14, Color.green);

        if (_showConfirmPopup)
            DrawPopupPage(display);
        else
            DrawMainPage(display);

        display.DrawText("SYS",  MFD.TEXT_ROWS - 1, 2, Color.white);
        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    private void DrawMainPage(MFD display)
    {
        bool enabled = false;
        int appliedDeg = 0;
        bool locked = false;
        bool canEdit = CanLocalEdit();
        bool canToggleLock = CanLocalToggleLock();

        if (rateLimitConfig != null)
        {
            enabled = rateLimitConfig.GetLimiterEnabled();
            appliedDeg = Mathf.RoundToInt(rateLimitConfig.GetLimitDegPerSec());
            locked = rateLimitConfig.GetRestrictToSimOwner();
        }

        Color lockColor = locked ? Color.yellow : Color.green;
        string lockText = locked ? "LOCK ON" : "LOCK OFF";

        display.DrawText("TOGGLE", 2, 1, Color.white);
        display.DrawText("RATE -", 7, 1, Color.white);
        display.DrawText("RATE +", 12, 1, Color.white);

        if (canToggleLock)
            display.DrawText("LOCK", 3, 37, Color.white);
        else
            display.DrawText("LOCK", 3, 37, new Color(0.35f, 0.35f, 0.35f, 1f));

        display.DrawText("LOCK", 3, 16, Color.green);
        display.DrawText(lockText, 4, 16, lockColor);

        if (locked && !canEdit)
            display.DrawText("LOCKED TO YOU", 5, 13, Color.yellow);
        else if (locked)
            display.DrawText("OWNER ONLY", 5, 15, Color.yellow);
        else
            display.DrawText("OPEN ACCESS", 5, 14, Color.green);

        display.DrawText("LIMITER", 9, 16, Color.green);
        display.DrawText(enabled ? "ON" : "OFF", 10, 19, enabled ? Color.green : Color.yellow);

        display.DrawText("RATE LIMIT", 13, 15, Color.green);
        display.DrawText(appliedDeg.ToString().PadLeft(3) + " DEG/S", 14, 15, Color.green);
        display.DrawText("STEP 5 DEG/S", 16, 16, Color.green);

        if (!canEdit)
        {
            display.DrawText("EDIT DISABLED", 19, 14, Color.yellow);
        }
        else if (!enabled)
        {
            display.DrawText("WARN LIMITER OFF", 19, 13, Color.yellow);
        }
        else if (appliedDeg > comfortWarnThresholdDegPerSec)
        {
            display.DrawText("WARN HIGH RATE", 19, 14, Color.yellow);
        }
    }

    private void DrawPopupPage(MFD display)
    {
        // Labels beside the actual top buttons:
        // T1 = top button index 0
        // T5 = top button index 4
        display.DrawText("BACK", 17, 1, Color.white);
        display.DrawText("CONT", 17, 43, Color.white);

        // Box sized to actually surround the popup text block
        DrawBox(display, new Vector2(-0.68f, 0.70f), new Vector2(0.68f,-0.68f), Color.green);
        DrawBox(display, new Vector2(-.70f, 0.72f), new Vector2(0.70f, -0.70f), Color.green);

        display.DrawText("PASSENGER COMFORT", 6, 15, Color.yellow);
        display.DrawText("WARNING", 7, 20, Color.yellow);

        if (_confirmAction == ACTION_SET_ENABLE && !_confirmEnableValue)
        {
            display.DrawText("RATE LIMITER WILL BE", 10, 14, Color.white);
            display.DrawText("DISABLED.", 11, 20, Color.white);
            display.DrawText("HIGH ANGULAR RATES MAY", 13, 13, Color.white);
            display.DrawText("DISCOMFORT PASSENGERS.", 14, 13, Color.white);
            display.DrawText("CONTINUE?", 18, 19, Color.white);
        }
        else
        {
            int targetDeg = GetConfirmTargetDeg();

            display.DrawText("SELECTED RATE EXCEEDS", 11, 12, Color.white);
            display.DrawText("COMFORT THRESHOLD.", 12, 14, Color.white);
            display.DrawText("NEW LIMIT " + targetDeg + " DEG/S", 14, 13, Color.white);
            display.DrawText("CONTINUE?", 17, 19, Color.white);
        }
    }

    private void OnPopupButton(MFD display, ButtonSide side, int num)
    {
        // T1 = BACK
        if (side == ButtonSide.Left && num == 3)
        {
            _showConfirmPopup = false;
            _confirmAction = ACTION_NONE;
            _confirmAdjustDelta = 0;
            return;
        }

        // T5 = CONTINUE
        if (side == ButtonSide.Right && num == 3)
        {
            _showConfirmPopup = false;
            CommitConfirmAction();
            _confirmAction = ACTION_NONE;
            _confirmAdjustDelta = 0;
            return;
        }

        if (side == ButtonSide.Bottom && num == 1)
        {
            _showConfirmPopup = false;
            _confirmAction = ACTION_NONE;
            _confirmAdjustDelta = 0;
        }
    }

    private void TryAdjustLimit(int delta)
    {
        if (rateLimitConfig == null) return;

        int curDeg = Mathf.RoundToInt(rateLimitConfig.GetLimitDegPerSec());
        int nextDeg = curDeg + (delta * 5);

        if (nextDeg < 0) nextDeg = 0;

        if (delta > 0 && nextDeg > comfortWarnThresholdDegPerSec)
        {
            _confirmAction = ACTION_ADJUST_LIMIT;
            _confirmEnableValue = true;
            _confirmAdjustDelta = delta;
            _showConfirmPopup = true;
            return;
        }

        rateLimitConfig.SendCustomNetworkEvent(
            NetworkEventTarget.Owner,
            nameof(AttitudeRateLimitConfigSync.Net_RequestAdjustLimitSteps),
            delta
        );
    }

    private void CommitConfirmAction()
    {
        if (rateLimitConfig == null) return;
        if (!CanLocalEdit()) return;

        if (_confirmAction == ACTION_SET_ENABLE)
        {
            rateLimitConfig.SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(AttitudeRateLimitConfigSync.Net_RequestSetLimiterEnabled),
                _confirmEnableValue
            );
            return;
        }

        if (_confirmAction == ACTION_ADJUST_LIMIT)
        {
            rateLimitConfig.SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(AttitudeRateLimitConfigSync.Net_RequestAdjustLimitSteps),
                _confirmAdjustDelta
            );
        }
    }

    private bool WouldNeedWarningForEnable(bool nextEnable)
    {
        if (!nextEnable) return true;
        return false;
    }

    private int GetConfirmTargetDeg()
    {
        if (rateLimitConfig == null) return 0;

        int curDeg = Mathf.RoundToInt(rateLimitConfig.GetLimitDegPerSec());
        if (_confirmAction == ACTION_ADJUST_LIMIT)
            return curDeg + (_confirmAdjustDelta * 5);

        return curDeg;
    }

    private bool CanLocalEdit()
    {
        if (rateLimitConfig == null) return false;
        return rateLimitConfig.CanLocalUserEdit();
    }

    private bool CanLocalToggleLock()
    {
        if (rateLimitConfig == null) return false;
        return rateLimitConfig.CanLocalUserToggleLock();
    }

    private void DrawBox(MFD display, Vector2 minUv, Vector2 maxUv, Color color)
    {
        Vector2 p1 = new Vector2(minUv.x, minUv.y);
        Vector2 p2 = new Vector2(maxUv.x, minUv.y);
        Vector2 p3 = new Vector2(maxUv.x, maxUv.y);
        Vector2 p4 = new Vector2(minUv.x, maxUv.y);

        display.DrawLine(p1, p2, color);
        display.DrawLine(p2, p3, color);
        display.DrawLine(p3, p4, color);
        display.DrawLine(p4, p1, color);
    }
}