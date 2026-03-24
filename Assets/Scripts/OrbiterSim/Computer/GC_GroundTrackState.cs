using UdonSharp;
using UnityEngine;

/// <summary>
/// GC_GroundTrackState
/// Predicted ground-track samples for the craft over the current primary body.
///
/// This is a data-only container populated by a separate computer/predictor.
/// Samples are future-looking and referenced to mission time seconds.
///
//// Conventions:
/// - bodyId: body the samples are relative to
/// - tSec[i]: mission time of sample i
/// - latDeg[i]: geocentric latitude in degrees
/// - lonDeg[i]: body-fixed longitude in degrees
/// - altMeters[i]: altitude above spherical body radius in meters
///
/// V1 scope:
/// - Current-primary prediction only
/// - Spherical body assumption
/// - No terrain intersection
/// - No SOI crossing handling
/// - No rendering-specific segmentation metadata
/// </summary>
public class GC_GroundTrackState : UdonSharpBehaviour
{
    [Header("Capacity")]
    public int maxSamples = 256;

    [Header("Validity / metadata")]
    public bool valid = false;

    [Tooltip("Body ID the current track is referenced to.")]
    public byte bodyId = 255;

    [Tooltip("Mission time when this track solution was generated.")]
    public double sourceTimeSec = 0.0;

    [Tooltip("Julian date corresponding to sourceTimeSec, if available.")]
    public double sourceJD = 0.0;

    [Tooltip("Prediction horizon in seconds.")]
    public double horizonSec = 0.0;

    [Tooltip("Uniform sample spacing in seconds.")]
    public double sampleStepSec = 0.0;

    [Tooltip("Number of valid samples currently stored.")]
    public int sampleCount = 0;

    [Header("Sample arrays")]
    public double[] tSec;
    public double[] latDeg;
    public double[] lonDeg;
    public double[] altMeters;

    [Header("Optional body-fixed unit vectors")]
    [Tooltip("Optional normalized body-fixed radial vectors for each sample.")]
    public Vector3[] bodyFixedUnit;

    [Header("Diagnostics")]
    public bool usedCurrentPrimaryOnly = true;
    public string lastStatus = "";

    public void EnsureSize()
    {
        int n = (maxSamples < 1) ? 1 : maxSamples;

        if (tSec == null || tSec.Length != n) tSec = new double[n];
        if (latDeg == null || latDeg.Length != n) latDeg = new double[n];
        if (lonDeg == null || lonDeg.Length != n) lonDeg = new double[n];
        if (altMeters == null || altMeters.Length != n) altMeters = new double[n];
        if (bodyFixedUnit == null || bodyFixedUnit.Length != n) bodyFixedUnit = new Vector3[n];
    }

    public void Clear()
    {
        EnsureSize();

        valid = false;
        bodyId = 255;
        sourceTimeSec = 0.0;
        sourceJD = 0.0;
        horizonSec = 0.0;
        sampleStepSec = 0.0;
        sampleCount = 0;
        usedCurrentPrimaryOnly = true;
        lastStatus = "";

        int n = maxSamples;
        if (n < 1) n = 1;

        for (int i = 0; i < n; i++)
        {
            tSec[i] = 0.0;
            latDeg[i] = 0.0;
            lonDeg[i] = 0.0;
            altMeters[i] = 0.0;
            bodyFixedUnit[i] = Vector3.zero;
        }
    }

    public void SetStatusInvalid(string status)
    {
        valid = false;
        sampleCount = 0;
        lastStatus = status;
    }

    public bool HasSamples()
    {
        return valid && sampleCount > 0;
    }
}