using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletScrollbar : UdonSharpBehaviour
{
    [Header("Targeting")]
    public RectTransform viewport;
    public RectTransform content;

    [Header("Visual Components")]
    public RectTransform handle;
    public RectTransform track;

    [Header("Settings")]
    [Range(0, 1)] public float scrollValue = 0f;
    public bool autoHideIfContentFits = true;

    private RectTransform _rectTransform;

    void Start()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (track == null) track = _rectTransform;
        UpdateUI();
    }

    // This allows TabletScreen to find it
    public bool IsPointInside(Vector3 localPoint)
    {
        if (_rectTransform == null) return false;
        return _rectTransform.rect.Contains(localPoint);
    }

    public void OnDown(int id, Vector3 worldPoint) => ProcessInput(worldPoint);
    public void OnStay(int id, Vector3 worldPoint) => ProcessInput(worldPoint);

    //private void ProcessInput(Vector3 worldPoint)
    //{
    //    Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
    //    float rectBottom = track.rect.yMin;
    //    float rectHeight = track.rect.height;

    //    // Map Y position to 0.0 - 1.0 (Top to Bottom)
    //    scrollValue = 1.0f - Mathf.Clamp01((localPoint.y - rectBottom) / rectHeight);
    //    UpdateUI();
    //}

    //public void UpdateUI()
    //{
    //    if (content == null || viewport == null) return;

    //    float viewH = viewport.rect.height;
    //    float contentH = content.rect.height;
    //    float maxScroll = Mathf.Max(0, contentH - viewH);

    //    if (autoHideIfContentFits) gameObject.SetActive(contentH > viewH);

    //    float newY = scrollValue * maxScroll;
    //    content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);

    //    if (handle != null)
    //    {
    //        float handleY = Mathf.Lerp(track.rect.yMax, track.rect.yMin, scrollValue);
    //        handle.anchoredPosition = new Vector2(0, handleY);
    //        float sizeRatio = Mathf.Clamp01(viewH / contentH);
    //        handle.sizeDelta = new Vector2(handle.sizeDelta.x, track.rect.height * sizeRatio);
    //    }
    //}
    private void ProcessInput(Vector3 worldPoint)
    {
        // Convert world point to the Track's local space
        Vector3 localPoint = track.InverseTransformPoint(worldPoint);

        // Track.rect.yMin/Max are relative to the track's pivot.
        // This calculates a 0-1 value where 1 is the top of the track.
        float height = track.rect.height;
        float relativeY = localPoint.y - track.rect.yMin;
        float percent = Mathf.Clamp01(relativeY / height);

        // Invert: Pen at top (percent 1) should be scrollValue 0
        scrollValue = 1.0f - percent;

        UpdateUI();
    }

    public void UpdateUI()
    {
        if (content == null || viewport == null || track == null) return;

        float viewH = viewport.rect.height;
        float contentH = content.rect.height;
        float maxScroll = Mathf.Max(0, contentH - viewH);

        // 1. Position Content (Pivot must be 0.5, 1)
        float newY = scrollValue * maxScroll;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);

        // 2. Position & Scale Handle
        if (handle != null)
        {
            // Calculate handle size based on visibility ratio
            float ratio = Mathf.Clamp01(viewH / contentH);
            float trackH = track.rect.height;
            float handleH = trackH * ratio;

            // Minimum handle size so it doesn't disappear (e.g., 20 pixels)
            handle.sizeDelta = new Vector2(handle.sizeDelta.x, Mathf.Max(handleH, 20f));

            // Position the handle within the track bounds
            // We subtract half the handle height from the edges so it stays inside
            float halfHandle = handle.sizeDelta.y * 0.5f;
            float topLimit = track.rect.yMax - halfHandle;
            float bottomLimit = track.rect.yMin + halfHandle;

            float finalY = Mathf.Lerp(topLimit, bottomLimit, scrollValue);
            handle.anchoredPosition = new Vector2(0, finalY);
        }
    }

}