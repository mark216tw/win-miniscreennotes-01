using System.IO;
using System.Text.Json;
using Com.MiniScreenNotes.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Com.MiniScreenNotes.Services;

public sealed class ConfigService : IDisposable
{
    private const int SaveDebounceMilliseconds = 500;

    private readonly ILogger<ConfigService> _logger;
    private readonly string _configFilePath;
    private readonly object _saveLock = new();
    private System.Threading.Timer? _saveTimer;
    private bool _hasPendingSave;
    private bool _disposed;
    private PanelProfile? _cachedActiveProfile;
    private string? _cachedActiveProfileId;

    [ActivatorUtilitiesConstructor]
    public ConfigService(ILogger<ConfigService> logger)
        : this(logger, null)
    {
    }

    public ConfigService(ILogger<ConfigService> logger, string? customConfigPath)
    {
        _logger = logger;
        _configFilePath = customConfigPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MiniScreenNotes",
            "config.json");

        CurrentConfig = LoadOrCreateDefault();
    }

    public AppConfig CurrentConfig { get; private set; }

    public event EventHandler? ConfigChanged;

    public PanelProfile GetActiveProfile()
    {
        if (_cachedActiveProfile is not null
            && _cachedActiveProfileId == CurrentConfig.ActiveProfileId
            && CurrentConfig.Profiles.Contains(_cachedActiveProfile))
        {
            return _cachedActiveProfile;
        }

        EnsureValidProfiles(CurrentConfig);
        var profile = CurrentConfig.Profiles.FirstOrDefault(p => p.Id == CurrentConfig.ActiveProfileId)
                      ?? CurrentConfig.Profiles[0];
        _cachedActiveProfile = profile;
        _cachedActiveProfileId = profile.Id;
        return profile;
    }

    public void SetActiveProfile(string profileId)
    {
        if (CurrentConfig.Profiles.Any(p => p.Id == profileId))
        {
            CurrentConfig.ActiveProfileId = profileId;
            _cachedActiveProfile = null;
            _cachedActiveProfileId = null;
            Save();
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Save()
    {
        lock (_saveLock)
        {
            CancelPendingSave();
            WriteConfigFile();
            _hasPendingSave = false;
        }
    }

    public void RequestSave()
    {
        lock (_saveLock)
        {
            if (_disposed)
            {
                return;
            }

            _hasPendingSave = true;
            _saveTimer ??= new System.Threading.Timer(
                _ => FlushPendingSave(),
                null,
                SaveDebounceMilliseconds,
                Timeout.Infinite);
            _saveTimer.Change(SaveDebounceMilliseconds, Timeout.Infinite);
        }
    }

    public void FlushPendingSave()
    {
        lock (_saveLock)
        {
            CancelPendingSave();
            if (!_hasPendingSave)
            {
                return;
            }

            WriteConfigFile();
            _hasPendingSave = false;
        }
    }

    private void CancelPendingSave()
    {
        _saveTimer?.Dispose();
        _saveTimer = null;
    }

    private void WriteConfigFile()
    {
        try
        {
            EnsureValidProfiles(CurrentConfig);
            var dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tempPath = _configFilePath + ".tmp";
            var json = JsonSerializer.Serialize(CurrentConfig, AppConfigJsonContext.Default.AppConfig);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _configFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "儲存設定檔失敗：{Path}", _configFilePath);
        }
    }

    public void Dispose()
    {
        lock (_saveLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelPendingSave();
            if (_hasPendingSave)
            {
                WriteConfigFile();
                _hasPendingSave = false;
            }
        }
    }

    public void NotifyConfigChanged()
    {
        Save();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppConfig LoadOrCreateDefault()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var loaded = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig);
                if (loaded is not null)
                {
                    EnsureValidProfiles(loaded);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "讀取設定檔失敗，將回退至預設組態：{Path}", _configFilePath);
        }

        var config = CreateDefaultConfig();
        return config;
    }

    public static AppConfig CreateDefaultConfig()
    {
        var fullProfile = new PanelProfile
        {
            Name = "完整標註（橫向）",
            Size = PanelSize.Medium,
            Orientation = PanelOrientation.Horizontal,
            DisplayMode = DisplayMode.FollowSystem,
            Theme = ThemeName.YellowEyeCare,
            AutoHideWhenDocked = true,
            AutoHideDelaySeconds = 2.0,
            Tools = PanelProfile.CreateDefaultTools()
        };

        var compactProfile = new PanelProfile
        {
            Name = "簡報精簡（直向側邊）",
            Size = PanelSize.Small,
            Orientation = PanelOrientation.Vertical,
            DisplayMode = DisplayMode.Dark,
            Theme = ThemeName.GlacierBlue,
            AutoHideWhenDocked = true,
            AutoHideDelaySeconds = 1.5,
            Tools =
            [
                new ToolItemConfig { Tool = ToolType.Cursor, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Pen, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Highlighter, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Arrow, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Laser, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Spotlight, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Magnifier, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.ColorPicker, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Undo, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Clear, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Copy, IsVisible = true },
                new ToolItemConfig { Tool = ToolType.Save, IsVisible = true }
            ]
        };

        EnsureAllToolsExist(compactProfile);

        return new AppConfig
        {
            ActiveProfileId = fullProfile.Id,
            Profiles = [fullProfile, compactProfile]
        };
    }

    private static void EnsureValidProfiles(AppConfig config)
    {
        if (config.Profiles.Count == 0)
        {
            var fallback = CreateDefaultConfig();
            config.Profiles = fallback.Profiles;
            config.ActiveProfileId = fallback.ActiveProfileId;
        }

        foreach (var profile in config.Profiles)
        {
            EnsureAllToolsExist(profile);
        }

        if (!config.Profiles.Any(p => p.Id == config.ActiveProfileId))
        {
            config.ActiveProfileId = config.Profiles[0].Id;
        }

        // 相容舊版全域面板位置設定：若目前組態尚未有獨立位置，自動遷移
        var active = config.Profiles.First(p => p.Id == config.ActiveProfileId);
        if (!active.LastPanelLeft.HasValue && !active.LastPanelTop.HasValue &&
            config.LastPanelLeft.HasValue && config.LastPanelTop.HasValue)
        {
            active.LastPanelLeft = config.LastPanelLeft;
            active.LastPanelTop = config.LastPanelTop;
            active.LastDockEdge = config.LastDockEdge;
        }
    }

    private static void EnsureAllToolsExist(PanelProfile profile)
    {
        var existing = profile.Tools.Select(t => t.Tool).ToHashSet();
        foreach (var tool in Enum.GetValues<ToolType>())
        {
            if (!existing.Contains(tool))
            {
                profile.Tools.Add(new ToolItemConfig { Tool = tool, IsVisible = false });
            }
        }
    }
}
