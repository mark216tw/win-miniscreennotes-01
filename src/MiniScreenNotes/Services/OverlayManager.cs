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

    public OverlayManager(AnnotationSession session)
    {
        _session = session;
        _session.StateChanged += OnSessionStateChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public bool OverlaysVisible => _overlaysVisible;

    public event Action<string>? InfoMessageRequested;
    public event EventHandler? TopMostZOrderRequired;
    public Func<System.Windows.Rect?>? ToolbarBoundsProvider { get; set; }

    public void Initialize(bool initialVisible = true)
    {
        _overlaysVisible = initialVisible;
        RebuildOverlays();
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

    private void RebuildOverlays()
    {
        foreach (var win in _overlays)
        {
            win.Close();
        }
        _overlays.Clear();

        foreach (var monitor in DisplayMonitorHelper.GetAllMonitors())
        {
            var overlay = new OverlayWindow(_session, monitor, () => ToolbarBoundsProvider?.Invoke());
            overlay.MagnifierWheelDelta += OnMagnifierWheelDelta;
            overlay.MagnifierSizeWheelDelta += OnMagnifierSizeWheelDelta;
            overlay.StepCounterResetRequested += () => InfoMessageRequested?.Invoke(Strings.Toast_StepReset);
            overlay.OverlayActivated += (_, _) => EnsureTopMostZOrder();
            if (_overlaysVisible)
            {
                overlay.Show();
                overlay.RefreshMonitorBounds();
            }
            _overlays.Add(overlay);
        }

        UpdateOverlayStates();
    }

    private void OnSessionStateChanged(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.Invoke(UpdateOverlayStates);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        DisplayMonitorHelper.InvalidateCache();
        WpfApplication.Current?.Dispatcher.Invoke(RebuildOverlays);
    }

    private void UpdateOverlayStates()
    {
        if (!_overlaysVisible)
        {
            _magnifier?.Stop();
            foreach (var overlay in _overlays)
            {
                if (overlay.IsVisible)
                {
                    overlay.Hide();
                }
            }

            _lastAppliedTopMostTool = null;
            return;
        }

        var tool = _session.CurrentTool;
        var anyOverlayShown = false;
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
            _magnifier ??= new MagnifierWindow();
            _magnifier.Start();
        }
        else
        {
            _magnifier?.Stop();
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

        _magnifier?.Close();
        _magnifier = null;

        foreach (var overlay in _overlays)
        {
            overlay.Close();
        }
        _overlays.Clear();
    }
}
