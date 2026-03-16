using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// CraftInitializer_DockedToStation
///
/// Deterministically starts the craft already HARD-docked to a station port.
///
/// Purpose:
/// - Force ephemeris + station evaluation at T0 = clock.Now()
/// - Compute the hard-docked craft pose from:
///     station state + station port cache + craft port cache + qMate
/// - Write craft r/v + attitude directly
/// - Initialize DockingRuntimeState to HARD
/// - Publish CraftNetState in MODE_DOCKED
///
/// This is a diagnostic initializer to test docked-state stability independently
/// of latch/capture transition behavior.
/// </summary>
public class CraftInitializer_DockedToStation : UdonSharpBehaviour
{
    [Header("Core refs")]
    public SimClock clock;
    public EphemerisSystem ephem;
    public StationPropSystem stationProp;
    public StationStateModel stationState;

    [Header("Craft target")]
    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;
    public CraftDockingPorts craftPorts;

    [Header("Docking state")]
    public DockingRuntimeState dock;
    public CraftNetState netCore;

    [Header("Dock target selection")]
    public int stationIndex = 0;
    public int stationPortIndex = 0;
    public int craftPortIndex = 0;
    public StationDockingPortsAuthoring stationDockPorts;

    [Header("Debug")]
    public bool log = true;

    public bool InitializeNow()
    {
        if (clock == null || ephem == null || craft == null || craftAtt == null ||
            craftPorts == null || dock == null || netCore == null)
        {
            if (log) Debug.Log("[CraftInitializer_DockedToStation] Missing core refs.");
            return false;
        }

        if (stationProp == null && stationState == null)
        {
            if (log) Debug.Log("[CraftInitializer_DockedToStation] Missing stationProp/stationState.");
            return false;
        }

        double T0 = clock.Now();

        // 1) Force ephemeris + station state at T0
        ephem.Evaluate(T0);
        if (stationProp != null) stationProp.Evaluate(T0);

        if (craftPorts != null)
            craftPorts.CacheNow();        
        stationDockPorts.CacheNow();
        StationStateModel st = stationState != null ? stationState :
                               (stationProp != null ? stationProp.station : null);

        int stCount = 0;
        if (st.dock_px_B != null) stCount = st.dock_px_B.Length;
        if (st.dock_py_B != null && st.dock_py_B.Length < stCount) stCount = st.dock_py_B.Length;
        if (st.dock_pz_B != null && st.dock_pz_B.Length < stCount) stCount = st.dock_pz_B.Length;
        if (st.dock_q_B  != null && st.dock_q_B.Length  < stCount) stCount = st.dock_q_B.Length;

        int cCount = 0;
        if (craftPorts.dock_px_B != null) cCount = craftPorts.dock_px_B.Length;
        if (craftPorts.dock_py_B != null && craftPorts.dock_py_B.Length < cCount) cCount = craftPorts.dock_py_B.Length;
        if (craftPorts.dock_pz_B != null && craftPorts.dock_pz_B.Length < cCount) cCount = craftPorts.dock_pz_B.Length;
        if (craftPorts.dock_q_B  != null && craftPorts.dock_q_B.Length  < cCount) cCount = craftPorts.dock_q_B.Length;

        // Optional: keep the public counts in sync for debugging
        st.dockingPortCount = stCount;
        craftPorts.dockingPortCount = cCount;

        if (stationPortIndex < 0 || stationPortIndex >= stCount)
        {
            if (log) Debug.Log("[CraftInitializer_DockedToStation] Invalid stationPortIndex. stationPortIndex=" + stationPortIndex + " stCount=" + stCount);
            return false;
        }

        if (craftPortIndex < 0 || craftPortIndex >= cCount)
        {
            if (log) Debug.Log("[CraftInitializer_DockedToStation] Invalid craftPortIndex. craftPortIndex=" + craftPortIndex + " cCount=" + cCount);
            return false;
        }


        // 2) Read station port pose in station BODY frame
        Vector3 pS_SB = new Vector3(
            (float)st.dock_px_B[stationPortIndex],
            (float)st.dock_py_B[stationPortIndex],
            (float)st.dock_pz_B[stationPortIndex]
        );
        Quaternion qS_SB = st.dock_q_B[stationPortIndex];

        // 3) Read craft port pose in craft BODY frame
        Vector3 pC_B = new Vector3(
            (float)craftPorts.dock_px_B[craftPortIndex],
            (float)craftPorts.dock_py_B[craftPortIndex],
            (float)craftPorts.dock_pz_B[craftPortIndex]
        );
        Quaternion qC_B = craftPorts.dock_q_B[craftPortIndex];


        if (log && stationDockPorts != null && stationDockPorts.portTransforms != null &&
            stationPortIndex >= 0 && stationPortIndex < stationDockPorts.portTransforms.Length)
        {
            Transform livePort = stationDockPorts.portTransforms[stationPortIndex];

            Quaternion qPort_E_fromCache = st.q_B2E * qS_SB;

            Vector3 cacheFwd = qPort_E_fromCache * Vector3.forward;
            Vector3 cacheUp  = qPort_E_fromCache * Vector3.up;

            Vector3 liveFwd = livePort.forward;
            Vector3 liveUp  = livePort.up;

            Debug.Log(
                "[DockInit] Station port compare " +
                " dotFwd=" + Vector3.Dot(cacheFwd.normalized, liveFwd.normalized) +
                " dotUp=" + Vector3.Dot(cacheUp.normalized, liveUp.normalized) +
                " cacheFwd=" + cacheFwd +
                " liveFwd=" + liveFwd +
                " cacheUp=" + cacheUp +
                " liveUp=" + liveUp
            );
        }

        if (log && craftPorts != null && craftPorts.portTransforms != null &&
            craftPortIndex >= 0 && craftPortIndex < craftPorts.portTransforms.Length)
        {
            Transform liveCraftPort = craftPorts.portTransforms[craftPortIndex];

            Quaternion qCraftPort_E_fromCache = craftAtt.qBE * qC_B;

            Vector3 cacheFwd = qCraftPort_E_fromCache * Vector3.forward;
            Vector3 cacheUp  = qCraftPort_E_fromCache * Vector3.up;

            Vector3 liveFwd = liveCraftPort.forward;
            Vector3 liveUp  = liveCraftPort.up;

            Debug.Log(
                "[DockInit] Craft port compare " +
                " dotFwd=" + Vector3.Dot(cacheFwd.normalized, liveFwd.normalized) +
                " dotUp=" + Vector3.Dot(cacheUp.normalized, liveUp.normalized) +
                " cacheFwd=" + cacheFwd +
                " liveFwd=" + liveFwd +
                " cacheUp=" + cacheUp +
                " liveUp=" + liveUp
            );
        }

        // 4) Compute hard-docked relative pose in station BODY frame
        Quaternion qCraftToStation = qS_SB * dock.GetQMate() * Quaternion.Inverse(qC_B);
        Vector3 relPos_SB = pS_SB - (qCraftToStation * pC_B);

        // 5) Compose craft inertial pose from station inertial pose
        Quaternion qS_E = st.q_B2E;                    // station BODY -> E
        Quaternion qC_E = qS_E * qCraftToStation;


        Quaternion qStationPort_E = st.q_B2E * qS_SB;
        Quaternion qCraftPort_E   = qC_E * qC_B;

        Vector3 sFwd = qStationPort_E * Vector3.forward;
        Vector3 sUp  = qStationPort_E * Vector3.up;

        Vector3 cFwd = qCraftPort_E * Vector3.forward;
        Vector3 cUp  = qCraftPort_E * Vector3.up;

        Debug.Log(
            "[DockInit] solved port relation " +
            " dotFwd=" + Vector3.Dot(sFwd.normalized, cFwd.normalized) +
            " dotUp=" + Vector3.Dot(sUp.normalized, cUp.normalized) +
            " qMate=" + dock.GetQMate()
        );


        Vector3 rS_E = new Vector3((float)st.rx, (float)st.ry, (float)st.rz);
        Vector3 vS_E = new Vector3((float)st.vx, (float)st.vy, (float)st.vz);

        Vector3 relPos_E = qS_E * relPos_SB;
        Vector3 rC_E = rS_E + relPos_E;

        Vector3 wS_E = ComputeStationOmegaE(st);
        Vector3 vC_E = vS_E + Vector3.Cross(wS_E, relPos_E);

        Quaternion qEB = Quaternion.Inverse(qC_E);
        Vector3 wC_B = qEB * wS_E;

        // 6) Write craft state
        craft.rx = rC_E.x; craft.ry = rC_E.y; craft.rz = rC_E.z;
        craft.vx = vC_E.x; craft.vy = vC_E.y; craft.vz = vC_E.z;
        craft.primaryBodyId = st.primaryBodyId;

        craftAtt.qBE = qC_E;
        craftAtt.wx = wC_B.x;
        craftAtt.wy = wC_B.y;
        craftAtt.wz = wC_B.z;

        // 7) Initialize dock runtime as HARD
        dock.active = true;
        dock.phase = DockingRuntimeState.DOCK_HARD;
        dock.dockedStationIndex = stationIndex;
        dock.stationPortIndex = stationPortIndex;
        dock.craftPortIndex = craftPortIndex;
        dock.captureTime = T0;
        dock.retractS = 1f;

        dock.relPos_SB = relPos_SB;
        dock.qCraftToStation = qCraftToStation;
        dock.targetRelPos_SB = relPos_SB;
        dock.target_qCraftToStation = qCraftToStation;

        // 8) Publish net state as DOCKED
        if (Networking.IsOwner(netCore.gameObject))
        {
            netCore.SetDocked(
                stationIndex,
                (byte)stationPortIndex,
                (byte)craftPortIndex,
                DockingRuntimeState.DOCK_HARD,
                T0,
                T0,
                relPos_SB,
                qCraftToStation,
                st.primaryBodyId,
                true
            );
        }

        if (log)
        {
            Debug.Log($"[CraftInitializer_DockedToStation] Started HARD docked at T0={T0:F2}s station={stationIndex} sPort={stationPortIndex} cPort={craftPortIndex}");
            Debug.Log(
        "[CraftInitializer_DockedToStation] stCountField=" + st.dockingPortCount +
        " st_px=" + (st.dock_px_B == null ? -1 : st.dock_px_B.Length) +
        " st_py=" + (st.dock_py_B == null ? -1 : st.dock_py_B.Length) +
        " st_pz=" + (st.dock_pz_B == null ? -1 : st.dock_pz_B.Length) +
        " st_q="  + (st.dock_q_B  == null ? -1 : st.dock_q_B.Length)
    );
        }

        return true;
    }

    private Vector3 ComputeStationOmegaE(StationStateModel st)
    {
        if (st.attitudeMode == StationStateModel.ATT_MODE_FIXED_INERTIAL)
            return Vector3.zero;

        Vector3 r = new Vector3((float)st.rrx, (float)st.rry, (float)st.rrz);
        Vector3 v = new Vector3((float)st.rvx, (float)st.rvy, (float)st.rvz);

        float r2 = r.sqrMagnitude;
        if (r2 < 1e-6f) return Vector3.zero;

        Vector3 h = Vector3.Cross(r, v);
        float rMag = Mathf.Sqrt(r2);
        if (rMag < 1e-6f) return Vector3.zero;

        return h / (r2 * rMag);
    }
}