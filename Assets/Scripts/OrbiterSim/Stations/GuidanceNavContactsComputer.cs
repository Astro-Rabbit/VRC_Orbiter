using UdonSharp;
using UnityEngine;
using System;
using VRC.SDKBase;

public class GuidanceNavContactsComputer : UdonSharpBehaviour
{
    [Header("Inputs")]
    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;
    public StationStateModel[] stations;

    [Header("Docking ports (craft)")]
    public CraftDockingPorts craftDockPorts;

    [Header("Output")]
    public GuidanceNavContactsState contacts;

    [Header("Policy")]
    public bool computeRange = true;

    [Tooltip("Only consider non-selected stations within this distance for slot1 full-detail.")]
    public double fullDetailRangeMeters = 500000.0;

    [Tooltip("If true, slot1 will always pick nearest-other even if far (ignores fullDetailRangeMeters).")]
    public bool allowSlot1BeyondRange = false;

    [Header("Debug")]
    public bool logMissing = false;

    [Header("Remote render sampling (optional)")]
    public SimClock clock;
    public CraftNetState netCore;
    public CraftNetAttitude netAtt;
    public CraftNetKinematics netKin;

    private double _simRenderT;

    private Quaternion _qCraftForRender = Quaternion.identity;
    private double _crxRender, _cryRender, _crzRender;
    private double _cvxRender, _cvyRender, _cvzRender;

    public void Evaluate()
    {

        if (craft == null || craftAtt == null || contacts == null || stations == null)
        {
            if (logMissing) Debug.Log("[GuidanceNavContactsComputer] Missing references.");
            return;
        }

        // Default to latest authoritative-follow state
        _qCraftForRender = craftAtt.qBE;
        _crxRender = craft.rx;
        _cryRender = craft.ry;
        _crzRender = craft.rz;
        _cvxRender = craft.vx;
        _cvyRender = craft.vy;
        _cvzRender = craft.vz;
        _simRenderT = 0.0;
        // Remote render presentation sample
        if (clock != null && netCore != null)
        {
            if (!Networking.IsOwner(netCore.gameObject))
            {
                double backTime = 0.0;
                if (netAtt != null) backTime = (double)netAtt.interpBackTimeSeconds;

                double tRender = clock.GetCachedRemoteRenderTime();
                byte presentedMode = netCore.GetPresentedMode(tRender);

                if (netAtt != null)
                    _qCraftForRender = netAtt.SampleRenderQuaternion(tRender);

                if (presentedMode == CraftNetState.MODE_INTEGRATED && netKin != null)
                {
                    // Contacts remain based on RAW coherent craft translation, not smoothed presentation.
                    // StationRenderManager does the visual smoothing later in body-relative space.
                    if (netKin.rawValid)
                    {
                        _simRenderT = netKin.rawSimT;
                        _crxRender = netKin.rawRx;
                        _cryRender = netKin.rawRy;
                        _crzRender = netKin.rawRz;
                        _cvxRender = netKin.rawVx;
                        _cvyRender = netKin.rawVy;
                        _cvzRender = netKin.rawVz;
                    }
                }
            }
        }

        int n = stations.Length;
        contacts.EnsureSize(n);
        contacts.ClearFull();

        // Craft inertial render position
        double crx = _crxRender;
        double cry = _cryRender;
        double crz = _crzRender;

        // Pass A: dr_E + range2 for all stations
        for (int i = 0; i < n; i++)
        {
            StationStateModel st = stations[i];
            bool ok = (st != null && st.valid);

            contacts.valid[i] = ok;

            if (!ok)
            {
                contacts.drx_E[i] = contacts.dry_E[i] = contacts.drz_E[i] = 0.0;
                contacts.range2_m2[i] = double.PositiveInfinity;
                contacts.range_m[i] = double.PositiveInfinity;
                continue;
            }

            double drx = st.rx - crx;
            double dry = st.ry - cry;
            double drz = st.rz - crz;

            contacts.drx_E[i] = drx;
            contacts.dry_E[i] = dry;
            contacts.drz_E[i] = drz;

            double r2 = drx * drx + dry * dry + drz * drz;
            contacts.range2_m2[i] = r2;

            contacts.range_m[i] = computeRange ? Math.Sqrt(r2) : 0.0;
        }

        int sel = contacts.selectedStationIndex;
        if (sel >= 0 && sel < n && contacts.valid[sel])
            FillFullSlot0(sel);

        int idx1 = FindNearestOther(sel);
        if (idx1 >= 0)
            FillFullSlot1(idx1);
    }

    private int FindNearestOther(int selectedIndex)
    {
        int n = stations.Length;
        double bestR2 = double.PositiveInfinity;
        int bestIdx = -1;

        double maxR2 = fullDetailRangeMeters * fullDetailRangeMeters;

        for (int i = 0; i < n; i++)
        {
            if (!contacts.valid[i]) continue;
            if (i == selectedIndex) continue;

            double r2 = contacts.range2_m2[i];
            if (!allowSlot1BeyondRange && r2 > maxR2) continue;

            if (r2 < bestR2)
            {
                bestR2 = r2;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    private void FillFullSlot0(int stationIndex)
    {
        StationStateModel st = stations[stationIndex];

        contacts.fullStationIndex0 = stationIndex;
        contacts.fullValid0 = true;

        // dv_E using render craft velocity
        contacts.dvx_E0 = st.vx - _cvxRender;
        contacts.dvy_E0 = st.vy - _cvyRender;
        contacts.dvz_E0 = st.vz - _cvzRender;

        double drxE = contacts.drx_E[stationIndex];
        double dryE = contacts.dry_E[stationIndex];
        double drzE = contacts.drz_E[stationIndex];
        RotateEToBody(_qCraftForRender, drxE, dryE, drzE, out contacts.drx_B0, out contacts.dry_B0, out contacts.drz_B0);

        contacts.selValid = true;
        contacts.sel_drx_E = contacts.drx_E[stationIndex];
        contacts.sel_dry_E = contacts.dry_E[stationIndex];
        contacts.sel_drz_E = contacts.drz_E[stationIndex];
        contacts.sel_drx_B = contacts.drx_B0;
        contacts.sel_dry_B = contacts.dry_B0;
        contacts.sel_drz_B = contacts.drz_B0;
        contacts.sel_dvx_E = contacts.dvx_E0;
        contacts.sel_dvy_E = contacts.dvy_E0;
        contacts.sel_dvz_E = contacts.dvz_E0;

        Quaternion qC = _qCraftForRender;
        Quaternion qT = st.q_B2E;
        contacts.qTargetInB0 = Quaternion.Inverse(qC) * qT;

        ComputeDockingForSlot0(st);
    }

    private void FillFullSlot1(int stationIndex)
    {
        StationStateModel st = stations[stationIndex];

        contacts.fullStationIndex1 = stationIndex;
        contacts.fullValid1 = true;

        contacts.dvx_E1 = st.vx - _cvxRender;
        contacts.dvy_E1 = st.vy - _cvyRender;
        contacts.dvz_E1 = st.vz - _cvzRender;

        double drxE = contacts.drx_E[stationIndex];
        double dryE = contacts.dry_E[stationIndex];
        double drzE = contacts.drz_E[stationIndex];
        RotateEToBody(_qCraftForRender, drxE, dryE, drzE, out contacts.drx_B1, out contacts.dry_B1, out contacts.drz_B1);

        Quaternion qC = _qCraftForRender;
        Quaternion qT = st.q_B2E;
        contacts.qTargetInB1 = Quaternion.Inverse(qC) * qT;

        if (contacts.computeDockForSlot1)
            ComputeDockingForSlot1(st);
    }

    private void ComputeDockingForSlot0(StationStateModel st)
    {
        contacts.dockValid0 = false;

        if (st == null) return;
        if (craftDockPorts == null) return;

        int sPort = contacts.selectedStationDockPortIndex;
        int cPort = contacts.selectedCraftDockPortIndex;

        if (sPort < 0 || sPort >= st.dockingPortCount) return;
        if (cPort < 0 || cPort >= craftDockPorts.dockingPortCount) return;

        contacts.craftPort_px_B0 = craftDockPorts.dock_px_B[cPort];
        contacts.craftPort_py_B0 = craftDockPorts.dock_py_B[cPort];
        contacts.craftPort_pz_B0 = craftDockPorts.dock_pz_B[cPort];
        contacts.qCraftPort_B0 = craftDockPorts.dock_q_B[cPort];

        Vector3 portPos_SB = new Vector3(
            (float)st.dock_px_B[sPort],
            (float)st.dock_py_B[sPort],
            (float)st.dock_pz_B[sPort]
        );
        Quaternion portRot_SB = st.dock_q_B[sPort];
        contacts.qTargetPort_E0 = st.q_B2E * portRot_SB;

        Vector3 drB = new Vector3((float)contacts.drx_B0, (float)contacts.dry_B0, (float)contacts.drz_B0);
        Quaternion qTargetInB = contacts.qTargetInB0;

        Vector3 portPos_B = drB + (qTargetInB * portPos_SB);
        Quaternion portRot_InB = qTargetInB * portRot_SB;

        contacts.targetPort_px_B0 = (double)portPos_B.x;
        contacts.targetPort_py_B0 = (double)portPos_B.y;
        contacts.targetPort_pz_B0 = (double)portPos_B.z;
        contacts.qTargetPortInB0 = portRot_InB;

        contacts.dockErr_px_B0 = contacts.targetPort_px_B0 - contacts.craftPort_px_B0;
        contacts.dockErr_py_B0 = contacts.targetPort_py_B0 - contacts.craftPort_py_B0;
        contacts.dockErr_pz_B0 = contacts.targetPort_pz_B0 - contacts.craftPort_pz_B0;

        Quaternion qCport = contacts.qCraftPort_B0;
        contacts.qDockErr0 = Quaternion.Inverse(qCport) * portRot_InB;

        contacts.dockStationPortIndex0 = sPort;
        contacts.dockCraftPortIndex0 = cPort;
        contacts.dockValid0 = true;
    }

    private void ComputeDockingForSlot1(StationStateModel st)
    {
        contacts.dockValid1 = false;

        if (st == null) return;
        if (craftDockPorts == null) return;

        int sPort = contacts.selectedStationDockPortIndex;
        int cPort = contacts.selectedCraftDockPortIndex;

        if (sPort < 0 || sPort >= st.dockingPortCount) return;
        if (cPort < 0 || cPort >= craftDockPorts.dockingPortCount) return;

        contacts.craftPort_px_B1 = craftDockPorts.dock_px_B[cPort];
        contacts.craftPort_py_B1 = craftDockPorts.dock_py_B[cPort];
        contacts.craftPort_pz_B1 = craftDockPorts.dock_pz_B[cPort];
        contacts.qCraftPort_B1 = craftDockPorts.dock_q_B[cPort];

        Vector3 portPos_SB = new Vector3(
            (float)st.dock_px_B[sPort],
            (float)st.dock_py_B[sPort],
            (float)st.dock_pz_B[sPort]
        );
        Quaternion portRot_SB = st.dock_q_B[sPort];
        contacts.qTargetPort_E1 = st.q_B2E * portRot_SB;

        Vector3 drB = new Vector3((float)contacts.drx_B1, (float)contacts.dry_B1, (float)contacts.drz_B1);
        Quaternion qTargetInB = contacts.qTargetInB1;

        Vector3 portPos_B = drB + (qTargetInB * portPos_SB);
        Quaternion portRot_InB = qTargetInB * portRot_SB;

        contacts.targetPort_px_B1 = (double)portPos_B.x;
        contacts.targetPort_py_B1 = (double)portPos_B.y;
        contacts.targetPort_pz_B1 = (double)portPos_B.z;
        contacts.qTargetPortInB1 = portRot_InB;

        contacts.dockErr_px_B1 = contacts.targetPort_px_B1 - contacts.craftPort_px_B1;
        contacts.dockErr_py_B1 = contacts.targetPort_py_B1 - contacts.craftPort_py_B1;
        contacts.dockErr_pz_B1 = contacts.targetPort_pz_B1 - contacts.craftPort_pz_B1;

        Quaternion qCport = contacts.qCraftPort_B1;
        contacts.qDockErr1 = Quaternion.Inverse(qCport) * portRot_InB;

        contacts.dockStationPortIndex1 = sPort;
        contacts.dockCraftPortIndex1 = cPort;
        contacts.dockValid1 = true;
    }

    private static void RotateEToBody(Quaternion qBE, double vxE, double vyE, double vzE,
                                     out double vxB, out double vyB, out double vzB)
    {
        Quaternion qEB = Quaternion.Inverse(qBE);
        Vector3 vB = qEB * new Vector3((float)vxE, (float)vyE, (float)vzE);
        vxB = (double)vB.x;
        vyB = (double)vB.y;
        vzB = (double)vB.z;
    }
}