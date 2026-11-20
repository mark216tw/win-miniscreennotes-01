using System.Collections.ObjectModel;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Resources.Strings;
using Com.MiniScreenNotes.Services;

namespace Com.MiniScreenNotes.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly ThemeService _themeService;
    private readonly StartupService _startupService;
    private readonly GlobalHotkeyService _hotkeyService;

    private PanelProfile _selectedProfile;
    private PanelSize _panelSize;
    private PanelOrientation _panelOrientation;
    private DisplayMode _displayMode;
    private ThemeName _themeName;
    private int _maxItemsPerLine;
    private bool _autoHideWhenDocked;
    private double _autoHideDelaySeconds;
    private ToolSettingItemViewModel? _selectedToolItem;

    private bool _quickSaveEnabled;
    private string _quickSaveDirectory = string.Empty;
    private ImageSaveFormat _quickSaveFormat;
    private bool _launchAtStartup;
    private bool _middleClickToggleEnabled;

    private bool _hotkeyCtrl;
    private bool _hotkeyAlt;
    private bool _hotkeyShift;
    private string _hotkeyKeyName = "A";
    private string _hotkeyStatusMessage = string.Empty;

    private bool _isLoadingProfile;

    public SettingsViewModel(
        ConfigService configService,
        ThemeService themeService,
        StartupService startupService,
        GlobalHotkeyService hotkeyService)
    {
        _configService = configService;
        _themeService = themeService;
        _startupService = startupService;
        _hotkeyService = hotkeyService;

        Profiles = new ObservableCollection<PanelProfile>(_configService.CurrentConfig.Profiles);
        Tools = [];
        AvailableHotkeyKeys = BuildHotkeyKeyNames();

        _selectedProfile = _configService.GetActiveProfile();
        LoadGlobalSettings();
        LoadProfileIntoFields(_selectedProfile);
    }

    public ObservableCollection<PanelProfile> Profiles { get; }
    public ObservableCollection<ToolSettingItemViewModel> Tools { get; }
    public IReadOnlyList<string> AvailableHotkeyKeys { get; }

    public PanelProfile SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (value is not null && SetProperty(ref _selectedProfile, value))
            {
                LoadProfileIntoFields(value);
                _configService.SetActiveProfile(value.Id);
                _themeService.ApplyTheme(value.Theme, value.DisplayMode);
            }
        }
    }

    public PanelSize PanelSize
    {
        get => _panelSize;
        set
        {
            if (SetProperty(ref _panelSize, value) && !_isLoadingProfile)
            {
                _selectedProfile.Size = value;
                SaveProfileChanges();
            }
        }
    }

    public PanelOrientation PanelOrientation
    {
        get => _panelOrientation;
        set
        {
            if (SetProperty(ref _panelOrientation, value) && !_isLoadingProfile)
            {
                _selectedProfile.Orientation = value;
                SaveProfileChanges();
            }
        }
    }

    public DisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (SetProperty(ref _displayMode, value) && !_isLoadingProfile)
            {
                _selectedProfile.DisplayMode = value;
                _themeService.ApplyTheme(_selectedProfile.Theme, value);
                SaveProfileChanges();
            }
        }
    }

    public ThemeName ThemeName
    {
        get => _themeName;
        set
        {
            if (SetProperty(ref _themeName, value) && !_isLoadingProfile)
            {
                _selectedProfile.Theme = value;
                _themeService.ApplyTheme(value, _selectedProfile.DisplayMode);
                SaveProfileChanges();
            }
        }
    }

    public int MaxItemsPerLine
    {
        get => _maxItemsPerLine;
        set
        {
            var clamped = Math.Clamp(value, 1, 26);
            if (SetProperty(ref _maxItemsPerLine, clamped) && !_isLoadingProfile)
            {
                _selectedProfile.MaxItemsPerLine = clamped;
                SaveProfileChanges();
            }
        }
    }

    public bool AutoHideWhenDocked
    {
        get => _autoHideWhenDocked;
        set
        {
            if (SetProperty(ref _autoHideWhenDocked, value) && !_isLoadingProfile)
            {
                _selectedProfile.AutoHideWhenDocked = value;
                SaveProfileChanges();
            }
        }
    }

    public double AutoHideDelaySeconds
    {
        get => _autoHideDelaySeconds;
        set
        {
            var clamped = Math.Round(Math.Clamp(value, 0.5, 30.0), 1);
            if (SetProperty(ref _autoHideDelaySeconds, clamped) && !_isLoadingProfile)
            {
                _selectedProfile.AutoHideDelaySeconds = clamped;
                _configService.RequestSave();
            }
        }
    }

    public ToolSettingItemViewModel? SelectedToolItem
    {
        get => _selectedToolItem;
        set => SetProperty(ref _selectedToolItem, value);
    }

    public bool QuickSaveEnabled
    {
        get => _quickSaveEnabled;
        set
        {
            if (SetProperty(ref _quickSaveEnabled, value))
            {
                _configService.CurrentConfig.QuickSaveEnabled = value;
                _configService.Save();
            }
        }
    }

    public string QuickSaveDirectory
    {
        get => _quickSaveDirectory;
        set
        {
            if (SetProperty(ref _quickSaveDirectory, value))
            {
                _configService.CurrentConfig.QuickSaveDirectory = value;
                _configService.Save();
            }
        }
    }

    public ImageSaveFormat QuickSaveFormat
    {
        get => _quickSaveFormat;
        set
        {
            if (SetProperty(ref _quickSaveFormat, value))
            {
                _configService.CurrentConfig.QuickSaveFormat = value;
                _configService.Save();
            }
        }
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set
        {
            if (SetProperty(ref _launchAtStartup, value))
            {
                if (_startupService.SetEnabled(value))
                {
                    _configService.CurrentConfig.LaunchAtStartup = value;
                    _configService.NotifyConfigChanged();
                }
            }
        }
    }

    public bool MiddleClickToggleEnabled
    {
        get => _middleClickToggleEnabled;
        set
        {
            if (SetProperty(ref _middleClickToggleEnabled, value))
            {
                _configService.CurrentConfig.MiddleClickToggleEnabled = value;
                _configService.Save();
            }
        }
    }

    public bool HotkeyCtrl
    {
        get => _hotkeyCtrl;
        set => SetProperty(ref _hotkeyCtrl, value);
    }

    public bool HotkeyAlt
    {
        get => _hotkeyAlt;
        set => SetProperty(ref _hotkeyAlt, value);
    }

    public bool HotkeyShift
    {
        get => _hotkeyShift;
        set => SetProperty(ref _hotkeyShift, value);
    }

    public string HotkeyKeyName
    {
        get => _hotkeyKeyName;
        set => SetProperty(ref _hotkeyKeyName, value);
    }

    public string HotkeyStatusMessage
    {
        get => _hotkeyStatusMessage;
        set => SetProperty(ref _hotkeyStatusMessage, value);
    }

    public void AddNewProfile()
    {
        var clone = _selectedProfile.Clone($"組態 {Profiles.Count + 1}");
        _configService.CurrentConfig.Profiles.Add(clone);
        Profiles.Add(clone);
        SelectedProfile = clone;
        _configService.NotifyConfigChanged();
    }

    public bool RenameSelectedProfile(string newName)
    {
        var trimmed = newName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || string.Equals(_selectedProfile.Name, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        _selectedProfile.Name = trimmed;
        var index = Profiles.IndexOf(_selectedProfile);
        if (index >= 0)
        {
            var current = _selectedProfile;
            Profiles.RemoveAt(index);
            Profiles.Insert(index, current);
            _selectedProfile = current;
            OnPropertyChanged(nameof(SelectedProfile));
        }

        _configService.NotifyConfigChanged();
        return true;
    }

    public bool CanDeleteSelectedProfile => Profiles.Count > 1;

    public void DeleteSelectedProfile()
    {
        if (!CanDeleteSelectedProfile)
        {
            return;
        }

        var toDelete = _selectedProfile;
        _configService.CurrentConfig.Profiles.Remove(toDelete);
        Profiles.Remove(toDelete);
        SelectedProfile = Profiles[0];
        _configService.NotifyConfigChanged();
    }

    public void MoveSelectedTool(int offset)
    {
        if (SelectedToolItem is null)
        {
            return;
        }

        var idx = Tools.IndexOf(SelectedToolItem);
        var target = idx + offset;
        if (idx < 0 || target < 0 || target >= Tools.Count)
        {
            return;
        }

        Tools.Move(idx, target);
        SyncToolsToProfile();
    }

    public void ShowAllTools()
    {
        foreach (var item in Tools)
        {
            item.IsVisible = true;
        }

        SyncToolsToProfile();
    }

    public void SyncToolsToProfile()
    {
        if (_isLoadingProfile)
        {
            return;
        }

        _selectedProfile.Tools = Tools.Select(t => t.ToConfig()).ToList();
        SaveProfileChanges();
    }

    public void ApplyHotkey()
    {
        uint modifiers = 0;
        if (HotkeyCtrl) modifiers |= Win32Interop.ModControl;
        if (HotkeyAlt) modifiers |= Win32Interop.ModAlt;
        if (HotkeyShift) modifiers |= Win32Interop.ModShift;

        if (modifiers == 0)
        {
            HotkeyStatusMessage = Strings.Hotkey_NeedModifier;
            return;
        }

        var vk = ParseVirtualKey(HotkeyKeyName);
        if (_hotkeyService.Register(modifiers, vk))
        {
            _configService.CurrentConfig.HotkeyModifiers = modifiers;
            _configService.CurrentConfig.HotkeyVirtualKey = vk;
            _configService.Save();
            HotkeyStatusMessage = Strings.Hotkey_Applied;
        }
        else
        {
            HotkeyStatusMessage = _hotkeyService.IsRegistered
                ? Strings.Hotkey_ApplyFailedOldKept
                : Strings.Hotkey_ApplyFailed;
        }
    }

    private void LoadGlobalSettings()
    {
        var cfg = _configService.CurrentConfig;
        _quickSaveEnabled = cfg.QuickSaveEnabled;
        _quickSaveDirectory = cfg.QuickSaveDirectory;
        _quickSaveFormat = cfg.QuickSaveFormat;
        _launchAtStartup = _startupService.IsEnabled();
        _middleClickToggleEnabled = cfg.MiddleClickToggleEnabled;

        _hotkeyCtrl = (cfg.HotkeyModifiers & Win32Interop.ModControl) != 0;
        _hotkeyAlt = (cfg.HotkeyModifiers & Win32Interop.ModAlt) != 0;
        _hotkeyShift = (cfg.HotkeyModifiers & Win32Interop.ModShift) != 0;
        _hotkeyKeyName = HotkeyDisplayHelper.GetKeyName(cfg.HotkeyVirtualKey);
    }

    private void LoadProfileIntoFields(PanelProfile profile)
    {
        _isLoadingProfile = true;
        try
        {
            PanelSize = profile.Size;
            PanelOrientation = profile.Orientation;
            DisplayMode = profile.DisplayMode;
            ThemeName = profile.Theme;
            MaxItemsPerLine = profile.MaxItemsPerLine;
            AutoHideWhenDocked = profile.AutoHideWhenDocked;
            AutoHideDelaySeconds = profile.AutoHideDelaySeconds;

            Tools.Clear();
            foreach (var tool in profile.Tools)
            {
                var vm = new ToolSettingItemViewModel(tool);
                vm.PropertyChanged += (_, _) => SyncToolsToProfile();
                Tools.Add(vm);
            }
        }
        finally
        {
            _isLoadingProfile = false;
        }
    }

    private void SaveProfileChanges()
    {
        _configService.NotifyConfigChanged();
    }

    private static List<string> BuildHotkeyKeyNames()
    {
        var list = new List<string>();
        for (var c = 'A'; c <= 'Z'; c++)
        {
            list.Add(c.ToString());
        }

        for (var i = 1; i <= 12; i++)
        {
            list.Add($"F{i}");
        }

        return list;
    }

    private static uint ParseVirtualKey(string keyName)
    {
        if (keyName.Length == 1 && keyName[0] is >= 'A' and <= 'Z')
        {
            return keyName[0];
        }

        if (keyName.StartsWith('F') && int.TryParse(keyName[1..], out var fn) && fn is >= 1 and <= 12)
        {
            return (uint)(0x70 + fn - 1);
        }

        return 0x41; // 'A'
    }
}
