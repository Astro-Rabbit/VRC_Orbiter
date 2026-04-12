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
    public SimManager simManager;

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

    [Header("Render smoothing (remote integrated only)")]
    public bool smoothRenderRelativePosition = true;
    public float renderSmoothTimeSeconds = 0.02f;
    public float renderSnapDistanceMeters = 500f;

    [Header("Debug")]
    public bool logMissing = false;

    [Header("Remote render sampling (optional)")]
    public SimClock clock;
    public CraftNetState netCore;
    public CraftNetAttitude netAtt;
    public CraftNetKinematics netKin;

    private double _simRenderT;

    [Header("Debug transition/contact output")]
    public bool debugModeTransitionContacts = false;
    public float debugModeTransitionLogWindowSeconds = 0.50f;

    private byte _dbgLastPresentedMode = CraftNetState.MODE_RAILS;
    private double _dbgLogUntilNet = -1.0;

    private bool _dbgPrevRender0Valid = false;
    private int _dbgPrevRender0Station = -1;
    private Vector3 _dbgPrevRender0DrB = Vector3.zero;

    private bool _dbgPrevRender1Valid = false;
    private int _dbgPrevRender1Station = -1;
    private Vector3 _dbgPrevRender1DrB = Vector3.zero;

    private Quaternion _qCraftForRender = Quaternion.identity;
    private double _crxRender, _cryRender, _crzRender;
    private double _cvxRender, _cvyRender, _cvzRender;

    // slot 0 smoothing state (selected)
    private int _smoothStationIndex0 = -1;
    private bool _smoothInit0 = false;
    private Vector3 _smoothDrE0 = Vector3.zero;

    // slot 1 smoothing state (nearest-other)
    private int _smoothStationIndex1 = -1;
    private bool _smoothInit1 = false;
    private Vector3 _smoothDrE1 = Vector3.zero;

    private int _prevSelectedStationIndex = -1;
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

        bool isSimOwner = (simManager != null) ? simManager.IsSimOwner() : (netCore != null && Networking.IsOwner(netCore.gameObject));
        bool allowRenderSmoothing = false;
        bool freezeActive = (simManager != null && simManager.IsFreezeActive());

        if (clock != null && netCore != null)
        {
            if (!isSimOwner && !freezeActive)
            {
                double tRender = clock.GetCachedRemoteRenderTime();
                byte presentedMode = netCore.GetPresentedMode(tRender);


                if (debugModeTransitionContacts)
                {
                    if (_dbgLastPresentedMode == CraftNetState.MODE_INTEGRATED &&
                        presentedMode == CraftNetState.MODE_RAILS)
                    {
                        _dbgLogUntilNet = tRender + (double)debugModeTransitionLogWindowSeconds;
                        Debug.Log("[ContactsDbg] presented transition INTEGRATED -> RAILS");
                    }

                    _dbgLastPresentedMode = presentedMode;
                }

                if (netAtt != null)
                    _qCraftForRender = netAtt.SampleRenderQuaternion(tRender);

                if (presentedMode == CraftNetState.MODE_INTEGRATED && netKin != null && netKin.rawValid)
                {
                    _simRenderT = netKin.rawSimT;
                    _crxRender = netKin.rawRx;
                    _cryRender = netKin.rawRy;
                    _crzRender = netKin.rawRz;
                    _cvxRender = netKin.rawVx;
                    _cvyRender = netKin.rawVy;
                    _cvzRender = netKin.rawVz;

                    allowRenderSmoothing = smoothRenderRelativePosition;
                }
            }
        }

        int n = stations.Length;
        contacts.EnsureSize(n);
        contacts.ClearFull();

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

        // Detect station change
        if (sel != _prevSelectedStationIndex)
        {
            _prevSelectedStationIndex = sel;

            // Only set if a valid station is selected
            if (sel >= 0 && sel < stations.Length && contacts.valid[sel])
            {
                contacts.selectedStationDockPortIndex = 0;
            }
        }


        if (sel >= 0 && sel < n && contacts.valid[sel])
            FillFullSlot0(sel, allowRenderSmoothing);
        else
            ResetSmooth0();

        int idx1 = FindNearestOther(sel);
        if (idx1 >= 0)
            FillFullSlot1(idx1, allowRenderSmoothing);
        else
            ResetSmooth1();



        if (clock != null && netCore != null)
        {
            double tRender = clock.GetCachedRemoteRenderTime();
            byte presentedMode = netCore.GetPresentedMode(tRender);
            DebugLogTransitionContacts(tRender, presentedMode, allowRenderSmoothing);
        }


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

    private void FillFullSlot0(int stationIndex, bool allowRenderSmoothing)
    {
        StationStateModel st = stations[stationIndex];

        contacts.fullStationIndex0 = stationIndex;
        contacts.fullValid0 = true;

        contacts.dvx_E0 = st.vx - _cvxRender;
        contacts.dvy_E0 = st.vy - _cvyRender;
        contacts.dvz_E0 = st.vz - _cvzRender;

        double drxE = contacts.drx_E[stationIndex];
        double dryE = contacts.dry_E[stationIndex];
        double drzE = contacts.drz_E[stationIndex];

        RotateEToBody(_qCraftForRender, drxE, dryE, drzE,
            out contacts.drx_B0, out contacts.dry_B0, out contacts.drz_B0);

        contacts.selValid = true;
        contacts.sel_drx_E = drxE;
        contacts.sel_dry_E = dryE;
        contacts.sel_drz_E = drzE;
        contacts.sel_drx_B = contacts.drx_B0;
        contacts.sel_dry_B = contacts.dry_B0;
        contacts.sel_drz_B = contacts.drz_B0;
        contacts.sel_dvx_E = contacts.dvx_E0;
        contacts.sel_dvy_E = contacts.dvy_E0;
        contacts.sel_dvz_E = contacts.dvz_E0;

        Quaternion qC = _qCraftForRender;
        Quaternion qT = st.q_B2E;
        contacts.qTargetInB0 = Quaternion.Inverse(qC) * qT;

        UpdateRenderSlot0(stationIndex, drxE, dryE, drzE, allowRenderSmoothing);

        ComputeDockingForSlot0(st);
    }

    private void FillFullSlot1(int stationIndex, bool allowRenderSmoothing)
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

        RotateEToBody(_qCraftForRender, drxE, dryE, drzE,
            out contacts.drx_B1, out contacts.dry_B1, out contacts.drz_B1);

        Quaternion qC = _qCraftForRender;
        Quaternion qT = st.q_B2E;
        contacts.qTargetInB1 = Quaternion.Inverse(qC) * qT;

        UpdateRenderSlot1(stationIndex, drxE, dryE, drzE, allowRenderSmoothing);

        if (contacts.computeDockForSlot1)
            ComputeDockingForSlot1(st);
    }

    private void UpdateRenderSlot0(int stationIndex, double drxE, double dryE, double drzE, bool allowRenderSmoothing)
    {
        contacts.renderFullValid0 = true;

        Vector3 targetDrE = new Vector3((float)drxE, (float)dryE, (float)drzE);
        Vector3 renderDrE = GetSmoothedRenderDrE0(stationIndex, targetDrE, allowRenderSmoothing);

        RotateEToBody(_qCraftForRender, renderDrE.x, renderDrE.y, renderDrE.z,
            out contacts.render_drx_B0, out contacts.render_dry_B0, out contacts.render_drz_B0);
    }

    private void UpdateRenderSlot1(int stationIndex, double drxE, double dryE, double drzE, bool allowRenderSmoothing)
    {
        contacts.renderFullValid1 = true;

        Vector3 targetDrE = new Vector3((float)drxE, (float)dryE, (float)drzE);
        Vector3 renderDrE = GetSmoothedRenderDrE1(stationIndex, targetDrE, allowRenderSmoothing);

        RotateEToBody(_qCraftForRender, renderDrE.x, renderDrE.y, renderDrE.z,
            out contacts.render_drx_B1, out contacts.render_dry_B1, out contacts.render_drz_B1);
    }

    private Vector3 GetSmoothedRenderDrE0(int stationIndex, Vector3 targetDrE, bool allowRenderSmoothing)
    {
        if (_smoothStationIndex0 != stationIndex)
        {
            _smoothStationIndex0 = stationIndex;
            _smoothInit0 = false;
        }

        if (!_smoothInit0 || !allowRenderSmoothing)
        {
            _smoothDrE0 = targetDrE;
            _smoothInit0 = true;
            return _smoothDrE0;
        }

        float dt = Time.deltaTime;
        if (dt < 0f) dt = 0f;
        if (dt > 0.25f) dt = 0.25f;

        Vector3 err = targetDrE - _smoothDrE0;
        float errMag = err.magnitude;

        if (renderSnapDistanceMeters > 0f && errMag > renderSnapDistanceMeters)
        {
            _smoothDrE0 = targetDrE;
            return _smoothDrE0;
        }

        float tau = renderSmoothTimeSeconds;
        if (tau < 0.0001f) tau = 0.0001f;

        float alpha = 1f - Mathf.Exp(-dt / tau);
        _smoothDrE0 = Vector3.Lerp(_smoothDrE0, targetDrE, alpha);
        return _smoothDrE0;
    }

    private Vector3 GetSmoothedRenderDrE1(int stationIndex, Vector3 targetDrE, bool allowRenderSmoothing)
    {
        if (_smoothStationIndex1 != stationIndex)
        {
            _smoothStationIndex1 = stationIndex;
            _smoothInit1 = false;
        }

        if (!_smoothInit1 || !allowRenderSmoothing)
        {
            _smoothDrE1 = targetDrE;
            _smoothInit1 = true;
            return _smoothDrE1;
        }

        float dt = Time.deltaTime;
        if (dt < 0f) dt = 0f;
        if (dt > 0.25f) dt = 0.25f;

        Vector3 err = targetDrE - _smoothDrE1;
        float errMag = err.magnitude;

        if (renderSnapDistanceMeters > 0f && errMag > renderSnapDistanceMeters)
        {
            _smoothDrE1 = targetDrE;
            return _smoothDrE1;
        }

        float tau = renderSmoothTimeSeconds;
        if (tau < 0.0001f) tau = 0.0001f;

        float alpha = 1f - Mathf.Exp(-dt / tau);
        _smoothDrE1 = Vector3.Lerp(_smoothDrE1, targetDrE, alpha);
        return _smoothDrE1;
    }

    private void ResetSmooth0()
    {
        _smoothStationIndex0 = -1;
        _smoothInit0 = false;
        contacts.renderFullValid0 = false;
        contacts.render_drx_B0 = contacts.render_dry_B0 = contacts.render_drz_B0 = 0.0;
    }

    private void ResetSmooth1()
    {
        _smoothStationIndex1 = -1;
        _smoothInit1 = false;
        contacts.renderFullValid1 = false;
        contacts.render_drx_B1 = contacts.render_dry_B1 = contacts.render_drz_B1 = 0.0;
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

    private void DebugLogTransitionContacts(
        double tRender,
        byte presentedMode,
        bool allowRenderSmoothing)
    {
        if (!debugModeTransitionContacts) return;
        if (simManager != null && simManager.IsSimOwner()) return;
        if (tRender > _dbgLogUntilNet) return;

        // Slot 0
        if (contacts.fullValid0)
        {
            Vector3 rawB0 = new Vector3(
                (float)contacts.drx_B0,
                (float)contacts.dry_B0,
                (float)contacts.drz_B0);

            Vector3 renderB0 = contacts.renderFullValid0
                ? new Vector3(
                    (float)contacts.render_drx_B0,
                    (float)contacts.render_dry_B0,
                    (float)contacts.render_drz_B0)
                : rawB0;

            Vector3 dRaw0 = Vector3.zero;
            Vector3 dRender0 = Vector3.zero;

            if (_dbgPrevRender0Valid && _dbgPrevRender0Station == contacts.fullStationIndex0)
            {
                dRender0 = renderB0 - _dbgPrevRender0DrB;
            }

            Debug.Log(
                "[ContactsDbg][S0] " +
                "mode=" + presentedMode +
                " tRender=" + tRender.ToString("F3") +
                " allowSmooth=" + allowRenderSmoothing +
                " st=" + contacts.fullStationIndex0 +
                " rawB=(" + rawB0.x.ToString("F2") + "," + rawB0.y.ToString("F2") + "," + rawB0.z.ToString("F2") + ")" +
                " renderB=(" + renderB0.x.ToString("F2") + "," + renderB0.y.ToString("F2") + "," + renderB0.z.ToString("F2") + ")" +
                " dRender=(" + dRender0.x.ToString("F2") + "," + dRender0.y.ToString("F2") + "," + dRender0.z.ToString("F2") + ")" +
                " |dRender|=" + dRender0.magnitude.ToString("F2")
            );

            _dbgPrevRender0Valid = true;
            _dbgPrevRender0Station = contacts.fullStationIndex0;
            _dbgPrevRender0DrB = renderB0;
        }
        else
        {
            Debug.Log(
                "[ContactsDbg][S0] " +
                "mode=" + presentedMode +
                " tRender=" + tRender.ToString("F3") +
                " allowSmooth=" + allowRenderSmoothing +
                " INVALID"
            );

            _dbgPrevRender0Valid = false;
            _dbgPrevRender0Station = -1;
            _dbgPrevRender0DrB = Vector3.zero;
        }

        // Slot 1
        if (contacts.fullValid1)
        {
            Vector3 rawB1 = new Vector3(
                (float)contacts.drx_B1,
                (float)contacts.dry_B1,
                (float)contacts.drz_B1);

            Vector3 renderB1 = contacts.renderFullValid1
                ? new Vector3(
                    (float)contacts.render_drx_B1,
                    (float)contacts.render_dry_B1,
                    (float)contacts.render_drz_B1)
                : rawB1;

            Vector3 dRender1 = Vector3.zero;

            if (_dbgPrevRender1Valid && _dbgPrevRender1Station == contacts.fullStationIndex1)
            {
                dRender1 = renderB1 - _dbgPrevRender1DrB;
            }

            Debug.Log(
                "[ContactsDbg][S1] " +
                "mode=" + presentedMode +
                " tRender=" + tRender.ToString("F3") +
                " allowSmooth=" + allowRenderSmoothing +
                " st=" + contacts.fullStationIndex1 +
                " rawB=(" + rawB1.x.ToString("F2") + "," + rawB1.y.ToString("F2") + "," + rawB1.z.ToString("F2") + ")" +
                " renderB=(" + renderB1.x.ToString("F2") + "," + renderB1.y.ToString("F2") + "," + renderB1.z.ToString("F2") + ")" +
                " dRender=(" + dRender1.x.ToString("F2") + "," + dRender1.y.ToString("F2") + "," + dRender1.z.ToString("F2") + ")" +
                " |dRender|=" + dRender1.magnitude.ToString("F2")
            );

            _dbgPrevRender1Valid = true;
            _dbgPrevRender1Station = contacts.fullStationIndex1;
            _dbgPrevRender1DrB = renderB1;
        }
        else
        {
            Debug.Log(
                "[ContactsDbg][S1] " +
                "mode=" + presentedMode +
                " tRender=" + tRender.ToString("F3") +
                " allowSmooth=" + allowRenderSmoothing +
                " INVALID"
            );

            _dbgPrevRender1Valid = false;
            _dbgPrevRender1Station = -1;
            _dbgPrevRender1DrB = Vector3.zero;
        }
    }


}