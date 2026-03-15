using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MFDTargetPage : MFDPage
{
    [Header("References")]
    public OrbitAnalyzer tgtAnalyzer;
    public GuidanceNavContactsComputer contacts;
    public GuidanceNavContactsState contactsState;

    private StationStateModel station = null;

    [UdonSynced] private int targetIndex = -1;
    [UdonSynced] private int portIndex = -1;

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
        } else if (side == ButtonSide.Left) {
            if (num == 1 && targetIndex > 0) {
                SetTargetIndex(targetIndex - 1);
            } else if (num == 3 && targetIndex < contacts.stations.Length - 1) {
                SetTargetIndex(targetIndex + 1);
            }
        } else if (side == ButtonSide.Right) {
            if (num == 1 && portIndex > 0) {
                SetPortIndex(portIndex - 1);
            } else if (num == 3 && portIndex < station.dockingPortCount - 1) {
                SetPortIndex(portIndex + 1);
            }
        }
    }

    public void SetTargetIndex(int index)
    {
        if (!Networking.IsOwner(gameObject)) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        targetIndex = index;
        OnTargetIndexChanged();
    }

    public void SetPortIndex(int index)
    {
        if (!Networking.IsOwner(gameObject)) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        portIndex = index;
        OnPortIndexChanged();
    }

    private void OnTargetIndexChanged()
    {
        contactsState.selectedStationIndex = targetIndex;
        station = contacts.stations[targetIndex];
        //FIXME: Needs to be replaced when stationstatemodel gets proper references
        tgtAnalyzer.conic = (ConicState)station.gameObject.transform.GetChild(0).GetComponent(typeof(UdonBehaviour));

        portIndex = -1;
        contactsState.selectedStationDockPortIndex = portIndex;
    }

    private void OnPortIndexChanged()
    {
        contactsState.selectedStationDockPortIndex = portIndex;
    }

    public override void DrawDisplay(MFD display)
    {
        int stationCount = contacts.stations.Length;
        for (int i = 0; i < stationCount; i++) {
            display.DrawText(contacts.stations[i].gameObject.name, 2 + i, 2, i == targetIndex ? Color.green : Color.white);
        }

        if (station != null) {
            int portCount = station.dockingPortCount;
            for (int i = 0; i < portCount; i++) {
                display.DrawText("Docking Port " + i, 2 + i, 24, i == portIndex ? Color.green : Color.white);
            }
        }

        display.DrawVerticalText(" Λ| ", 5, 0, Color.white);
        display.DrawVerticalText(" |V ", 15, 0, Color.white);
        display.DrawVerticalText(" Λ| ", 5, MFD.TEXT_COLUMNS - 1, Color.white);
        display.DrawVerticalText(" |V ", 15, MFD.TEXT_COLUMNS - 1, Color.white);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    public override void OnDeserialization()
    {
        OnTargetIndexChanged();
        OnPortIndexChanged();
    }
}
