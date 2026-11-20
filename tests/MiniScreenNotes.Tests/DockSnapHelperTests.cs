using System.Windows;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;
using Xunit;

namespace Com.MiniScreenNotes.Tests;

public sealed class DockSnapHelperTests
{
    private static readonly Rect PrimaryMonitor = new(0, 0, 1920, 1080);

    [Fact]
    public void HorizontalPanel_ShouldOnlySnapToTopOrBottom()
    {
        var nearTop = new Rect(400, 10, 500, 56);
        var nearBottom = new Rect(400, 1010, 500, 56);
        var nearLeft = new Rect(5, 400, 500, 56);

        Assert.Equal(DockEdge.Top, DockSnapHelper.DetectDockEdge(nearTop, PrimaryMonitor, PanelOrientation.Horizontal));
        Assert.Equal(DockEdge.Bottom, DockSnapHelper.DetectDockEdge(nearBottom, PrimaryMonitor, PanelOrientation.Horizontal));
        Assert.Equal(DockEdge.None, DockSnapHelper.DetectDockEdge(nearLeft, PrimaryMonitor, PanelOrientation.Horizontal));
    }

    [Fact]
    public void VerticalPanel_ShouldOnlySnapToLeftOrRight()
    {
        var nearLeft = new Rect(8, 200, 56, 480);
        var nearRight = new Rect(1855, 200, 56, 480);
        var nearTop = new Rect(400, 5, 56, 480);

        Assert.Equal(DockEdge.Left, DockSnapHelper.DetectDockEdge(nearLeft, PrimaryMonitor, PanelOrientation.Vertical));
        Assert.Equal(DockEdge.Right, DockSnapHelper.DetectDockEdge(nearRight, PrimaryMonitor, PanelOrientation.Vertical));
        Assert.Equal(DockEdge.None, DockSnapHelper.DetectDockEdge(nearTop, PrimaryMonitor, PanelOrientation.Vertical));
    }

    [Fact]
    public void ClampToAvailableMonitors_ShouldBringOffScreenPanelBackInside()
    {
        var offScreen = new Point(3500, 1500);
        var panelSize = new Size(400, 60);
        var clamped = DockSnapHelper.ClampToAvailableMonitors(offScreen, panelSize, [PrimaryMonitor]);

        Assert.InRange(clamped.X, 0, 1920 - 400);
        Assert.InRange(clamped.Y, 0, 1080 - 60);
    }

    [Theory]
    [InlineData(15, 12, PanelOrientation.Horizontal, 12, 2)]
    [InlineData(15, 9, PanelOrientation.Horizontal, 9, 2)]
    [InlineData(15, 6, PanelOrientation.Horizontal, 6, 3)]
    [InlineData(15, 6, PanelOrientation.Vertical, 3, 6)]
    [InlineData(5, 9, PanelOrientation.Vertical, 1, 5)]
    public void CalculateGridDimensions_ShouldWrapLinesCorrectly(
        int count,
        int maxPerLine,
        PanelOrientation orientation,
        int expectedCols,
        int expectedRows)
    {
        var (cols, rows) = DockSnapHelper.CalculateGridDimensions(count, maxPerLine, orientation);
        Assert.Equal(expectedCols, cols);
        Assert.Equal(expectedRows, rows);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(8, 8, 0)]
    [InlineData(9, 0, 1)]
    [InlineData(11, 2, 1)]
    public void GetGridCell_Vertical_ShouldFillColumnsBeforeWrapping(int index, int expectedRow, int expectedColumn)
    {
        var (cols, rows) = DockSnapHelper.CalculateGridDimensions(12, 9, PanelOrientation.Vertical);

        var (row, column) = DockSnapHelper.GetGridCell(index, cols, rows, PanelOrientation.Vertical);

        Assert.Equal(expectedRow, row);
        Assert.Equal(expectedColumn, column);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(5, 0, 5)]
    [InlineData(6, 1, 0)]
    [InlineData(14, 2, 2)]
    public void GetGridCell_Horizontal_ShouldFillRowsBeforeWrapping(int index, int expectedRow, int expectedColumn)
    {
        var (cols, rows) = DockSnapHelper.CalculateGridDimensions(15, 6, PanelOrientation.Horizontal);

        var (row, column) = DockSnapHelper.GetGridCell(index, cols, rows, PanelOrientation.Horizontal);

        Assert.Equal(expectedRow, row);
        Assert.Equal(expectedColumn, column);
    }

    [Fact]
    public void GetSlideOffset_DockedEdges_ShouldSlideFullPanelOutOfEdge()
    {
        var panelSize = new Size(320, 56);
        var pos = new Point(400, 0);

        Assert.Equal(new Vector(0, -56), DockSnapHelper.GetSlideOffset(DockEdge.Top, PanelOrientation.Horizontal, panelSize, PrimaryMonitor, pos));
        Assert.Equal(new Vector(0, 56), DockSnapHelper.GetSlideOffset(DockEdge.Bottom, PanelOrientation.Horizontal, panelSize, PrimaryMonitor, pos));
        Assert.Equal(new Vector(-320, 0), DockSnapHelper.GetSlideOffset(DockEdge.Left, PanelOrientation.Vertical, panelSize, PrimaryMonitor, pos));
        Assert.Equal(new Vector(320, 0), DockSnapHelper.GetSlideOffset(DockEdge.Right, PanelOrientation.Vertical, panelSize, PrimaryMonitor, pos));
    }

    [Fact]
    public void GetSlideOffset_FloatingPanel_ShouldSlideTowardNearestSideByOrientation()
    {
        var horizontalSize = new Size(320, 56);
        var upperHalf = new Point(400, 120);
        var lowerHalf = new Point(400, 820);

        Assert.Equal(
            new Vector(0, -24),
            DockSnapHelper.GetSlideOffset(DockEdge.None, PanelOrientation.Horizontal, horizontalSize, PrimaryMonitor, upperHalf, 24));
        Assert.Equal(
            new Vector(0, 24),
            DockSnapHelper.GetSlideOffset(DockEdge.None, PanelOrientation.Horizontal, horizontalSize, PrimaryMonitor, lowerHalf, 24));

        var verticalSize = new Size(56, 320);
        var leftHalf = new Point(120, 300);
        var rightHalf = new Point(1600, 300);

        Assert.Equal(
            new Vector(-24, 0),
            DockSnapHelper.GetSlideOffset(DockEdge.None, PanelOrientation.Vertical, verticalSize, PrimaryMonitor, leftHalf, 24));
        Assert.Equal(
            new Vector(24, 0),
            DockSnapHelper.GetSlideOffset(DockEdge.None, PanelOrientation.Vertical, verticalSize, PrimaryMonitor, rightHalf, 24));
    }
}
