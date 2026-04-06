using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDMapPage : MFDPage
{
    [Header("References")]
    public GuidanceNavCoreState nav;
    public BodyCatalog bodies;
    public GroundTrackDisplayDriver groundTrack;

    [Header("Map Layout (display UV 0..1)")]
    [Tooltip("xmin, ymin, xmax, ymax in MFD UV space.")]
    public Vector4 mapRectUv = new Vector4(0.04f, 0.16f, 0.96f, 0.84f);

    [Header("Image Source UV")]
    [Tooltip("umin, vmin, umax, vmax in source texture UV space.")]
    public Vector4 mapSourceUv = new Vector4(0f, 0f, 1f, 1f);

    [Header("Map Border")]
    public Color borderColor = Color.green;
    public float borderPadUv = 0.008f;

    [Header("Display Data")]
    public bool hasValidMap = false;
    public string bodyLabel = "---";

    public int utcYear;
    public int utcMonth;
    public int utcDay;
    public int utcHour;
    public int utcMinute;
    public int utcSecond;

    public double latDeg;
    public double lonDeg;
    public double altMeters;

    private bool _haveLatLon = false;

    void Update()
    {
        RefreshData();
    }

    private void RefreshData()
    {
        hasValidMap = false;
        bodyLabel = "---";
        _haveLatLon = false;
        altMeters = 0.0;

        if (nav == null || !nav.valid || bodies == null)
            return;

        if (nav.primaryId == bodies.earthId) bodyLabel = "EARTH";
        else if (nav.primaryId == bodies.moonId) bodyLabel = "MOON";
        else bodyLabel = "BODY " + nav.primaryId;

        OrbitHelpers.JulianDateToUtc(
            nav.jd,
            out utcYear,
            out utcMonth,
            out utcDay,
            out utcHour,
            out utcMinute,
            out utcSecond
        );

        _haveLatLon = OrbitHelpers.TryGetSubpointLatLonDeg(
            nav,
            out latDeg,
            out lonDeg
        );

        altMeters = nav.rMag - nav.radiusPrimary;

        // Debug.Log(
        //     "[MFDMapPage] primary=" + nav.primaryId +
        //     " lat=" + latDeg.ToString("0.000") +
        //     " lon=" + lonDeg.ToString("0.000") +
        //     " have=" + (_haveLatLon ? "1" : "0")
        // );

        if (groundTrack != null && groundTrack.MapMatchesBody(nav.primaryId))
            hasValidMap = true;
    }

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
        }
    }

    private string FormatDate()
    {
        return utcYear.ToString("0000") + "-" +
               utcMonth.ToString("00") + "-" +
               utcDay.ToString("00");
    }

    private string FormatUtc()
    {
        return utcHour.ToString("00") + ":" +
               utcMinute.ToString("00") + ":" +
               utcSecond.ToString("00");
    }

    private string FormatLatitude()
    {
        if (!_haveLatLon) return "LAT ---";

        double a = System.Math.Abs(latDeg);
        string hemi = (latDeg >= 0.0) ? "N" : "S";
        return "LAT " + a.ToString("0.00") + " " + hemi;
    }

    private string FormatLongitude()
    {
        if (!_haveLatLon) return "LON ---";

        double a = System.Math.Abs(lonDeg);
        string hemi = (lonDeg >= 0.0) ? "E" : "W";
        return "LON " + a.ToString("0.00") + " " + hemi;
    }

    private void DrawMapBorder(MFD display)
    {
        float x0 = mapRectUv.x - borderPadUv;
        float y0 = mapRectUv.y - borderPadUv;
        float x1 = mapRectUv.z + borderPadUv;
        float y1 = mapRectUv.w + borderPadUv;

        Vector2 bl = UvToMfd(x0, y0);
        Vector2 br = UvToMfd(x1, y0);
        Vector2 tr = UvToMfd(x1, y1);
        Vector2 tl = UvToMfd(x0, y1);

        display.DrawLine(bl, br, borderColor);
        display.DrawLine(br, tr, borderColor);
        display.DrawLine(tr, tl, borderColor);
        display.DrawLine(tl, bl, borderColor);
    }

    private Vector2 UvToMfd(float u, float v)
    {
        return new Vector2(
            2f * (u - 0.5f),
            2f * (v - 0.5f)
        );
    }

    public override void DrawDisplay(MFD display)
    {
        display.ClearGraphics();
        display.ClearText();

        if (hasValidMap) {
            display.SetImagePanel(
                groundTrack.mapRT,
                mapRectUv,
                mapSourceUv,
                Color.white
            );
        } else {
            display.ClearImagePanel();
        }

        DrawMapBorder(display);

        // Top edge
        display.DrawText(bodyLabel, 1, 2, Color.green);
        display.DrawText("DATE " + FormatDate(), 1, 14, Color.green);
        display.DrawText("UTC " + FormatUtc(), 1, 33, Color.green);

        // Bottom edge
        display.DrawText(FormatLatitude(), 21, 2, Color.green);
        display.DrawText(FormatLongitude(), 21, 18, Color.green);
        display.DrawText(MFD.FormatNumber("ALT", altMeters), 21, 35, Color.green);

        if (!hasValidMap) {
            display.DrawText("NO MAP", 12, 20, Color.green);
        }

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }
}