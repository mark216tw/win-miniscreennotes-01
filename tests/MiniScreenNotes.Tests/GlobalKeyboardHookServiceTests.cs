using Com.MiniScreenNotes.Services;
using Xunit;

namespace Com.MiniScreenNotes.Tests;

public sealed class GlobalKeyboardHookServiceTests
{
    [Fact]
    public void ShouldCaptureEscape_WhenToolActiveAndNoBlockers_ReturnsTrue()
    {
        Assert.True(GlobalKeyboardHookService.ShouldCaptureEscape(
            toolbarVisible: true,
            isCursorMode: false,
            isTextInputActive: false,
            isPopupOpen: false,
            isDialogOpen: false));
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, false, false, true)]
    public void ShouldCaptureEscape_WhenAnyBlockerActive_ReturnsFalse(
        bool toolbarVisible,
        bool isCursorMode,
        bool isTextInputActive,
        bool isPopupOpen,
        bool isDialogOpen)
    {
        Assert.False(GlobalKeyboardHookService.ShouldCaptureEscape(
            toolbarVisible,
            isCursorMode,
            isTextInputActive,
            isPopupOpen,
            isDialogOpen));
    }
}

