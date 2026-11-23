using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Resources.Strings;
using Com.MiniScreenNotes.Views;
using Microsoft.Win32;
using WpfApplication = System.Windows.Application;

namespace Com.MiniScreenNotes.Services;

public sealed class OverlayManager : IDisposable
{
    private readonly AnnotationSession _session;
    private readonly List<OverlayWindow> _overlays = [];
    private MagnifierWindow? _magnifier;
    private bool _overlaysVisible = true;
    private bool _disposed;
    private ToolType? _lastAppliedTopMostTool;
    private double _spotlightRadius = 140.0;
    private double _magnifierZoom = 2.5;
    private double _magnifierDiameter = 300.0;

    public OverlayManager(AnnotationSession session)
    {
        _session = session;
        _session.StateChanged += OnSessionStateChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public bool OverlaysVisible => _overlaysVisible;

    public bool IsInlineTextEditing => _overlays.Any(overlay => overlay.IsEditingText);

    public event Action<string>? InfoMessageRequested;
    public event EventHandler? TopMostZOrderRequired;
    public Func<System.Windows.Rect?>? ToolbarBoundsProvider { get; set; }

    public static bool ShouldKeepOverlaysAlive(
        bool overlaysVisible,
        bool isCursorMode,
        CanvasBackgroundMode backgroundMode,
        bool annotationsVisible,
        int elementCount) =>
        overlaysVisible
        && (!isCursorMode
            || backgroundMode != CanvasBackgroundMode.Transparent
            || (annotationsVisible && elementCount > 0));

    public void Initialize(bool initialVisible = true)
    {
        _overlaysVisible = initialVisible;
        UpdateOverlayStates();
    }

    public void SetOverlaysVisible(bool visible)
    {
        if (_disposed || _overlaysVisible == visible)
        {
            return;
        }

        _overlaysVisible = visible;
        UpdateOverlayStates();
    }

    private bool NeedsOverlays() =>
        ShouldKeepOverlaysAlive(
            _overlaysVisible,
            _session.IsCursorMode,
            _session.BackgroundMode,
            _session.AnnotationsVisible,
            _session.Elements.Count);

    private void CreateOverlays()
    {
        foreach (var monitor in DisplayMonitorHelper.GetAllMonitors())
        {
            var overlay = new OverlayWindow(_session, monitor, () => ToolbarBoundsProvider?.Invoke())
            {
                SpotlightRadius = _spotlightRadius
            };
            overlay.MagnifierWheelDelta += OnMagnifierWheelDelta;
            overlay.MagnifierSizeWheelDelta += OnMagnifierSizeWheelDelta;
            overlay.SpotlightRadiusChanged += OnSpotlightRadiusChanged;
            overlay.StepCounterResetRequested += () => InfoMessageRequested?.Invoke(Strings.Toast_StepReset);
            overlay.OverlayActivated += (_, _) => EnsureTopMostZOrder();
            overlay.Show();
            overlay.RefreshMonitorBounds();
            _overlays.Add(overlay);
        }
    }

    private void ReleaseOverlays()
    {
        ReleaseMagnifier();

        if (_overlays.Count == 0)
        {
            return;
        }

        foreach (var win in _overlays)
        {
            win.Close();
        }
        _overlays.Clear();
    }

    private void ReleaseMagnifier()
    {
        if (_magnifier is null)
        {
            return;
        }

        _magnifierZoom = _magnifier.ZoomFactor;
        _magnifierDiameter = _magnifier.LensDipDiameter;
        _magnifier.Close();
        _magnifier = null;
    }

    private void RebuildOverlaysIfNeeded()
    {
        ReleaseOverlays();
        _lastAppliedTopMostTool = null;
        UpdateOverlayStates();
    }

    private void OnSessionStateChanged(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.Invoke(UpdateOverlayStates);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        DisplayMonitorHelper.InvalidateCache();
        WpfApplication.Current?.Dispatcher.Invoke(RebuildOverlaysIfNeeded);
    }

    private void OnSpotlightRadiusChanged(double radius)
    {
        _spotlightRadius = radius;
        foreach (var overlay in _overlays)
        {
            overlay.SpotlightRadius = radius;
        }
    }

    private void UpdateOverlayStates()
    {
        if (_disposed)
        {
            return;
        }

        if (!NeedsOverlays())
        {
            ReleaseOverlays();
            _lastAppliedTopMostTool = null;
            return;
        }

        var tool = _session.CurrentTool;
        var anyOverlayShown = false;

        if (_overlays.Count == 0)
        {
            CreateOverlays();
            anyOverlayShown = _overlays.Count > 0;
        }

        foreach (var overlay in _overlays)
        {
            if (!overlay.IsVisible)
            {
                overlay.Show();
                overlay.RefreshMonitorBounds();
                anyOverlayShown = true;
            }

            overlay.ApplyInteractiveState();
        }

        if (tool == ToolType.Magnifier)
        {
            _magnifier ??= new MagnifierWindow(_magnifierZoom, _magnifierDiameter);
            _magnifier.Start();
        }
        else
        {
            ReleaseMagnifier();
        }

        // 僅在工具切換或覆蓋層重新顯示時重設 TopMost，避免每次狀態變更都對所有螢幕 SetWindowPos
        if (anyOverlayShown || _lastAppliedTopMostTool != tool)
        {
            EnsureTopMostZOrder();
        }

        _lastAppliedTopMostTool = tool;
    }

    private void EnsureTopMostZOrder()
    {
        if (_magnifier is { IsVisible: true })
        {
            _magnifier.BringToTopMost();
        }

        TopMostZOrderRequired?.Invoke(this, EventArgs.Empty);
    }

    private void OnMagnifierWheelDelta(int delta)
    {
        _magnifier?.AdjustZoomByWheel(delta);
    }

    private void OnMagnifierSizeWheelDelta(int delta)
    {
        _magnifier?.AdjustDiameterByWheel(delta);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.StateChanged -= OnSessionStateChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        ReleaseOverlays();
    }
}
