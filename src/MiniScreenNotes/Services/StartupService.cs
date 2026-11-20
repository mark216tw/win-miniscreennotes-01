using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Com.MiniScreenNotes.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "MiniScreenNotes";
    private readonly ILogger<StartupService> _logger;

    public StartupService(ILogger<StartupService> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "讀取開機自動啟動狀態失敗");
            return false;
        }
    }

    public bool SetEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return false;
            }

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return false;
                }

                key.SetValue(AppValueName, $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue(AppValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新開機自動啟動設定失敗：{Enable}", enable);
            return false;
        }
    }
}
