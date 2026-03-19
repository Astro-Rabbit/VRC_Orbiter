
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using TMPro;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletScrollButtons : UdonSharpBehaviour
{
    public TabletButton[] buttons;
    private Image[] checkmark;
    private bool[] checkedem;
    public TMP_Text checkedNumText;

    void Start()
    {
        if (buttons != null)
        {
            checkedem = new bool[buttons.Length];
            checkmark = new Image[buttons.Length];
            for(int i = 0; i < buttons.Length; i++)
            {
                checkmark[i] = buttons[i].transform.GetChild(2).GetComponent<Image>();
                checkmark[i].enabled = false;
            }
        }
        CheckButton();
    }
    public void CheckButton()
    {
        int totaltrue = 0;
        for (int i = 0; i < buttons.Length; i++)
        {
            
            
            if (buttons[i].released)
            {
                checkmark[i].enabled = !checkmark[i].enabled;
                checkedem[i] = true;
            }
            if (checkedem[i])
            {
                totaltrue++;
            }
        }
        int totalbools = buttons.Length;

        checkedNumText.text = totaltrue + "/" + totalbools;
    }
    
}
