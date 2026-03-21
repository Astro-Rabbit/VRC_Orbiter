using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// GroundTrackMapDriver_Texture
///
/// Feeds an equirectangular ground-track display shader using a 1D segment data texture.
/// Each texel stores one segment as RGBA = (ax, ay, bx, by) in UV space.
///
/// Assumptions:
/// - map is equirectangular
/// - longitude 0 deg is at horizontal center of the image
/// - latitude 0 deg is at vertical center of the image
/// - north is up
///
/// Notes:
/// - Uses a dedicated runtime-created Texture2D for segment data
/// - Designed for low refresh cadence, not every frame unless desired
/// </summary>
public class GroundTrackMapDriver : UdonSharpBehaviour
{
    [Header("Input")]
    public GC_GroundTrackState track;
    public GuidanceNavCoreState nav;

    [Header("Target material")]
    public Material targetMaterial;

    [Header("Refresh")]
    public bool updateEveryFrame = false;
    public float refreshIntervalSec = 0.25f;
    public bool refreshOnTrackChange = true;

    [Header("Segment texture")]
    [Tooltip("Maximum number of rendered segments. Shader loop currently supports up to 128.")]
    public int maxShaderSegments = 64;

    [Tooltip("Try RGBAFloat first. If unsupported in your runtime, switch code to RGBAHalf.")]
    public bool clearUnusedSegments = true;

    [Header("Track build")]
    public int maxInputSamples = 256;

    [Tooltip("If > 0, use simple decimation to fit this many source segments before seam splitting.")]
    public bool splitAtLongitudeWrap = true;

    [Tooltip("Optional longitude offset in degrees applied before mapping to UV.")]
    public float longitudeOffsetDeg = 0f;

    [Tooltip("Flip V if the map texture is upside down.")]
    public bool flipV = false;

    [Header("Appearance")]
    public Color trackColor = Color.green;
    [Range(0.0005f, 0.05f)] public float trackWidthUV = 0.004f;
    [Range(0.0001f, 0.02f)] public float trackSoftnessUV = 0.0015f;

    public bool showCurrentMarker = true;
    public Color markerColor = Color.yellow;
    [Range(0.001f, 0.05f)] public float markerRadiusUV = 0.010f;
    [Range(0.0001f, 0.02f)] public float markerSoftnessUV = 0.002f;

    [Header("Runtime")]
    public float lastRefreshRealtime = -9999f;
    public double lastSeenSourceTimeSec = -1.0;
    public byte lastSeenBodyId = 255;
    public int lastSeenSampleCount = -1;

    public int lastSegmentCount = 0;
    public bool lastValid = false;
    public string lastStatus = "";

    private Texture2D _segTex;
    private Color[] _segPixels;
    private Vector4[] _segments;

    void Start()
    {
        EnsureResources();
        ClearShader();
        RefreshNow();
    }

    void Update()
    {
        if (targetMaterial == null) return;

        if (updateEveryFrame)
        {
            RefreshNow();
            return;
        }

        bool changed = false;

        if (track != null && refreshOnTrackChange)
        {
            if (track.sourceTimeSec != lastSeenSourceTimeSec ||
                track.bodyId != lastSeenBodyId ||
                track.sampleCount != lastSeenSampleCount)
            {
                changed = true;
            }
        }

        float nowRT = Time.time;
        bool dueByTime = (nowRT - lastRefreshRealtime) >= GetRefreshIntervalSec();

        if (changed || dueByTime)
        {
            RefreshNow();
        }
    }

    public void RefreshNow()
    {
        EnsureResources();

        if (targetMaterial == null)
        {
            lastValid = false;
            lastStatus = "Missing target material";
            return;
        }

        ApplyStyle();

        if (track == null)
        {
            lastValid = false;
            lastStatus = "Missing track state";
            ClearShader();
            StampRefreshState();
            return;
        }

        if (!track.valid || track.sampleCount < 1)
        {
            lastValid = false;
            lastStatus = track.lastStatus;
            ClearShader();
            StampRefreshState();
            return;
        }

        int n = track.sampleCount;
        if (n > maxInputSamples) n = maxInputSamples;
        if (n < 1)
        {
            lastValid = false;
            lastStatus = "No samples";
            ClearShader();
            StampRefreshState();
            return;
        }

        Vector2 currentUV;
        if (TryGetCurrentMarkerUVFromNav(out currentUV))
        {
            targetMaterial.SetVector("_CurrentPoint", new Vector4(currentUV.x, currentUV.y, 0f, 0f));
            targetMaterial.SetFloat("_HasCurrentPoint", showCurrentMarker ? 1f : 0f);
        }
        else
        {
            targetMaterial.SetFloat("_HasCurrentPoint", 0f);
        }

        int segCount = BuildSegments(n);

        UploadSegments(segCount);

        lastSegmentCount = segCount;
        lastValid = (segCount > 0);
        lastStatus = track.lastStatus;

        StampRefreshState();
    }

    private void StampRefreshState()
    {
        lastRefreshRealtime = Time.time;

        if (track != null)
        {
            lastSeenSourceTimeSec = track.sourceTimeSec;
            lastSeenBodyId = track.bodyId;
            lastSeenSampleCount = track.sampleCount;
        }
    }

    private void EnsureResources()
    {
        int segCap = GetSegmentCapacity();

        if (_segments == null || _segments.Length != segCap)
            _segments = new Vector4[segCap];

        if (_segPixels == null || _segPixels.Length != segCap)
            _segPixels = new Color[segCap];

        if (_segTex == null || _segTex.width != segCap || _segTex.height != 1)
        {
            // Try RGBAFloat first. If VRChat dislikes this, swap to RGBAHalf.
            _segTex = new Texture2D(segCap, 1, TextureFormat.RGBAFloat, false, true);
            _segTex.wrapMode = TextureWrapMode.Clamp;
            _segTex.filterMode = FilterMode.Point;
            _segTex.anisoLevel = 0;

            if (targetMaterial != null)
            {
                targetMaterial.SetTexture("_SegTex", _segTex);
                targetMaterial.SetFloat("_SegTexWidth", segCap);
            }
        }
    }

    private void ApplyStyle()
    {
        targetMaterial.SetColor("_TrackColor", trackColor);
        targetMaterial.SetFloat("_TrackWidth", trackWidthUV);
        targetMaterial.SetFloat("_TrackSoftness", trackSoftnessUV);

        targetMaterial.SetColor("_MarkerColor", markerColor);
        targetMaterial.SetFloat("_MarkerRadius", markerRadiusUV);
        targetMaterial.SetFloat("_MarkerSoftness", markerSoftnessUV);

        if (_segTex != null)
        {
            targetMaterial.SetTexture("_SegTex", _segTex);
            targetMaterial.SetFloat("_SegTexWidth", _segTex.width);
        }
    }

    private void ClearShader()
    {
        EnsureResources();

        int segCap = GetSegmentCapacity();

        for (int i = 0; i < segCap; i++)
        {
            _segPixels[i] = Color.clear;
        }

        _segTex.SetPixels(_segPixels);
        _segTex.Apply(false, false);

        targetMaterial.SetTexture("_SegTex", _segTex);
        targetMaterial.SetFloat("_SegTexWidth", segCap);
        targetMaterial.SetFloat("_SegCount", 0f);
        targetMaterial.SetFloat("_HasCurrentPoint", 0f);

        lastSegmentCount = 0;
    }

    private int BuildSegments(int sampleCount)
    {
        int segCap = GetSegmentCapacity();

        for (int i = 0; i < segCap; i++)
            _segments[i] = Vector4.zero;

        if (sampleCount < 2) return 0;

        // Simple decimation so we can inspect up to segCap source segments.
        int step = 1;
        int rawSegments = sampleCount - 1;
        if (rawSegments > segCap)
        {
            step = Mathf.CeilToInt((float)rawSegments / (float)segCap);
            if (step < 1) step = 1;
        }

        int segCount = 0;

        int prevIdx = 0;
        Vector2 prevUV = LonLatDegToUV((float)track.lonDeg[prevIdx], (float)track.latDeg[prevIdx]);

        for (int idx = step; idx < sampleCount; idx += step)
        {
            Vector2 nextUV = LonLatDegToUV((float)track.lonDeg[idx], (float)track.latDeg[idx]);

            if (splitAtLongitudeWrap)
                segCount = AddWrappedSegment(prevUV, nextUV, segCount, segCap);
            else
                segCount = AddSimpleSegment(prevUV, nextUV, segCount, segCap);

            if (segCount >= segCap) break;

            prevUV = nextUV;
            prevIdx = idx;
        }

        if (segCount < segCap && prevIdx != (sampleCount - 1))
        {
            Vector2 lastUV = LonLatDegToUV((float)track.lonDeg[sampleCount - 1], (float)track.latDeg[sampleCount - 1]);

            if (splitAtLongitudeWrap)
                segCount = AddWrappedSegment(prevUV, lastUV, segCount, segCap);
            else
                segCount = AddSimpleSegment(prevUV, lastUV, segCount, segCap);
        }

        return segCount;
    }

    private void UploadSegments(int segCount)
    {
        int segCap = GetSegmentCapacity();

        for (int i = 0; i < segCap; i++)
        {
            Vector4 s = _segments[i];
            _segPixels[i] = new Color(s.x, s.y, s.z, s.w);
        }

        if (!clearUnusedSegments)
        {
            // If disabled, we still upload the full buffer as built above.
        }

        _segTex.SetPixels(_segPixels);
        _segTex.Apply(false, false);

        targetMaterial.SetTexture("_SegTex", _segTex);
        targetMaterial.SetFloat("_SegTexWidth", segCap);
        targetMaterial.SetFloat("_SegCount", segCount);
    }

    private int AddSimpleSegment(Vector2 a, Vector2 b, int segCount, int segCap)
    {
        if (segCount >= segCap) return segCount;
        _segments[segCount] = new Vector4(a.x, a.y, b.x, b.y);
        return segCount + 1;
    }

    private int AddWrappedSegment(Vector2 a, Vector2 b, int segCount, int segCap)
    {
        if (segCount >= segCap) return segCount;

        float du = b.x - a.x;

        if (du >= -0.5f && du <= 0.5f)
        {
            _segments[segCount] = new Vector4(a.x, a.y, b.x, b.y);
            return segCount + 1;
        }

        Vector2 bU = b;
        if (du > 0.5f) bU.x -= 1f;
        else if (du < -0.5f) bU.x += 1f;

        float boundaryX;
        if (bU.x < 0f) boundaryX = 0f;
        else if (bU.x > 1f) boundaryX = 1f;
        else
        {
            _segments[segCount] = new Vector4(a.x, a.y, b.x, b.y);
            return segCount + 1;
        }

        float denom = (bU.x - a.x);
        if (Mathf.Abs(denom) < 1e-6f)
        {
            _segments[segCount] = new Vector4(a.x, a.y, b.x, b.y);
            return segCount + 1;
        }

        float t = (boundaryX - a.x) / denom;
        t = Mathf.Clamp01(t);

        float yCross = Mathf.Lerp(a.y, bU.y, t);

        _segments[segCount] = new Vector4(a.x, a.y, boundaryX, yCross);
        segCount++;
        if (segCount >= segCap) return segCount;

        float otherSideX = (boundaryX < 0.5f) ? 1f : 0f;
        _segments[segCount] = new Vector4(otherSideX, yCross, b.x, b.y);
        segCount++;

        return segCount;
    }

    private Vector2 LonLatDegToUV(float lonDeg, float latDeg)
    {
        float lon = lonDeg + longitudeOffsetDeg;

        while (lon < -180f) lon += 360f;
        while (lon >= 180f) lon -= 360f;

        if (latDeg > 90f) latDeg = 90f;
        if (latDeg < -90f) latDeg = -90f;

        float u = 0.5f + (lon / 360f);
        float v = 0.5f + (latDeg / 180f);

        if (flipV) v = 1f - v;

        return new Vector2(u, v);
    }

    private float GetRefreshIntervalSec()
    {
        if (refreshIntervalSec < 0.02f) return 0.02f;
        return refreshIntervalSec;
    }


    private bool TryGetCurrentMarkerUVFromNav(out Vector2 uv)
    {
        uv = Vector2.zero;

        if (nav == null || !nav.valid)
            return false;

        Quaternion qE2PF = Quaternion.Inverse(nav.qPF2E);
        Vector3 rE = new Vector3((float)nav.r_x, (float)nav.r_y, (float)nav.r_z);
        Vector3 rPF = qE2PF * rE;

        double x = rPF.x;
        double y = rPF.y;
        double z = rPF.z;

        double r2 = x * x + y * y + z * z;
        if (r2 <= 1e-12)
            return false;

        double rMag = Math.Sqrt(r2);
        double latRad = Math.Asin(Mathf.Clamp((float)(z / rMag), -1f, 1f));
        double lonRad = Math.Atan2(y, x);

        float lonDeg = (float)(lonRad * 180.0 / Math.PI);
        float latDeg = (float)(latRad * 180.0 / Math.PI);

        uv = LonLatDegToUV(lonDeg, latDeg);
        return true;
    }

    private int GetSegmentCapacity()
    {
        int n = maxShaderSegments;
        if (n < 1) n = 1;
        if (n > 128) n = 128; // shader hard cap
        return n;
    }
}