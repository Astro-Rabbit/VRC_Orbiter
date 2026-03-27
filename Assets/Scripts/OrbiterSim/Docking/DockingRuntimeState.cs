using UdonSharp;
using UnityEngine;

/// <summary>
/// DockingRuntimeState
/// Owner-authoritative docking state machine + persisted docking reference.
///
/// Philosophy:
/// - Unity latch triggers decide *when* capture is allowed and *which target port* we are latched to.
/// - Once captured, simulation motion is driven deterministically relative to the station (SOFT/RETRACT/HARD),
///   not by rails/integrated free flight.
///
/// Frames:
/// - E: solver inertial (SSB ecliptic inertial), same as CraftStateModel and StationStateModel.
/// - S: station BODY frame (station axes), origin at station root/CG.
/// - B: craft BODY frame.
///
/// Stored relative docking reference is expressed in station BODY frame:
/// - relPos_SB: craft CG position relative to station origin, expressed in station body axes.
/// - qCraftToStation: craft BODY -> station BODY (Quaternion).
///
/// Hard target relative pose (also in station body frame):
/// - targetRelPos_SB
/// - target_qCraftToStation
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DockingRuntimeState : UdonSharpBehaviour
{
    // --------------------
    // Dock phases (byte)
    // --------------------
    public const byte DOCK_NONE    = 0;
    public const byte DOCK_SOFT    = 1; // captured: freeze relative pose, damp relative motion
    public const byte DOCK_RETRACT = 2; // retract drives target pose
    public const byte DOCK_HARD    = 3; // welded

    [Header("State")]
    public byte phase = DOCK_NONE;
    public bool active = false;

    [Header("Latched pairing (frozen at capture)")]
    [Tooltip("Index into your stations[] list (SimManager/ContactsComputer ordering).")]
    public int dockedStationIndex = -1;

    [Tooltip("Station docking port index (within the station's port cache).")]
    public int stationPortIndex = -1;

    [Tooltip("Craft docking port index (within the craft's port cache).")]
    public int craftPortIndex = -1;

    [Header("Timing")]
    public double captureTime = 0.0;

    [Header("Retract driver")]
    [Range(0f, 1f)] public float retractS = 0f;

    [Tooltip("Retract speed in 1/sec. retractS = MoveTowards(retractS, 1, speed*dt).")]
    public float retractSpeed = 0.35f;

    [Header("Retract command")]
    [Tooltip("Set true to begin retract from DOCK_SOFT.")]
    public bool retractCommanded = false;

    [Header("Soft/Hard tolerances (used for auto-advance gates, not for latch detection)")]
    public double hardCapturePosTolM = 0.02;     // 2 cm
    public float  hardCaptureAngTolDeg = 2.0f;   // 2 deg

    [Header("Port mating convention")]
    [Tooltip("Rotation applied in station PORT frame to get desired craft PORT frame. Default: flip +Z (180° about +Y).")]

    [Header("Persisted relative pose (station body frame)")]
    public Vector3 relPos_SB = Vector3.zero;                 // craft CG rel station origin, expressed in station body axes
    public Quaternion qCraftToStation = Quaternion.identity;  // craft BODY -> station BODY

    [Header("Hard target relative pose (station body frame)")]
    public Vector3 targetRelPos_SB = Vector3.zero;
    public Quaternion target_qCraftToStation = Quaternion.identity;

    [Header("Debug")]
    public bool debugLatched = false;

    public Quaternion GetQMate()
    {
        // Port convention:
        // +Z = outward docking axis
        // +Y = roll-up reference
        // To make two outward-facing ports mate face-to-face,
        // flip forward while preserving up.
        return Quaternion.AngleAxis(180f, Vector3.up);
    }
    public void ResetState()
    {
        phase = DOCK_NONE;
        active = false;

        dockedStationIndex = -1;
        stationPortIndex = -1;
        craftPortIndex = -1;

        captureTime = 0.0;
        retractS = 0f;

        relPos_SB = Vector3.zero;
        qCraftToStation = Quaternion.identity;

        targetRelPos_SB = Vector3.zero;
        target_qCraftToStation = Quaternion.identity;

        debugLatched = false;
        retractCommanded = false;

        // Keep qMate as-configured (do NOT reset it here)
    }


    public static string DockPhaseName(byte phase)
    {
        switch (phase)
        {
            case DockingRuntimeState.DOCK_NONE:    return "NONE";
            case DockingRuntimeState.DOCK_SOFT:    return "SOFT";
            case DockingRuntimeState.DOCK_RETRACT: return "RETRACT";
            case DockingRuntimeState.DOCK_HARD:    return "HARD";
        }
        return "UNKNOWN(" + phase + ")";
    }

    public void CommandRetract()
    {
        retractCommanded = true;
    }

    public void ClearRetractCommand()
    {
        retractCommanded = false;
    }
    public bool IsDocked()
    {
        return active && (phase == DOCK_HARD);
    }
}