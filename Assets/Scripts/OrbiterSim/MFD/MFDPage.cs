using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public abstract class MFDPage : UdonSharpBehaviour
{
    protected int activeDisplayCount = 0;
    protected MFD[] activeDisplays = { null };

    public virtual void AddDisplay(MFD display)
    {
        if (activeDisplayCount >= activeDisplays.Length) {
            MFD[] newArray = new MFD[activeDisplays.Length * 2];
            for (int i = 0; i < activeDisplayCount; i++) {
                newArray[i] = activeDisplays[i];
            }
            activeDisplays = newArray;
        }

        activeDisplays[activeDisplayCount] = display;
        activeDisplayCount++;
    }

    public virtual void RemoveDisplay(MFD display)
    {
        int index;
        for (index = 0; index < activeDisplayCount; index++) {
            if (activeDisplays[index] == display) {
                activeDisplays[index] = null;
                break;
            }
        }
        for (; index + 1 < activeDisplayCount; index++) {
            activeDisplays[index] = activeDisplays[index + 1];
        }

        activeDisplayCount--;
    }

    public abstract void OnButton(MFD display, ButtonSide side, int num);

    public abstract void DrawDisplay(MFD display);
}
