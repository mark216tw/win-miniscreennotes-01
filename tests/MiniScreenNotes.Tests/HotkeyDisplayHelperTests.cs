using Com.MiniScreenNotes.Helpers;
using Xunit;

namespace Com.MiniScreenNotes.Tests;

public sealed class HotkeyDisplayHelperTests
{
    [Fact]
    public void Format_ShouldJoinModifiersAndKeyName()
    {
        var result = HotkeyDisplayHelper.Format(
            Win32Interop.ModControl | Win32Interop.ModAlt,
            0x41);

        Assert.Equal("Ctrl+Alt+A", result);
    }

    [Fact]
    public void Format_ShouldSupportFunctionKeysAndShift()
    {
        var result = HotkeyDisplayHelper.Format(
            Win32Interop.ModControl | Win32Interop.ModShift,
            0x7B);

        Assert.Equal("Ctrl+Shift+F12", result);
    }

    [Fact]
    public void GetKeyName_ShouldFallbackToAForUnknownVirtualKey()
    {
        Assert.Equal("A", HotkeyDisplayHelper.GetKeyName(0x00));
    }

    [Fact]
    public void Format_WithoutModifiers_ShouldReturnKeyNameOnly()
    {
        Assert.Equal("B", HotkeyDisplayHelper.Format(0, 0x42));
    }
}
