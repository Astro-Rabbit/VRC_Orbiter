using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class TabletSliderEditor : EditorWindow
{
    [MenuItem("GameObject/UI/Custom Tablet Slider", false, 10)]
    public static void CreateSlider(MenuCommand menuCommand)
    {
        // 1. Create the Root
        GameObject root = new GameObject("TabletSlider", typeof(RectTransform), typeof(BoxCollider));
        GameObjectUtility.SetParentAndAlign(root, menuCommand.context as GameObject);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(200, 40);

        BoxCollider col = root.GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(200, 40, 1);

        // 2. Add the TabletSlider Script
        // Note: Assumes the TabletSlider script is compiled and present
        var sliderScript = root.AddComponent<TabletSlider>();

        // 3. Create Background (The Track)
        GameObject bg = CreateUIElement("Background", root, typeof(Image));
        SetAnchors(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // 4. Create Fill Area and Fill
        GameObject fillArea = CreateUIElement("Fill Area", root, typeof(RectTransform));
        SetAnchors(fillArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        GameObject fill = CreateUIElement("Fill", fillArea, typeof(Image));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.pivot = new Vector2(0, 0.5f);
        SetAnchors(fillRect, Vector2.zero, new Vector2(0, 1)); // Anchor Max X starts at 0
        fill.GetComponent<Image>().color = Color.cyan;

        // 5. Create Handle
        GameObject handle = CreateUIElement("Handle", root, typeof(Image));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(10, 50); // Slightly taller than slider
        SetAnchors(handleRect, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        handle.GetComponent<Image>().color = Color.white;

        // 6. Create Value Text
        GameObject textObj = CreateUIElement("ValueText", root, typeof(TextMeshProUGUI));
        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "0.00";
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        SetAnchors(textObj.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        // 7. Link references to the script
        sliderScript.fill = fillRect;
        sliderScript.handle = handleRect;
        sliderScript.valueText = tmp;

        // Finalize
        Undo.RegisterCreatedObjectUndo(root, "Create Tablet Slider");
        Selection.activeObject = root;
    }

    private static GameObject CreateUIElement(string name, GameObject parent, params System.Type[] components)
    {
        GameObject go = new GameObject(name, components);
        go.transform.SetParent(parent.transform, false);
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