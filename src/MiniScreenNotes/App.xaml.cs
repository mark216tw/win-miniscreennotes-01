using System.IO;
using System.Windows;
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

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activateEvent;
    private CancellationTokenSource? _signalListenerCts;
    private IHost? _host;

    private ToolbarWindow? _toolbarWindow;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private ToastWindow? _toastWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
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

        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
#if DEBUG
                logging.AddDebug();
#endif
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<ConfigService>();
                services.AddSingleton<ThemeService>();
                services.AddSingleton<StartupService>();
                services.AddSingleton<AnnotationSession>();
                services.AddSingleton<GlobalHotkeyService>();
                services.AddSingleton<GlobalMouseHookService>();
                services.AddSingleton<ScreenCaptureService>();
                services.AddSingleton<OverlayManager>();
                services.AddSingleton<TrayService>();
                services.AddSingleton<ToolbarWindow>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsWindow>();
                services.AddTransient<AboutWindow>();
            })
            .Build();

        _host.Start();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        var configService = _host.Services.GetRequiredService<ConfigService>();
        var themeService = _host.Services.GetRequiredService<ThemeService>();
        var session = _host.Services.GetRequiredService<AnnotationSession>();
        var hotkeyService = _host.Services.GetRequiredService<GlobalHotkeyService>();
        var mouseHookService = _host.Services.GetRequiredService<GlobalMouseHookService>();
        var captureService = _host.Services.GetRequiredService<ScreenCaptureService>();
        var overlayManager = _host.Services.GetRequiredService<OverlayManager>();
        var trayService = _host.Services.GetRequiredService<TrayService>();

        var activeProfile = configService.GetActiveProfile();
        themeService.ApplyTheme(activeProfile.Theme, activeProfile.DisplayMode);
        session.InitializeFromConfig(configService.CurrentConfig);

        configService.ConfigChanged += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                var p = configService.GetActiveProfile();
                themeService.ApplyTheme(p.Theme, p.DisplayMode);
            });
        };

        captureService.NotificationRequested += (msg, folder) =>
            Dispatcher.Invoke(() => ShowToast(msg, folder));

        overlayManager.InfoMessageRequested += msg =>
            Dispatcher.Invoke(() => ShowToast(msg, null));

        var startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        overlayManager.Initialize(!startMinimized);

        _toolbarWindow = _host.Services.GetRequiredService<ToolbarWindow>();
        _toolbarWindow.OpenSettingsRequested += (_, _) => OpenSettingsWindow();
        _toolbarWindow.PanelVisibilityChanged += (_, isVisible) => overlayManager.SetOverlaysVisible(isVisible);
        overlayManager.ToolbarBoundsProvider = () => _toolbarWindow?.GetCurrentScreenDipBounds();
        overlayManager.TopMostZOrderRequired += (_, _) =>
            Dispatcher.Invoke(() => _toolbarWindow?.BringToTopMost());

        hotkeyService.HotkeyPressed += (_, _) => Dispatcher.Invoke(() => _toolbarWindow.ToggleVisibility());
        if (!hotkeyService.Register(configService.CurrentConfig.HotkeyModifiers, configService.CurrentConfig.HotkeyVirtualKey))
        {
            ShowToast(Strings.Toast_HotkeyFailed, null);
        }

        mouseHookService.ShouldHandleMiddleClick = () =>
            configService.CurrentConfig.MiddleClickToggleEnabled
            && _toolbarWindow is { IsVisible: true };
        mouseHookService.MiddleButtonClicked += (_, _) => session.ToggleCursorMode();
        mouseHookService.Start();

        trayService.ToggleToolbarRequested += (_, _) => Dispatcher.Invoke(() => _toolbarWindow.ToggleVisibility());
        trayService.OpenSettingsRequested += (_, _) => Dispatcher.Invoke(OpenSettingsWindow);
        trayService.OpenAboutRequested += (_, _) => Dispatcher.Invoke(OpenAboutWindow);
        trayService.ClearAllRequested += (_, _) => Dispatcher.Invoke(session.ClearAll);
        trayService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);
        trayService.Initialize();

        StartActivationListener();

        if (!startMinimized)
        {
            _toolbarWindow.ShowAndRestorePosition();
        }

        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "未處理的 UI 例外");
            WriteCrashLog("Dispatcher", args.Exception);
            args.Handled = true;
        };
    }

    private void StartActivationListener()
    {
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateSignalName);
        _signalListenerCts = new CancellationTokenSource();
        var token = _signalListenerCts.Token;

        _ = Task.Run(() =>
        {
            var handles = new WaitHandle[] { _activateEvent, token.WaitHandle };
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (WaitHandle.WaitAny(handles) != 0 || token.IsCancellationRequested)
                    {
                        break;
                    }

                    Dispatcher.Invoke(() => _toolbarWindow?.ShowAndRestorePosition());
                }
            }
            catch (ObjectDisposedException)
            {
                // 應用程式關閉時事件已釋放
            }
        }, token);
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
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
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
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
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
        _signalListenerCts?.Cancel();
        _activateEvent?.Dispose();

        if (_host is not null)
        {
            _host.Services.GetService<ConfigService>()?.FlushPendingSave();
            _host.Services.GetService<GlobalMouseHookService>()?.Dispose();
            _host.Services.GetService<GlobalHotkeyService>()?.Dispose();
            _host.Services.GetService<OverlayManager>()?.Dispose();
            _host.Services.GetService<TrayService>()?.Dispose();
            _host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
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
