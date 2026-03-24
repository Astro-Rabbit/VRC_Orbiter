
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletInputEnable : UdonSharpBehaviour
{
    void Start()
    {
        
    }
    private void OnEnable()
    {
        transform.localScale = Vector3.one;
    }
    private void OnDisable()
    {
        transform.localScale = Vector3.zero;
    }
}
