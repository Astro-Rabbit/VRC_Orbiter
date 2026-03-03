using UdonSharp;
using UnityEngine;
using System;

/// <summary>
/// MoonTilePlannerSweptDebug
/// Milestone A:
/// - Propagate a Keplerian ellipse (conic elements) in a Moon-centered inertial frame.
/// - Compute instantaneous horizon cap C(t0) and swept cap union S(t0,T) by sampling future times.
/// - Pick up to 16 tiles to render on a debug sphere shader:
///     - First, tiles needed "now" (C(t0))
///     - Then, tiles needed soon (S(t0,T)) by earliest-needed time
///
/// Conventions (debug):
/// - +Y is north pole.
/// - lon=0 at +X, lon=+90 at +Z.
/// - No body rotation / prime meridian drift handled in this milestone.
/// </summary>
public class MoonPatchPlannerDebug : UdonSharpBehaviour
{
    [Header("Time (seconds)")]
    public double tNow = 0.0;          // drive this from your SimClock later; for debug you can animate it
    public bool advanceTime = true;
    public double timeRate = 1.0;      // seconds per real second

    [Header("Moon Parameters")]
    public double moonRadiusM = 1737400.0;
    public double mu = 4.9048695e12;   // Moon GM (m^3/s^2) - replace with your value if different

    [Header("Conic Elements (Moon-centered)")]
    public double a = 1837400.0;       // semi-major axis (m)  (example ~100km altitude circular: R+100km)
    [Range(0f, 0.99f)] public float e01 = 0f;
    public double iDeg = 0.0;
    public double raanDeg = 0.0;
    public double argpDeg = 0.0;
    public double M0Deg = 0.0;         // mean anomaly at epoch
    public double epochT = 0.0;        // epoch in same seconds timebase as tNow

    [Header("Planner Sampling")]
    public double lookaheadT = 100.0;  // seconds
    public double sampleDt = 5.0;      // seconds (match 1/5s loader cadence later)

    [Header("Tile Level (fixed for Milestone A)")]
    [Range(6, 10)] public int levelN = 8;

    [Header("Debug Render")]
    public Material debugMat;          // material using Orbiter/MoonTileDebug
    [Range(1, 16)] public int maxSlots = 16;

    [Header("Outputs (debug)")]
    public double altitudeM;
    public double subLatDeg;
    public double subLonDeg;
    public double horizonAlphaDeg;
    public int nowTileCount;
    public int sweptUniqueTileCount;

    // ----------------------------
    // Internal buffers
    // ----------------------------

    // "Now" tile list (can exceed 16; we keep a bounded list for sorting/picking)
    private int[] _nowKeys = new int[1024];
    private int _nowCount;

    // Swept unique tiles with earliest needed time
    private int[] _sKeys = new int[4096];
    private double[] _sNeedT = new double[4096];
    private int _sCount;

    // Chosen tiles to render (<=16), store bounds as Vector4(lonMin,lonMax,latMin,latMax)
    private Vector4[] _slots = new Vector4[16];

    // ----------------------------
    // Unity lifecycle
    // ----------------------------

    void Start()
    {
        ClearShaderTiles();
    }

    void Update()
    {
        if (advanceTime)
        {
            tNow += Time.deltaTime * timeRate;
        }
    }

    void LateUpdate()
    {
        if (debugMat == null) return;

        // 1) Propagate to get r(tNow)
        Vector3 rNow = PropagateR((double)tNow);

        double rMag = rNow.magnitude;
        altitudeM = rMag - moonRadiusM;

        // 2) Subpoint + horizon angle
        Vector3 u = rNow / (float)rMag;

        subLatDeg = Math.Asin(Clamp(u.y, -1.0, 1.0)) * Rad2Deg;
        subLonDeg = WrapLonDeg((float)(Math.Atan2(u.z, u.x) * Rad2Deg));

        if (rMag <= moonRadiusM * 1.000001)
            horizonAlphaDeg = 180.0;
        else
            horizonAlphaDeg = Math.Acos(moonRadiusM / rMag) * Rad2Deg;

        // 3) Build NOW set at tNow
        _nowCount = 0;
        FillCapTileKeys(levelN, (float)subLatDeg, (float)subLonDeg, (float)horizonAlphaDeg, _nowKeys, ref _nowCount, _nowKeys.Length);
        nowTileCount = _nowCount;

        // 4) Build SWEPT set over lookahead by sampling future times, tracking earliest-needed time
        _sCount = 0;

        int K = (sampleDt <= 0.0) ? 0 : (int)Math.Floor(lookaheadT / sampleDt);
        if (K < 0) K = 0;

        for (int k = 0; k <= K; k++)
        {
            double tk = tNow + k * sampleDt;
            Vector3 rK = PropagateR(tk);

            double rKm = rK.magnitude;
            Vector3 uK = rK / (float)rKm;

            float latK = (float)(Math.Asin(Clamp(uK.y, -1.0, 1.0)) * Rad2Deg);
            float lonK = WrapLonDeg((float)(Math.Atan2(uK.z, uK.x) * Rad2Deg));

            double alphaK = (rKm <= moonRadiusM * 1.000001) ? 180.0 : (Math.Acos(moonRadiusM / rKm) * Rad2Deg);

            // temp list for this sample
            int tmpCount = 0;
            FillCapTileKeys(levelN, latK, lonK, (float)alphaK, _nowKeys, ref tmpCount, _nowKeys.Length);

            for (int i = 0; i < tmpCount; i++)
            {
                int key = _nowKeys[i];
                AddOrUpdateSwept(key, tk);
            }
        }

        sweptUniqueTileCount = _sCount;

        // 5) Pick up to 16 slots:
        //    - First: all NOW tiles (or as many as fit)
        //    - Then: remaining from swept set by earliest-needed time, skipping those already included
        for (int s = 0; s < 16; s++) _slots[s] = Vector4.zero;

        int slotFill = 0;

        // Add NOW tiles first
        for (int i = 0; i < _nowCount && slotFill < maxSlots; i++)
        {
            int key = _nowKeys[i];
            if (!SlotContainsKey(key, slotFill))
            {
                _slots[slotFill++] = BoundsForKey(key);
            }
        }

        // Sort swept by tNeed ascending (simple selection sort; N is small in practice)
        SortSweptByNeedTime();

        // Add SWEPT tiles
        for (int i = 0; i < _sCount && slotFill < maxSlots; i++)
        {
            int key = _sKeys[i];
            if (!SlotContainsKey(key, slotFill) && !NowContainsKey(key))
            {
                _slots[slotFill++] = BoundsForKey(key);
            }
        }

        // 6) Push to shader
        for (int i = 0; i < 16; i++)
            debugMat.SetVector("_Tile" + i, _slots[i]);
    }

    // ----------------------------
    // Kepler propagation (ellipse)
    // ----------------------------

    private Vector3 PropagateR(double t)
    {
        double e = (double)e01;

        // Mean motion
        double n = Math.Sqrt(mu / (a * a * a));

        // Mean anomaly at time t
        double M = (M0Deg * Deg2Rad) + n * (t - epochT);
        M = WrapAngleRad(M);

        // Solve Kepler: M = E - e sin E
        double E = SolveKeplerE(M, e);

        // True anomaly
        double sinE = Math.Sin(E);
        double cosE = Math.Cos(E);
        double sqrt1me2 = Math.Sqrt(1.0 - e * e);

        double sinNu = (sqrt1me2 * sinE) / (1.0 - e * cosE);
        double cosNu = (cosE - e) / (1.0 - e * cosE);
        double nu = Math.Atan2(sinNu, cosNu);

        // Radius
        double r = a * (1.0 - e * cosE);

        // Perifocal position (PQW)
        double xP = r * Math.Cos(nu);
        double yP = r * Math.Sin(nu);
        double zP = 0.0;

        // Rotate PQW -> inertial: Rz(raan)*Rx(i)*Rz(argp)
        double i = iDeg * Deg2Rad;
        double raan = raanDeg * Deg2Rad;
        double argp = argpDeg * Deg2Rad;

        double cO = Math.Cos(raan), sO = Math.Sin(raan);
        double ci = Math.Cos(i),    si = Math.Sin(i);
        double cw = Math.Cos(argp), sw = Math.Sin(argp);

        // Combined rotation matrix elements for PQW->IJK
        double R11 =  cO*cw - sO*sw*ci;
        double R12 = -cO*sw - sO*cw*ci;
        double R13 =  sO*si;

        double R21 =  sO*cw + cO*sw*ci;
        double R22 = -sO*sw + cO*cw*ci;
        double R23 = -cO*si;

        double R31 =  sw*si;
        double R32 =  cw*si;
        double R33 =  ci;

        double x = R11*xP + R12*yP + R13*zP;
        double y = R21*xP + R22*yP + R23*zP;
        double z = R31*xP + R32*yP + R33*zP;

        return new Vector3((float)x, (float)y, (float)z);
    }

    private double SolveKeplerE(double M, double e)
    {
        // Newton-Raphson; good enough for e < 1
        double E = (e < 0.8) ? M : Math.PI;
        for (int it = 0; it < 12; it++)
        {
            double f = E - e * Math.Sin(E) - M;
            double fp = 1.0 - e * Math.Cos(E);
            double dE = -f / fp;
            E += dE;
            if (Math.Abs(dE) < 1e-10) break;
        }
        return E;
    }

    // ----------------------------
    // Cap -> tiles (matches your Python indexing)
    // ----------------------------

    private void FillCapTileKeys(int n, float lat0Deg, float lon0Deg, float alphaDeg,
                                 int[] outKeys, ref int outCount, int outMax)
    {
        int nlng = 1 << (n - 3);
        int nlat = 1 << (n - 4);

        float dlon = 360f / nlng;
        float dlat = 180f / nlat;

        float lat0 = lat0Deg * Mathf.Deg2Rad;
        float cosAlpha = Mathf.Cos(alphaDeg * Mathf.Deg2Rad);

        float latMinCap = Mathf.Clamp(lat0Deg - alphaDeg, -90f, 90f);
        float latMaxCap = Mathf.Clamp(lat0Deg + alphaDeg, -90f, 90f);

        int ilatMin = Mathf.Clamp((int)Mathf.Floor((90f - latMaxCap) / dlat), 0, nlat - 1);
        int ilatMax = Mathf.Clamp((int)Mathf.Floor((90f - latMinCap) / dlat), 0, nlat - 1);

        int total = 0;
        for (int ilat = ilatMin; ilat <= ilatMax; ilat++)
        {
            float bandLatMax = 90f - dlat * ilat;
            float bandLatMin = bandLatMax - dlat;

            float dl1 = ComputeDeltaLonMaxRad(lat0, cosAlpha, bandLatMin * Mathf.Deg2Rad);
            float dl2 = ComputeDeltaLonMaxRad(lat0, cosAlpha, bandLatMax * Mathf.Deg2Rad);
            float dLonMax = Mathf.Max(dl1, dl2);
            if (dLonMax <= 0f) continue;

            float dLonMaxDeg = dLonMax * Mathf.Rad2Deg;

            float aDeg = WrapLonDeg(lon0Deg - dLonMaxDeg);
            float bDeg = WrapLonDeg(lon0Deg + dLonMaxDeg);

            if (aDeg <= bDeg)
            {
                int i0 = LonToIlng(nlng, dlon, aDeg);
                int i1 = LonToIlng(nlng, dlon, bDeg);
                for (int ilng = i0; ilng <= i1; ilng++)
                {
                    int key = PackKey(n, ilat, ilng);
                    if (total < outMax) outKeys[total] = key;
                    total++;
                }
            }
            else
            {
                int i0 = LonToIlng(nlng, dlon, aDeg);
                for (int ilng = i0; ilng < nlng; ilng++)
                {
                    int key = PackKey(n, ilat, ilng);
                    if (total < outMax) outKeys[total] = key;
                    total++;
                }
                int j1 = LonToIlng(nlng, dlon, bDeg);
                for (int ilng = 0; ilng <= j1; ilng++)
                {
                    int key = PackKey(n, ilat, ilng);
                    if (total < outMax) outKeys[total] = key;
                    total++;
                }
            }
        }

        outCount = Mathf.Min(total, outMax);
    }

    private float ComputeDeltaLonMaxRad(float lat0Rad, float cosAlpha, float phiRad)
    {
        float s0 = Mathf.Sin(lat0Rad);
        float c0 = Mathf.Cos(lat0Rad);
        float s  = Mathf.Sin(phiRad);
        float c  = Mathf.Cos(phiRad);

        float denom = c * c0;
        if (Mathf.Abs(denom) < 1e-6f) return Mathf.PI; // conservative

        float x = (cosAlpha - s * s0) / denom;
        x = Mathf.Clamp(x, -1f, 1f);
        return Mathf.Acos(x);
    }

    private int LonToIlng(int nlng, float dlon, float lonDeg)
    {
        float lonW = WrapLonDeg(lonDeg);
        int ilng = (int)Mathf.Floor((lonW + 180f) / dlon);
        if (ilng < 0) ilng = 0;
        if (ilng >= nlng) ilng = nlng - 1;
        return ilng;
    }

    // ----------------------------
    // Swept set tracking (no Dictionary in Udon -> linear search arrays)
    // ----------------------------

    private void AddOrUpdateSwept(int key, double tNeed)
    {
        for (int i = 0; i < _sCount; i++)
        {
            if (_sKeys[i] == key)
            {
                if (tNeed < _sNeedT[i]) _sNeedT[i] = tNeed;
                return;
            }
        }

        if (_sCount < _sKeys.Length)
        {
            _sKeys[_sCount] = key;
            _sNeedT[_sCount] = tNeed;
            _sCount++;
        }
        // else: overflow; for n<=10 and short lookahead this should not happen. If it does, we raise buffer sizes.
    }

    private void SortSweptByNeedTime()
    {
        // selection sort; fine for a few hundred items
        for (int i = 0; i < _sCount - 1; i++)
        {
            int best = i;
            double bestT = _sNeedT[i];
            for (int j = i + 1; j < _sCount; j++)
            {
                if (_sNeedT[j] < bestT)
                {
                    bestT = _sNeedT[j];
                    best = j;
                }
            }
            if (best != i)
            {
                int tk = _sKeys[i]; _sKeys[i] = _sKeys[best]; _sKeys[best] = tk;
                double tt = _sNeedT[i]; _sNeedT[i] = _sNeedT[best]; _sNeedT[best] = tt;
            }
        }
    }

    private bool NowContainsKey(int key)
    {
        for (int i = 0; i < _nowCount; i++)
            if (_nowKeys[i] == key) return true;
        return false;
    }

    private bool SlotContainsKey(int key, int slotCount)
    {
        // We only store bounds in slots, so check against bounds-from-key equivalence is annoying.
        // Instead, just avoid dupes by using NowContainsKey + swept ordering; this is fine.
        // We'll keep this as always false for now.
        return false;
    }

    // ----------------------------
    // Tile key packing + bounds
    // ----------------------------

    // Pack (n, ilat, ilng) into an int. Ranges:
    // n up to 15, ilat up to 255, ilng up to 255 -> safe.
    private int PackKey(int n, int ilat, int ilng)
    {
        return (n << 16) | (ilat << 8) | (ilng);
    }

    private void UnpackKey(int key, out int n, out int ilat, out int ilng)
    {
        n = (key >> 16) & 0xFF;
        ilat = (key >> 8) & 0xFF;
        ilng = key & 0xFF;
    }

    private Vector4 BoundsForKey(int key)
    {
        UnpackKey(key, out int n, out int ilat, out int ilng);

        int nlng = 1 << (n - 3);
        int nlat = 1 << (n - 4);

        float dlon = 360f / nlng;
        float dlat = 180f / nlat;

        float lonMin = -180f + dlon * ilng;
        float lonMax = lonMin + dlon;

        float latMax = 90f - dlat * ilat;
        float latMin = latMax - dlat;

        return new Vector4(lonMin, lonMax, latMin, latMax);
    }

    // ----------------------------
    // Shader helpers
    // ----------------------------

    private void ClearShaderTiles()
    {
        if (debugMat == null) return;
        for (int i = 0; i < 16; i++)
            debugMat.SetVector("_Tile" + i, Vector4.zero);
    }

    // ----------------------------
    // Utils
    // ----------------------------

    private const double Deg2Rad = Math.PI / 180.0;
    private const double Rad2Deg = 180.0 / Math.PI;

    private static double Clamp(double v, double a, double b)
    {
        if (v < a) return a;
        if (v > b) return b;
        return v;
    }

    private static float WrapLonDeg(float lon)
    {
        lon = Mathf.Repeat(lon + 180f, 360f) - 180f;
        return lon;
    }

    private static double WrapAngleRad(double a)
    {
        // [-pi, pi)
        a = a % (2.0 * Math.PI);
        if (a >= Math.PI) a -= 2.0 * Math.PI;
        if (a < -Math.PI) a += 2.0 * Math.PI;
        return a;
    }
}