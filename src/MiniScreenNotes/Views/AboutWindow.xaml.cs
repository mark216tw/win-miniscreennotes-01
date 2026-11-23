using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Resources.Strings;
using Com.MiniScreenNotes.Services;

namespace Com.MiniScreenNotes.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(ConfigService configService)
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "1.0.0";
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "1.0.0.0";

        Title = Strings.About_Title;
        VersionText.Text = string.Format(Strings.About_VersionFormat, version);
        FileVersionText.Text = string.Format(Strings.About_FileVersionFormat, fileVersion);
        LicenseText.Text = string.Format(Strings.About_LicenseFormat, Strings.About_License);
        PrivacyText.Text = Strings.About_Privacy;

        OsText.Text = RuntimeInformation.OSDescription;
        ArchitectureText.Text = string.Format(
            Strings.About_ArchitectureValue,
            RuntimeInformation.OSArchitecture,
            RuntimeInformation.ProcessArchitecture);
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;
        DisplaysText.Text = BuildDisplaysText();
        DataFolderText.Text = GetDataFolderPath();

        var config = configService.CurrentConfig;
        HotkeyText.Text = $"{HotkeyDisplayHelper.Format(config.HotkeyModifiers, config.HotkeyVirtualKey)}（{Strings.About_HotkeyCustomizable}）";
        ShortcutList.ItemsSource = BuildShortcuts(config);
    }

    private static string BuildDisplaysText()
    {
        var monitors = DisplayMonitorHelper.GetAllMonitors();
        return string.Join(
            Environment.NewLine,
            monitors.Select(monitor => string.Format(
                Strings.About_DisplayEntry,
                monitor.DeviceName.Replace(@"\\.\", string.Empty),
                monitor.PhysicalBounds.Width,
                monitor.PhysicalBounds.Height,
                (int)Math.Round(monitor.DpiScaleX * 100.0),
                monitor.IsPrimary ? Strings.About_PrimarySuffix : string.Empty)));
    }

    private static string GetDataFolderPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MiniScreenNotes");

    private static List<ShortcutInfo> BuildShortcuts(AppConfig config) =>
    [
        new(HotkeyDisplayHelper.Format(config.HotkeyModifiers, config.HotkeyVirtualKey), Strings.About_Shortcut_TogglePanel),
        new(Strings.About_Key_MiddleMouse, Strings.About_Shortcut_ToggleCursor),
        new("Ctrl+Z", Strings.About_Shortcut_Undo),
        new("Ctrl+Y", Strings.About_Shortcut_Redo),
        new("Ctrl+C", Strings.About_Shortcut_Copy),
        new("Ctrl+S", Strings.About_Shortcut_Save),
        new("Esc", Strings.About_Shortcut_CursorMode),
        new("Ctrl+Enter", Strings.About_Shortcut_ConfirmText),
        new(Strings.About_Key_WheelCtrl, Strings.About_Shortcut_Magnifier),
        new(Strings.About_Key_Wheel, Strings.About_Shortcut_Spotlight),
        new(Strings.About_Key_RightMouse, Strings.About_Shortcut_RightClickClear)
    ];

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
