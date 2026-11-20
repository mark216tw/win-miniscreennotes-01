using System.Runtime.InteropServices;
using System.Windows;

namespace Com.MiniScreenNotes.Helpers;

public static class DisplayMonitorHelper
{
    private static readonly object SyncRoot = new();
    private static List<MonitorInfo>? _cachedMonitors;

    public static void InvalidateCache()
    {
        lock (SyncRoot)
        {
            _cachedMonitors = null;
        }
    }

    public static List<MonitorInfo> GetAllMonitors()
    {
        lock (SyncRoot)
        {
            return _cachedMonitors ??= EnumerateMonitors();
        }
    }

    private static List<MonitorInfo> EnumerateMonitors()
    {
        var result = new List<MonitorInfo>();

        Win32Interop.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref Win32Interop.RectStruct _, IntPtr _) =>
        {
            var info = new Win32Interop.MonitorInfoEx
            {
                Size = Marshal.SizeOf<Win32Interop.MonitorInfoEx>()
            };

            if (Win32Interop.GetMonitorInfo(hMonitor, ref info))
            {
                double scaleX = 1.0;
                double scaleY = 1.0;
                if (Win32Interop.GetDpiForMonitor(hMonitor, Win32Interop.MdtEffectiveDpi, out var dpiX, out var dpiY) == 0
                    && dpiX > 0 && dpiY > 0)
                {
                    scaleX = dpiX / 96.0;
                    scaleY = dpiY / 96.0;
                }

                result.Add(new MonitorInfo
                {
                    DeviceName = string.IsNullOrWhiteSpace(info.DeviceName) ? $"DISPLAY_{result.Count + 1}" : info.DeviceName,
                    IsPrimary = (info.Flags & 1) != 0,
                    PhysicalBounds = info.Monitor,
                    PhysicalWorkArea = info.WorkArea,
                    DpiScaleX = scaleX,
                    DpiScaleY = scaleY
                });
            }

            return true;
        }, IntPtr.Zero);

        if (result.Count == 0)
        {
            var w = (int)SystemParameters.PrimaryScreenWidth;
            var h = (int)SystemParameters.PrimaryScreenHeight;
            result.Add(new MonitorInfo
            {
                DeviceName = "PRIMARY",
                IsPrimary = true,
                PhysicalBounds = new Win32Interop.RectStruct { Left = 0, Top = 0, Right = w, Bottom = h },
                PhysicalWorkArea = new Win32Interop.RectStruct { Left = 0, Top = 0, Right = w, Bottom = h },
                DpiScaleX = 1.0,
                DpiScaleY = 1.0
            });
        }

        return result;
    }

    public static MonitorInfo GetMonitorContainingDipPoint(Point dipPoint)
    {
        var monitors = GetAllMonitors();
        foreach (var monitor in monitors)
        {
            if (monitor.DipBounds.Contains(dipPoint))
            {
                return monitor;
            }
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    public static MonitorInfo GetMonitorContainingPhysicalPoint(int x, int y)
    {
        var monitors = GetAllMonitors();
        foreach (var monitor in monitors)
        {
            var bounds = monitor.PhysicalBounds;
            if (x >= bounds.Left && x < bounds.Right && y >= bounds.Top && y < bounds.Bottom)
            {
                return monitor;
            }
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    public static MonitorInfo GetPrimaryMonitor()
    {
        var monitors = GetAllMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }
}
