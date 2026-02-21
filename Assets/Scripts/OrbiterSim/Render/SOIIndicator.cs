using UdonSharp;
using UnityEngine;
using System;

public class SOIIndicator : UdonSharpBehaviour
{
    [Header("Proxy (child of body proxy)")]
    public Transform soiProxy;

    [Header("Body proxy (the thing you're attached to)")]
    public Transform bodyProxy;

    [Tooltip("SOI diameter = body diameter * ratio")]
    public float diameterRatio = 38.0f;

    public bool show = true;

    public void Apply()
    {
        if (soiProxy == null || bodyProxy == null) return;

        soiProxy.gameObject.SetActive(show);
        if (!show) return;

        float bodyDiameter = bodyProxy.localScale.x; // assuming uniform
        float soiDiameter = bodyDiameter * diameterRatio;

        soiProxy.localPosition = Vector3.zero;
        soiProxy.localRotation = Quaternion.identity;
        soiProxy.localScale = Vector3.one * soiDiameter;
    }
}
