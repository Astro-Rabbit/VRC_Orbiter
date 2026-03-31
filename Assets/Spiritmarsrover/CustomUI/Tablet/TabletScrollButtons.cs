
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

    //void Start()
    //{
    //    if (buttons != null)
    //    {
    //        checkedem = new bool[buttons.Length];
    //        checkmark = new Image[buttons.Length];
    //        for(int i = 0; i < buttons.Length; i++)
    //        {
    //            checkmark[i] = buttons[i].transform.GetChild(2).GetComponent<Image>();
    //            checkmark[i].enabled = false;
    //        }
    //    }
    //    CheckButton();
    //}
    //public void CheckButton()
    //{
    //    int totaltrue = 0;
    //    for (int i = 0; i < buttons.Length; i++)
    //    {


    //        if (buttons[i].released)
    //        {
    //            checkmark[i].enabled = !checkmark[i].enabled;
    //            checkedem[i] = true;
    //        }
    //        if (checkedem[i])
    //        {
    //            totaltrue++;
    //        }
    //    }
    //    int totalbools = buttons.Length;

    //    checkedNumText.text = totaltrue + "/" + totalbools;
    //}

    void Start()
    {
        if (buttons != null)
        {
            checkedem = new bool[buttons.Length];
            checkmark = new Image[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                // Assuming child 2 is the checkmark image
                checkmark[i] = buttons[i].transform.GetChild(2).GetComponent<Image>();
                checkmark[i].enabled = false;
                checkedem[i] = false;
            }
        }
        UpdateTotalText();
    }
    public void CheckButton()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].released)
            {
                // 1. Toggle the visual image
                checkmark[i].enabled = !checkmark[i].enabled;

                // 2. Sync the boolean to the image's new state
                checkedem[i] = checkmark[i].enabled;

                // 3. IMPORTANT: Reset the button's released state 
                // so it doesn't trigger again until the next physical press
                buttons[i].released = false;
            }
        }

        UpdateTotalText();
    }

    // Moved text update to its own function for cleanliness
    private void UpdateTotalText()
    {
        int totaltrue = 0;
        for (int i = 0; i < checkedem.Length; i++)
        {
            if (checkedem[i])
            {
                totaltrue++;
            }
        }

        int totalbools = buttons.Length;
        if (checkedNumText != null)
        {
            checkedNumText.text = totaltrue + "/" + totalbools;
        }
    }
}
