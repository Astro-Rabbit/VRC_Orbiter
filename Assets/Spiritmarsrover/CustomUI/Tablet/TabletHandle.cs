using UdonSharp;
using UnityEngine;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletHandle : UdonSharpBehaviour
{
    [HideInInspector] public TabletPen hoveringPen;

    public void OnTriggerEnter(Collider other)
    {
        TabletPen pen = other.GetComponent<TabletPen>();
        if (pen) hoveringPen = pen;
    }

    public void OnTriggerExit(Collider other)
    {
        if (hoveringPen != null && other.gameObject == hoveringPen.gameObject)
            hoveringPen = null;
    }
}