using UdonSharp;
using UnityEngine;

/// <summary>
/// OrreryCraftDirectionMarkers
///
/// Shows local orbital direction markers around the craft when the orrery
/// is in craft focus. Intended to replace the orbit ribbon in that mode.
///
/// Markers are presentation-only:
/// - directions come from nav RTN basis in solver inertial frame (E)
/// - positions are placed near the inner edge of the orrery display sphere
/// - no marker rotation is applied (option B)
/// </summary>
public class OrreryCraftDirectionMarkers : UdonSharpBehaviour
{
    [Header("Display Enable")]
    public bool displayEnabled = true;

    [Header("References")]
    public OrreryController orrery;
    public GuidanceNavCoreState nav;

    [Header("Marker Transforms")]
    public Transform progradeTf;
    public Transform retrogradeTf;
    public Transform radialOutTf;
    public Transform radialInTf;
    public Transform normalTf;
    public Transform antiNormalTf;

    [Header("Display")]
    [Tooltip("Inset from the usable hologram edge, in orrery local units.")]
    public float edgeInsetLocal = 0.025f;

    [Tooltip("Uniform marker scale in local units.")]
    public float markerScaleLocal = 0.01f;

    [Tooltip("Hide all markers unless the orrery is in craft focus.")]
    public bool hideOutsideCraftFocus = true;

    [Header("Ticking")]
    [Tooltip("If true, markers update from their own LateUpdate. If false, another system must call TickMarkers().")]
    public bool useInternalLateUpdate = false;


    private Renderer[] _markerRenderers;
    private bool _markerRenderersCached = false;

    void LateUpdate()
    {
        if (!useInternalLateUpdate) return;
        TickMarkers();
    }

    public void TickMarkers()
    {
        if (orrery == null || nav == null)
        {
            SetAllMarkersActive(false);
            return;
        }

        if (!nav.valid)
        {
            SetAllMarkersActive(false);
            return;
        }
        if (!displayEnabled)
        {
            SetAllMarkersActive(false);
            return;
        }
        bool show = true;

        if (hideOutsideCraftFocus && orrery.focusMode != OrreryController.FOCUS_CRAFT)
            show = false;

        if (!show)
        {
            SetAllMarkersActive(false);
            return;
        }

        Vector3 craftLocal = orrery.MapWorldPointEToOrreryLocal(
            nav.rC_x,
            nav.rC_y,
            nav.rC_z
        );

        float edgeRadiusLocal = ComputeEdgeRadiusLocal();
        if (edgeRadiusLocal <= 0.0f)
        {
            SetAllMarkersActive(false);
            return;
        }

        Vector3 progradeLocal   = SafeMapDir(nav.That_E);
        Vector3 retrogradeLocal = SafeMapDir(-nav.That_E);
        Vector3 radialOutLocal  = SafeMapDir(nav.Rhat_E);
        Vector3 radialInLocal   = SafeMapDir(-nav.Rhat_E);
        Vector3 normalLocal     = SafeMapDir(nav.Nhat_E);
        Vector3 antiNormalLocal = SafeMapDir(-nav.Nhat_E);

        ApplyMarker(progradeTf,   true, craftLocal, progradeLocal, edgeRadiusLocal);
        ApplyMarker(retrogradeTf, true, craftLocal, retrogradeLocal, edgeRadiusLocal);
        ApplyMarker(radialOutTf,  true, craftLocal, radialOutLocal, edgeRadiusLocal);
        ApplyMarker(radialInTf,   true, craftLocal, radialInLocal, edgeRadiusLocal);
        ApplyMarker(normalTf,     true, craftLocal, normalLocal, edgeRadiusLocal);
        ApplyMarker(antiNormalTf, true, craftLocal, antiNormalLocal, edgeRadiusLocal);
    }

    private float ComputeEdgeRadiusLocal()
    {
        float usableRadius = orrery.hologramRadiusUnity * orrery.autoScaleFill;
        float r = usableRadius - edgeInsetLocal;
        if (r < 0.001f) r = 0.001f;
        return r;
    }

    private Vector3 SafeMapDir(Vector3 dirE)
    {
        if (dirE.sqrMagnitude < 1e-12f)
            return Vector3.forward;

        Vector3 local = orrery.MapWorldDirectionEToOrreryLocal(dirE.x, dirE.y, dirE.z);
        if (local.sqrMagnitude < 1e-12f)
            return Vector3.forward;

        return local.normalized;
    }

    private void ApplyMarker(Transform tf, bool active, Vector3 craftLocal, Vector3 dirLocal, float edgeRadiusLocal)
    {
        if (tf == null) return;

        tf.gameObject.SetActive(active);

        if (!active) return;

        Vector3 markerLocal = craftLocal + dirLocal * edgeRadiusLocal;
        tf.localPosition = markerLocal;

        Vector3 inwardLocal = craftLocal - markerLocal;
        if (inwardLocal.sqrMagnitude < 1e-12f)
            inwardLocal = -dirLocal;
        inwardLocal.Normalize();

        Vector3 upLocal = Vector3.up;

        // Avoid degenerate LookRotation if inward is nearly parallel to up
        float d = Mathf.Abs(Vector3.Dot(inwardLocal, upLocal));
        if (d > 0.98f)
            upLocal = Vector3.right;

        tf.localRotation = Quaternion.LookRotation(inwardLocal, upLocal);
        tf.localScale = Vector3.one * markerScaleLocal;
    }

    private void SetAllMarkersActive(bool active)
    {
        SetMarkerActive(progradeTf, active);
        SetMarkerActive(retrogradeTf, active);
        SetMarkerActive(radialOutTf, active);
        SetMarkerActive(radialInTf, active);
        SetMarkerActive(normalTf, active);
        SetMarkerActive(antiNormalTf, active);
    }

    private void SetMarkerActive(Transform tf, bool active)
    {
        if (tf == null) return;
        tf.gameObject.SetActive(active);
    }

    private void CacheMarkerRenderers()
    {
        if (_markerRenderersCached) return;

        int count = 0;

        count += CountRenderers(progradeTf);
        count += CountRenderers(retrogradeTf);
        count += CountRenderers(radialOutTf);
        count += CountRenderers(radialInTf);
        count += CountRenderers(normalTf);
        count += CountRenderers(antiNormalTf);

        _markerRenderers = new Renderer[count];

        int k = 0;
        k = CollectRenderers(progradeTf, _markerRenderers, k);
        k = CollectRenderers(retrogradeTf, _markerRenderers, k);
        k = CollectRenderers(radialOutTf, _markerRenderers, k);
        k = CollectRenderers(radialInTf, _markerRenderers, k);
        k = CollectRenderers(normalTf, _markerRenderers, k);
        k = CollectRenderers(antiNormalTf, _markerRenderers, k);

        _markerRenderersCached = true;
    }

    private int CountRenderers(Transform root)
    {
        if (root == null) return 0;
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        if (rs == null) return 0;
        return rs.Length;
    }

    private int CollectRenderers(Transform root, Renderer[] dst, int start)
    {
        if (root == null) return start;

        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        if (rs == null) return start;

        int n = rs.Length;
        for (int i = 0; i < n; i++)
        {
            dst[start++] = rs[i];
        }

        return start;
    }
    public void ApplyClipVolumeParams(Vector3 centerWorld, float radiusWorld)
    {
        CacheMarkerRenderers();

        if (_markerRenderers == null) return;

        int n = _markerRenderers.Length;
        for (int i = 0; i < n; i++)
        {
            Renderer r = _markerRenderers[i];
            if (r == null) continue;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetVector("_ClipCenterWorld", new Vector4(centerWorld.x, centerWorld.y, centerWorld.z, 0f));
            mpb.SetFloat("_ClipRadiusWorld", radiusWorld);
            r.SetPropertyBlock(mpb);
        }
    }

}