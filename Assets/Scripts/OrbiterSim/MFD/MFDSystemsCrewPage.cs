using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDSystemsCrewPage : MFDPage
{
    [Header("References")]
    public DockingOccupancyGate occupancyGate;
    public CraftNetState netState;

    private VRCPlayerApi[] _players = new VRCPlayerApi[80];

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 1)
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
        display.ClearImagePanel();

        display.DrawText("SYSTEMS / CREW", 0, 16, Color.green);

        VRCPlayerApi.GetPlayers(_players);
        int playerCount = VRCPlayerApi.GetPlayerCount();

        bool isDocked = IsDocked();

        DrawInstanceList(display, playerCount);

        if (isDocked)
        {
            DrawStationList(display, playerCount);
            DrawCountsDocked(display, playerCount);
        }
        else
        {
            DrawCountsUndocked(display, playerCount);
            display.DrawText("NO DOCK", 4, 28, Color.green);
        }

        display.DrawText("SYS",  MFD.TEXT_ROWS - 1, 2, Color.white);
        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    // --------------------------------------------------------
    // Dock state (REAL SOURCE)
    // --------------------------------------------------------

    private bool IsDocked()
    {
        if (netState == null) return false;

        // Option A (simple, authoritative)
        return netState.mode == CraftNetState.MODE_DOCKED;

        // Option B (better visual consistency for remotes)
        // double t = Networking.GetServerTimeInSeconds();
        // return netState.GetPresentedMode(t) == CraftNetState.MODE_DOCKED;
    }

    // --------------------------------------------------------
    // Left side: full instance
    // --------------------------------------------------------

    private void DrawInstanceList(MFD display, int playerCount)
    {
        display.DrawText("INSTANCE", 2, 1, Color.green);

        int row = 3;

        for (int i = 0; i < playerCount && i < _players.Length; i++)
        {
            VRCPlayerApi p = _players[i];
            if (!Utilities.IsValid(p)) continue;

            bool onboard = IsPlayerOnboard(p);

            // Color coding
            Color c = onboard ? Color.white : new Color(0.6f, 1f, 0.6f, 1f);

            // Marker
            string marker = onboard ? "C " : "S ";

            string name = TrimName(p.displayName, 20);

            if (row < 20)
            {
                display.DrawText(marker + name, row, 1, c);
                row++;
            }
        }
    }

    // --------------------------------------------------------
    // Right side: station occupants (only when docked)
    // --------------------------------------------------------

    private void DrawStationList(MFD display, int playerCount)
    {
        display.DrawText("STATION", 2, 27, Color.green);

        int row = 3;

        for (int i = 0; i < playerCount && i < _players.Length; i++)
        {
            VRCPlayerApi p = _players[i];
            if (!Utilities.IsValid(p)) continue;

            if (IsPlayerOnboard(p)) continue;

            if (row < 20)
            {
                display.DrawText(TrimName(p.displayName, 18), row, 27, Color.white);
                row++;
            }
        }
    }

    // --------------------------------------------------------
    // Counts
    // --------------------------------------------------------

    private void DrawCountsDocked(MFD display, int playerCount)
    {
        int stationCount = GetStationCount(playerCount);
        int onboardCount = playerCount - stationCount;

        if (onboardCount < 0) onboardCount = 0;

        display.DrawText("ONBOARD " + onboardCount, 21, 1, Color.green);
        display.DrawText("STATION " + stationCount, 21, 27, Color.green);
    }

    private void DrawCountsUndocked(MFD display, int playerCount)
    {
        int onboardCount = GetOnboardCount(playerCount);

        display.DrawText("ONBOARD " + onboardCount, 21, 1, Color.green);
        display.DrawText("INST " + playerCount, 21, 27, Color.green);
    }

    private int GetOnboardCount(int playerCount)
    {
        int count = 0;

        for (int i = 0; i < playerCount && i < _players.Length; i++)
        {
            VRCPlayerApi p = _players[i];
            if (!Utilities.IsValid(p)) continue;

            if (IsPlayerOnboard(p))
                count++;
        }

        return count;
    }

    private int GetStationCount(int playerCount)
    {
        int count = 0;

        for (int i = 0; i < playerCount && i < _players.Length; i++)
        {
            VRCPlayerApi p = _players[i];
            if (!Utilities.IsValid(p)) continue;

            if (!IsPlayerOnboard(p))
                count++;
        }

        return count;
    }

    // --------------------------------------------------------
    // Occupancy logic (authoritative source)
    // --------------------------------------------------------

    private bool IsPlayerOnboard(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return false;

        if (occupancyGate == null)
            return true;

        // outside == not onboard craft
        return !occupancyGate.IsPlayerOutside(player);
    }

    private string TrimName(string s, int maxLen)
    {
        if (s == null) return "";
        if (s.Length <= maxLen) return s;
        return s.Substring(0, maxLen);
    }
}