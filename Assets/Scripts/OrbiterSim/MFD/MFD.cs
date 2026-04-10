using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;

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

    // Must match values in MFDGraphicsShader.shader
    public const int TEXT_ROWS = 24;
    public const int TEXT_COLUMNS = 48;
    const int MAX_SHAPES = 32;

    [Header("Optional image panel")]
    public Texture imageTex;
    public bool imageEnabled = false;
    public Vector4 imageRectUv = new Vector4(0, 0, 1, 1);
    public Vector4 imageSourceUv = new Vector4(0, 0, 1, 1);
    public Color imageTint = Color.white;

    private Texture2D charDataTex;
    private byte[] blankText;

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
        blankText = new byte[TEXT_COLUMNS * TEXT_ROWS * 4];
        for (int i = 0; i < blankText.Length; i++) {
            blankText[i] = 0;
        }

        graphicsMaterial.SetVectorArray("atlasRects", fontData.atlasRects);
        graphicsMaterial.SetVectorArray("charRects", fontData.charRects);

        charDataTex = new Texture2D(TEXT_COLUMNS, TEXT_ROWS, TextureFormat.RGBA32, false, true);
        ClearText();
        graphicsMaterial.SetTexture("_TextDataTex", charDataTex);

        shapeColors = new Color[MAX_SHAPES];
        shapeData1 = new float[MAX_SHAPES];
        shapeData2 = new Vector4[MAX_SHAPES];

        currentPage = core.pageList[currentPageId];
        currentPage.AddDisplay(this);

        ClearImagePanel();
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
        ClearImagePanel();

        currentPage = core.pageList[currentPageId];
        currentPage.AddDisplay(this);
        Redraw();
    }

    public void ClearText()
    {
        charDataTex.LoadRawTextureData(blankText);
    }

    public void ClearGraphics()
    {
        shapeCount = 0;
    }

    private void SetChar(int row, int col, char c, Color color)
    {
    }

    public void DrawText(string text, int row, int col, Color color)
    {
        int len = text.Length;
        for (int i = 0; i < len && col + i < TEXT_COLUMNS; i++) {
            color.a = (byte)text[i] / 255f;
            charDataTex.SetPixel(col + i, row, color);
        }
    }

    public void DrawVerticalText(string text, int row, int col, Color color)
    {
        int len = text.Length;
        for (int i = 0; i < len && row + i < TEXT_ROWS; i++) {
            color.a = (byte)text[i] / 255f;
            charDataTex.SetPixel(col, row + i, color);
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
        // Text
        charDataTex.Apply();

        // Vector graphics
        graphicsMaterial.SetColorArray("shapeColors", shapeColors);
        graphicsMaterial.SetFloatArray("shapeData1", shapeData1);
        graphicsMaterial.SetVectorArray("shapeData2", shapeData2);
        graphicsMaterial.SetInt("shapeCount", shapeCount);

        // Optional image panel
        graphicsMaterial.SetFloat("_ImageEnabled", imageEnabled ? 1f : 0f);

        if (imageTex != null) {
            graphicsMaterial.SetTexture("_ImageTex", imageTex);
        }

        graphicsMaterial.SetVector("_ImageRect", imageRectUv);
        graphicsMaterial.SetVector("_ImageUvRect", imageSourceUv);
        graphicsMaterial.SetColor("_ImageTint", imageTint);
    }

    public override void OnDeserialization()
    {
        OnPageIdChange();
    }

    public static string FormatNumber(string title, double num)
    {
        string[] suffixes = new[] { "", "k", "M", "G", "T" };

        int i;
        for (i = 0; i < 5; i++) {
            if (Math.Abs(num) <= 1000) {
                break;
            }
            num /= 1000.0;
        }

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
        return title.PadRight(4) + (ratio * 100).ToString("0.0").PadLeft(5) + "%";
    }

    public void SetImagePanel(Texture tex, Vector4 rectUv, Vector4 sourceUv, Color tint)
    {
        imageTex = tex;
        imageRectUv = rectUv;
        imageSourceUv = sourceUv;
        imageTint = tint;
        imageEnabled = (tex != null);
    }

    public void ClearImagePanel()
    {
        imageTex = null;
        imageEnabled = false;
        imageRectUv = new Vector4(0, 0, 1, 1);
        imageSourceUv = new Vector4(0, 0, 1, 1);
        imageTint = Color.white;
    }
}