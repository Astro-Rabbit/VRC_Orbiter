using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDTargetPage : MFDPage
{
    [Header("References")]
    public CraftNetState netCore;
    public OrbitAnalyzer tgtAnalyzer;
    public GuidanceNavContactsComputer contacts;
    public GuidanceNavContactsState contactsState;

    [Header("Target Orbit Sources")]
    [Tooltip("Must match contacts.stations[] order exactly.")]
    public ConicState[] stationConics;

    private StationStateModel station = null;
    private int cachedTargetIndex = int.MinValue;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        int targetIndex = GetSelectedTargetIndex();
        int portIndex = GetSelectedPortIndex();

        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
            return;
        }

        if (contacts == null || contacts.stations == null) {
            return;
        }

        int stationCount = contacts.stations.Length;

        if (side == ButtonSide.Left) {
            if (num == 1 && targetIndex > 0) {
                RequestTargetIndex(targetIndex - 1);
            } else if (num == 3 && targetIndex < stationCount - 1) {
                RequestTargetIndex(targetIndex + 1);
            }
        } else if (side == ButtonSide.Right) {
            if (targetIndex < 0 || targetIndex >= stationCount) {
                return;
            }

            StationStateModel st = contacts.stations[targetIndex];
            if (st == null) {
                return;
            }

            int portCount = st.dockingPortCount;

            if (num == 1 && portIndex > 0) {
                RequestPortIndex(targetIndex, portIndex - 1);
            } else if (num == 3 && portIndex < portCount - 1) {
                RequestPortIndex(targetIndex, portIndex + 1);
            }
        }
    }

    private void Update()
    {
        RefreshSelectedStationBindings();
    }

    private int GetSelectedTargetIndex()
    {
        if (contactsState == null) return -1;
        return contactsState.selectedStationIndex;
    }

    private int GetSelectedPortIndex()
    {
        if (contactsState == null) return -1;
        return contactsState.selectedStationDockPortIndex;
    }

    private void RequestTargetIndex(int index)
    {
        if (netCore == null) return;

        netCore.SendCustomNetworkEvent(
            NetworkEventTarget.Owner,
            nameof(CraftNetState.Net_RequestSelectedStation),
            index
        );
    }

    private void RequestPortIndex(int targetIndex, int portIndex)
    {
        if (netCore == null) return;

        netCore.SendCustomNetworkEvent(
            NetworkEventTarget.Owner,
            nameof(CraftNetState.Net_RequestSelectedStationPort),
            targetIndex,
            portIndex
        );
    }

    private void RefreshSelectedStationBindings()
    {
        int targetIndex = GetSelectedTargetIndex();

        if (targetIndex == cachedTargetIndex) {
            return;
        }

        cachedTargetIndex = targetIndex;
        station = null;

        if (tgtAnalyzer != null) {
            tgtAnalyzer.conic = null;
        }

        if (contacts == null || contacts.stations == null) {
            return;
        }

        if (targetIndex < 0 || targetIndex >= contacts.stations.Length) {
            return;
        }

        station = contacts.stations[targetIndex];

        if (tgtAnalyzer == null || stationConics == null) {
            return;
        }

        if (targetIndex < 0 || targetIndex >= stationConics.Length) {
            return;
        }

        tgtAnalyzer.conic = stationConics[targetIndex];
        if (tgtAnalyzer.conic != null) {
            tgtAnalyzer.UpdateInfo();
        }
    }

    public override void DrawDisplay(MFD display)
    {
        RefreshSelectedStationBindings();

        display.ClearGraphics();
        display.ClearText();

        int targetIndex = GetSelectedTargetIndex();
        int portIndex = GetSelectedPortIndex();

        if (contacts != null && contacts.stations != null) {
            int stationCount = contacts.stations.Length;

            for (int i = 0; i < stationCount; i++) {
                string name = contacts.stations[i] != null ? contacts.stations[i].gameObject.name : ("Station " + i);
                display.DrawText(name, 2 + i, 2, i == targetIndex ? Color.green : Color.white);
            }
        }

        if (station != null) {
            int portCount = station.dockingPortCount;
            for (int i = 0; i < portCount; i++) {
                display.DrawText("Docking Port " + i, 2 + i, 24, i == portIndex ? Color.green : Color.white);
            }
        }

        display.DrawVerticalText(" ^| ", 5, 0, Color.white);
        display.DrawVerticalText(" |V ", 15, 0, Color.white);
        display.DrawVerticalText(" ^| ", 5, MFD.TEXT_COLUMNS - 1, Color.white);
        display.DrawVerticalText(" |V ", 15, MFD.TEXT_COLUMNS - 1, Color.white);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}