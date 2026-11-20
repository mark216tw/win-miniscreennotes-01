using System.IO;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Services;
using Com.MiniScreenNotes.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Com.MiniScreenNotes.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MiniScreenNotesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SaveAndReload_ShouldPersistProfilesAndQuickSaveSettings()
    {
        var path = Path.Combine(_tempDir, "config.json");
        var service = new ConfigService(NullLogger<ConfigService>.Instance, path);

        var custom = service.GetActiveProfile().Clone("教學自訂組態");
        custom.Size = PanelSize.Large;
        custom.Orientation = PanelOrientation.Vertical;
        custom.Theme = ThemeName.ForestGreen;
        custom.AutoHideDelaySeconds = 4.5;

        service.CurrentConfig.Profiles.Add(custom);
        service.CurrentConfig.QuickSaveEnabled = true;
        service.CurrentConfig.QuickSaveDirectory = @"C:\Captures";
        service.CurrentConfig.QuickSaveFormat = ImageSaveFormat.Jpeg;
        service.SetActiveProfile(custom.Id);

        var reloaded = new ConfigService(NullLogger<ConfigService>.Instance, path);
        var active = reloaded.GetActiveProfile();

        Assert.Equal("教學自訂組態", active.Name);
        Assert.Equal(PanelSize.Large, active.Size);
        Assert.Equal(PanelOrientation.Vertical, active.Orientation);
        Assert.Equal(ThemeName.ForestGreen, active.Theme);
        Assert.Equal(4.5, active.AutoHideDelaySeconds);
        Assert.True(reloaded.CurrentConfig.QuickSaveEnabled);
        Assert.Equal(@"C:\Captures", reloaded.CurrentConfig.QuickSaveDirectory);
        Assert.Equal(ImageSaveFormat.Jpeg, reloaded.CurrentConfig.QuickSaveFormat);
    }

    [Fact]
    public void MiddleClickToggle_ShouldDefaultEnabledAndPersist()
    {
        var path = Path.Combine(_tempDir, "middle_click.json");
        var service = new ConfigService(NullLogger<ConfigService>.Instance, path);

        Assert.True(service.CurrentConfig.MiddleClickToggleEnabled);

        service.CurrentConfig.MiddleClickToggleEnabled = false;
        service.Save();

        var reloaded = new ConfigService(NullLogger<ConfigService>.Instance, path);
        Assert.False(reloaded.CurrentConfig.MiddleClickToggleEnabled);
    }

    [Fact]
    public void CorruptedConfigFile_ShouldFallbackToDefaultProfiles()
    {
        var path = Path.Combine(_tempDir, "corrupted.json");
        File.WriteAllText(path, "{ not valid json !!!");

        var service = new ConfigService(NullLogger<ConfigService>.Instance, path);

        Assert.NotEmpty(service.CurrentConfig.Profiles);
        Assert.Equal(ThemeName.YellowEyeCare, service.GetActiveProfile().Theme);
    }

    [Fact]
    public void DependencyInjection_ShouldResolveAllCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<AnnotationSession>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<GlobalMouseHookService>();
        services.AddSingleton<ScreenCaptureService>();
        services.AddSingleton<TrayService>();
        services.AddTransient<SettingsViewModel>();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ConfigService>());
        Assert.NotNull(provider.GetRequiredService<ThemeService>());
        Assert.NotNull(provider.GetRequiredService<StartupService>());
        Assert.NotNull(provider.GetRequiredService<AnnotationSession>());
        Assert.NotNull(provider.GetRequiredService<GlobalHotkeyService>());
        Assert.NotNull(provider.GetRequiredService<GlobalMouseHookService>());
        Assert.NotNull(provider.GetRequiredService<ScreenCaptureService>());
        Assert.NotNull(provider.GetRequiredService<TrayService>());
        Assert.NotNull(provider.GetRequiredService<SettingsViewModel>());
    }

    [Fact]
    public void SaveAndReload_ShouldPersistIndependentPositionPerProfile()
    {
        var path = Path.Combine(_tempDir, "profile_positions.json");
        var service = new ConfigService(NullLogger<ConfigService>.Instance, path);

        var first = service.CurrentConfig.Profiles[0];
        var second = service.CurrentConfig.Profiles[1];

        first.LastPanelLeft = 120;
        first.LastPanelTop = 16;
        first.LastDockEdge = DockEdge.Top;

        second.LastPanelLeft = 1860;
        second.LastPanelTop = 320;
        second.LastDockEdge = DockEdge.Right;

        service.Save();

        var reloaded = new ConfigService(NullLogger<ConfigService>.Instance, path);
        var reloadedFirst = reloaded.CurrentConfig.Profiles.First(p => p.Id == first.Id);
        var reloadedSecond = reloaded.CurrentConfig.Profiles.First(p => p.Id == second.Id);

        Assert.Equal(120, reloadedFirst.LastPanelLeft);
        Assert.Equal(16, reloadedFirst.LastPanelTop);
        Assert.Equal(DockEdge.Top, reloadedFirst.LastDockEdge);

        Assert.Equal(1860, reloadedSecond.LastPanelLeft);
        Assert.Equal(320, reloadedSecond.LastPanelTop);
        Assert.Equal(DockEdge.Right, reloadedSecond.LastDockEdge);
    }

    [Fact]
    public void ExtraSmallPanelSizeAndSingleMaxItemsPerLine_ShouldPersistAndReturnExpectedMetrics()
    {
        var path = Path.Combine(_tempDir, "extra_small.json");
        var service = new ConfigService(NullLogger<ConfigService>.Instance, path);

        var profile = service.GetActiveProfile();
        profile.Size = PanelSize.ExtraSmall;
        profile.MaxItemsPerLine = 14;
        service.Save();

        var reloaded = new ConfigService(NullLogger<ConfigService>.Instance, path);
        var active = reloaded.GetActiveProfile();

        Assert.Equal(PanelSize.ExtraSmall, active.Size);
        Assert.Equal(24, active.GetButtonSize());
        Assert.Equal(12, active.GetIconSize());
        Assert.Equal(14, active.GetMaxItemsPerLine());
    }

    [Fact]
    public void RenameSelectedProfile_ShouldUpdateNameAndPersist()
    {
        var path = Path.Combine(_tempDir, "rename_profile.json");
        var configService = new ConfigService(NullLogger<ConfigService>.Instance, path);
        var vm = new SettingsViewModel(
            configService,
            new ThemeService(NullLogger<ThemeService>.Instance),
            new StartupService(NullLogger<StartupService>.Instance),
            new GlobalHotkeyService(NullLogger<GlobalHotkeyService>.Instance));

        var renamed = vm.RenameSelectedProfile("  簡報專用  ");

        Assert.True(renamed);
        Assert.Equal("簡報專用", vm.SelectedProfile.Name);

        var reloaded = new ConfigService(NullLogger<ConfigService>.Instance, path);
        Assert.Equal("簡報專用", reloaded.GetActiveProfile().Name);
    }

    [Fact]
    public void AllThemes_ShouldPersistAndReloadCorrectly()
    {
        var allThemes = Enum.GetValues<ThemeName>();
        Assert.Equal(10, allThemes.Length);

        var path = Path.Combine(_tempDir, "all_themes.json");
        var service = new ConfigService(NullLogger<ConfigService>.Instance, path);

        foreach (var theme in allThemes)
        {
            service.GetActiveProfile().Theme = theme;
            service.Save();

            var reloaded = new ConfigService(NullLogger<ConfigService>.Instance, path);
            Assert.Equal(theme, reloaded.GetActiveProfile().Theme);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
