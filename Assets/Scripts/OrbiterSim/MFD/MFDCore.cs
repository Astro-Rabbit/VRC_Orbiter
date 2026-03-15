
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum MFDPageID : byte
{
Menu,
Orbit,
Target,
Align,
Transfer,
}

public class MFDCore : UdonSharpBehaviour
{
    public MFDPage[] pageList;
}
