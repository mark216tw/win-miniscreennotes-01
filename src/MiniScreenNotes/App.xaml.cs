using System.IO;
using System.Runtime;
using System.Windows;
using System.Windows.Threading;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Resources.Strings;
using Com.MiniScreenNotes.Services;
using Com.MiniScreenNotes.ViewModels;
using Com.MiniScreenNotes.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace Com.MiniScreenNotes;

public partial class App : WpfApplication
{
    private const string SingleInstanceMutexName = "Local\\Com.MiniScreenNotes.SingleInstance";
    private const string ActivateSignalName = "Local\\Com.MiniScreenNotes.ActivateSignal";
    private static readonly TimeSpan IdleMemoryTrimDelay = TimeSpan.FromSeconds(2);

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activationWaitHandle;
    private MemoryTrimHelper? _memoryTrimHelper;
    private IHost? _host;

    private ToolbarWindow? _toolbarWindow;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private ToastWindow? _toastWindow;
    private OverlayManager? _overlayManager;
    private bool _isToolbarVisible;
    private bool _backgroundServicesInitialized;

    protected override void OnStartup(StartupEventArgs e)
    {
        EnableStartupProfileOptimization();
        base.OnStartup(e);

        try
        {
            InitializeApplication(e);
        }
        catch (Exception ex)
        {
            WriteCrashLog("Startup", ex);
            WpfMessageBox.Show(
                $"mini螢幕筆記 啟動失敗：\n{ex.Message}\n\n詳細記錄已寫入 %AppData%\\MiniScreenNotes\\error.log",
                "mini螢幕筆記",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void InitializeApplication(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            TrySignalExistingInstance();
            Shutdown();
            return;
        }

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        });

        builder.Logging.ClearProviders();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services.AddSingleton<ConfigService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<StartupService>();
        builder.Services.AddSingleton<AnnotationSession>();
        builder.Services.AddSingleton<GlobalHotkeyService>();
        builder.Services.AddSingleton<GlobalMouseHookService>();
        builder.Services.AddSingleton<GlobalKeyboardHookService>();
        builder.Services.AddSingleton<ScreenCaptureService>();
        builder.Services.AddSingleton<OverlayManager>();
        builder.Services.AddSingleton<TrayService>();
        builder.Services.AddSingleton<ToolbarWindow>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsWindow>();
        builder.Services.AddTransient<AboutWindow>();

        _host = builder.Build();
        _memoryTrimHelper = new MemoryTrimHelper();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        var configService = _host.Services.GetRequiredService<ConfigService>();
        var themeService = _host.Services.GetRequiredService<ThemeService>();
        var session = _host.Services.GetRequiredService<AnnotationSession>();
        var captureService = _host.Services.GetRequiredService<ScreenCaptureService>();
        var overlayManager = _host.Services.GetRequiredService<OverlayManager>();
        _overlayManager = overlayManager;

        var activeProfile = configService.GetActiveProfile();
        themeService.ApplyTheme(activeProfile.Theme, activeProfile.DisplayMode);
        session.InitializeFromConfig(configService.CurrentConfig);

        configService.ConfigChanged += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                var p = configService.GetActiveProfile();
                themeService.ApplyTheme(p.Theme, p.DisplayMode);
                SyncMouseHookState();
            });
        };

        captureService.NotificationRequested += (msg, folder) =>
            Dispatcher.Invoke(() => ShowToast(msg, folder));

        overlayManager.InfoMessageRequested += msg =>
            Dispatcher.Invoke(() => ShowToast(msg, null));

        overlayManager.ToolbarBoundsProvider = () => _toolbarWindow?.GetCurrentScreenDipBounds();
        overlayManager.TopMostZOrderRequired += (_, _) =>
            Dispatcher.Invoke(() => _toolbarWindow?.BringToTopMost());

        var startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        _isToolbarVisible = !startMinimized;
        overlayManager.Initialize(!startMinimized);

        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "未處理的 UI 例外");
            WriteCrashLog("Dispatcher", args.Exception);
            args.Handled = true;
        };

        if (startMinimized)
        {
            InitializeBackgroundServices();
            _memoryTrimHelper.ScheduleTrim(IdleMemoryTrimDelay);
        }
        else
        {
            EnsureToolbarWindow().ShowAndRestorePosition();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, InitializeBackgroundServices);
        }
    }

    private void InitializeBackgroundServices()
    {
        if (_backgroundServicesInitialized || _host is null)
        {
            return;
        }

        _backgroundServicesInitialized = true;

        var configService = _host.Services.GetRequiredService<ConfigService>();
        var session = _host.Services.GetRequiredService<AnnotationSession>();
        var hotkeyService = _host.Services.GetRequiredService<GlobalHotkeyService>();
        var mouseHookService = _host.Services.GetRequiredService<GlobalMouseHookService>();
        var keyboardHookService = _host.Services.GetRequiredService<GlobalKeyboardHookService>();
        var trayService = _host.Services.GetRequiredService<TrayService>();

        hotkeyService.HotkeyPressed += (_, _) => Dispatcher.Invoke(ToggleToolbarVisibility);
        if (!hotkeyService.Register(configService.CurrentConfig.HotkeyModifiers, configService.CurrentConfig.HotkeyVirtualKey))
        {
            ShowToast(Strings.Toast_HotkeyFailed, null);
        }

        mouseHookService.ShouldHandleMiddleClick = () =>
            configService.CurrentConfig.MiddleClickToggleEnabled && _isToolbarVisible;
        mouseHookService.MiddleButtonClicked += (_, _) => session.ToggleCursorMode();
        SyncMouseHookState();

        keyboardHookService.ShouldHandleEscape = () =>
            GlobalKeyboardHookService.ShouldCaptureEscape(
                toolbarVisible: _isToolbarVisible,
                isCursorMode: session.IsCursorMode,
                isTextInputActive: _overlayManager?.IsInlineTextEditing == true,
                isPopupOpen: _toolbarWindow?.IsAnyPopupOpen == true,
                isDialogOpen: _settingsWindow is { IsVisible: true } || _aboutWindow is { IsVisible: true });
        keyboardHookService.EscapePressed += (_, _) =>
            Dispatcher.Invoke(() => session.IsCursorMode = true);
        keyboardHookService.Start();

        trayService.ToggleToolbarRequested += (_, _) => Dispatcher.Invoke(ToggleToolbarVisibility);
        trayService.OpenSettingsRequested += (_, _) => Dispatcher.Invoke(OpenSettingsWindow);
        trayService.OpenAboutRequested += (_, _) => Dispatcher.Invoke(OpenAboutWindow);
        trayService.ClearAllRequested += (_, _) => Dispatcher.Invoke(session.ClearAll);
        trayService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);
        trayService.Initialize();

        StartActivationListener();
    }

    private ToolbarWindow EnsureToolbarWindow()
    {
        if (_toolbarWindow is not null)
        {
            return _toolbarWindow;
        }

        var overlayManager = _host!.Services.GetRequiredService<OverlayManager>();
        _toolbarWindow = _host.Services.GetRequiredService<ToolbarWindow>();
        _toolbarWindow.OpenSettingsRequested += (_, _) => OpenSettingsWindow();
        _toolbarWindow.PanelVisibilityChanged += (_, isVisible) =>
        {
            _isToolbarVisible = isVisible;
            overlayManager.SetOverlaysVisible(isVisible);
            SyncMouseHookState();

            if (isVisible)
            {
                _memoryTrimHelper?.CancelPendingTrim();
            }
            else
            {
                _memoryTrimHelper?.ScheduleTrim(IdleMemoryTrimDelay);
            }
        };

        return _toolbarWindow;
    }

    private void ToggleToolbarVisibility()
    {
        EnsureToolbarWindow().ToggleVisibility();
    }

    private void SyncMouseHookState()
    {
        if (_host is null || !_backgroundServicesInitialized)
        {
            return;
        }

        var configService = _host.Services.GetRequiredService<ConfigService>();
        var mouseHookService = _host.Services.GetRequiredService<GlobalMouseHookService>();
        mouseHookService.SetEnabled(configService.CurrentConfig.MiddleClickToggleEnabled && _isToolbarVisible);
    }

    private void StartActivationListener()
    {
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateSignalName);
        _activationWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    Dispatcher.BeginInvoke(() => EnsureToolbarWindow().ShowAndRestorePosition());
                }
            },
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private static void TrySignalExistingInstance()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(ActivateSignalName);
            existing.Set();
        }
        catch
        {
            // 既有實例尚未就緒時忽略
        }
    }

    private void OpenSettingsWindow()
    {
        if (_host is null)
        {
            return;
        }

        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = _host.Services.GetRequiredService<SettingsWindow>();
        var themeService = _host.Services.GetRequiredService<ThemeService>();
        _settingsWindow.SourceInitialized += (_, _) => themeService.ApplyTitleBarTheme(_settingsWindow);
        _settingsWindow.OpenAboutRequested += (_, _) => OpenAboutWindow();
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            if (!_isToolbarVisible)
            {
                _memoryTrimHelper?.ScheduleTrim(IdleMemoryTrimDelay);
            }
        };
        _settingsWindow.Show();
    }

    private void OpenAboutWindow()
    {
        if (_host is null)
        {
            return;
        }

        if (_aboutWindow is { IsLoaded: true })
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = _host.Services.GetRequiredService<AboutWindow>();
        var themeService = _host.Services.GetRequiredService<ThemeService>();
        _aboutWindow.SourceInitialized += (_, _) => themeService.ApplyTitleBarTheme(_aboutWindow);
        _aboutWindow.Closed += (_, _) =>
        {
            _aboutWindow = null;
            if (!_isToolbarVisible)
            {
                _memoryTrimHelper?.ScheduleTrim(IdleMemoryTrimDelay);
            }
        };
        _aboutWindow.Show();
    }

    private void ShowToast(string message, string? folderToOpen)
    {
        if (_toastWindow is { IsLoaded: true })
        {
            _toastWindow.UpdateMessage(message, folderToOpen);
            return;
        }

        _toastWindow?.Close();
        _toastWindow = new ToastWindow(message, folderToOpen);
        _toastWindow.Closed += (_, _) => _toastWindow = null;
        _toastWindow.Show();
    }

    private void ExitApplication()
    {
        _toolbarWindow?.SaveCurrentPosition();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationWaitHandle?.Unregister(null);
        _activationWaitHandle = null;
        _activateEvent?.Dispose();
        _activateEvent = null;

        _memoryTrimHelper?.Dispose();
        _memoryTrimHelper = null;

        if (_host is not null)
        {
            _host.Services.GetService<ConfigService>()?.FlushPendingSave();
            _host.Services.GetService<GlobalMouseHookService>()?.Dispose();
            _host.Services.GetService<GlobalKeyboardHookService>()?.Dispose();
            _host.Services.GetService<GlobalHotkeyService>()?.Dispose();
            _host.Services.GetService<OverlayManager>()?.Dispose();
            _host.Services.GetService<TrayService>()?.Dispose();
            _host.Services.GetService<ThemeService>()?.Dispose();
            _host.Dispose();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void EnableStartupProfileOptimization()
    {
        try
        {
            var profileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MiniScreenNotes");
            Directory.CreateDirectory(profileDir);
            ProfileOptimization.SetProfileRoot(profileDir);
            ProfileOptimization.StartProfile("Startup.profile");
        }
        catch
        {
            // Multicore JIT 設定檔啟用失敗時忽略，不影響啟動
        }
    }

    private static void WriteCrashLog(string stage, Exception ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MiniScreenNotes");
            Directory.CreateDirectory(dir);
            var logPath = Path.Combine(dir, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{stage}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 忽略寫入記錄檔失敗
        }
    }
}
