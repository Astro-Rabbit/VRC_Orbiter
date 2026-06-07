using UdonSharp;
using UnityEngine;

// public class DSKYController : UdonSharpBehaviour
// {
//     public Renderer dskyRenderer;
//     private Material dskyMat;

//     // Standard 7-segment bitmasks (A=1, B=2, C=4, D=8, E=16, F=32, G=64)
//     private readonly int[] digitMasks = {
//         63,  // 0: ABCDEF
//         6,   // 1: BC
//         91,  // 2: ABDEG
//         79,  // 3: ABCDG
//         102, // 4: BCFG
//         109, // 5: ACDFG
//         125, // 6: ACDEFG
//         7,   // 7: ABC
//         127, // 8: ABCDEFG
//         111  // 9: ABCDFG
//     };

//     private float[] currentMasks = new float[24];
//     private float timer;

//     void Start()
//     {
//         if (dskyRenderer) dskyMat = dskyRenderer.material;
//     }

//     void Update()
//     {
//         timer += Time.deltaTime;

//         // 1. Simulate the 3 main registers (Digits 1-15) with random-ish mission data
//         for (int i = 0; i < 15; i++)
//         {
//             int displayValue = (int)(Mathf.PerlinNoise(i, timer * 0.5f) * 10);
//             currentMasks[i] = digitMasks[displayValue];
//         }

//         // 2. Simulate Program, Verb, and Noun (Digits 16-21)
//         SetDigitValue(15, 3); // PROG
//         SetDigitValue(16, 7);
//         SetDigitValue(17, 1); // VERB
//         SetDigitValue(18, 6);
//         SetDigitValue(19, 9); // NOUN
//         SetDigitValue(20, 9);

//         // 3. Digit 22: Plus Signs Logic
//         // A=Cross, B=Minus (Bottom) | C=Cross, D=Minus (Mid) | E=Cross, F=Minus (Top)
//         int plusSigns = 0;
//         if (Mathf.Sin(timer * 2) > 0) plusSigns |= (1 | 2); // Bottom Plus (+)
//         else plusSigns |= 2;                                // Bottom Minus (-)
        
//         plusSigns |= (4 | 8);  // Middle always Plus
//         plusSigns |= 32;       // Top always Minus
//         currentMasks[21] = plusSigns; 

//         // 4. Digit 23: Empty
//         currentMasks[22] = 0;

//         // 5. Digit 24: Status Lights & Bars
//         // A=COMP ACTY (Flickering), others stay ON
//         int statusBits = 0;
//         if (Mathf.Sin(timer * 10) > 0.8f) statusBits |= 64; // COMP ACTY (Bit A)
//         statusBits |= 32;  // Program Light (Bit B)
//         statusBits |= 16;  // Top Bar (Bit C)
//         statusBits |= 8;  // Mid Bar (Bit D)
//         statusBits |= 4; // Low Bar (Bit E)
//         statusBits |= 2; // Verb Light (Bit F)
//         currentMasks[23] = statusBits;

//         UpdateShader();
//     }

//     void SetDigitValue(int index, int val)
//     {
//         currentMasks[index] = digitMasks[Mathf.Clamp(val, 0, 9)];
//     }

//     void UpdateShader()
//     {
//         if (!dskyMat) return;

//         // Pack the 24 floats into 6 Vector4s as required by your shader
//         dskyMat.SetVector("_DigitData1", new Vector4(currentMasks[0], currentMasks[1], currentMasks[2], currentMasks[3]));
//         dskyMat.SetVector("_DigitData2", new Vector4(currentMasks[4], currentMasks[5], currentMasks[6], currentMasks[7]));
//         dskyMat.SetVector("_DigitData3", new Vector4(currentMasks[8], currentMasks[9], currentMasks[10], currentMasks[11]));
//         dskyMat.SetVector("_DigitData4", new Vector4(currentMasks[12], currentMasks[13], currentMasks[14], currentMasks[15]));
//         dskyMat.SetVector("_DigitData5", new Vector4(currentMasks[16], currentMasks[17], currentMasks[18], currentMasks[19]));
//         dskyMat.SetVector("_DigitData6", new Vector4(currentMasks[20], currentMasks[21], currentMasks[22], currentMasks[23]));
//     }//
// }
public class DSKYController : UdonSharpBehaviour
{
    //public Renderer dskyRenderer;
    public Material dskyMat;

    private readonly int[] digitMasks = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
    private float[] currentMasks = new float[24];
    private float sequenceTimer = 0;

    [Header("Animation Settings")]
    public float segmentsPerSecond = 30f; // Speed of the serial fill

    void Start()
    {
        //if (dskyRenderer) dskyMat = dskyRenderer.material;
    }

    void Update()
    {
        sequenceTimer += Time.deltaTime;

        // --- STAGE 1: Serial Fill (One digit at a time, moving to the next) ---
        // There are 24 digits * 7 segments = 168 steps total.
        float totalSerialSteps = 24 * 7;
        float serialDuration = totalSerialSteps / segmentsPerSecond;

        if (sequenceTimer < serialDuration)
        {
            int totalSegmentsOn = Mathf.FloorToInt(sequenceTimer * segmentsPerSecond);
            int currentDigitIdx = totalSegmentsOn / 7;
            int segmentsInCurrentDigit = totalSegmentsOn % 7;

            for (int i = 0; i < 24; i++)
            {
                if (i < currentDigitIdx) 
                    currentMasks[i] = 127; // Previous digits stay full
                else if (i == currentDigitIdx) 
                    currentMasks[i] = GetFillMask(segmentsInCurrentDigit + 1); // Current digit filling
                else 
                    currentMasks[i] = 0; // Future digits stay off
            }
        }
        // --- STAGE 2: Parallel Fill (All digits fill 1-7 segments at the same time) ---
        else if (sequenceTimer < serialDuration + 1.5f)
        {
            float stageTime = sequenceTimer - serialDuration;
            // Fills all segments in 1.5 seconds
            int segmentCount = Mathf.FloorToInt((stageTime / 1.5f) * 8);
            int fillMask = GetFillMask(segmentCount);

            for (int i = 0; i < 21; i++) currentMasks[i] = fillMask;
        }
        // --- STAGE 3: Lamp Test (Everything stays on) ---
        else if (sequenceTimer < serialDuration + 3.0f)
        {
            for (int i = 0; i < 24; i++) currentMasks[i] = 127;
        }
        // --- STAGE 4: Active Simulation ---
        else
        {
            RunActiveSimulation(sequenceTimer - (serialDuration + 3.0f));
        }

        UpdateShader();
    }

    int GetFillMask(int count)
    {
        if (count <= 0) return 0;
        if (count >= 7) return 127;
        return (1 << count) - 1;
    }

    void RunActiveSimulation(float simTime)
    {
        // 1. Registers (Digits 1-15)
        for (int i = 0; i < 15; i++)
        {
            int val = (int)(Mathf.PerlinNoise(i, simTime * 0.5f) * 10);
            currentMasks[i] = digitMasks[val];
        }

        // 2. Program, Verb, Noun (Digits 16-21)
        currentMasks[15] = digitMasks[0]; currentMasks[16] = digitMasks[2]; 
        currentMasks[17] = digitMasks[3]; currentMasks[18] = digitMasks[7]; 
        currentMasks[19] = digitMasks[0]; currentMasks[20] = digitMasks[0]; 

        // 3. Digit 22: Plus Signs
        int signBits = 0;
        signBits |= 2; // Bottom Minus
        if (Mathf.Sin(simTime * 2) > 0) signBits |= 1; // Bottom Plus toggle
        signBits |= (4 | 8); // Middle Plus
        signBits |= 32;      // Top Minus
        currentMasks[21] = signBits;

        // 4. Digit 23: Empty
        currentMasks[22] = 0;

        // 5. Digit 24: Status Lights (Using your specific bit mapping)
        int statusBits = 0;
        if (Mathf.Sin(simTime * 10) > 0.8f) statusBits |= 64; // COMP ACTY (A)
        statusBits |= 32; // Program Light (B)
        statusBits |= 16; // Top Bar (C)
        statusBits |= 8;  // Mid Bar (D)
        statusBits |= 4;  // Low Bar (E)
        statusBits |= 2;  // Verb Light (F)
        statusBits |= 1;  // Noun Light (G)
        currentMasks[23] = statusBits;
    }

    void UpdateShader()
    {
        if (!dskyMat) return;
        dskyMat.SetVector("_DigitData1", new Vector4(currentMasks[0], currentMasks[1], currentMasks[2], currentMasks[3]));
        dskyMat.SetVector("_DigitData2", new Vector4(currentMasks[4], currentMasks[5], currentMasks[6], currentMasks[7]));
        dskyMat.SetVector("_DigitData3", new Vector4(currentMasks[8], currentMasks[9], currentMasks[10], currentMasks[11]));
        dskyMat.SetVector("_DigitData4", new Vector4(currentMasks[12], currentMasks[13], currentMasks[14], currentMasks[15]));
        dskyMat.SetVector("_DigitData5", new Vector4(currentMasks[16], currentMasks[17], currentMasks[18], currentMasks[19]));
        dskyMat.SetVector("_DigitData6", new Vector4(currentMasks[20], currentMasks[21], currentMasks[22], currentMasks[23]));
    }
    public override void Interact()
    {
        sequenceTimer = 0f;
    }

}