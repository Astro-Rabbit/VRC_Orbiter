
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MFDContinousTest : UdonSharpBehaviour
{
    public GameObject panel;
    public float moveSpeed = 0.5f;
    public void up()
    {
        // Moves the panel "Up" along its local Y axis
        panel.transform.localPosition += new Vector3(0, moveSpeed * Time.deltaTime, 0);
    }

    public void down()
    {
        // Moves the panel "Down" along its local Y axis
        panel.transform.localPosition += new Vector3(0, -moveSpeed * Time.deltaTime, 0);
    }

    public void left()
    {
        // Moves the panel "Left" along its local X axis
        panel.transform.localPosition += new Vector3(-moveSpeed * Time.deltaTime, 0, 0);
    }

    public void right()
    {
        // Moves the panel "Right" along its local X axis
        panel.transform.localPosition += new Vector3(moveSpeed * Time.deltaTime, 0, 0);
    }
}
