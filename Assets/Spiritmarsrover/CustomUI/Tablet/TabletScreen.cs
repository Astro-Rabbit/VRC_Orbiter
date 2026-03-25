using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletScreen : UdonSharpBehaviour
{
    public TabletNavigationManager navManager;
    public TabletButton homebutton;
    //public void HandleTouchDown(Vector3 worldPoint)
    //{
    //    if (navManager.CurrentPage == null) return;

    //    // Get all buttons on the CURRENT active page only
    //    TabletButton[] buttons = navManager.CurrentPage.GetComponentsInChildren<TabletButton>(false);

    //    foreach (TabletButton btn in buttons)
    //    {
    //        // Convert the world hit point to the local space of the specific button
    //        Vector3 localPoint = btn.transform.InverseTransformPoint(worldPoint);

    //        // Check if the hit point is inside the RectTransform boundaries
    //        if (btn.IsPointInside(localPoint))
    //        {
    //            btn.Press();
    //            break; // Stop after finding the first hit button
    //        }
    //    }
    //    Vector3 localPoint2 = homebutton.transform.InverseTransformPoint(worldPoint);
    //    if (homebutton.IsPointInside(localPoint2))
    //    {
    //        homebutton.Press();
    //    }
    //}

    public TabletButton GetButtonAtPoint(Vector3 worldPoint)
    {
        if (navManager.CurrentPage == null) return null;
        TabletButton[] buttons = navManager.CurrentPage.GetComponentsInChildren<TabletButton>(false);

        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            TabletButton btn = buttons[i];

            // Convert world hit point to local space of this specific button
            Vector3 localPoint = btn.transform.InverseTransformPoint(worldPoint);

            if (btn.IsPointInside(localPoint))
            {
                return btn; // Return the first one hit (which is the top-most)
            }
        }
        if (homebutton.IsPointInside(homebutton.transform.InverseTransformPoint(worldPoint)))
        {
            return homebutton;
        }
        return null;
    }

    public TabletSlider GetSliderAtPoint(Vector3 worldPoint)
    {
        if (navManager.CurrentPage == null) return null;

        // Get all sliders on the CURRENT active page
        TabletSlider[] sliders = navManager.CurrentPage.GetComponentsInChildren<TabletSlider>(false);

        for (int i = sliders.Length - 1; i >= 0; i--)
        {
            TabletSlider slider = sliders[i];

            // Convert world hit point to local space of the slider
            Vector3 localPoint = slider.transform.InverseTransformPoint(worldPoint);

            if (slider.IsPointInside(localPoint))
            {
                return slider;
            }
        }
        return null;
    }
    public TabletScrollbar GetScrollbarAtPoint(Vector3 worldPoint)
    {
        if (navManager.CurrentPage == null) return null;

        TabletScrollbar[] scrollbars = navManager.CurrentPage.GetComponentsInChildren<TabletScrollbar>(false);

        for (int i = scrollbars.Length - 1; i >= 0; i--)
        {
            TabletScrollbar sb = scrollbars[i];
            Vector3 localPoint = sb.transform.InverseTransformPoint(worldPoint);

            if (sb.IsPointInside(localPoint))
            {
                //Debug.Log("[TabletScreen] Scrollbar hit");
                return sb;
            }
        }
        
        return null;
    }
}