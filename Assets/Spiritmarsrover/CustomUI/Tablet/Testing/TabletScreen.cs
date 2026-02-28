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
}