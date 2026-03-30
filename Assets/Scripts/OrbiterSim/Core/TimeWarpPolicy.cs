using UdonSharp;
using UnityEngine;
using System;

public class TimeWarpPolicy : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public CraftStateModel craft;
    public GuidanceNavContactsState contacts;
    public CraftNetState netState;

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

    [Header("Station gates")]
    public bool stationCapsEnabled = true;

    [Tooltip("Outer gate max range (m). Gate can apply only inside this range.")]
    public double outerStationRangeMeters = 10000.0;

    [Tooltip("Outer gate max score in seconds. score = range / relVel.")]
    public double outerStationScoreSeconds = 120.0;

    [Tooltip("Outer gate warp cap.")]
    public double outerStationMaxWarp = 50.0;

    [Tooltip("Inner gate max range (m). Gate can apply only inside this range.")]
    public double innerStationRangeMeters = 1000.0;

    [Tooltip("Inner gate max score in seconds. score = range / relVel.")]
    public double innerStationScoreSeconds = 30.0;

    [Tooltip("Inner gate warp cap.")]
    public double innerStationMaxWarp = 10.0;

    [Tooltip("Small velocity floor to avoid divide-by-zero and huge scores at tiny rel speed.")]
    public double stationRelativeSpeedFloorMps = 0.01;

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
    public double nearestStationRelativeSpeedMps = 0.0;
    public double nearestStationScoreSeconds = double.PositiveInfinity;

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
        nearestStationRelativeSpeedMps = 0.0;
        nearestStationScoreSeconds = double.PositiveInfinity;
        activeStationZone = 0;
    }

    private bool IsDocked()
    {
        if (netState == null) return false;
        return netState.mode == CraftNetState.MODE_DOCKED;
    }

    private double GetStationRelativeSpeedMps(int stationIndex)
    {
        if (contacts == null) return 0.0;
        if (stationIndex < 0) return 0.0;

        double vx = 0.0;
        double vy = 0.0;
        double vz = 0.0;
        bool found = false;

        // Check full slot 0
        if (contacts.fullValid0 && contacts.fullStationIndex0 == stationIndex)
        {
            vx = contacts.dvx_E0;
            vy = contacts.dvy_E0;
            vz = contacts.dvz_E0;
            found = true;
        }

        // Check full slot 1
        if (!found && contacts.fullValid1 && contacts.fullStationIndex1 == stationIndex)
        {
            vx = contacts.dvx_E1;
            vy = contacts.dvy_E1;
            vz = contacts.dvz_E1;
            found = true;
        }

        if (!found)
        {
            // Not promoted → no velocity info → treat as slow
            return 0.0;
        }

        // magnitude
        double speed = System.Math.Sqrt(vx * vx + vy * vy + vz * vz);
        return speed;
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

        // Docked craft: no station gating at all.
        if (IsDocked()) return double.PositiveInfinity;

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

        double relSpeed = GetStationRelativeSpeedMps(bestIdx);
        if (relSpeed < 0.0) relSpeed = -relSpeed;

        nearestStationRelativeSpeedMps = relSpeed;

        double denom = relSpeed;
        if (denom < stationRelativeSpeedFloorMps) denom = stationRelativeSpeedFloorMps;

        double scoreSeconds = bestRange / denom;
        nearestStationScoreSeconds = scoreSeconds;

        double bestCap = double.PositiveInfinity;
        int bestZone = 0;

        // Outer gate
        if (bestRange <= outerStationRangeMeters && scoreSeconds <= outerStationScoreSeconds)
        {
            bestCap = outerStationMaxWarp;
            bestZone = 1;
        }

        // Inner gate
        if (bestRange <= innerStationRangeMeters && scoreSeconds <= innerStationScoreSeconds)
        {
            if (innerStationMaxWarp < bestCap)
            {
                bestCap = innerStationMaxWarp;
                bestZone = 2;
            }
        }

        if (bestZone != 0)
        {
            stationCapActive = true;
            activeStationZone = bestZone;
        }

        return bestCap;
    }


}