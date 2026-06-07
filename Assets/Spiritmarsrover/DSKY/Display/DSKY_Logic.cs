using UdonSharp;
using UnityEngine;

public class DSKY_Logic : UdonSharpBehaviour
{
    public Material displayRenderer;

    // Bitmask lookup for digits 0-9
    // Segments: 0=A, 1=B, 2=C, 3=D, 4=E, 5=F, 6=G
    private int[] digitToMask = {
        0b00111111, // 0 (ABCDEF on)
        0b00000110, // 1 (BC on)
        0b01011011, // 2 (ABDEG on)
        0b01001111, // 3 (ABCDG on)
        0b01100110, // 4 (BCFG on)
        0b01101101, // 5 (ACDFG on)
        0b01111101, // 6 (ACDEFG on)
        0b00000111, // 7 (ABC on)
        0b01111111, // 8 (ABCDEFG on)
        0b01101111  // 9 (ABCDFG on)
    };

    public void SetNumber(int digitIndex, int value)
    {
        // Safety check
        if (value < 0 || value > 9) return;

        int mask = digitToMask[value];
        UpdateShader(digitIndex, mask);
    }

    // This handles the weirdness of packing data into Vector4s for the shader
    private Vector4 data1 = new Vector4(0,0,0,0);
    private Vector4 data2 = new Vector4(0,0,0,0);

    private void UpdateShader(int index, int mask)
    {
        if (index < 4) {
            if (index == 0) data1.x = mask;
            if (index == 1) data1.y = mask;
            if (index == 2) data1.z = mask;
            if (index == 3) data1.w = mask;
        } else {
            if (index == 4) data2.x = mask;
            if (index == 5) data2.y = mask;
            if (index == 6) data2.z = mask;
            if (index == 7) data2.w = mask;
        }

        displayRenderer.SetVector("_DigitData1", data1);
        displayRenderer.SetVector("_DigitData2", data2);
    }
    
    // Test function: Call this to see "1 2 3 4"
    public void TestDisplay()
    {
        SetNumber(0, 1);
        SetNumber(1, 2);
        SetNumber(2, 3);
        SetNumber(3, 4);
    }
    public override void Interact()
    {
        TestDisplay();
    }

}