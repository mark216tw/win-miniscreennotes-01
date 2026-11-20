using System.Windows;
using System.Windows.Interop;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WpfApplication = System.Windows.Application;

namespace Com.MiniScreenNotes.Services;

public sealed class ThemeService : IDisposable
{
    private const string ThemeDictionaryPrefix = "Resources/Themes/";
    private readonly ILogger<ThemeService> _logger;
    private ThemeName? _appliedTheme;
    private bool? _appliedDark;
    private ThemeName _lastTheme;
    private DisplayMode _lastDisplayMode;
    private bool _hasApplied;
    private bool _disposed;

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public bool IsEffectiveDark { get; private set; }

    public void ApplyTheme(ThemeName theme, DisplayMode displayMode)
    {
        _lastTheme = theme;
        _lastDisplayMode = displayMode;
        _hasApplied = true;

        var isDark = displayMode switch
        {
            DisplayMode.Dark => true,
            DisplayMode.Light => false,
            _ => IsWindowsDarkMode()
        };

        IsEffectiveDark = isDark;
        if (_appliedTheme == theme && _appliedDark == isDark)
        {
            return;
        }

        var modeSuffix = isDark ? "Dark" : "Light";
        var uriString = $"pack://application:,,,/MiniScreenNotes;component/{ThemeDictionaryPrefix}{theme}.{modeSuffix}.xaml";

        try
        {
            var app = WpfApplication.Current;
            if (app is null)
            {
                return;
            }

            var merged = app.Resources.MergedDictionaries;
            for (var i = merged.Count - 1; i >= 0; i--)
            {
                var source = merged[i].Source?.OriginalString;
                if (source is not null && source.Contains(ThemeDictionaryPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    merged.RemoveAt(i);
                }
            }

            merged.Add(new ResourceDictionary { Source = new Uri(uriString, UriKind.Absolute) });

            foreach (Window window in app.Windows)
            {
                ApplyTitleBarTheme(window);
            }

            _appliedTheme = theme;
            _appliedDark = isDark;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "套用主題失敗：{Theme} ({Mode})", theme, modeSuffix);
        }
    }

    public void ApplyTitleBarTheme(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            Win32Interop.ApplyDarkTitleBar(hwnd, IsEffectiveDark);
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (!_hasApplied || _lastDisplayMode != DisplayMode.FollowSystem)
        {
            return;
        }

        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color))
        {
            return;
        }

        var app = WpfApplication.Current;
        if (app is null)
        {
            return;
        }

        app.Dispatcher.BeginInvoke(() => ApplyTheme(_lastTheme, _lastDisplayMode));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intVal)
            {
                return intVal == 0;
            }
        }
        catch
        {
            // 無法讀取系統登錄檔時回退為淺色模式
        }

        return false;
    }
}
