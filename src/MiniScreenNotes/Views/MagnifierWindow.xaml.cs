using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Com.MiniScreenNotes.Helpers;
using Microsoft.Win32;

namespace Com.MiniScreenNotes.Views;

public partial class MagnifierWindow : Window
{
    private const double MinZoom = 1.0;
    private const double MaxZoom = 10.0;
    private const double MinLensDipDiameter = 200.0;
    private const double MaxLensDipDiameter = 500.0;
    private const double DefaultLensDipDiameter = 280.0;
    private const double BorderThicknessDip = 3.0;
    private const double ShadowPaddingDip = 18.0;
    private const int MinFrameIntervalMs = 16;

    private readonly Stopwatch _frameClock = Stopwatch.StartNew();

    private double _zoomFactor = 2.5;
    private double _lensDipDiameter = DefaultLensDipDiameter;
    private bool _isRunning;
    private IntPtr _hwnd;
    private List<MonitorInfo> _monitors = [];

    private int _bufferSize;
    private IntPtr _memDc;
    private IntPtr _dibBitmap;
    private IntPtr _oldObj;
    private IntPtr _dibBits;
    private WriteableBitmap? _writeableBitmap;

    private int _lastPhysLeft = int.MinValue;
    private int _lastPhysTop = int.MinValue;
    private int _lastCaptureSrcX = int.MinValue;
    private int _lastCaptureSrcY = int.MinValue;
    private int _lastCaptureSize = int.MinValue;
    private int _lastCursorX = int.MinValue;
    private int _lastCursorY = int.MinValue;
    private int _lensConfigVersion;
    private int _lastRenderedConfigVersion = -1;
    private long _lastFrameMs = -MinFrameIntervalMs;
    private bool _pendingReveal;

    public MagnifierWindow(double initialZoom = 2.5, double initialDiameter = DefaultLensDipDiameter)
    {
        InitializeComponent();

        _zoomFactor = Math.Clamp(initialZoom, MinZoom, MaxZoom);
        _lensDipDiameter = Math.Clamp(initialDiameter, MinLensDipDiameter, MaxLensDipDiameter);

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.SetToolWindowStyle(_hwnd, noActivate: true);
            Win32Interop.SetClickThrough(_hwnd, true);
            Win32Interop.TryExcludeFromCapture(_hwnd, true);
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        ApplyLensDimensions();
    }

    public double ZoomFactor => _zoomFactor;
    public double LensDipDiameter => _lensDipDiameter;

    public void Start()
    {
        _monitors = DisplayMonitorHelper.GetAllMonitors();
        ApplyScalingMode();
        UpdateBadge();

        // 先以透明狀態顯示並完成擷取與定位，避免第一幀閃現於預設位置的鏡頭陰影
        if (!IsVisible)
        {
            Opacity = 0.0;
            _pendingReveal = true;
            Show();
        }

        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
        }

        _lastPhysLeft = int.MinValue;
        _lastPhysTop = int.MinValue;
        _lastCaptureSrcX = int.MinValue;
        _lastCaptureSrcY = int.MinValue;
        _lastCaptureSize = int.MinValue;
        _lastCursorX = int.MinValue;
        _lastCursorY = int.MinValue;
        _lastRenderedConfigVersion = -1;
        _lastFrameMs = -MinFrameIntervalMs;

        if (!_isRunning)
        {
            _isRunning = true;
            CompositionTarget.Rendering += OnCompositionTargetRendering;
        }

        UpdateLens();
        BringToTopMost();

        RevealIfReady();
    }

    public void Stop()
    {
        if (_isRunning)
        {
            _isRunning = false;
            CompositionTarget.Rendering -= OnCompositionTargetRendering;
        }

        _pendingReveal = false;

        if (IsVisible)
        {
            Hide();
        }

        ReleaseCaptureResources();
    }

    public void BringToTopMost()
    {
        if (!IsVisible)
        {
            return;
        }

        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
        }

        Win32Interop.BringToTopMostNoActivate(_hwnd);
    }

    public void AdjustZoomByWheel(int wheelDelta)
    {
        var step = _zoomFactor < 3.0 ? 0.5 : 1.0;
        _zoomFactor = Math.Clamp(_zoomFactor + (wheelDelta > 0 ? step : -step), MinZoom, MaxZoom);
        _lensConfigVersion++;
        ApplyScalingMode();
        UpdateBadge();
    }

    public void AdjustDiameterByWheel(int wheelDelta)
    {
        _lensDipDiameter = Math.Clamp(
            _lensDipDiameter + (wheelDelta > 0 ? 30.0 : -30.0),
            MinLensDipDiameter,
            MaxLensDipDiameter);
        _lensConfigVersion++;
        ApplyLensDimensions();
        _lastPhysLeft = int.MinValue;
        _lastPhysTop = int.MinValue;
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Stop();
        base.OnClosed(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        DisplayMonitorHelper.InvalidateCache();
        _monitors = DisplayMonitorHelper.GetAllMonitors();
        _lastCursorX = int.MinValue;
        _lastCursorY = int.MinValue;
        _lastRenderedConfigVersion = -1;
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        if (!_isRunning || !IsVisible)
        {
            return;
        }

        var nowMs = _frameClock.ElapsedMilliseconds;
        if (nowMs - _lastFrameMs < MinFrameIntervalMs)
        {
            return;
        }

        _lastFrameMs = nowMs;
        UpdateLens();
        RevealIfReady();
    }

    private void RevealIfReady()
    {
        if (!_pendingReveal || _lastPhysLeft == int.MinValue)
        {
            return;
        }

        _pendingReveal = false;
        Opacity = 1.0;
    }

    private void ApplyScalingMode()
    {
        RenderOptions.SetBitmapScalingMode(
            LensImage,
            _zoomFactor >= 3.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
    }

    private void ApplyLensDimensions()
    {
        var borderDiameter = _lensDipDiameter + (BorderThicknessDip * 2.0);
        var radius = _lensDipDiameter / 2.0;

        Width = borderDiameter + ShadowPaddingDip;
        Height = borderDiameter + ShadowPaddingDip;

        LensBorder.Width = borderDiameter;
        LensBorder.Height = borderDiameter;
        LensBorder.CornerRadius = new CornerRadius(borderDiameter / 2.0);

        LensClipGeometry.Center = new Point(radius, radius);
        LensClipGeometry.RadiusX = radius;
        LensClipGeometry.RadiusY = radius;

        CrosshairH.X1 = radius - 8.0;
        CrosshairH.Y1 = radius;
        CrosshairH.X2 = radius + 8.0;
        CrosshairH.Y2 = radius;

        CrosshairV.X1 = radius;
        CrosshairV.Y1 = radius - 8.0;
        CrosshairV.X2 = radius;
        CrosshairV.Y2 = radius + 8.0;
    }

    private void UpdateBadge()
    {
        ZoomBadgeText.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}x", _zoomFactor);
    }

    private void UpdateLens()
    {
        if (!Win32Interop.GetCursorPos(out var cursor))
        {
            return;
        }

        if (cursor.X == _lastCursorX
            && cursor.Y == _lastCursorY
            && _lensConfigVersion == _lastRenderedConfigVersion)
        {
            return;
        }

        if (_monitors.Count == 0)
        {
            _monitors = DisplayMonitorHelper.GetAllMonitors();
        }

        var currentMonitor = _monitors.FirstOrDefault(m =>
            cursor.X >= m.PhysicalBounds.Left && cursor.X < m.PhysicalBounds.Right &&
            cursor.Y >= m.PhysicalBounds.Top && cursor.Y < m.PhysicalBounds.Bottom)
            ?? _monitors[0];

        // 依目前螢幕實際 DPI 換算物理直徑，並維持奇數像素寬高讓游標精準對齊中心像素
        var lensPhysicalDiameter = _lensDipDiameter * currentMonitor.DpiScaleX;
        var captureSize = Math.Max(9, (int)Math.Round(lensPhysicalDiameter / _zoomFactor) | 1);
        var half = captureSize / 2;
        var srcX = cursor.X - half;
        var srcY = cursor.Y - half;

        if (srcX != _lastCaptureSrcX || srcY != _lastCaptureSrcY || captureSize != _lastCaptureSize)
        {
            CaptureIntoReusableBuffer(srcX, srcY, captureSize);
            _lastCaptureSrcX = srcX;
            _lastCaptureSrcY = srcY;
            _lastCaptureSize = captureSize;
        }

        // 以單一原子 Win32 SetWindowPos（實體像素、不動 Z-Order）更新位置，消除 X/Y 分段移動與 DIP 捨入抖動
        if (_hwnd != IntPtr.Zero && Win32Interop.GetWindowRect(_hwnd, out var rect))
        {
            var physLeft = cursor.X - (rect.Width / 2);
            var physTop = cursor.Y - (rect.Height / 2);

            if (physLeft != _lastPhysLeft || physTop != _lastPhysTop)
            {
                _lastPhysLeft = physLeft;
                _lastPhysTop = physTop;
                Win32Interop.MoveWindowAtomicNoZOrder(_hwnd, physLeft, physTop);
            }
        }

        _lastCursorX = cursor.X;
        _lastCursorY = cursor.Y;
        _lastRenderedConfigVersion = _lensConfigVersion;
    }

    private void CaptureIntoReusableBuffer(int srcX, int srcY, int captureSize)
    {
        var screenDc = Win32Interop.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (!EnsureCaptureBuffers(screenDc, captureSize))
            {
                return;
            }

            Win32Interop.BitBlt(
                _memDc,
                0,
                0,
                captureSize,
                captureSize,
                screenDc,
                srcX,
                srcY,
                Win32Interop.Srccopy | Win32Interop.CaptureBlt);

            if (_writeableBitmap is not null && _dibBits != IntPtr.Zero)
            {
                var stride = captureSize * 4;
                _writeableBitmap.WritePixels(
                    new Int32Rect(0, 0, captureSize, captureSize),
                    _dibBits,
                    stride * captureSize,
                    stride);
            }
        }
        finally
        {
            _ = Win32Interop.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private bool EnsureCaptureBuffers(IntPtr screenDc, int captureSize)
    {
        if (_bufferSize == captureSize && _memDc != IntPtr.Zero && _dibBitmap != IntPtr.Zero && _writeableBitmap is not null)
        {
            return true;
        }

        ReleaseCaptureResources();

        _memDc = Win32Interop.CreateCompatibleDC(screenDc);
        if (_memDc == IntPtr.Zero)
        {
            return false;
        }

        var bmi = new Win32Interop.BitmapInfo
        {
            Header = new Win32Interop.BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<Win32Interop.BitmapInfoHeader>(),
                Width = captureSize,
                Height = -captureSize,
                Planes = 1,
                BitCount = 32,
                Compression = Win32Interop.BiRgb
            }
        };

        _dibBitmap = Win32Interop.CreateDIBSection(
            screenDc,
            ref bmi,
            Win32Interop.DibRgbColors,
            out _dibBits,
            IntPtr.Zero,
            0);

        if (_dibBitmap == IntPtr.Zero || _dibBits == IntPtr.Zero)
        {
            ReleaseCaptureResources();
            return false;
        }

        _oldObj = Win32Interop.SelectObject(_memDc, _dibBitmap);
        _bufferSize = captureSize;
        _writeableBitmap = new WriteableBitmap(captureSize, captureSize, 96, 96, PixelFormats.Bgr32, null);
        LensImage.Source = _writeableBitmap;
        return true;
    }

    private void ReleaseCaptureResources()
    {
        if (_oldObj != IntPtr.Zero && _memDc != IntPtr.Zero)
        {
            _ = Win32Interop.SelectObject(_memDc, _oldObj);
            _oldObj = IntPtr.Zero;
        }

        if (_dibBitmap != IntPtr.Zero)
        {
            _ = Win32Interop.DeleteObject(_dibBitmap);
            _dibBitmap = IntPtr.Zero;
        }

        if (_memDc != IntPtr.Zero)
        {
            _ = Win32Interop.DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }

        _dibBits = IntPtr.Zero;
        _bufferSize = 0;
        _writeableBitmap = null;
        LensImage.Source = null;
        _lastCaptureSrcX = int.MinValue;
        _lastCaptureSrcY = int.MinValue;
        _lastCaptureSize = int.MinValue;
    }
}
