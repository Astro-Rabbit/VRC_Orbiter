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
    public TMP_Text text;
    public Material graphicsMaterial;

    public MFDPage currentPage;
    [UdonSynced] private byte currentPageId = (byte)MFDPageID.Menu;

    public const int TEXT_ROWS = 24;
    public const int TEXT_COLUMNS = 48;

    private char[][] charGrid;
    private Color[][] charColors;

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
        charGrid = new char[TEXT_ROWS][];
        charColors = new Color[TEXT_ROWS][];
        for (int i = 0; i < TEXT_ROWS; i++) {
            charGrid[i] = new char[TEXT_COLUMNS];
            charColors[i] = new Color[TEXT_COLUMNS];
        }
        ClearText();

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

    [NetworkCallable]
    public void SetPage(byte pageId)
    {
        if (Networking.IsOwner(gameObject)) {
            currentPageId = pageId;
            RequestSerialization();
            OnPageIdChange();
        } else {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "SetPage", pageId);
        }
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
        for (int i = 0; i < TEXT_ROWS; i++) {
            for (int j = 0; j < TEXT_COLUMNS; j++) {
                charGrid[i][j] = ' ';
            }
        }
    }

    public void ClearGraphics()
    {
        shapeCount = 0;
    }

    public void DrawText(string text, int row, int col, Color color)
    {
        int len = text.Length;
        for (int i = 0; i < len && i + col < TEXT_COLUMNS; i++) {
            charGrid[row][i + col] = text[i];
            charColors[row][i + col] = color;
        }
    }

    public void DrawVerticalText(string text, int row, int col, Color color)
    {
        int len = text.Length;
        for (int i = 0; i < len && i + row < TEXT_ROWS; i++) {
            charGrid[i + row][col] = text[i];
            charColors[i + row][col] = color;
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
        // Concatenate text grid into a string with markup for TextMeshPro
        StringBuilder builder = new StringBuilder("<mspace=0.6em>");
        Color lastColor = Color.white;
        for (int i = 0; i < TEXT_ROWS; i++) {
            for (int j = 0; j < TEXT_COLUMNS; j++) {
                char next = charGrid[i][j];
                Color current = charColors[i][j];

                if (next != ' ' && current != lastColor) {
                    builder.Append("<color=#");
                    builder.Append(((int)Math.Round(current.r * 255)).ToString("X2"));
                    builder.Append(((int)Math.Round(current.g * 255)).ToString("X2"));
                    builder.Append(((int)Math.Round(current.b * 255)).ToString("X2"));
                    builder.Append(">");

                    lastColor = current;
                }

                builder.Append(next);
            }
            builder.Append('\n');
        }

        text.text = builder.ToString();

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

        // FIXME: Is there a better way to fall back?
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
