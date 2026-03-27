using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class OrreryInnerShell : UdonSharpBehaviour
{
    public OrreryDualPickup mainScript;

    private void OnTriggerEnter(Collider other)
    {
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null) mainScript.SetPenInInner(pen, true);
    }

    private void OnTriggerExit(Collider other)
    {
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen != null) mainScript.SetPenInInner(pen, false);
    }
}