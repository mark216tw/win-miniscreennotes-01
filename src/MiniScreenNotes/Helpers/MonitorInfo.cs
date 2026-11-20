using System.Windows;

namespace Com.MiniScreenNotes.Helpers;

public sealed class MonitorInfo
{
    public required string DeviceName { get; init; }
    public required bool IsPrimary { get; init; }
    public required Win32Interop.RectStruct PhysicalBounds { get; init; }
    public required Win32Interop.RectStruct PhysicalWorkArea { get; init; }
    public required double DpiScaleX { get; init; }
    public required double DpiScaleY { get; init; }

    public Rect DipBounds => new(
        PhysicalBounds.Left / DpiScaleX,
        PhysicalBounds.Top / DpiScaleY,
        PhysicalBounds.Width / DpiScaleX,
        PhysicalBounds.Height / DpiScaleY);

    public Rect DipWorkArea => new(
        PhysicalWorkArea.Left / DpiScaleX,
        PhysicalWorkArea.Top / DpiScaleY,
        PhysicalWorkArea.Width / DpiScaleX,
        PhysicalWorkArea.Height / DpiScaleY);
}
