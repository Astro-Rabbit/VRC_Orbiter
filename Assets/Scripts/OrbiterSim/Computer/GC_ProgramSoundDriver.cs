using UdonSharp;
using UnityEngine;

/// <summary>
/// GC_ProgramSoundDriver
///
/// Watches GC runtime program state and plays shared program transition sounds.
///
/// V1 behavior:
/// - MANUAL -> AUTO              : play programOn
/// - AUTO   -> different AUTO    : play programOn
/// - AUTO   -> MANUAL            : play programOff
/// - Same program               : no sound
/// - First initialization       : no sound
///
/// Notes:
/// - This is state-driven, not input-driven.
/// - Every client runs this locally against their current runtime mirror.
/// - Intended for a world-space/shared AudioSource on the craft.
/// </summary>
public class GC_ProgramSoundDriver : UdonSharpBehaviour
{
    [Header("References")]
    public GC_RuntimeState runtime;
    public AudioSource audioSource;

    [Header("Clips")]
    public AudioClip programOnClip;
    public AudioClip programOffClip;

    [Header("Options")]
    [Tooltip("Run in LateUpdate so GC_Core has already updated activeProgramId this frame.")]
    public bool useLateUpdate = true;

    [Tooltip("If true, do not play any sound until the first non-NONE program is observed.")]
    public bool waitForFirstResolvedProgram = false;

    private bool _initialized = false;
    private byte _lastProgramId = GC_RuntimeState.PROG_NONE;

    void Update()
    {
        if (!useLateUpdate) TickDriver();
    }

    void LateUpdate()
    {
        if (useLateUpdate) TickDriver();
    }

    private void TickDriver()
    {
        if (runtime == null || audioSource == null) return;

        byte cur = runtime.activeProgramId;

        // Optional startup guard: wait until program state becomes something meaningful.
        if (!_initialized && waitForFirstResolvedProgram && cur == GC_RuntimeState.PROG_NONE)
            return;

        if (!_initialized)
        {
            _lastProgramId = cur;
            _initialized = true;
            return;
        }

        if (cur == _lastProgramId) return;

        bool wasAuto = IsAutoProgram(_lastProgramId);
        bool isAuto  = IsAutoProgram(cur);

        // MANUAL -> AUTO or AUTO -> different AUTO
        if (isAuto)
        {
            PlayProgramOn();
        }
        // AUTO -> MANUAL
        else if (wasAuto && cur == GC_RuntimeState.PROG_MANUAL)
        {
            PlayProgramOff();
        }

        _lastProgramId = cur;
    }

    private bool IsAutoProgram(byte programId)
    {
        switch (programId)
        {
            case GC_RuntimeState.PROG_HOLD_ATT:
            case GC_RuntimeState.PROG_POINT_DIR_E:
            case GC_RuntimeState.PROG_KILL_ROT:
            case GC_RuntimeState.PROG_HOLD_PROGRADE:
            case GC_RuntimeState.PROG_HOLD_RETRO:
            case GC_RuntimeState.PROG_HOLD_RAD_OUT:
            case GC_RuntimeState.PROG_HOLD_RAD_IN:
            case GC_RuntimeState.PROG_HOLD_NORMAL:
            case GC_RuntimeState.PROG_HOLD_ANTINORM:
            case GC_RuntimeState.PROG_RELVEL_PRO:
            case GC_RuntimeState.PROG_RELVEL_RETRO:
            case GC_RuntimeState.PROG_EXEC_NODE:
            case GC_RuntimeState.PROG_DOCK_POINT_PORT:
            case GC_RuntimeState.PROG_DOCK_ALIGN_PORTS:

                return true;

            default:
                return false;
        }
    }

    private void PlayProgramOn()
    {
        if (audioSource == null || programOnClip == null) return;
        audioSource.PlayOneShot(programOnClip);
    }

    private void PlayProgramOff()
    {
        if (audioSource == null || programOffClip == null) return;
        audioSource.PlayOneShot(programOffClip);
    }

    public void ResetDriver()
    {
        _initialized = false;
        _lastProgramId = GC_RuntimeState.PROG_NONE;
    }

    public void SyncNowSilently()
    {
        if (runtime == null) return;
        _lastProgramId = runtime.activeProgramId;
        _initialized = true;
    }
}