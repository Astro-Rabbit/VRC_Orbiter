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

        target.uvs = new Vector4[127 - 32];

        for (int i = 32; i < 127; i++)
            target.uvs[i - 32] = GetUV(font, (char)i);

        EditorUtility.SetDirty(target);
        Debug.Log("MFD font bake complete.");
    }

    Vector4 GetUV(TMP_FontAsset font, char c)
    {
        if (!font.characterLookupTable.TryGetValue(c, out TMP_Character ch))
        {
            Debug.LogWarning($"Missing glyph: {c}");
            return Vector4.zero;
        }

        var gr = ch.glyph.glyphRect;

        float u0 = (float)gr.x / atlasW;
        float u1 = (float)(gr.x + gr.width) / atlasW;

        float v0 = (float)gr.y / atlasH;
        float v1 = (float)(gr.y + gr.height) / atlasH;

        return new Vector4(u0, v0, u1, v1);
    }
}
#endif