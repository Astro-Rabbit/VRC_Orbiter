using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class TabletScrollbarEditor : EditorWindow
{
    [MenuItem("GameObject/UI/Custom Tablet Scroll Window", false, 11)]
    public static void CreateScrollWindow(MenuCommand menuCommand)
    {
        // 1. Create the Main Root Container
        GameObject root = CreateUIElement("TabletScrollWindow", menuCommand.context as GameObject, typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(300, 400);

        // 2. Create the Viewport (The Masking Area)
        GameObject viewport = CreateUIElement("Viewport", root, typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform viewRect = viewport.GetComponent<RectTransform>();
        viewport.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Dim background
        viewport.GetComponent<Mask>().showMaskGraphic = true;
        // Anchor to fill most of the space, leaving room for scrollbar on the right
        SetAnchors(viewRect, Vector2.zero, new Vector2(0.9f, 1f));

        // 3. Create the Content (The Actual List)
        GameObject content = CreateUIElement("Content", viewport, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.pivot = new Vector2(0.5f, 1f); // CRITICAL: Pivot at top
        SetAnchors(contentRect, new Vector2(0, 1), new Vector2(1, 1)); // Anchor to top-width
        contentRect.sizeDelta = new Vector2(0, 500); // Initial height

        // Configure Layout
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 5;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 4. Create the Scrollbar Root
        GameObject scrollbar = CreateUIElement("Scrollbar", root, typeof(RectTransform));//, typeof(BoxCollider));
        RectTransform scrollRect = scrollbar.GetComponent<RectTransform>();
        SetAnchors(scrollRect, new Vector2(0.92f, 0f), new Vector2(1f, 1f)); // Position on right side

        // Add the Script
        var scrollScript = scrollbar.AddComponent<TabletScrollbar>();

        // Add Physics Collider for TabletPen
        //BoxCollider col = scrollbar.GetComponent<BoxCollider>();
        //col.isTrigger = true;
        //col.size = new Vector3(30, 400, 1); // Matches scrollbar height roughly

        // 5. Create Track (Background)
        GameObject track = CreateUIElement("Track", scrollbar, typeof(Image));
        SetAnchors(track.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        track.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // 6. Create Handle (The Thumb)
        GameObject handle = CreateUIElement("Handle", scrollbar, typeof(Image));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(0, 50); // Height will be auto-calculated by script
        SetAnchors(handleRect, new Vector2(0, 1), new Vector2(1, 1)); // Start at top
        handle.GetComponent<Image>().color = new Color(0.7f, 0.7f, 0.7f, 1f);

        // 7. Connect References
        scrollScript.viewport = viewRect;
        scrollScript.content = contentRect;
        scrollScript.track = track.GetComponent<RectTransform>();
        scrollScript.handle = handleRect;

        // 8. Add a Sample Item so we can see it working immediately
        GameObject sample = CreateUIElement("SampleItem", content, typeof(Image));
        sample.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 100);
        sample.GetComponent<Image>().color = Color.gray;

        // Finalize
        Undo.RegisterCreatedObjectUndo(root, "Create Tablet Scroll Window");
        Selection.activeObject = root;
    }

    private static GameObject CreateUIElement(string name, GameObject parent, params System.Type[] components)
    {
        GameObject go = new GameObject(name, components);
        if (parent != null)
        {
            go.transform.SetParent(parent.transform, false);
            GameObjectUtility.SetParentAndAlign(go, parent);
        }
        return go;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}