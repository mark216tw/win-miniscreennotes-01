using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Services;
using Xunit;

namespace Com.MiniScreenNotes.Tests;

public sealed class OverlayManagerTests
{
    [Fact]
    public void ShouldKeepOverlaysAlive_WhenHidden_ReturnsFalseEvenWithAnnotationsAndTool()
    {
        Assert.False(OverlayManager.ShouldKeepOverlaysAlive(
            overlaysVisible: false,
            isCursorMode: false,
            backgroundMode: CanvasBackgroundMode.Whiteboard,
            annotationsVisible: true,
            elementCount: 5));
    }

    [Fact]
    public void ShouldKeepOverlaysAlive_WhenCursorModeTransparentAndEmpty_ReturnsFalse()
    {
        Assert.False(OverlayManager.ShouldKeepOverlaysAlive(
            overlaysVisible: true,
            isCursorMode: true,
            backgroundMode: CanvasBackgroundMode.Transparent,
            annotationsVisible: true,
            elementCount: 0));
    }

    [Fact]
    public void ShouldKeepOverlaysAlive_WhenCursorModeTransparentAndAnnotationsHidden_ReturnsFalse()
    {
        Assert.False(OverlayManager.ShouldKeepOverlaysAlive(
            overlaysVisible: true,
            isCursorMode: true,
            backgroundMode: CanvasBackgroundMode.Transparent,
            annotationsVisible: false,
            elementCount: 3));
    }

    [Fact]
    public void ShouldKeepOverlaysAlive_WhenCursorModeWithVisibleAnnotations_ReturnsTrue()
    {
        Assert.True(OverlayManager.ShouldKeepOverlaysAlive(
            overlaysVisible: true,
            isCursorMode: true,
            backgroundMode: CanvasBackgroundMode.Transparent,
            annotationsVisible: true,
            elementCount: 1));
    }

    [Fact]
    public void ShouldKeepOverlaysAlive_WhenWhiteboardOrBlackboardActive_ReturnsTrue()
    {
        Assert.True(OverlayManager.ShouldKeepOverlaysAlive(
            overlaysVisible: true,
            isCursorMode: true,
            backgroundMode: CanvasBackgroundMode.Whiteboard,
            annotationsVisible: false,
            elementCount: 0));

        Assert.True(OverlayManager.ShouldKeepOverlaysAlive(
            overlaysVisible: true,
            isCursorMode: true,
            backgroundMode: CanvasBackgroundMode.Blackboard,
            annotationsVisible: false,
            elementCount: 0));
    }

    [Fact]
    public void ShouldKeepOverlaysAlive_WhenDrawingOrPresentationToolActive_ReturnsTrue()
    {
        Assert.True(OverlayManager.ShouldKeepOverlaysAlive(
            overlaysVisible: true,
            isCursorMode: false,
            backgroundMode: CanvasBackgroundMode.Transparent,
            annotationsVisible: true,
            elementCount: 0));
    }
}
