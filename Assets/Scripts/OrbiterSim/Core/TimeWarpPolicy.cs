using UdonSharp;
using UnityEngine;
using System;

public class TimeWarpPolicy : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public CraftStateModel craft;
    public GuidanceNavContactsState contacts;

    [Header("Requested warp")]
    [Tooltip("Warp the pilot wants. Actual applied warp may be lower due to caps.")]
    public double requestedTimeScale = 1.0;

    [Header("Global cap")]
    public double maxGlobalWarp = 100000.0;

    [Header("Body altitude zones (parallel arrays, same length)")]
    [Tooltip("Body ID for each zone row.")]
    public byte[] bodyZoneBodyId;

    [Tooltip("Max altitude above surface (m) for each zone row.")]
    public double[] bodyZoneMaxAltitudeMeters;

    [Tooltip("Max allowed warp for each zone row.")]
    public double[] bodyZoneMaxWarp;

    [Header("Station zones")]
    public bool stationCapsEnabled = true;

    [Tooltip("Outer station zone radius (m).")]
    public double outerStationRangeMeters = 10000.0;
    public double outerStationMaxWarp = 10.0;

    [Tooltip("Inner station zone radius (m).")]
    public double innerStationRangeMeters = 1000.0;
    public double innerStationMaxWarp = 1.0;

    [Header("Debug")]
    public double currentBodyCap = double.PositiveInfinity;
    public double currentStationCap = double.PositiveInfinity;
    public double currentEnvironmentalCap = double.PositiveInfinity;
    public double currentAllowedTimeScale = 1.0;

    public bool bodyCapActive = false;
    public bool stationCapActive = false;

    public byte activeBodyId = 255;
    public int activeBodyZoneIndex = -1;
    public double activeBodyAltitudeMeters = 0.0;

    public int nearestStationIndex = -1;
    public double nearestStationRangeMeters = double.PositiveInfinity;
    public int activeStationZone = 0; // 0 none, 1 outer, 2 inner

    public void SetRequestedTimeScale(double newScale)
    {
        if (newScale < 0.0) newScale = 0.0;
        if (newScale > maxGlobalWarp) newScale = maxGlobalWarp;
        requestedTimeScale = newScale;
    }

    public double GetRequestedTimeScale()
    {
        return requestedTimeScale;
    }

    public double EvaluateAllowedTimeScale()
    {
        ResetDebugState();

        double allowed = requestedTimeScale;

        if (allowed > maxGlobalWarp) allowed = maxGlobalWarp;
        if (allowed < 0.0) allowed = 0.0;

        double bodyCap = GetBodyCap();
        currentBodyCap = bodyCap;
        if (bodyCap < allowed) allowed = bodyCap;

        double stationCap = GetStationCap();
        currentStationCap = stationCap;
        if (stationCap < allowed) allowed = stationCap;

        currentEnvironmentalCap = allowed;
        currentAllowedTimeScale = allowed;
        return allowed;
    }

    private void ResetDebugState()
    {
        currentBodyCap = double.PositiveInfinity;
        currentStationCap = double.PositiveInfinity;
        currentEnvironmentalCap = double.PositiveInfinity;
        currentAllowedTimeScale = requestedTimeScale;

        bodyCapActive = false;
        stationCapActive = false;

        activeBodyId = 255;
        activeBodyZoneIndex = -1;
        activeBodyAltitudeMeters = 0.0;

        nearestStationIndex = -1;
        nearestStationRangeMeters = double.PositiveInfinity;
        activeStationZone = 0;
    }

    private double GetBodyCap()
    {
        if (bodies == null || craft == null) return double.PositiveInfinity;
        if (bodyZoneBodyId == null || bodyZoneMaxAltitudeMeters == null || bodyZoneMaxWarp == null)
            return double.PositiveInfinity;

        int count = bodyZoneBodyId.Length;
        if (bodyZoneMaxAltitudeMeters.Length < count) count = bodyZoneMaxAltitudeMeters.Length;
        if (bodyZoneMaxWarp.Length < count) count = bodyZoneMaxWarp.Length;

        double bestCap = double.PositiveInfinity;
        byte bestBodyId = 255;
        int bestZoneIndex = -1;
        double bestAlt = 0.0;

        for (int i = 0; i < count; i++)
        {
            byte bodyId = bodyZoneBodyId[i];
            double bodyRadius = bodies.GetRadius(bodyId);
            if (bodyRadius <= 0.0) continue;

            double dist = bodies.GetCraftDistanceToBody(bodyId, craft);
            double alt = dist - bodyRadius;
            if (alt < 0.0) alt = 0.0;

            if (alt > bodyZoneMaxAltitudeMeters[i]) continue;

            double zoneCap = bodyZoneMaxWarp[i];
            if (zoneCap < bestCap)
            {
                bestCap = zoneCap;
                bestBodyId = bodyId;
                bestZoneIndex = i;
                bestAlt = alt;
            }
        }

        if (bestZoneIndex >= 0)
        {
            bodyCapActive = true;
            activeBodyId = bestBodyId;
            activeBodyZoneIndex = bestZoneIndex;
            activeBodyAltitudeMeters = bestAlt;
        }

        return bestCap;
    }

    private double GetStationCap()
    {
        if (!stationCapsEnabled) return double.PositiveInfinity;
        if (contacts == null) return double.PositiveInfinity;
        if (contacts.valid == null || contacts.range_m == null) return double.PositiveInfinity;

        int count = contacts.valid.Length;
        if (contacts.range_m.Length < count) count = contacts.range_m.Length;

        double bestRange = double.PositiveInfinity;
        int bestIdx = -1;

        for (int i = 0; i < count; i++)
        {
            if (!contacts.valid[i]) continue;

            double range = contacts.range_m[i];
            if (range < bestRange)
            {
                bestRange = range;
                bestIdx = i;
            }
        }

        if (bestIdx < 0) return double.PositiveInfinity;

        nearestStationIndex = bestIdx;
        nearestStationRangeMeters = bestRange;

        if (bestRange <= innerStationRangeMeters)
        {
            stationCapActive = true;
            activeStationZone = 2;
            return innerStationMaxWarp;
        }

        if (bestRange <= outerStationRangeMeters)
        {
            stationCapActive = true;
            activeStationZone = 1;
            return outerStationMaxWarp;
        }

        return double.PositiveInfinity;
    }
}