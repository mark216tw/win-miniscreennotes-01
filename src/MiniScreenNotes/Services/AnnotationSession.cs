using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Models.Annotations;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace Com.MiniScreenNotes.Services;

public sealed class AnnotationSession
{
    private readonly List<IAnnotationElement> _elements = [];
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    private bool _isCursorMode = true;
    private ToolType _selectedAnnotationTool = ToolType.Pen;
    private WpfColor _currentColor = WpfColor.FromRgb(239, 68, 68);
    private double _strokeWidth = 4.0;
    private bool _annotationsVisible = true;
    private CanvasBackgroundMode _backgroundMode = CanvasBackgroundMode.Transparent;
    private int _stepCounter = 1;

    public IReadOnlyList<IAnnotationElement> Elements => _elements;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int NextStepNumber => _stepCounter;

    public event EventHandler? ElementsChanged;
    public event EventHandler? StateChanged;

    public bool IsCursorMode
    {
        get => _isCursorMode;
        set
        {
            if (_isCursorMode != value)
            {
                _isCursorMode = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public ToolType SelectedAnnotationTool
    {
        get => _selectedAnnotationTool;
        set
        {
            if (value == ToolType.Cursor)
            {
                return;
            }

            if (_selectedAnnotationTool != value)
            {
                _selectedAnnotationTool = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public ToolType CurrentTool
    {
        get => _isCursorMode ? ToolType.Cursor : _selectedAnnotationTool;
        set
        {
            if (value == ToolType.Cursor)
            {
                IsCursorMode = true;
                return;
            }

            SelectAnnotationTool(value);
        }
    }

    public void ToggleCursorMode()
    {
        IsCursorMode = !_isCursorMode;
    }

    public void SelectAnnotationTool(ToolType tool)
    {
        if (tool == ToolType.Cursor)
        {
            ToggleCursorMode();
            return;
        }

        var changed = _selectedAnnotationTool != tool || _isCursorMode;
        _selectedAnnotationTool = tool;
        _isCursorMode = false;
        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public WpfColor CurrentColor
    {
        get => _currentColor;
        set
        {
            if (_currentColor != value)
            {
                _currentColor = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double StrokeWidth
    {
        get => _strokeWidth;
        set
        {
            var clamped = Math.Clamp(value, 1.0, 30.0);
            if (Math.Abs(_strokeWidth - clamped) > 0.01)
            {
                _strokeWidth = clamped;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool AnnotationsVisible
    {
        get => _annotationsVisible;
        set
        {
            if (_annotationsVisible != value)
            {
                _annotationsVisible = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
                ElementsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public CanvasBackgroundMode BackgroundMode
    {
        get => _backgroundMode;
        set
        {
            if (_backgroundMode != value)
            {
                _backgroundMode = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void InitializeFromConfig(AppConfig config)
    {
        try
        {
            if (WpfColorConverter.ConvertFromString(config.SelectedColorHex) is WpfColor color)
            {
                _currentColor = color;
            }
        }
        catch
        {
            // 使用預設色
        }

        _strokeWidth = Math.Clamp(config.StrokeWidth, 1.0, 30.0);
    }

    public void AddElement(IAnnotationElement element)
    {
        var action = new AddAnnotationAction(element);
        action.Execute(_elements);
        _undoStack.Push(action);
        _redoStack.Clear();

        if (element is StepNumberAnnotation step && step.Number >= _stepCounter)
        {
            _stepCounter = step.Number + 1;
        }

        NotifyAllChanged();
    }

    public void RemoveElements(IEnumerable<IAnnotationElement> toRemove)
    {
        var indexed = new List<(int Index, IAnnotationElement Element)>();
        foreach (var item in toRemove)
        {
            var idx = _elements.IndexOf(item);
            if (idx >= 0)
            {
                indexed.Add((idx, item));
            }
        }

        if (indexed.Count == 0)
        {
            return;
        }

        var action = new RemoveAnnotationsAction(indexed);
        action.Execute(_elements);
        _undoStack.Push(action);
        _redoStack.Clear();
        RecalculateStepCounter();
        NotifyAllChanged();
    }

    public void ClearAll()
    {
        if (_elements.Count == 0)
        {
            return;
        }

        var action = new ClearAnnotationsAction(_elements);
        action.Execute(_elements);
        _undoStack.Push(action);
        _redoStack.Clear();
        _stepCounter = 1;
        NotifyAllChanged();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var action = _undoStack.Pop();
        action.Undo(_elements);
        _redoStack.Push(action);
        RecalculateStepCounter();
        NotifyAllChanged();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var action = _redoStack.Pop();
        action.Execute(_elements);
        _undoStack.Push(action);
        RecalculateStepCounter();
        NotifyAllChanged();
    }

    public void ResetStepCounter()
    {
        _stepCounter = 1;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CycleBackgroundMode()
    {
        BackgroundMode = BackgroundMode switch
        {
            CanvasBackgroundMode.Transparent => CanvasBackgroundMode.Whiteboard,
            CanvasBackgroundMode.Whiteboard => CanvasBackgroundMode.Blackboard,
            _ => CanvasBackgroundMode.Transparent
        };
    }

    private void RecalculateStepCounter()
    {
        var maxStep = _elements.OfType<StepNumberAnnotation>().Select(s => s.Number).DefaultIfEmpty(0).Max();
        _stepCounter = maxStep + 1;
    }

    private void NotifyAllChanged()
    {
        ElementsChanged?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
