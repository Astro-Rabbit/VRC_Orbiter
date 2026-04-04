using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ShaderPropertyController : UdonSharpBehaviour
{
    [Header("Settings")]
    public Renderer targetRenderer;
    public string propertyName = "_MotionRate";

    [Header("Slider Reference")]
    public Slider slider;

    private Material _mat;

    void Start()
    {
        if (targetRenderer != null)
            _mat = targetRenderer.material; // Creates a local instance of the material
    }

    // Call this from your TabletSlider's event system
    public void OnSliderChanged()
    {
        if (_mat == null || slider == null) return;

        // Applies the slider's currentValue directly to the shader property
        _mat.SetFloat(propertyName, slider.value);
    }
}