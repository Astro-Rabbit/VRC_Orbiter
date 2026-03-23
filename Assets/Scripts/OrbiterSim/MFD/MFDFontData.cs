using UdonSharp;
using UnityEngine;

/// Udon-friendly baked font data for MFD shader text
/// UV rect format: (uMin, vMin, uMax, vMax)
public class MFDFontData : UdonSharpBehaviour
{
    [Header("Atlas")]
    public Texture2D atlas;

    [Header("Character UV's")]
    public Vector4[] uvs;
}