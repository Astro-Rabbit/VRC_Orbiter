#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

[CustomEditor(typeof(MFDFontData))]
public class MFDFontDataBaker : Editor
{
    TMP_FontAsset sourceFont;

    private int atlasW;
    private int atlasH;
    private float pointSize;
    private float baseline;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("TMP Bake", EditorStyles.boldLabel);

        sourceFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "TMP Font Asset",
            sourceFont,
            typeof(TMP_FontAsset),
            false);

        if (GUILayout.Button("Bake Font Data"))
        {
            if (sourceFont == null)
            {
                Debug.LogError("Assign a TMP Font Asset first.");
                return;
            }

            Bake((MFDFontData)target, sourceFont);
        }
    }

    void Bake(MFDFontData target, TMP_FontAsset font)
    {
        if (font.atlasTexture == null)
        {
            Debug.LogError("TMP font has no atlas texture.");
            return;
        }

        Undo.RecordObject(target, "Bake MFD Font");

        target.atlas = font.atlasTexture as Texture2D;

        atlasW = target.atlas.width;
        atlasH = target.atlas.height;

        pointSize = font.faceInfo.pointSize;
        baseline = (font.faceInfo.baseline - font.faceInfo.descentLine) * pointSize / font.faceInfo.lineHeight;

        target.atlasRects = new Vector4[127 - 32];
        target.charRects = new Vector4[127 - 32];

        for (int i = 32; i < 127; i++)
            GetRects(font, (char)i, out target.atlasRects[i - 32], out target.charRects[i - 32]);

        EditorUtility.SetDirty(target);
        Debug.Log("MFD font bake complete.");
    }

    void GetRects(TMP_FontAsset font, char c, out Vector4 atlasRect, out Vector4 charRect)
    {
        if (!font.characterLookupTable.TryGetValue(c, out TMP_Character ch))
            Debug.LogWarning($"Missing glyph: {c}");

        var gr = ch.glyph.glyphRect;

        float u0 = (float)gr.x / atlasW;
        float u1 = (float)(gr.x + gr.width) / atlasW;

        float v0 = (float)gr.y / atlasH;
        float v1 = (float)(gr.y + gr.height) / atlasH;

        atlasRect = new Vector4(u0, v0, u1, v1);

        // Assumes a monospaced font with a roughly the same aspect ratio as the shader characters (0.6)
        var gm = ch.glyph.metrics;
        float charX = gm.horizontalBearingX / gm.horizontalAdvance;
        float charW = gm.width / gm.horizontalAdvance;
        float charY = (gm.horizontalBearingY + baseline) / pointSize;
        float charH = gm.height / pointSize;

        charRect = new Vector4(charX, charY, charW, charH);
    }
}
#endif