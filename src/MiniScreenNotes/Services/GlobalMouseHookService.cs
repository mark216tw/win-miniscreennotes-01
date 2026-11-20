using Com.MiniScreenNotes.Helpers;
using Microsoft.Extensions.Logging;
using WpfApplication = System.Windows.Application;

namespace Com.MiniScreenNotes.Services;

public sealed class GlobalMouseHookService : IDisposable
{
    private readonly ILogger<GlobalMouseHookService> _logger;
    private readonly Win32Interop.LowLevelMouseProc _hookProc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _swallowNextMiddleUp;
    private bool _disposed;

    public GlobalMouseHookService(ILogger<GlobalMouseHookService> logger)
    {
        _logger = logger;
        _hookProc = HookCallback;
    }

    public Func<bool>? ShouldHandleMiddleClick { get; set; }
    public event EventHandler? MiddleButtonClicked;

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    public void Start()
    {
        if (_disposed || _hookId != IntPtr.Zero)
        {
            return;
        }

        var moduleHandle = Win32Interop.GetModuleHandle(null);
        _hookId = Win32Interop.SetWindowsHookEx(Win32Interop.WhMouseLl, _hookProc, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
        {
            _logger.LogWarning("安裝全域滑鼠掛鉤失敗，滑鼠中鍵切換游標模式將無法運作。");
        }
    }

    public void Stop()
    {
        _swallowNextMiddleUp = false;
        if (_hookId != IntPtr.Zero)
        {
            _ = Win32Interop.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            if (msg == Win32Interop.WmMButtonDown)
            {
                if (ShouldHandleMiddleClick?.Invoke() == true)
                {
                    _swallowNextMiddleUp = true;
                    WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
                        MiddleButtonClicked?.Invoke(this, EventArgs.Empty));
                    return new IntPtr(1);
                }
            }
            else if (msg == Win32Interop.WmMButtonUp && _swallowNextMiddleUp)
            {
                _swallowNextMiddleUp = false;
                return new IntPtr(1);
            }
        }

        return Win32Interop.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
