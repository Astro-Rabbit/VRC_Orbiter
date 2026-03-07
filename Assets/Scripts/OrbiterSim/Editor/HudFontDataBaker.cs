#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// Bake TMP font glyphs into HudFontData.
/// Select the GameObject with HudFontData,
/// assign the TMP font in the inspector,
/// then click "Bake".
/// </summary>
[CustomEditor(typeof(HudFontData))]
public class HudFontDataBaker : Editor
{
    TMP_FontAsset sourceFont;

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

            Bake((HudFontData)target, sourceFont);
        }
    }

    static void Bake(HudFontData target, TMP_FontAsset font)
    {
        if (font.atlasTexture == null)
        {
            Debug.LogError("TMP font has no atlas texture.");
            return;
        }

        Undo.RecordObject(target, "Bake HUD Font");

        target.atlas = font.atlasTexture as Texture2D;

        int atlasW = target.atlas.width;
        int atlasH = target.atlas.height;

        // Digits
        target.uv_0 = GetUV(font, '0', atlasW, atlasH);
        target.uv_1 = GetUV(font, '1', atlasW, atlasH);
        target.uv_2 = GetUV(font, '2', atlasW, atlasH);
        target.uv_3 = GetUV(font, '3', atlasW, atlasH);
        target.uv_4 = GetUV(font, '4', atlasW, atlasH);
        target.uv_5 = GetUV(font, '5', atlasW, atlasH);
        target.uv_6 = GetUV(font, '6', atlasW, atlasH);
        target.uv_7 = GetUV(font, '7', atlasW, atlasH);
        target.uv_8 = GetUV(font, '8', atlasW, atlasH);
        target.uv_9 = GetUV(font, '9', atlasW, atlasH);

        // Symbols
        target.uv_minus = GetUV(font, '-', atlasW, atlasH);
        target.uv_plus  = GetUV(font, '+', atlasW, atlasH);
        target.uv_dot   = GetUV(font, '.', atlasW, atlasH);

        // Uppercase
        target.uv_A = GetUV(font, 'A', atlasW, atlasH);
        target.uv_B = GetUV(font, 'B', atlasW, atlasH);
        target.uv_C = GetUV(font, 'C', atlasW, atlasH);
        target.uv_D = GetUV(font, 'D', atlasW, atlasH);
        target.uv_E = GetUV(font, 'E', atlasW, atlasH);
        target.uv_F = GetUV(font, 'F', atlasW, atlasH);
        target.uv_G = GetUV(font, 'G', atlasW, atlasH);
        target.uv_H = GetUV(font, 'H', atlasW, atlasH);
        target.uv_I = GetUV(font, 'I', atlasW, atlasH);
        target.uv_J = GetUV(font, 'J', atlasW, atlasH);
        target.uv_K = GetUV(font, 'K', atlasW, atlasH);
        target.uv_L = GetUV(font, 'L', atlasW, atlasH);
        target.uv_M = GetUV(font, 'M', atlasW, atlasH);
        target.uv_N = GetUV(font, 'N', atlasW, atlasH);
        target.uv_O = GetUV(font, 'O', atlasW, atlasH);
        target.uv_P = GetUV(font, 'P', atlasW, atlasH);
        target.uv_Q = GetUV(font, 'Q', atlasW, atlasH);
        target.uv_R = GetUV(font, 'R', atlasW, atlasH);
        target.uv_S = GetUV(font, 'S', atlasW, atlasH);
        target.uv_T = GetUV(font, 'T', atlasW, atlasH);
        target.uv_U = GetUV(font, 'U', atlasW, atlasH);
        target.uv_V = GetUV(font, 'V', atlasW, atlasH);
        target.uv_W = GetUV(font, 'W', atlasW, atlasH);
        target.uv_X = GetUV(font, 'X', atlasW, atlasH);
        target.uv_Y = GetUV(font, 'Y', atlasW, atlasH);
        target.uv_Z = GetUV(font, 'Z', atlasW, atlasH);

        // Digits aspect
        target.aspect_0 = GetAspect(font, '0');
        target.aspect_1 = GetAspect(font, '1');
        target.aspect_2 = GetAspect(font, '2');
        target.aspect_3 = GetAspect(font, '3');
        target.aspect_4 = GetAspect(font, '4');
        target.aspect_5 = GetAspect(font, '5');
        target.aspect_6 = GetAspect(font, '6');
        target.aspect_7 = GetAspect(font, '7');
        target.aspect_8 = GetAspect(font, '8');
        target.aspect_9 = GetAspect(font, '9');

        // Symbols aspect
        target.aspect_minus = GetAspect(font, '-');
        target.aspect_plus  = GetAspect(font, '+');
        target.aspect_dot   = GetAspect(font, '.');

        // Uppercase aspect
        target.aspect_A = GetAspect(font, 'A');
        target.aspect_B = GetAspect(font, 'B');
        target.aspect_C = GetAspect(font, 'C');
        target.aspect_D = GetAspect(font, 'D');
        target.aspect_E = GetAspect(font, 'E');
        target.aspect_F = GetAspect(font, 'F');
        target.aspect_G = GetAspect(font, 'G');
        target.aspect_H = GetAspect(font, 'H');
        target.aspect_I = GetAspect(font, 'I');
        target.aspect_J = GetAspect(font, 'J');
        target.aspect_K = GetAspect(font, 'K');
        target.aspect_L = GetAspect(font, 'L');
        target.aspect_M = GetAspect(font, 'M');
        target.aspect_N = GetAspect(font, 'N');
        target.aspect_O = GetAspect(font, 'O');
        target.aspect_P = GetAspect(font, 'P');
        target.aspect_Q = GetAspect(font, 'Q');
        target.aspect_R = GetAspect(font, 'R');
        target.aspect_S = GetAspect(font, 'S');
        target.aspect_T = GetAspect(font, 'T');
        target.aspect_U = GetAspect(font, 'U');
        target.aspect_V = GetAspect(font, 'V');
        target.aspect_W = GetAspect(font, 'W');
        target.aspect_X = GetAspect(font, 'X');
        target.aspect_Y = GetAspect(font, 'Y');
        target.aspect_Z = GetAspect(font, 'Z');

        EditorUtility.SetDirty(target);
        Debug.Log("HUD font bake complete.");
    }

    static float GetAspect(TMP_FontAsset font, char c)
    {
        if (!font.characterLookupTable.TryGetValue(c, out TMP_Character ch))
        {
            Debug.LogWarning($"Missing glyph for aspect: {c}");
            return 0.6f;
        }

        var gr = ch.glyph.glyphRect;
        if (gr.height <= 0) return 0.6f;

        return (float)gr.width / (float)gr.height;
    }

    static Vector4 GetUV(TMP_FontAsset font, char c, int atlasW, int atlasH)
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