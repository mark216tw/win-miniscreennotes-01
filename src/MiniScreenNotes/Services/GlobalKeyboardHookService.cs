using System.Runtime.InteropServices;
using Com.MiniScreenNotes.Helpers;
using Microsoft.Extensions.Logging;
using WpfApplication = System.Windows.Application;

namespace Com.MiniScreenNotes.Services;

public sealed class GlobalKeyboardHookService : IDisposable
{
    private readonly ILogger<GlobalKeyboardHookService> _logger;
    private readonly Win32Interop.LowLevelKeyboardProc _hookProc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _swallowNextEscapeUp;
    private bool _disposed;

    public GlobalKeyboardHookService(ILogger<GlobalKeyboardHookService> logger)
    {
        _logger = logger;
        _hookProc = HookCallback;
    }

    public Func<bool>? ShouldHandleEscape { get; set; }
    public event EventHandler? EscapePressed;

    public static bool ShouldCaptureEscape(
        bool toolbarVisible,
        bool isCursorMode,
        bool isTextInputActive,
        bool isPopupOpen,
        bool isDialogOpen) =>
        toolbarVisible
        && !isCursorMode
        && !isTextInputActive
        && !isPopupOpen
        && !isDialogOpen;

    public void Start()
    {
        if (_disposed || _hookId != IntPtr.Zero)
        {
            return;
        }

        var moduleHandle = Win32Interop.GetModuleHandle(null);
        _hookId = Win32Interop.SetWindowsHookEx(Win32Interop.WhKeyboardLl, _hookProc, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
        {
            _logger.LogWarning("安裝全域鍵盤掛鉤失敗，Esc 將只在視窗取得焦點時運作。");
        }
    }

    public void Stop()
    {
        _swallowNextEscapeUp = false;
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
            var isKeyDown = msg is Win32Interop.WmKeyDown or Win32Interop.WmSysKeyDown;
            var isKeyUp = msg is Win32Interop.WmKeyUp or Win32Interop.WmSysKeyUp;

            if ((isKeyDown || isKeyUp)
                && Marshal.PtrToStructure<Win32Interop.KeyboardHookStruct>(lParam).VirtualKeyCode == Win32Interop.VkEscape)
            {
                if (isKeyDown && ShouldHandleEscape?.Invoke() == true)
                {
                    _swallowNextEscapeUp = true;
                    WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
                        EscapePressed?.Invoke(this, EventArgs.Empty));
                    return new IntPtr(1);
                }

                if (isKeyUp && _swallowNextEscapeUp)
                {
                    _swallowNextEscapeUp = false;
                    return new IntPtr(1);
                }
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
