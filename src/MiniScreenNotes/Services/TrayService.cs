using System.Drawing;
using System.Windows.Forms;
using Com.MiniScreenNotes.Resources.Strings;
using WpfApplication = System.Windows.Application;

namespace Com.MiniScreenNotes.Services;

public sealed class TrayService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly StartupService _startupService;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private Icon? _customIcon;
    private ToolStripMenuItem? _profilesMenu;
    private ToolStripMenuItem? _autoStartMenuItem;

    public TrayService(ConfigService configService, StartupService startupService)
    {
        _configService = configService;
        _startupService = startupService;
    }

    public event EventHandler? ToggleToolbarRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? OpenAboutRequested;
    public event EventHandler? ClearAllRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var (icon, ownsIcon) = LoadAppIcon();
        _customIcon = ownsIcon ? icon : null;
        var menu = new ContextMenuStrip();
        _contextMenu = menu;
        menu.Opening += (_, _) => RefreshMenuState();

        var showHideItem = new ToolStripMenuItem(Strings.Tray_ShowHide);
        showHideItem.Click += (_, _) => ToggleToolbarRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(showHideItem);

        _profilesMenu = new ToolStripMenuItem(Strings.Tray_Profiles);
        menu.Items.Add(_profilesMenu);

        var clearItem = new ToolStripMenuItem(Strings.Tray_ClearAll);
        clearItem.Click += (_, _) => ClearAllRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(clearItem);

        menu.Items.Add(new ToolStripSeparator());

        _autoStartMenuItem = new ToolStripMenuItem(Strings.Tray_AutoStart)
        {
            CheckOnClick = true
        };
        _autoStartMenuItem.Click += (_, _) =>
        {
            var target = _autoStartMenuItem.Checked;
            if (_startupService.SetEnabled(target))
            {
                _configService.CurrentConfig.LaunchAtStartup = target;
                _configService.Save();
            }
            else
            {
                _autoStartMenuItem.Checked = _startupService.IsEnabled();
            }
        };
        menu.Items.Add(_autoStartMenuItem);

        var settingsItem = new ToolStripMenuItem(Strings.Tray_Settings);
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        var aboutItem = new ToolStripMenuItem(Strings.Tray_About);
        aboutItem.Click += (_, _) => OpenAboutRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(aboutItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem(Strings.Tray_Exit);
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = Strings.AppTitle,
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleToolbarRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public void RefreshMenuState()
    {
        if (_profilesMenu is null)
        {
            return;
        }

        _profilesMenu.DropDownItems.Clear();
        var activeId = _configService.CurrentConfig.ActiveProfileId;

        foreach (var profile in _configService.CurrentConfig.Profiles)
        {
            var item = new ToolStripMenuItem(profile.Name)
            {
                Checked = profile.Id == activeId
            };
            var id = profile.Id;
            item.Click += (_, _) => _configService.SetActiveProfile(id);
            _profilesMenu.DropDownItems.Add(item);
        }

        if (_autoStartMenuItem is not null)
        {
            _autoStartMenuItem.Checked = _startupService.IsEnabled();
        }
    }

    private static (Icon Icon, bool OwnsIcon) LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/MiniScreenNotes;component/Resources/AppIcon.ico", UriKind.Absolute);
            var streamInfo = WpfApplication.GetResourceStream(uri);
            if (streamInfo?.Stream is not null)
            {
                using var stream = streamInfo.Stream;
                return (new Icon(stream), true);
            }
        }
        catch
        {
            // 回退系統預設圖示
        }

        return (SystemIcons.Application, false);
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip = null;
            _notifyIcon.Icon = null;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _contextMenu?.Dispose();
        _contextMenu = null;

        _customIcon?.Dispose();
        _customIcon = null;
    }
}
