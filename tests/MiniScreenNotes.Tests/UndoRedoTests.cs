using System.Windows;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Models.Annotations;
using Com.MiniScreenNotes.Services;
using Xunit;
using WpfColor = System.Windows.Media.Color;

namespace Com.MiniScreenNotes.Tests;

public sealed class UndoRedoTests
{
    [Fact]
    public void AddAndUndoRedo_ShouldMaintainElementOrderAndStacks()
    {
        var session = new AnnotationSession();
        var stroke1 = new FreehandStroke("M1", WpfColor.FromRgb(255, 0, 0), 4, false);
        stroke1.AddPoint(new Point(10, 10));
        stroke1.AddPoint(new Point(50, 50));

        var shape = new ShapeAnnotation("M1", ToolType.Arrow, new Point(0, 0), new Point(100, 100), WpfColor.FromRgb(0, 0, 255), 4);

        session.AddElement(stroke1);
        session.AddElement(shape);

        Assert.Equal(2, session.Elements.Count);
        Assert.True(session.CanUndo);
        Assert.False(session.CanRedo);

        session.Undo();
        Assert.Single(session.Elements);
        Assert.True(session.CanRedo);

        session.Redo();
        Assert.Equal(2, session.Elements.Count);
    }

    [Fact]
    public void ClearAll_ShouldBeUndoable()
    {
        var session = new AnnotationSession();
        session.AddElement(new StampAnnotation("M1", true, new Point(40, 40), 40));
        session.AddElement(new StampAnnotation("M1", false, new Point(80, 80), 40));

        session.ClearAll();
        Assert.Empty(session.Elements);

        session.Undo();
        Assert.Equal(2, session.Elements.Count);
    }

    [Fact]
    public void StepNumber_ShouldAutoIncrementAndRollbackOnUndo()
    {
        var session = new AnnotationSession();
        Assert.Equal(1, session.NextStepNumber);

        session.AddElement(new StepNumberAnnotation("M1", new Point(10, 10), session.NextStepNumber, WpfColor.FromRgb(239, 68, 68), 18));
        Assert.Equal(2, session.NextStepNumber);

        session.AddElement(new StepNumberAnnotation("M1", new Point(60, 60), session.NextStepNumber, WpfColor.FromRgb(239, 68, 68), 18));
        Assert.Equal(3, session.NextStepNumber);

        session.Undo();
        Assert.Equal(2, session.NextStepNumber);

        session.Redo();
        Assert.Equal(3, session.NextStepNumber);
    }

    [Fact]
    public void RemoveElements_ShouldRestoreExactPositionOnUndo()
    {
        var session = new AnnotationSession();
        var a = new StampAnnotation("M1", true, new Point(10, 10), 40);
        var b = new StampAnnotation("M1", false, new Point(50, 50), 40);
        var c = new StampAnnotation("M1", true, new Point(90, 90), 40);

        session.AddElement(a);
        session.AddElement(b);
        session.AddElement(c);

        session.RemoveElements([b]);
        Assert.Equal(2, session.Elements.Count);
        Assert.Same(a, session.Elements[0]);
        Assert.Same(c, session.Elements[1]);

        session.Undo();
        Assert.Equal(3, session.Elements.Count);
        Assert.Same(b, session.Elements[1]);
    }

    [Fact]
    public void CursorMode_ShouldBeIndependentFromSelectedAnnotationTool()
    {
        var session = new AnnotationSession();
        Assert.True(session.IsCursorMode);
        Assert.Equal(ToolType.Pen, session.SelectedAnnotationTool);
        Assert.Equal(ToolType.Cursor, session.CurrentTool);

        // 選擇標註工具時自動關閉游標模式並切換為該工具
        session.SelectAnnotationTool(ToolType.Arrow);
        Assert.False(session.IsCursorMode);
        Assert.Equal(ToolType.Arrow, session.SelectedAnnotationTool);
        Assert.Equal(ToolType.Arrow, session.CurrentTool);

        // 重複點擊同一標註工具仍保持 ON 狀態
        session.SelectAnnotationTool(ToolType.Arrow);
        Assert.False(session.IsCursorMode);
        Assert.Equal(ToolType.Arrow, session.SelectedAnnotationTool);
        Assert.Equal(ToolType.Arrow, session.CurrentTool);

        // 切換游標模式 ON 不影響已選標註工具
        session.ToggleCursorMode();
        Assert.True(session.IsCursorMode);
        Assert.Equal(ToolType.Arrow, session.SelectedAnnotationTool);
        Assert.Equal(ToolType.Cursor, session.CurrentTool);

        // 再切回游標模式 OFF 立即恢復原標註工具
        session.ToggleCursorMode();
        Assert.False(session.IsCursorMode);
        Assert.Equal(ToolType.Arrow, session.SelectedAnnotationTool);
        Assert.Equal(ToolType.Arrow, session.CurrentTool);
    }
}
