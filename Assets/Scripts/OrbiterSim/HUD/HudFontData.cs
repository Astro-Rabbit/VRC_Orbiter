using UdonSharp;
using UnityEngine;

/// <summary>
/// Udon-friendly baked font data for HUD shader text.
/// UV rect format: (uMin, vMin, uMax, vMax)
/// Aspect = glyph width / glyph height
/// </summary>
public class HudFontData : UdonSharpBehaviour
{
    [Header("Atlas")]
    public Texture2D atlas;

    [Header("Digits UV")]
    public Vector4 uv_0;
    public Vector4 uv_1;
    public Vector4 uv_2;
    public Vector4 uv_3;
    public Vector4 uv_4;
    public Vector4 uv_5;
    public Vector4 uv_6;
    public Vector4 uv_7;
    public Vector4 uv_8;
    public Vector4 uv_9;

    [Header("Symbols UV")]
    public Vector4 uv_minus;
    public Vector4 uv_plus;
    public Vector4 uv_dot;

    [Header("Uppercase UV")]
    public Vector4 uv_A;
    public Vector4 uv_B;
    public Vector4 uv_C;
    public Vector4 uv_D;
    public Vector4 uv_E;
    public Vector4 uv_F;
    public Vector4 uv_G;
    public Vector4 uv_H;
    public Vector4 uv_I;
    public Vector4 uv_J;
    public Vector4 uv_K;
    public Vector4 uv_L;
    public Vector4 uv_M;
    public Vector4 uv_N;
    public Vector4 uv_O;
    public Vector4 uv_P;
    public Vector4 uv_Q;
    public Vector4 uv_R;
    public Vector4 uv_S;
    public Vector4 uv_T;
    public Vector4 uv_U;
    public Vector4 uv_V;
    public Vector4 uv_W;
    public Vector4 uv_X;
    public Vector4 uv_Y;
    public Vector4 uv_Z;

    [Header("Digits Aspect")]
    public float aspect_0 = 0.6f;
    public float aspect_1 = 0.6f;
    public float aspect_2 = 0.6f;
    public float aspect_3 = 0.6f;
    public float aspect_4 = 0.6f;
    public float aspect_5 = 0.6f;
    public float aspect_6 = 0.6f;
    public float aspect_7 = 0.6f;
    public float aspect_8 = 0.6f;
    public float aspect_9 = 0.6f;

    [Header("Symbols Aspect")]
    public float aspect_minus = 0.2f;
    public float aspect_plus = 0.5f;
    public float aspect_dot = 0.2f;

    [Header("Uppercase Aspect")]
    public float aspect_A = 0.6f;
    public float aspect_B = 0.6f;
    public float aspect_C = 0.6f;
    public float aspect_D = 0.6f;
    public float aspect_E = 0.6f;
    public float aspect_F = 0.6f;
    public float aspect_G = 0.6f;
    public float aspect_H = 0.6f;
    public float aspect_I = 0.6f;
    public float aspect_J = 0.6f;
    public float aspect_K = 0.6f;
    public float aspect_L = 0.6f;
    public float aspect_M = 0.6f;
    public float aspect_N = 0.6f;
    public float aspect_O = 0.6f;
    public float aspect_P = 0.6f;
    public float aspect_Q = 0.6f;
    public float aspect_R = 0.6f;
    public float aspect_S = 0.6f;
    public float aspect_T = 0.6f;
    public float aspect_U = 0.6f;
    public float aspect_V = 0.6f;
    public float aspect_W = 0.6f;
    public float aspect_X = 0.6f;
    public float aspect_Y = 0.6f;
    public float aspect_Z = 0.6f;
}