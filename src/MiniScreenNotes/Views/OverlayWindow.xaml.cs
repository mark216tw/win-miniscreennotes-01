using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Models.Annotations;
using Com.MiniScreenNotes.Services;
using Com.MiniScreenNotes.Views.Controls;
using WpfCursors = System.Windows.Input.Cursors;
using WpfKey = System.Windows.Input.Key;
using WpfKeyboard = System.Windows.Input.Keyboard;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Com.MiniScreenNotes.Views;

public partial class OverlayWindow : Window
{
    private const double PanelCursorProximityDip = 48.0;

    // 保留 1 實體像素不覆蓋，避免 Windows 將整個螢幕的最上層視窗誤判為全螢幕 App
    private const int FullscreenInsetPhysicalPixels = 1;

    private readonly AnnotationSession _session;
    private readonly Func<Rect?>? _toolbarBoundsProvider;
    private readonly AnnotationCanvas _canvas;
    private readonly LaserOverlayLayer _laserLayer;
    private readonly SpotlightOverlayLayer _spotlightLayer;
    private Point _pendingTextPos;
    private IntPtr _hwnd;
    private bool? _lastClickThrough;

    public OverlayWindow(
        AnnotationSession session,
        MonitorInfo monitor,
        Func<Rect?>? toolbarBoundsProvider = null)
    {
        InitializeComponent();

        _session = session;
        _toolbarBoundsProvider = toolbarBoundsProvider;
        Monitor = monitor;

        Left = monitor.DipBounds.Left;
        Top = monitor.DipBounds.Top;
        Width = GetInsetPhysicalLength(monitor.PhysicalBounds.Width, monitor.DpiScaleX);
        Height = GetInsetPhysicalLength(monitor.PhysicalBounds.Height, monitor.DpiScaleY);

        _canvas = new AnnotationCanvas(session, monitor.DeviceName);
        _canvas.TextInputRequested += BeginInlineTextInput;
        _canvas.StepCounterReset += () => StepCounterResetRequested?.Invoke();

        _laserLayer = new LaserOverlayLayer();
        _spotlightLayer = new SpotlightOverlayLayer();
        _spotlightLayer.RadiusChanged += radius => SpotlightRadiusChanged?.Invoke(radius);

        Panel.SetZIndex(_canvas, 1);
        Panel.SetZIndex(_spotlightLayer, 10);
        Panel.SetZIndex(_laserLayer, 15);

        RootGrid.Children.Insert(0, _canvas);
        RootGrid.Children.Insert(1, _spotlightLayer);
        RootGrid.Children.Insert(2, _laserLayer);

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.SetToolWindowStyle(_hwnd);
            Win32Interop.RefreshWindowFrame(_hwnd);
            RefreshMonitorBounds();
            ApplyInteractiveState();
        };

        Loaded += (_, _) => RefreshMonitorBounds();

        MouseMove += OverlayWindow_MouseMove;
        MouseLeave += OverlayWindow_MouseLeave;
        MouseWheel += OverlayWindow_MouseWheel;
        PreviewKeyDown += OverlayWindow_PreviewKeyDown;
        Activated += (_, _) => OverlayActivated?.Invoke(this, EventArgs.Empty);
        PreviewMouseDown += (_, _) => OverlayActivated?.Invoke(this, EventArgs.Empty);
    }

    public MonitorInfo Monitor { get; }

    public double SpotlightRadius
    {
        get => _spotlightLayer.Radius;
        set => _spotlightLayer.Radius = value;
    }

    public event Action<int>? MagnifierWheelDelta;
    public event Action<int>? MagnifierSizeWheelDelta;
    public event Action<double>? SpotlightRadiusChanged;
    public event Action? StepCounterResetRequested;
    public event EventHandler? OverlayActivated;

    protected override void OnClosed(EventArgs e)
    {
        _laserLayer.IsActive = false;
        _spotlightLayer.IsActive = false;
        _canvas.Detach();
        base.OnClosed(e);
    }

    public void RefreshMonitorBounds()
    {
        var hwnd = _hwnd;
        if (hwnd == IntPtr.Zero)
        {
            _hwnd = hwnd = new WindowInteropHelper(this).Handle;
        }

        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var bounds = Monitor.PhysicalBounds;
        Win32Interop.SetWindowRectPhysical(
            hwnd,
            bounds.Left,
            bounds.Top,
            bounds.Width - FullscreenInsetPhysicalPixels,
            bounds.Height - FullscreenInsetPhysicalPixels);
    }

    private static double GetInsetPhysicalLength(int physicalLength, double dpiScale) =>
        Math.Max(1.0, (physicalLength - FullscreenInsetPhysicalPixels) / dpiScale);

    public void ApplyInteractiveState()
    {
        var hwnd = _hwnd;
        if (hwnd == IntPtr.Zero)
        {
            _hwnd = hwnd = new WindowInteropHelper(this).Handle;
        }

        var isClickThrough = _session.IsCursorMode
                             || (!_session.AnnotationsVisible && _session.BackgroundMode == CanvasBackgroundMode.Transparent
                                 && _session.CurrentTool is not (ToolType.Laser or ToolType.Spotlight or ToolType.Magnifier));

        if (hwnd != IntPtr.Zero && _lastClickThrough != isClickThrough)
        {
            Win32Interop.SetClickThrough(hwnd, isClickThrough);
            _lastClickThrough = isClickThrough;
        }

        _laserLayer.IsActive = _session.CurrentTool == ToolType.Laser;
        _spotlightLayer.IsActive = _session.CurrentTool == ToolType.Spotlight;

        Cursor = _session.CurrentTool switch
        {
            ToolType.Cursor => WpfCursors.Arrow,
            ToolType.Text => WpfCursors.IBeam,
            ToolType.Laser or ToolType.Spotlight or ToolType.Magnifier => WpfCursors.None,
            _ => WpfCursors.Cross
        };

        if (_session.CurrentTool != ToolType.Text && InlineTextBox.Visibility == Visibility.Visible)
        {
            CommitInlineTextInput();
        }
    }

    private void OverlayWindow_MouseMove(object sender, WpfMouseEventArgs e)
    {
        var pos = e.GetPosition(RootGrid);

        if (_session.CurrentTool == ToolType.Laser)
        {
            _laserLayer.UpdatePointer(pos, e.LeftButton == MouseButtonState.Pressed);
        }
        else if (_session.CurrentTool == ToolType.Spotlight)
        {
            _spotlightLayer.UpdatePosition(pos);
            UpdateProximityCursor(pos);
        }
        else if (_session.CurrentTool == ToolType.Magnifier)
        {
            UpdateProximityCursor(pos);
        }
    }

    private void UpdateProximityCursor(Point localPos)
    {
        var toolbarBounds = _toolbarBoundsProvider?.Invoke();
        if (toolbarBounds is { IsEmpty: false } rect)
        {
            var expanded = rect;
            expanded.Inflate(PanelCursorProximityDip, PanelCursorProximityDip);
            var screenDip = new Point(Monitor.DipBounds.Left + localPos.X, Monitor.DipBounds.Top + localPos.Y);
            var targetCursor = expanded.Contains(screenDip) ? WpfCursors.Arrow : WpfCursors.None;
            if (Cursor != targetCursor)
            {
                Cursor = targetCursor;
            }
        }
        else if (Cursor != WpfCursors.None)
        {
            Cursor = WpfCursors.None;
        }
    }

    private void OverlayWindow_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        if (_session.CurrentTool == ToolType.Laser)
        {
            _laserLayer.ClearPointer();
        }
        else if (_session.CurrentTool == ToolType.Spotlight)
        {
            _spotlightLayer.ClearPosition();
        }
    }

    private void OverlayWindow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_session.CurrentTool == ToolType.Spotlight)
        {
            _spotlightLayer.AdjustRadius(e.Delta > 0 ? 18.0 : -18.0);
            e.Handled = true;
        }
        else if (_session.CurrentTool == ToolType.Magnifier)
        {
            if ((WpfKeyboard.Modifiers & WpfModifierKeys.Control) == WpfModifierKeys.Control)
            {
                MagnifierSizeWheelDelta?.Invoke(e.Delta);
            }
            else
            {
                MagnifierWheelDelta?.Invoke(e.Delta);
            }
            e.Handled = true;
        }
    }

    private void OverlayWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (InlineTextBox.Visibility == Visibility.Visible)
        {
            return;
        }

        var ctrl = (WpfKeyboard.Modifiers & WpfModifierKeys.Control) == WpfModifierKeys.Control;
        if (ctrl && e.Key == WpfKey.Z)
        {
            _session.Undo();
            e.Handled = true;
        }
        else if (ctrl && e.Key == WpfKey.Y)
        {
            _session.Redo();
            e.Handled = true;
        }
        else if (e.Key == WpfKey.Escape)
        {
            _session.IsCursorMode = true;
            e.Handled = true;
        }
    }

    private void BeginInlineTextInput(Point pos)
    {
        if (InlineTextBox.Visibility == Visibility.Visible)
        {
            CommitInlineTextInput();
        }

        _pendingTextPos = pos;
        InlineTextBox.Text = string.Empty;
        InlineTextBox.FontSize = Math.Clamp(16.0 + (_session.StrokeWidth * 1.6), 16.0, 48.0);
        InlineTextBox.Foreground = new SolidColorBrush(_session.CurrentColor);
        Canvas.SetLeft(InlineTextBox, pos.X);
        Canvas.SetTop(InlineTextBox, pos.Y);
        InlineTextBox.Visibility = Visibility.Visible;
        InlineTextBox.Focus();
    }

    private void InlineTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var ctrl = (WpfKeyboard.Modifiers & WpfModifierKeys.Control) == WpfModifierKeys.Control;
        if (e.Key == WpfKey.Escape)
        {
            InlineTextBox.Text = string.Empty;
            InlineTextBox.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (ctrl && e.Key == WpfKey.Enter)
        {
            CommitInlineTextInput();
            e.Handled = true;
        }
    }

    private void InlineTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (InlineTextBox.Visibility == Visibility.Visible)
        {
            CommitInlineTextInput();
        }
    }

    private void CommitInlineTextInput()
    {
        var text = InlineTextBox.Text.Trim();
        InlineTextBox.Visibility = Visibility.Collapsed;
        InlineTextBox.Text = string.Empty;

        if (!string.IsNullOrWhiteSpace(text))
        {
            var annotation = new TextAnnotation(
                Monitor.DeviceName,
                _pendingTextPos,
                text,
                _session.CurrentColor,
                InlineTextBox.FontSize);
            _session.AddElement(annotation);
        }
    }
}
