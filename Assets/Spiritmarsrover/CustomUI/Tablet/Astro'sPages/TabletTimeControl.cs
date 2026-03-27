using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletTimeControl : UdonSharpBehaviour
{
    [Header("Core")]
    public SimClock clock;
    public SimManager simManager;

    [Header("UI Text")]
    public TMP_Text ownerText;
    public TMP_Text statusText;
    public TMP_Text warpText;

    [Header("Buttons")]
    public TabletButton warpDownButton;
    public TabletButton warp1xButton;
    public TabletButton warpUpButton;

    [Header("Refresh")]
    public float refreshInterval = 0.1f;

    // Fixed warp ladder
    // Current behavior: no 0x, just real-time and up
    private readonly double[] _warpLadder = new double[]
    {
        1.0, 2.0, 5.0, 10.0, 50.0, 100.0, 500.0, 1000.0, 5000.0, 10000.0
    };

    private float _refreshTimer = 0f;
    private bool _lastCanControl = false;

    void Start()
    {
        RefreshUI(true);
    }

    void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= refreshInterval)
        {
            _refreshTimer = 0f;
            RefreshUI(false);
        }
    }

    public void WarpUp()
    {
        if (!CanControl()) return;
        if (simManager == null) return;
        if (clock == null) return;

        double current = clock.timeScale;
        double next = GetNextHigherWarp(current);
        simManager.SetRequestedWarp(next);

        RefreshUI(true);
    }

    public void WarpDown()
    {
        if (!CanControl()) return;
        if (simManager == null) return;
        if (clock == null) return;

        double current = clock.timeScale;
        double next = GetNextLowerWarp(current);
        simManager.SetRequestedWarp(next);

        RefreshUI(true);
    }

    public void WarpTo1x()
    {
        if (!CanControl()) return;
        if (simManager == null) return;

        simManager.SetRequestedWarp(1.0);
        RefreshUI(true);
    }



    private void RefreshUI(bool forceButtonRefresh)
    {
        UpdateOwnerText();
        UpdateStatusText();
        UpdateWarpText();

        bool canControl = CanControl();
        if (forceButtonRefresh || canControl != _lastCanControl)
        {
            ApplyButtonState(warpDownButton, canControl);
            ApplyButtonState(warp1xButton, canControl);
            ApplyButtonState(warpUpButton, canControl);
            _lastCanControl = canControl;
        }
    }

    private bool CanControl()
    {
        if (simManager == null) return false;
        if (clock == null) return false;

        // Option C: read-only for non-owners
        return simManager.IsSimOwner() && Networking.IsOwner(clock.gameObject);
    }

    private void UpdateOwnerText()
    {
        if (ownerText == null) return;

        string name = "---";

        if (simManager != null)
        {
            VRCPlayerApi owner = Networking.GetOwner(simManager.gameObject);
            if (owner != null)
                name = owner.displayName;
        }

        ownerText.text = "OWNER: " + name;
    }

    private void UpdateStatusText()
    {
        if (statusText == null) return;

        statusText.text = CanControl() ? "STATUS: ACTIVE" : "STATUS: READ ONLY";
    }

    private void UpdateWarpText()
    {
        if (warpText == null) return;

        double actualWarp = 1.0;
        if (clock != null)
            actualWarp = clock.timeScale;

        double allowedWarp = actualWarp;
        if (simManager != null && simManager.warpPolicy != null)
            allowedWarp = simManager.warpPolicy.currentAllowedTimeScale;

        warpText.text = "WARP: " + FormatWarp(actualWarp) + " / ALLOW: " + FormatWarp(allowedWarp);
    }

    private void ApplyButtonState(TabletButton btn, bool enabledState)
    {
        if (btn == null) return;

        btn.mode = enabledState ? TabletButtonMode.Trigger : TabletButtonMode.None;

        if (btn.targetGraphic != null)
        {
            btn.targetGraphic.color = enabledState ? btn.normalColor : btn.disabledColor;
        }
    }

    private double GetNextHigherWarp(double current)
    {
        int n = _warpLadder.Length;
        for (int i = 0; i < n; i++)
        {
            if (_warpLadder[i] > current + 1e-9)
                return _warpLadder[i];
        }

        return _warpLadder[n - 1];
    }

    private double GetNextLowerWarp(double current)
    {
        for (int i = _warpLadder.Length - 1; i >= 0; i--)
        {
            if (_warpLadder[i] < current - 1e-9)
                return _warpLadder[i];
        }

        return _warpLadder[0];
    }

    private string FormatWarp(double warp)
    {
        double rounded = System.Math.Round(warp);

        if (System.Math.Abs(warp - rounded) < 1e-9)
            return ((int)rounded).ToString() + "x";

        return warp.ToString("F2") + "x";
    }
}