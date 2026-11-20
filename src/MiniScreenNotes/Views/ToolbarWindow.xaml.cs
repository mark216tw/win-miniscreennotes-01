using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Resources.Strings;
using Com.MiniScreenNotes.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Com.MiniScreenNotes.Views;

public partial class ToolbarWindow : Window
{
    private static readonly string[] PresetColors =
    [
        "#EF4444", "#F97316", "#EAB308", "#22C55E", "#06B6D4",
        "#3B82F6", "#8B5CF6", "#EC4899", "#FFFFFF", "#111827"
    ];

    private static readonly TimeSpan SlideInDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan SlideOutDuration = TimeSpan.FromMilliseconds(170);
    private static readonly TimeSpan HandleSlideInDuration = TimeSpan.FromMilliseconds(140);

    private enum PanelTransitionKind
    {
        None,
        Showing,
        Hiding,
        Expanding,
        Collapsing
    }

    private readonly ConfigService _configService;
    private readonly AnnotationSession _session;
    private readonly ScreenCaptureService _captureService;
    private readonly DispatcherTimer _autoHideTimer;
    private readonly Dictionary<ToolType, FrameworkElement> _toolButtons = [];
    private readonly Dictionary<string, WpfButton> _colorSwatches = [];

    private Ellipse? _colorIndicatorDot;
    private string? _activeProfileId;
    private bool _hasPositioned;
    private DockEdge _currentDockEdge = DockEdge.None;
    private bool _isCollapsed;
    private double _expandedLeft;
    private double _expandedTop;
    private double _expandedWidth;
    private double _expandedHeight;
    private PanelTransitionKind _transitionKind = PanelTransitionKind.None;
    private int _transitionVersion;

    private bool _isDragging;
    private Point _dragStartScreenDip;
    private double _dragOriginLeft;
    private double _dragOriginTop;

    public ToolbarWindow(
        ConfigService configService,
        AnnotationSession session,
        ScreenCaptureService captureService)
    {
        InitializeComponent();

        _configService = configService;
        _session = session;
        _captureService = captureService;

        _autoHideTimer = new DispatcherTimer();
        _autoHideTimer.Tick += AutoHideTimer_Tick;

        BuildColorSwatches();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.SetToolWindowStyle(hwnd);
            Win32Interop.TryExcludeFromCapture(hwnd, true);
        };

        _configService.ConfigChanged += (_, _) => Dispatcher.Invoke(ApplyProfile);
        _session.StateChanged += (_, _) => Dispatcher.Invoke(SyncToolButtonStates);

        ApplyProfile();
    }

    public event EventHandler? OpenSettingsRequested;
    public event EventHandler<bool>? PanelVisibilityChanged;

    public void ShowAndRestorePosition()
    {
        if (IsVisible)
        {
            if (_transitionKind == PanelTransitionKind.Hiding)
            {
                if (_isCollapsed)
                {
                    ExpandPanel();
                }
                else
                {
                    CancelActiveTransitions(resetVisualToRest: false);
                    StartExpandedSlideIn(resumeFromCurrent: true, PanelTransitionKind.Showing);
                }

                PanelVisibilityChanged?.Invoke(this, true);
                Activate();
                BringToTopMost();
                return;
            }

            if (_isCollapsed || _transitionKind == PanelTransitionKind.Collapsing)
            {
                ExpandPanel();
            }
            else if (!IsMouseOver && !_autoHideTimer.IsEnabled && _transitionKind == PanelTransitionKind.None)
            {
                ScheduleAutoHideIfDocked();
            }

            PanelVisibilityChanged?.Invoke(this, true);
            Activate();
            BringToTopMost();
            return;
        }

        CancelActiveTransitions(resetVisualToRest: true);

        // 先以透明狀態顯示並完成佈局與定位，避免第一幀閃現於錯誤位置
        Opacity = 0.0;
        Show();

        var profile = _configService.GetActiveProfile();
        _activeProfileId = profile.Id;
        RestorePositionForProfile(profile);

        StartExpandedSlideIn(resumeFromCurrent: false, PanelTransitionKind.Showing);
        PanelVisibilityChanged?.Invoke(this, true);
        Activate();
        BringToTopMost();
    }

    public void BringToTopMost()
    {
        if (!IsVisible)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        Win32Interop.BringToTopMostNoActivate(hwnd);
    }

    public Rect? GetCurrentScreenDipBounds() =>
        IsVisible && _transitionKind != PanelTransitionKind.Hiding
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : null;

    public void HideAndRememberPosition()
    {
        _autoHideTimer.Stop();
        ColorPopup.IsOpen = false;
        StrokePopup.IsOpen = false;

        // 關閉面板時立即切回游標模式並一併隱藏螢幕標註，避免干擾操作或殘留畫面
        _session.IsCursorMode = true;
        PanelVisibilityChanged?.Invoke(this, false);

        if (!IsVisible || _transitionKind == PanelTransitionKind.Hiding)
        {
            return;
        }

        SaveCurrentPosition();

        if (_isCollapsed)
        {
            StartCollapsedHandleSlideOutAndHide();
            return;
        }

        CancelActiveTransitions(resetVisualToRest: false);
        _transitionKind = PanelTransitionKind.Hiding;
        var version = ++_transitionVersion;
        var offset = GetCurrentSlideOffset();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        if (_currentDockEdge != DockEdge.None)
        {
            Left = _expandedLeft;
            Top = _expandedTop;
            Opacity = 1.0;

            AnimateExpandedTransformAndOpacity(
                offset.X,
                offset.Y,
                0.0,
                SlideOutDuration,
                ease,
                () =>
                {
                    if (version == _transitionVersion)
                    {
                        FinishHide();
                    }
                });
        }
        else
        {
            ExpandedTranslateTransform.X = 0;
            ExpandedTranslateTransform.Y = 0;
            ExpandedContainer.Opacity = 1.0;

            AnimateWindowPositionAndOpacity(
                _expandedLeft + offset.X,
                _expandedTop + offset.Y,
                0.0,
                SlideOutDuration,
                ease,
                () =>
                {
                    if (version == _transitionVersion)
                    {
                        FinishHide();
                    }
                });
        }
    }

    public void ToggleVisibility()
    {
        if (IsVisible && _transitionKind != PanelTransitionKind.Hiding)
        {
            HideAndRememberPosition();
        }
        else
        {
            ShowAndRestorePosition();
        }
    }

    public void SaveCurrentPosition()
    {
        if (!_hasPositioned)
        {
            return;
        }

        var profile = _configService.GetActiveProfile();
        WritePositionToProfile(profile);
        _configService.RequestSave();
    }

    private void WritePositionToProfile(PanelProfile profile)
    {
        var useExpandedSnapshot = _isCollapsed || _transitionKind != PanelTransitionKind.None;
        var left = useExpandedSnapshot ? _expandedLeft : Left;
        var top = useExpandedSnapshot ? _expandedTop : Top;
        profile.LastPanelLeft = left;
        profile.LastPanelTop = top;
        profile.LastDockEdge = _currentDockEdge;

        _configService.CurrentConfig.LastPanelLeft = left;
        _configService.CurrentConfig.LastPanelTop = top;
        _configService.CurrentConfig.LastDockEdge = _currentDockEdge;
    }

    private void RestorePositionForProfile(PanelProfile profile)
    {
        if (_isCollapsed)
        {
            CollapsedHandle.Visibility = Visibility.Collapsed;
            ExpandedContainer.Visibility = Visibility.Visible;
            _isCollapsed = false;
        }

        // 首次顯示時 WPF 會先套用視窗最小寬度，需重新量測才能依內容縮回正確尺寸
        InvalidateMeasure();
        UpdateLayout();

        var allMonitors = DisplayMonitorHelper.GetAllMonitors();
        var monitorBounds = allMonitors.Select(m => m.DipBounds).ToList();
        var panelSize = new Size(Math.Max(ActualWidth, 48), Math.Max(ActualHeight, 48));

        if (profile.LastPanelLeft.HasValue && profile.LastPanelTop.HasValue)
        {
            var dockEdge = IsDockEdgeValidForOrientation(profile.LastDockEdge, profile.Orientation)
                ? profile.LastDockEdge
                : DockEdge.None;

            var safe = DockSnapHelper.ClampToAvailableMonitors(
                new Point(profile.LastPanelLeft.Value, profile.LastPanelTop.Value),
                panelSize,
                monitorBounds);

            if (dockEdge != DockEdge.None)
            {
                var center = new Point(safe.X + (panelSize.Width / 2.0), safe.Y + (panelSize.Height / 2.0));
                var targetMonitor = allMonitors.FirstOrDefault(m => m.DipBounds.Contains(center))
                    ?? DisplayMonitorHelper.GetPrimaryMonitor();
                safe = DockSnapHelper.GetDockedExpandedPosition(
                    new Rect(safe.X, safe.Y, panelSize.Width, panelSize.Height),
                    targetMonitor.DipBounds,
                    dockEdge);
            }

            Left = safe.X;
            Top = safe.Y;
            _currentDockEdge = dockEdge;
        }
        else
        {
            var primary = DisplayMonitorHelper.GetPrimaryMonitor().DipWorkArea;
            if (profile.Orientation == PanelOrientation.Vertical)
            {
                Left = primary.Right - panelSize.Width - 16;
                Top = primary.Top + ((primary.Height - panelSize.Height) / 2.0);
            }
            else
            {
                Left = primary.Left + ((primary.Width - panelSize.Width) / 2.0);
                Top = primary.Top + 16;
            }

            _currentDockEdge = DockEdge.None;
        }

        _expandedLeft = Left;
        _expandedTop = Top;
        _expandedWidth = ActualWidth;
        _expandedHeight = ActualHeight;
        _hasPositioned = true;
    }

    private void ApplyProfile()
    {
        var profile = _configService.GetActiveProfile();
        var switchedProfile = _activeProfileId is not null && _activeProfileId != profile.Id;

        if (switchedProfile && _hasPositioned)
        {
            var previousProfile = _configService.CurrentConfig.Profiles
                .FirstOrDefault(p => p.Id == _activeProfileId);
            if (previousProfile is not null)
            {
                WritePositionToProfile(previousProfile);
                _configService.Save();
            }
        }

        _activeProfileId = profile.Id;

        var btnSize = profile.GetButtonSize();
        var iconSize = profile.GetIconSize();

        var (_, rows) = RebuildToolButtons(profile, btnSize, iconSize);
        ArrangeCompactUtilityStrip(profile, rows);

        if (switchedProfile && IsVisible)
        {
            _autoHideTimer.Stop();
            CancelActiveTransitions(resetVisualToRest: true);
            RestorePositionForProfile(profile);
            BringToTopMost();
        }
        else if (!IsDockEdgeValidForOrientation(_currentDockEdge, profile.Orientation))
        {
            // 若方向變更導致原吸附邊緣不再合法，解除吸附
            CancelActiveTransitions(resetVisualToRest: true);
            _currentDockEdge = DockEdge.None;
            ExpandPanel();
        }

        _autoHideTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(profile.AutoHideDelaySeconds, 0.3, 60.0));

        if (!profile.AutoHideWhenDocked)
        {
            _autoHideTimer.Stop();
            if (_isCollapsed || _transitionKind == PanelTransitionKind.Collapsing)
            {
                ExpandPanel();
            }
        }
        else if (IsVisible && !_isCollapsed && !IsMouseOver && !_autoHideTimer.IsEnabled && _transitionKind == PanelTransitionKind.None)
        {
            ScheduleAutoHideIfDocked();
        }

        SyncToolButtonStates();
    }

    private void ArrangeCompactUtilityStrip(PanelProfile profile, int rows)
    {
        var isHorizontal = profile.Orientation == PanelOrientation.Horizontal;
        var utilityBtnSize = profile.Size switch
        {
            PanelSize.ExtraSmall => 16.0,
            PanelSize.Small => 18.0,
            PanelSize.Large => 24.0,
            _ => 20.0
        };
        var utilityIconSize = profile.Size switch
        {
            PanelSize.ExtraSmall => 10.0,
            PanelSize.Small => 11.0,
            PanelSize.Large => 14.0,
            _ => 12.0
        };

        // 清空原容器再重新配置（涵蓋建構期尚未建立 VisualTree 的情況）
        UtilityStrip.Children.Clear();
        RootDockPanel.Children.Clear();

        SettingsButton.Width = utilityBtnSize;
        SettingsButton.Height = utilityBtnSize;
        SettingsButton.Margin = new Thickness(0);
        SettingsIconBox.Width = utilityIconSize;
        SettingsIconBox.Height = utilityIconSize;

        CloseButton.Width = utilityBtnSize;
        CloseButton.Height = utilityBtnSize;
        CloseButton.Margin = new Thickness(0);
        CloseIconBox.Width = utilityIconSize;
        CloseIconBox.Height = utilityIconSize;

        UtilityStrip.Width = double.NaN;
        UtilityStrip.Height = double.NaN;

        if (isHorizontal)
        {
            FixedSeparator.Width = 1;
            FixedSeparator.Height = double.NaN;
            FixedSeparator.Margin = new Thickness(3, 2, 3, 2);

            UtilityStrip.Width = utilityBtnSize;
            DockPanel.SetDock(CloseButton, Dock.Top);
            if (rows >= 2)
            {
                DockPanel.SetDock(SettingsButton, Dock.Top);
                SettingsButton.Margin = new Thickness(0, 2, 0, 0);
            }
            else
            {
                DockPanel.SetDock(SettingsButton, Dock.Bottom);
            }

            UtilityStrip.Children.Add(CloseButton);
            UtilityStrip.Children.Add(SettingsButton);

            DockPanel.SetDock(UtilityStrip, Dock.Right);
            DockPanel.SetDock(FixedSeparator, Dock.Right);
            RootDockPanel.Children.Add(UtilityStrip);
            RootDockPanel.Children.Add(FixedSeparator);
            RootDockPanel.Children.Add(ToolsGrid);
        }
        else
        {
            // 直向面板（單欄／多欄）：設定與關閉按鈕統一置於最下方
            FixedSeparator.Width = double.NaN;
            FixedSeparator.Height = 1;
            FixedSeparator.Margin = new Thickness(2, 3, 2, 3);

            UtilityStrip.Height = utilityBtnSize;
            DockPanel.SetDock(CloseButton, Dock.Right);
            DockPanel.SetDock(SettingsButton, Dock.Left);
            UtilityStrip.Children.Add(CloseButton);
            UtilityStrip.Children.Add(SettingsButton);

            DockPanel.SetDock(UtilityStrip, Dock.Bottom);
            DockPanel.SetDock(FixedSeparator, Dock.Bottom);
            RootDockPanel.Children.Add(UtilityStrip);
            RootDockPanel.Children.Add(FixedSeparator);
            RootDockPanel.Children.Add(ToolsGrid);
        }
    }

    private (int Columns, int Rows) RebuildToolButtons(PanelProfile profile, double btnSize, double iconSize)
    {
        ToolsGrid.Children.Clear();
        _toolButtons.Clear();
        _colorIndicatorDot = null;

        var visibleTools = profile.Tools.Where(t => t.IsVisible).ToList();
        var (cols, rows) = DockSnapHelper.CalculateGridDimensions(
            visibleTools.Count,
            profile.GetMaxItemsPerLine(),
            profile.Orientation);

        ToolsGrid.ColumnDefinitions.Clear();
        ToolsGrid.RowDefinitions.Clear();

        for (var column = 0; column < cols; column++)
        {
            ToolsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (var row = 0; row < rows; row++)
        {
            ToolsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < visibleTools.Count; index++)
        {
            var item = visibleTools[index];
            var element = CreateToolControl(item.Tool, btnSize, iconSize);
            var (row, column) = DockSnapHelper.GetGridCell(index, cols, rows, profile.Orientation);
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            _toolButtons[item.Tool] = element;
            ToolsGrid.Children.Add(element);
        }

        return (cols, rows);
    }

    private FrameworkElement CreateToolControl(ToolType tool, double btnSize, double iconSize)
    {
        var label = ToolMetadataHelper.GetDisplayName(tool);
        var toolTip = tool == ToolType.StepNumber
            ? label + Strings.Tooltip_StepNumberHint
            : label;
        var iconKey = ToolMetadataHelper.GetIconResourceKey(tool);
        var content = BuildIconContent(tool, iconKey, iconSize);

        if (ToolMetadataHelper.IsSelectableModeTool(tool) || tool == ToolType.ToggleVisibility)
        {
            var toggle = new ToggleButton
            {
                Width = btnSize,
                Height = btnSize,
                Margin = new Thickness(1),
                Style = (Style)FindResource("ToolbarToggleButtonStyle"),
                ToolTip = toolTip,
                Content = content
            };
            AutomationProperties.SetName(toggle, label);
            toggle.Click += (_, _) => HandleToolClick(tool, toggle);
            return toggle;
        }

        var button = new WpfButton
        {
            Width = btnSize,
            Height = btnSize,
            Margin = new Thickness(1),
            Style = (Style)FindResource("ToolbarIconButtonStyle"),
            ToolTip = toolTip,
            Content = content
        };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => HandleToolClick(tool, button);
        return button;
    }

    private FrameworkElement BuildIconContent(ToolType tool, string iconKey, double iconSize)
    {
        var geometry = TryFindResource(iconKey) as Geometry;
        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stretch = Stretch.Uniform
        };

        if (tool == ToolType.CheckMark)
        {
            path.Fill = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94));
        }
        else if (tool == ToolType.CrossMark)
        {
            path.Fill = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68));
        }
        else
        {
            path.SetBinding(Shape.FillProperty, new System.Windows.Data.Binding("Foreground")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ContentControl), 1)
            });
        }

        var viewbox = new Viewbox
        {
            Width = iconSize,
            Height = iconSize,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = path
        };

        if (tool == ToolType.ColorPicker)
        {
            var grid = new Grid
            {
                Width = iconSize,
                Height = iconSize,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(viewbox);
            _colorIndicatorDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 1,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Fill = new SolidColorBrush(_session.CurrentColor)
            };
            grid.Children.Add(_colorIndicatorDot);
            return grid;
        }

        return viewbox;
    }

    private void HandleToolClick(ToolType tool, FrameworkElement anchor)
    {
        if (tool == ToolType.Cursor)
        {
            _session.ToggleCursorMode();
            SyncToolButtonStates();
            return;
        }

        if (ToolMetadataHelper.IsSelectableModeTool(tool))
        {
            _session.SelectAnnotationTool(tool);
            SyncToolButtonStates();
            return;
        }

        switch (tool)
        {
            case ToolType.ColorPicker:
                StrokePopup.IsOpen = false;
                ColorPopup.PlacementTarget = anchor;
                HexColorBox.Text = $"#{_session.CurrentColor.R:X2}{_session.CurrentColor.G:X2}{_session.CurrentColor.B:X2}";
                ColorPopup.IsOpen = !ColorPopup.IsOpen;
                break;

            case ToolType.StrokeWidthPicker:
                ColorPopup.IsOpen = false;
                StrokePopup.PlacementTarget = anchor;
                StrokeWidthSlider.Value = _session.StrokeWidth;
                StrokeWidthLabel.Text = $"{(int)Math.Round(_session.StrokeWidth)} px";
                StrokePopup.IsOpen = !StrokePopup.IsOpen;
                break;

            case ToolType.ToggleVisibility:
                _session.AnnotationsVisible = !_session.AnnotationsVisible;
                break;

            case ToolType.BackgroundMode:
                _session.CycleBackgroundMode();
                break;

            case ToolType.Undo:
                _session.Undo();
                break;

            case ToolType.Redo:
                _session.Redo();
                break;

            case ToolType.Clear:
                _session.ClearAll();
                break;

            case ToolType.Copy:
                _captureService.CopyMonitorToClipboard(GetCursorMonitor());
                break;

            case ToolType.Save:
                _captureService.SaveMonitorToFile(GetCursorMonitor());
                break;
        }
    }

    private void SyncToolButtonStates()
    {
        foreach (var (tool, element) in _toolButtons)
        {
            if (element is ToggleButton toggle)
            {
                toggle.IsChecked = tool switch
                {
                    ToolType.ToggleVisibility => !_session.AnnotationsVisible,
                    ToolType.Cursor => _session.IsCursorMode,
                    _ => _session.SelectedAnnotationTool == tool
                };

                if (tool == ToolType.ToggleVisibility)
                {
                    toggle.ToolTip = _session.AnnotationsVisible
                        ? Strings.Tooltip_AnnotationsVisible
                        : Strings.Tooltip_AnnotationsHidden;
                }
            }
            else if (element is WpfButton btn)
            {
                if (tool == ToolType.Undo)
                {
                    btn.IsEnabled = _session.CanUndo;
                }
                else if (tool == ToolType.Redo)
                {
                    btn.IsEnabled = _session.CanRedo;
                }
                else if (tool == ToolType.Clear)
                {
                    btn.IsEnabled = _session.Elements.Count > 0;
                }
                else if (tool == ToolType.BackgroundMode)
                {
                    var bgName = _session.BackgroundMode switch
                    {
                        CanvasBackgroundMode.Whiteboard => Strings.Background_Whiteboard,
                        CanvasBackgroundMode.Blackboard => Strings.Background_Blackboard,
                        _ => Strings.Background_Transparent
                    };
                    btn.ToolTip = $"{Strings.Action_Background}（目前：{bgName}）";
                }
            }
        }

        if (_colorIndicatorDot is not null)
        {
            _colorIndicatorDot.Fill = new SolidColorBrush(_session.CurrentColor);
        }

        UpdateColorSwatchSelection();
    }

    private void BuildColorSwatches()
    {
        ColorSwatchesGrid.Children.Clear();
        _colorSwatches.Clear();

        foreach (var hex in PresetColors)
        {
            var color = (WpfColor)WpfColorConverter.ConvertFromString(hex);
            var check = new System.Windows.Shapes.Path
            {
                Data = (Geometry)FindResource("Icon_CheckMark"),
                Stretch = Stretch.Uniform,
                Width = 14,
                Height = 14,
                Fill = GetContrastBrush(color),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };

            var btn = new WpfButton
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(3),
                Style = (Style)FindResource("ColorSwatchButtonStyle"),
                Background = new SolidColorBrush(color),
                ToolTip = hex,
                Content = check
            };
            AutomationProperties.SetName(btn, $"顏色 {hex}");
            btn.Click += (_, _) =>
            {
                ApplySelectedColor(color, hex);
                ColorPopup.IsOpen = false;
            };
            _colorSwatches[hex] = btn;
            ColorSwatchesGrid.Children.Add(btn);
        }

        UpdateColorSwatchSelection();
    }

    private static Brush GetContrastBrush(WpfColor color)
    {
        var luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
        return luminance > 160 ? Brushes.Black : Brushes.White;
    }

    private void UpdateColorSwatchSelection()
    {
        var currentHex = $"#{_session.CurrentColor.R:X2}{_session.CurrentColor.G:X2}{_session.CurrentColor.B:X2}";
        foreach (var (hex, button) in _colorSwatches)
        {
            if (button.Content is System.Windows.Shapes.Path check)
            {
                check.Visibility = string.Equals(hex, currentHex, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }

    private void ApplySelectedColor(WpfColor color, string hex)
    {
        _session.CurrentColor = color;
        _configService.CurrentConfig.SelectedColorHex = hex;
        _configService.RequestSave();
    }

    private void ApplyHexColor_Click(object sender, RoutedEventArgs e) => TryApplyHexColor();

    private bool TryApplyHexColor()
    {
        var hex = HexColorBox.Text.Trim();
        if (!hex.StartsWith('#'))
        {
            hex = "#" + hex;
        }

        try
        {
            if (WpfColorConverter.ConvertFromString(hex) is WpfColor color)
            {
                ApplySelectedColor(color, hex);
                HexColorBox.ClearValue(Control.BorderBrushProperty);
                HexColorBox.ToolTip = null;
                ColorPopup.IsOpen = false;
                return true;
            }
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException)
        {
        }

        HexColorBox.BorderBrush = (Brush)FindResource("DangerBrush");
        HexColorBox.ToolTip = Strings.HexColor_Invalid;
        return false;
    }

    private void HexColorBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryApplyHexColor();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ColorPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void HexColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        HexColorBox.ClearValue(Control.BorderBrushProperty);
        HexColorBox.ToolTip = null;
    }

    private void PresetStroke_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton btn && double.TryParse(btn.Tag?.ToString(), out var value))
        {
            StrokeWidthSlider.Value = value;
            StrokePopup.IsOpen = false;
        }
    }

    private void StrokeWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var rounded = Math.Round(e.NewValue);
        _session.StrokeWidth = rounded;
        StrokeWidthLabel.Text = $"{(int)rounded} px";
        _configService.CurrentConfig.StrokeWidth = rounded;
        _configService.RequestSave();
    }

    private void ExpandedContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject) || _transitionKind == PanelTransitionKind.Hiding)
        {
            return;
        }

        _autoHideTimer.Stop();
        CancelActiveTransitions(resetVisualToRest: true);
        ColorPopup.IsOpen = false;
        StrokePopup.IsOpen = false;

        _isDragging = true;
        _dragOriginLeft = Left;
        _dragOriginTop = Top;
        _dragStartScreenDip = PointToScreenDip(e.GetPosition(this));
        ExpandedContainer.CaptureMouse();
        e.Handled = true;
    }

    private void ExpandedContainer_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var current = PointToScreenDip(e.GetPosition(this));
        Left = _dragOriginLeft + (current.X - _dragStartScreenDip.X);
        Top = _dragOriginTop + (current.Y - _dragStartScreenDip.Y);
        e.Handled = true;
    }

    private void ExpandedContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ExpandedContainer.ReleaseMouseCapture();

        var profile = _configService.GetActiveProfile();
        var panelRect = new Rect(Left, Top, ActualWidth, ActualHeight);
        var monitor = GetCurrentMonitor();
        var detected = DockSnapHelper.DetectDockEdge(panelRect, monitor.DipBounds, profile.Orientation);

        _currentDockEdge = detected;
        if (detected != DockEdge.None)
        {
            var snapped = DockSnapHelper.GetDockedExpandedPosition(panelRect, monitor.DipBounds, detected);
            Left = snapped.X;
            Top = snapped.Y;
        }

        _expandedLeft = Left;
        _expandedTop = Top;
        _expandedWidth = ActualWidth;
        _expandedHeight = ActualHeight;

        SaveCurrentPosition();
        if (!IsMouseOver)
        {
            ScheduleAutoHideIfDocked();
        }

        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is ButtonBase)
            {
                return true;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private void Window_MouseEnter(object sender, WpfMouseEventArgs e)
    {
        _autoHideTimer.Stop();
        if (_transitionKind == PanelTransitionKind.Hiding)
        {
            return;
        }

        if (_isCollapsed || _transitionKind == PanelTransitionKind.Collapsing)
        {
            ExpandPanel();
        }
    }

    private void Window_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        ScheduleAutoHideIfDocked();
    }

    private void OptionPopup_Closed(object? sender, EventArgs e)
    {
        if (!IsMouseOver)
        {
            ScheduleAutoHideIfDocked();
        }
    }

    private void CollapsedHandle_MouseEnter(object sender, WpfMouseEventArgs e)
    {
        _autoHideTimer.Stop();
        if (_transitionKind == PanelTransitionKind.Hiding)
        {
            return;
        }

        ExpandPanel();
    }

    private void ScheduleAutoHideIfDocked()
    {
        var profile = _configService.GetActiveProfile();
        if (_isDragging
            || _isCollapsed
            || _transitionKind is PanelTransitionKind.Hiding or PanelTransitionKind.Collapsing
            || _currentDockEdge == DockEdge.None
            || !profile.AutoHideWhenDocked)
        {
            return;
        }

        if (ColorPopup.IsOpen || StrokePopup.IsOpen || profile.AutoHideDelaySeconds <= 0)
        {
            return;
        }

        _autoHideTimer.Interval = TimeSpan.FromSeconds(profile.AutoHideDelaySeconds);
        _autoHideTimer.Start();
    }

    private void AutoHideTimer_Tick(object? sender, EventArgs e)
    {
        _autoHideTimer.Stop();
        if (!IsMouseOver && !ColorPopup.IsOpen && !StrokePopup.IsOpen && _currentDockEdge != DockEdge.None)
        {
            CollapseToHandle();
        }
    }

    private void CollapseToHandle()
    {
        if (_isCollapsed
            || _currentDockEdge == DockEdge.None
            || _transitionKind is PanelTransitionKind.Collapsing or PanelTransitionKind.Hiding)
        {
            return;
        }

        if (_transitionKind == PanelTransitionKind.None)
        {
            _expandedLeft = Left;
            _expandedTop = Top;
            _expandedWidth = ActualWidth;
            _expandedHeight = ActualHeight;
        }

        CancelActiveTransitions(resetVisualToRest: false);
        _transitionKind = PanelTransitionKind.Collapsing;
        var version = ++_transitionVersion;
        var offset = GetCurrentSlideOffset();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        Left = _expandedLeft;
        Top = _expandedTop;
        Opacity = 1.0;

        AnimateExpandedTransformAndOpacity(
            offset.X,
            offset.Y,
            0.0,
            SlideOutDuration,
            ease,
            () =>
            {
                if (version == _transitionVersion)
                {
                    CompleteCollapseToHandle();
                }
            });
    }

    private void CompleteCollapseToHandle()
    {
        CancelActiveTransitions(resetVisualToRest: true);

        var isHorizontalDock = _currentDockEdge is DockEdge.Top or DockEdge.Bottom;
        var handleW = isHorizontalDock ? 80.0 : 12.0;
        var handleH = isHorizontalDock ? 12.0 : 80.0;

        CollapsedHandle.Width = handleW;
        CollapsedHandle.Height = handleH;
        HandleAccentBar.Width = isHorizontalDock ? 44.0 : 4.0;
        HandleAccentBar.Height = isHorizontalDock ? 4.0 : 44.0;

        ExpandedContainer.Visibility = Visibility.Collapsed;
        CollapsedHandle.Visibility = Visibility.Visible;
        _isCollapsed = true;

        var monitor = GetCurrentMonitor();
        var handlePos = DockSnapHelper.GetHiddenHandlePosition(
            _expandedLeft,
            _expandedTop,
            _expandedWidth,
            _expandedHeight,
            handleW,
            handleH,
            monitor.DipBounds,
            _currentDockEdge);

        Left = handlePos.X;
        Top = handlePos.Y;

        StartCollapsedHandleSlideIn(handleW, handleH, monitor.DipBounds, handlePos);
        BringToTopMost();
    }

    private void ExpandPanel()
    {
        if (_transitionKind == PanelTransitionKind.Collapsing)
        {
            CancelActiveTransitions(resetVisualToRest: false);
            StartExpandedSlideIn(resumeFromCurrent: true, PanelTransitionKind.Expanding);
            BringToTopMost();
            return;
        }

        if (!_isCollapsed)
        {
            return;
        }

        CancelActiveTransitions(resetVisualToRest: true);

        CollapsedHandle.Visibility = Visibility.Collapsed;
        ExpandedContainer.Visibility = Visibility.Visible;
        _isCollapsed = false;

        UpdateLayout();
        var monitor = GetCurrentMonitor();
        var expandedRect = new Rect(_expandedLeft, _expandedTop, ActualWidth, ActualHeight);
        var pos = _currentDockEdge != DockEdge.None
            ? DockSnapHelper.GetDockedExpandedPosition(expandedRect, monitor.DipBounds, _currentDockEdge)
            : new Point(_expandedLeft, _expandedTop);

        Left = pos.X;
        Top = pos.Y;
        _expandedLeft = Left;
        _expandedTop = Top;
        _expandedWidth = ActualWidth;
        _expandedHeight = ActualHeight;

        if (_currentDockEdge != DockEdge.None)
        {
            StartExpandedSlideIn(resumeFromCurrent: false, PanelTransitionKind.Expanding);
        }

        BringToTopMost();
    }

    private void StartExpandedSlideIn(bool resumeFromCurrent, PanelTransitionKind kind)
    {
        _transitionKind = kind;
        var version = ++_transitionVersion;
        var offset = GetCurrentSlideOffset();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (_currentDockEdge != DockEdge.None)
        {
            Left = _expandedLeft;
            Top = _expandedTop;
            Opacity = 1.0;

            if (!resumeFromCurrent)
            {
                ExpandedTranslateTransform.X = offset.X;
                ExpandedTranslateTransform.Y = offset.Y;
                ExpandedContainer.Opacity = 0.2;
            }

            AnimateExpandedTransformAndOpacity(
                0.0,
                0.0,
                1.0,
                SlideInDuration,
                ease,
                () =>
                {
                    if (version == _transitionVersion)
                    {
                        CancelActiveTransitions(resetVisualToRest: true);
                        if (!IsMouseOver)
                        {
                            ScheduleAutoHideIfDocked();
                        }
                    }
                });
        }
        else
        {
            ExpandedTranslateTransform.X = 0.0;
            ExpandedTranslateTransform.Y = 0.0;
            ExpandedContainer.Opacity = 1.0;

            if (!resumeFromCurrent)
            {
                Left = _expandedLeft + offset.X;
                Top = _expandedTop + offset.Y;
                Opacity = 0.0;
            }

            AnimateWindowPositionAndOpacity(
                _expandedLeft,
                _expandedTop,
                1.0,
                SlideInDuration,
                ease,
                () =>
                {
                    if (version == _transitionVersion)
                    {
                        CancelActiveTransitions(resetVisualToRest: true);
                        Left = _expandedLeft;
                        Top = _expandedTop;
                    }
                });
        }
    }

    private void StartCollapsedHandleSlideIn(double handleWidth, double handleHeight, Rect monitorBounds, Point handlePos)
    {
        var profile = _configService.GetActiveProfile();
        var offset = DockSnapHelper.GetSlideOffset(
            _currentDockEdge,
            profile.Orientation,
            new Size(handleWidth, handleHeight),
            monitorBounds,
            handlePos);

        var version = ++_transitionVersion;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        HandleTranslateTransform.X = offset.X;
        HandleTranslateTransform.Y = offset.Y;
        CollapsedHandle.Opacity = 0.0;

        AnimateHandleTransformAndOpacity(
            0.0,
            0.0,
            1.0,
            HandleSlideInDuration,
            ease,
            () =>
            {
                if (version == _transitionVersion)
                {
                    CancelActiveTransitions(resetVisualToRest: true);
                }
            });
    }

    private void StartCollapsedHandleSlideOutAndHide()
    {
        CancelActiveTransitions(resetVisualToRest: false);
        _transitionKind = PanelTransitionKind.Hiding;
        var version = ++_transitionVersion;
        var profile = _configService.GetActiveProfile();
        var monitor = GetCurrentMonitor();
        var offset = DockSnapHelper.GetSlideOffset(
            _currentDockEdge,
            profile.Orientation,
            new Size(Math.Max(CollapsedHandle.ActualWidth, CollapsedHandle.Width), Math.Max(CollapsedHandle.ActualHeight, CollapsedHandle.Height)),
            monitor.DipBounds,
            new Point(Left, Top));

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        AnimateHandleTransformAndOpacity(
            offset.X,
            offset.Y,
            0.0,
            HandleSlideInDuration,
            ease,
            () =>
            {
                if (version == _transitionVersion)
                {
                    FinishHide();
                }
            });
    }

    private void FinishHide()
    {
        CancelActiveTransitions(resetVisualToRest: true);
        if (!_isCollapsed && _hasPositioned)
        {
            Left = _expandedLeft;
            Top = _expandedTop;
        }

        Hide();
    }

    private void AnimateExpandedTransformAndOpacity(
        double toX,
        double toY,
        double toOpacity,
        TimeSpan duration,
        IEasingFunction easing,
        Action onCompleted)
    {
        var xAnim = new DoubleAnimation(toX, duration) { EasingFunction = easing };
        var yAnim = new DoubleAnimation(toY, duration) { EasingFunction = easing };
        var opacityAnim = new DoubleAnimation(toOpacity, duration) { EasingFunction = easing };
        opacityAnim.Completed += (_, _) => onCompleted();

        ExpandedTranslateTransform.BeginAnimation(TranslateTransform.XProperty, xAnim);
        ExpandedTranslateTransform.BeginAnimation(TranslateTransform.YProperty, yAnim);
        ExpandedContainer.BeginAnimation(OpacityProperty, opacityAnim);
    }

    private void AnimateHandleTransformAndOpacity(
        double toX,
        double toY,
        double toOpacity,
        TimeSpan duration,
        IEasingFunction easing,
        Action onCompleted)
    {
        var xAnim = new DoubleAnimation(toX, duration) { EasingFunction = easing };
        var yAnim = new DoubleAnimation(toY, duration) { EasingFunction = easing };
        var opacityAnim = new DoubleAnimation(toOpacity, duration) { EasingFunction = easing };
        opacityAnim.Completed += (_, _) => onCompleted();

        HandleTranslateTransform.BeginAnimation(TranslateTransform.XProperty, xAnim);
        HandleTranslateTransform.BeginAnimation(TranslateTransform.YProperty, yAnim);
        CollapsedHandle.BeginAnimation(OpacityProperty, opacityAnim);
    }

    private void AnimateWindowPositionAndOpacity(
        double toLeft,
        double toTop,
        double toOpacity,
        TimeSpan duration,
        IEasingFunction easing,
        Action onCompleted)
    {
        var leftAnim = new DoubleAnimation(toLeft, duration) { EasingFunction = easing };
        var topAnim = new DoubleAnimation(toTop, duration) { EasingFunction = easing };
        var opacityAnim = new DoubleAnimation(toOpacity, duration) { EasingFunction = easing };
        opacityAnim.Completed += (_, _) => onCompleted();

        BeginAnimation(LeftProperty, leftAnim);
        BeginAnimation(TopProperty, topAnim);
        BeginAnimation(OpacityProperty, opacityAnim);
    }

    private void CancelActiveTransitions(bool resetVisualToRest)
    {
        _transitionVersion++;
        _transitionKind = PanelTransitionKind.None;

        var currentLeft = Left;
        var currentTop = Top;
        var currentOpacity = Opacity;
        var currentExpandedX = ExpandedTranslateTransform.X;
        var currentExpandedY = ExpandedTranslateTransform.Y;
        var currentExpandedOpacity = ExpandedContainer.Opacity;
        var currentHandleX = HandleTranslateTransform.X;
        var currentHandleY = HandleTranslateTransform.Y;
        var currentHandleOpacity = CollapsedHandle.Opacity;

        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(OpacityProperty, null);
        ExpandedTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
        ExpandedTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
        ExpandedContainer.BeginAnimation(OpacityProperty, null);
        HandleTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
        HandleTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
        CollapsedHandle.BeginAnimation(OpacityProperty, null);

        Left = currentLeft;
        Top = currentTop;

        if (resetVisualToRest)
        {
            Opacity = 1.0;
            ExpandedTranslateTransform.X = 0.0;
            ExpandedTranslateTransform.Y = 0.0;
            ExpandedContainer.Opacity = 1.0;
            HandleTranslateTransform.X = 0.0;
            HandleTranslateTransform.Y = 0.0;
            CollapsedHandle.Opacity = 1.0;
        }
        else
        {
            Opacity = currentOpacity;
            ExpandedTranslateTransform.X = currentExpandedX;
            ExpandedTranslateTransform.Y = currentExpandedY;
            ExpandedContainer.Opacity = currentExpandedOpacity;
            HandleTranslateTransform.X = currentHandleX;
            HandleTranslateTransform.Y = currentHandleY;
            CollapsedHandle.Opacity = currentHandleOpacity;
        }
    }

    private Vector GetCurrentSlideOffset()
    {
        var profile = _configService.GetActiveProfile();
        var width = Math.Max(_expandedWidth > 0 ? _expandedWidth : ActualWidth, 48.0);
        var height = Math.Max(_expandedHeight > 0 ? _expandedHeight : ActualHeight, 48.0);
        var position = new Point(_expandedLeft, _expandedTop);
        var center = new Point(position.X + (width / 2.0), position.Y + (height / 2.0));
        var monitor = DisplayMonitorHelper.GetMonitorContainingDipPoint(center);

        return DockSnapHelper.GetSlideOffset(
            _currentDockEdge,
            profile.Orientation,
            new Size(width, height),
            monitor.DipBounds,
            position);
    }

    private static bool IsDockEdgeValidForOrientation(DockEdge edge, PanelOrientation orientation) =>
        orientation == PanelOrientation.Horizontal
            ? edge is DockEdge.Top or DockEdge.Bottom
            : edge is DockEdge.Left or DockEdge.Right;

    private MonitorInfo GetCurrentMonitor()
    {
        var center = new Point(Left + (ActualWidth / 2.0), Top + (ActualHeight / 2.0));
        return DisplayMonitorHelper.GetMonitorContainingDipPoint(center);
    }

    private static MonitorInfo GetCursorMonitor()
    {
        if (Win32Interop.GetCursorPos(out var cursor))
        {
            return DisplayMonitorHelper.GetMonitorContainingPhysicalPoint(cursor.X, cursor.Y);
        }

        return DisplayMonitorHelper.GetPrimaryMonitor();
    }

    private Point PointToScreenDip(Point relativePoint)
    {
        var screenPx = PointToScreen(relativePoint);
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is not null)
        {
            return source.CompositionTarget.TransformFromDevice.Transform(screenPx);
        }

        return screenPx;
    }

    private void ToolbarWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ColorPopup.IsOpen)
            {
                ColorPopup.IsOpen = false;
                e.Handled = true;
                return;
            }

            if (StrokePopup.IsOpen)
            {
                StrokePopup.IsOpen = false;
                e.Handled = true;
                return;
            }

            HideAndRememberPosition();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control
            || Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Z:
                _session.Undo();
                e.Handled = true;
                break;

            case Key.Y:
                _session.Redo();
                e.Handled = true;
                break;

            case Key.C:
                _captureService.CopyMonitorToClipboard(GetCursorMonitor());
                e.Handled = true;
                break;

            case Key.S:
                _captureService.SaveMonitorToFile(GetCursorMonitor());
                e.Handled = true;
                break;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideAndRememberPosition();
    }
}
