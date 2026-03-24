using UdonSharp;
using System;
using System.Text;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

public enum ButtonSide
{
    Left,
    Right,
    Top,
    Bottom,
}

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MFD : UdonSharpBehaviour
{
    public MFDCore core;
    public Canvas canvas;
    public MFDFontData fontData;
    public Material graphicsMaterial;

    public MFDPage currentPage;
    [UdonSynced] private byte currentPageId = (byte)MFDPageID.Menu;

    public const int TEXT_ROWS = 24;
    public const int TEXT_COLUMNS = 48;

    private float[] charGrid; // Unity materials don't support uploading int arrays for some reason
    private Color[] charColors;

    // Must match value in MFDGraphicsShader.shader
    const int MAX_SHAPES = 256;

    private int shapeCount = 0;
    private Color[] shapeColors;
    private float[] shapeData1;
    private Vector4[] shapeData2;

    public void L1() { OnButton(ButtonSide.Left, 0); }
    public void L2() { OnButton(ButtonSide.Left, 1); }
    public void L3() { OnButton(ButtonSide.Left, 2); }
    public void L4() { OnButton(ButtonSide.Left, 3); }
    public void L5() { OnButton(ButtonSide.Left, 4); }

    public void R1() { OnButton(ButtonSide.Right, 0); }
    public void R2() { OnButton(ButtonSide.Right, 1); }
    public void R3() { OnButton(ButtonSide.Right, 2); }
    public void R4() { OnButton(ButtonSide.Right, 3); }
    public void R5() { OnButton(ButtonSide.Right, 4); }

    public void T1() { OnButton(ButtonSide.Top, 0); }
    public void T2() { OnButton(ButtonSide.Top, 1); }
    public void T3() { OnButton(ButtonSide.Top, 2); }
    public void T4() { OnButton(ButtonSide.Top, 3); }
    public void T5() { OnButton(ButtonSide.Top, 4); }

    public void B1() { OnButton(ButtonSide.Bottom, 0); }
    public void B2() { OnButton(ButtonSide.Bottom, 1); }
    public void B3() { OnButton(ButtonSide.Bottom, 2); }
    public void B4() { OnButton(ButtonSide.Bottom, 3); }
    public void B5() { OnButton(ButtonSide.Bottom, 4); }

    public void Start()
    {
        charGrid = new float[TEXT_ROWS * TEXT_COLUMNS];
        charColors = new Color[TEXT_ROWS * TEXT_COLUMNS];
        ClearText();

        graphicsMaterial.SetVectorArray("atlasRects", fontData.atlasRects);
        graphicsMaterial.SetVectorArray("charRects", fontData.charRects);

        shapeColors = new Color[MAX_SHAPES];
        shapeData1 = new float[MAX_SHAPES];
        shapeData2 = new Vector4[MAX_SHAPES];

        currentPage = core.pageList[currentPageId];
        currentPage.AddDisplay(this);
        Redraw();
    }

    public void Update()
    {
        Redraw();
    }

    public void OnButton(ButtonSide side, int num)
    {
        currentPage.OnButton(this, side, num);
    }

    public void SetPage(byte pageId)
    {
        if (!Networking.IsOwner(gameObject)) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        currentPageId = pageId;
        RequestSerialization();
        OnPageIdChange();
    }

    private void OnPageIdChange()
    {
        currentPage.RemoveDisplay(this);
        ClearGraphics();
        ClearText();

        currentPage = core.pageList[currentPageId];
        currentPage.AddDisplay(this);
        Redraw();
    }

    public void ClearText()
    {
        // Trying to avoid long loops inside Udon
        charGrid = new float[TEXT_ROWS * TEXT_COLUMNS];
    }

    public void ClearGraphics()
    {
        shapeCount = 0;
    }

    public void DrawText(string text, int row, int col, Color color)
    {
        int len = text.Length;
        for (int i = 0; i < len && i + col < TEXT_COLUMNS; i++) {
            charGrid[row*TEXT_COLUMNS + i + col] = (float)text[i];
            charColors[row*TEXT_COLUMNS + i + col] = color;
        }
    }

    public void DrawVerticalText(string text, int row, int col, Color color)
    {
        int len = text.Length;
        for (int i = 0; i < len && i + row < TEXT_ROWS; i++) {
            charGrid[(i + row)*TEXT_COLUMNS + col] = (float)text[i];
            charColors[(i + row)*TEXT_COLUMNS + col] = color;
        }
    }

    public void DrawConic(Vector2 center, float vertexDist, float angle, float eccentricity, Color color)
    {
        if (shapeCount >= MAX_SHAPES) {
            return;
        }

        shapeColors[shapeCount] = color;
        shapeData1[shapeCount] = vertexDist;
        shapeData2[shapeCount] = new Vector4(eccentricity, angle, center.x, center.y);

        shapeCount++;
    }

    public void DrawLine(Vector2 a, Vector2 b, Color color)
    {
        if (shapeCount >= MAX_SHAPES) {
            return;
        }

        shapeColors[shapeCount] = color;
        shapeData1[shapeCount] = 0f;
        shapeData2[shapeCount] = new Vector4(a.x, a.y, b.x, b.y);

        shapeCount++;

    }

    private void Redraw()
    {
        currentPage.DrawDisplay(this);
        FlushDrawCommands();
    }

    private void FlushDrawCommands()
    {
        // Upload text data to screen material
        graphicsMaterial.SetFloatArray("charGrid", charGrid);
        graphicsMaterial.SetColorArray("charColors", charColors);

        // Upload graphics shape data to screen material
        graphicsMaterial.SetColorArray("shapeColors", shapeColors);
        graphicsMaterial.SetFloatArray("shapeData1", shapeData1);
        graphicsMaterial.SetVectorArray("shapeData2", shapeData2);
        graphicsMaterial.SetInt("shapeCount", shapeCount);
    }

    public override void OnDeserialization()
    {
        OnPageIdChange();
    }

    public static string FormatNumber(string title, double num)
    {
        string[] suffixes = new[] {"", "k", "M", "G", "T"};

        int i;
        for (i = 0; i < 5; i++) {
            if (Math.Abs(num) <= 1000) {
                break;
            }

            num /= 1000.0;
        }

        // 999.9 Teranumbers of RAM oughta be enough for anyone, eh?
        if (i == 5) {
            return "";
        }

        string format;
        if (num >= 100) {
            format = "000.0";
        } else if (num >= 10) {
            format = "00.00";
        } else {
            format = "0.000";
        }
        if (i == 0) {
            format += "0";
        }

        return title.PadRight(4) + num.ToString(format) + suffixes[i];
    }

    public static string FormatAngle(string title, double angle)
    {
        return title.PadRight(3) + (180.0 / Math.PI * angle).ToString("0.0").PadLeft(6) + "°";
    }

    public static string FormatPercent(string title, double ratio)
    {
        return title.PadRight(4) + (ratio*100).ToString("0.0").PadLeft(5) + "%";
    }
}