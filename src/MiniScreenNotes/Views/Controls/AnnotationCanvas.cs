using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Models.Annotations;
using Com.MiniScreenNotes.Services;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfKey = System.Windows.Input.Key;
using WpfKeyboard = System.Windows.Input.Keyboard;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Com.MiniScreenNotes.Views.Controls;

public sealed class AnnotationCanvas : FrameworkElement
{
    private static readonly WpfBrush TransparentBackgroundBrush = CreateFrozenBrush(WpfColor.FromArgb(1, 0, 0, 0));
    private static readonly WpfBrush WhiteboardBackgroundBrush = CreateFrozenBrush(WpfColor.FromRgb(248, 250, 252));
    private static readonly WpfBrush BlackboardBackgroundBrush = CreateFrozenBrush(WpfColor.FromRgb(30, 41, 59));

    private readonly AnnotationSession _session;
    private readonly string _monitorId;

    private IAnnotationElement? _activeElement;
    private Point _dragStart;
    private bool _isDragging;
    private readonly HashSet<IAnnotationElement> _erasedBatch = [];
    private DrawingGroup? _committedCache;
    private bool _cacheDirty = true;
    private bool _detached;

    public AnnotationCanvas(AnnotationSession session, string monitorId)
    {
        _session = session;
        _monitorId = monitorId;
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = true;

        _session.ElementsChanged += OnSessionElementsChanged;
        _session.StateChanged += OnSessionStateChanged;
    }

    public event Action<Point>? TextInputRequested;
    public event Action? StepCounterReset;

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _session.ElementsChanged -= OnSessionElementsChanged;
        _session.StateChanged -= OnSessionStateChanged;
        _committedCache = null;
        _activeElement = null;
        _erasedBatch.Clear();
    }

    private void OnSessionElementsChanged(object? sender, EventArgs e)
    {
        _cacheDirty = true;
        InvalidateVisual();
    }

    private void OnSessionStateChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // 繪圖背景（透明模式使用 alpha=1 攔截滑鼠；白板/黑板使用實心柔和底色）
        var bgBrush = _session.BackgroundMode switch
        {
            CanvasBackgroundMode.Whiteboard => WhiteboardBackgroundBrush,
            CanvasBackgroundMode.Blackboard => BlackboardBackgroundBrush,
            _ => TransparentBackgroundBrush
        };
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (!_session.AnnotationsVisible)
        {
            return;
        }

        if (_cacheDirty)
        {
            RebuildCommittedCache();
        }

        if (_committedCache is not null)
        {
            dc.DrawDrawing(_committedCache);
        }

        _activeElement?.Render(dc);
    }

    private void RebuildCommittedCache()
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            foreach (var element in _session.Elements)
            {
                if (element.MonitorId == _monitorId && !_erasedBatch.Contains(element))
                {
                    element.Render(dc);
                }
            }
        }

        group.Freeze();
        _committedCache = group;
        _cacheDirty = false;
    }

    private static WpfBrush CreateFrozenBrush(WpfColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var tool = _session.CurrentTool;
        if (tool is ToolType.Cursor or ToolType.Laser or ToolType.Spotlight or ToolType.Magnifier)
        {
            return;
        }

        var pos = e.GetPosition(this);
        _dragStart = pos;

        if (tool == ToolType.Text)
        {
            TextInputRequested?.Invoke(pos);
            e.Handled = true;
            return;
        }

        if (tool == ToolType.StepNumber)
        {
            var radius = Math.Clamp(14.0 + (_session.StrokeWidth * 1.1), 16.0, 32.0);
            var step = new StepNumberAnnotation(_monitorId, pos, _session.NextStepNumber, _session.CurrentColor, radius);
            _session.AddElement(step);
            e.Handled = true;
            return;
        }

        _isDragging = true;
        CaptureMouse();

        switch (tool)
        {
            case ToolType.Pen:
            case ToolType.Highlighter:
                var stroke = new FreehandStroke(_monitorId, _session.CurrentColor, _session.StrokeWidth, tool == ToolType.Highlighter);
                stroke.AddPoint(pos);
                _activeElement = stroke;
                break;

            case ToolType.Line:
            case ToolType.Arrow:
            case ToolType.HollowRectangle:
            case ToolType.FilledRectangle:
            case ToolType.HollowEllipse:
            case ToolType.FilledEllipse:
                _activeElement = new ShapeAnnotation(_monitorId, tool, pos, pos, _session.CurrentColor, _session.StrokeWidth);
                break;

            case ToolType.CheckMark:
            case ToolType.CrossMark:
                _activeElement = new StampAnnotation(_monitorId, tool == ToolType.CheckMark, pos, 44.0);
                break;

            case ToolType.Eraser:
                CommitErasedBatch();
                TryEraseAt(pos);
                break;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        if (_session.CurrentTool == ToolType.StepNumber)
        {
            _session.ResetStepCounter();
            StepCounterReset?.Invoke();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(WpfMouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDragging)
        {
            return;
        }

        var pos = e.GetPosition(this);
        var shiftDown = (WpfKeyboard.Modifiers & WpfModifierKeys.Shift) == WpfModifierKeys.Shift;

        switch (_activeElement)
        {
            case FreehandStroke stroke:
                stroke.AddPoint(pos);
                break;

            case ShapeAnnotation shape:
                shape.EndPoint = shiftDown ? ApplyShiftConstraint(shape.ShapeTool, _dragStart, pos) : pos;
                break;

            case StampAnnotation stamp:
                var dist = (pos - _dragStart).Length;
                if (dist > 12.0)
                {
                    stamp.Size = Math.Clamp(dist * 2.0, 28.0, 240.0);
                }
                break;

            case null when _session.CurrentTool == ToolType.Eraser:
                TryEraseAt(pos);
                break;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();

        if (_activeElement is not null)
        {
            _session.AddElement(_activeElement);
            _activeElement = null;
        }
        else if (_session.CurrentTool == ToolType.Eraser)
        {
            CommitErasedBatch();
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        if (_activeElement is not null)
        {
            _session.AddElement(_activeElement);
            _activeElement = null;
        }

        CommitErasedBatch();
        InvalidateVisual();
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

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
    }

    private void TryEraseAt(Point pos)
    {
        var tolerance = Math.Max(10.0, _session.StrokeWidth * 2.0);
        var changed = false;

        for (var i = _session.Elements.Count - 1; i >= 0; i--)
        {
            var el = _session.Elements[i];
            if (el.MonitorId == _monitorId && !_erasedBatch.Contains(el) && el.HitTest(pos, tolerance))
            {
                _erasedBatch.Add(el);
                changed = true;
            }
        }

        if (changed)
        {
            _cacheDirty = true;
        }
    }

    private void CommitErasedBatch()
    {
        if (_erasedBatch.Count == 0)
        {
            return;
        }

        _session.RemoveElements(_erasedBatch);
        _erasedBatch.Clear();
    }

    private static Point ApplyShiftConstraint(ToolType tool, Point start, Point current)
    {
        var dx = current.X - start.X;
        var dy = current.Y - start.Y;

        if (tool is ToolType.Line or ToolType.Arrow)
        {
            // 鎖定最近 45° 倍數角度（0°, 45°, 90°, 135°, 180°...）
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            var angle = Math.Atan2(dy, dx);
            var snapped = Math.Round(angle / (Math.PI / 4.0)) * (Math.PI / 4.0);
            return new Point(start.X + (Math.Cos(snapped) * length), start.Y + (Math.Sin(snapped) * length));
        }

        // 矩形/橢圓鎖定正方形/正圓
        var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return new Point(
            start.X + (dx >= 0 ? side : -side),
            start.Y + (dy >= 0 ? side : -side));
    }
}
