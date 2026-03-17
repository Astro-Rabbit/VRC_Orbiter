using UdonSharp;
using UnityEngine;
using TMPro;
using System;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletClock : UdonSharpBehaviour
{
    public TMP_Text clockDisplay;

    void Update()
    {
        if (clockDisplay == null) return;

        DateTime now = DateTime.Now;
        // Format: HH:MM (24-hour)//
        clockDisplay.text = string.Format("{0:D2}:{1:D2}", now.Hour, now.Minute);
    }
}