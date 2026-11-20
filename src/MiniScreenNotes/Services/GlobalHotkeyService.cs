using System.Windows.Interop;
using Com.MiniScreenNotes.Helpers;
using Microsoft.Extensions.Logging;

namespace Com.MiniScreenNotes.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int TogglePanelHotkeyId = 9001;
    private readonly ILogger<GlobalHotkeyService> _logger;
    private HwndSource? _hwndSource;
    private bool _isRegistered;
    private uint _registeredModifiers;
    private uint _registeredVirtualKey;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger)
    {
        _logger = logger;
    }

    public event EventHandler? HotkeyPressed;

    public bool IsRegistered => _isRegistered;

    public bool Register(uint modifiers, uint virtualKey)
    {
        EnsureMessageWindow();

        if (_isRegistered && modifiers == _registeredModifiers && virtualKey == _registeredVirtualKey)
        {
            return true;
        }

        var hadPrevious = _isRegistered;
        var previousModifiers = _registeredModifiers;
        var previousVirtualKey = _registeredVirtualKey;
        Unregister();

        if (_hwndSource is not null && TryRegisterCore(modifiers, virtualKey))
        {
            _registeredModifiers = modifiers;
            _registeredVirtualKey = virtualKey;
            return true;
        }

        if (hadPrevious && _hwndSource is not null && TryRegisterCore(previousModifiers, previousVirtualKey))
        {
            _registeredModifiers = previousModifiers;
            _registeredVirtualKey = previousVirtualKey;
            _logger.LogWarning("新快捷鍵註冊失敗，已回復原本的快捷鍵");
        }

        return false;
    }

    public void Unregister()
    {
        if (_isRegistered && _hwndSource is not null)
        {
            _ = Win32Interop.UnregisterHotKey(_hwndSource.Handle, TogglePanelHotkeyId);
        }

        _isRegistered = false;
    }

    private bool TryRegisterCore(uint modifiers, uint virtualKey)
    {
        if (_hwndSource is null)
        {
            return false;
        }

        var success = Win32Interop.RegisterHotKey(
            _hwndSource.Handle,
            TogglePanelHotkeyId,
            modifiers | Win32Interop.ModNoRepeat,
            virtualKey);

        _isRegistered = success;
        if (!success)
        {
            _logger.LogWarning("全域快捷鍵註冊失敗：Modifiers={Modifiers}, VK={VK}", modifiers, virtualKey);
        }

        return success;
    }

    private void EnsureMessageWindow()
    {
        if (_hwndSource is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("MiniScreenNotes_HotkeySink")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WmHotkey && wParam.ToInt32() == TogglePanelHotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
    }
}
