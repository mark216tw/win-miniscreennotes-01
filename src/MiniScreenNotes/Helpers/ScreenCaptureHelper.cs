using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Com.MiniScreenNotes.Helpers;

public static class ScreenCaptureHelper
{
    public static BitmapSource? CapturePhysicalRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var screenDc = Win32Interop.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return null;
        }

        var memDc = IntPtr.Zero;
        var hBitmap = IntPtr.Zero;
        var oldObj = IntPtr.Zero;

        try
        {
            memDc = Win32Interop.CreateCompatibleDC(screenDc);
            hBitmap = Win32Interop.CreateCompatibleBitmap(screenDc, width, height);
            oldObj = Win32Interop.SelectObject(memDc, hBitmap);

            Win32Interop.BitBlt(
                memDc,
                0,
                0,
                width,
                height,
                screenDc,
                x,
                y,
                Win32Interop.Srccopy | Win32Interop.CaptureBlt);

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            if (oldObj != IntPtr.Zero && memDc != IntPtr.Zero)
            {
                _ = Win32Interop.SelectObject(memDc, oldObj);
            }

            if (hBitmap != IntPtr.Zero)
            {
                _ = Win32Interop.DeleteObject(hBitmap);
            }

            if (memDc != IntPtr.Zero)
            {
                _ = Win32Interop.DeleteDC(memDc);
            }

            _ = Win32Interop.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
