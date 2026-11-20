namespace Com.MiniScreenNotes.Models;

public sealed class PanelProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "預設組態";
    public PanelSize Size { get; set; } = PanelSize.Medium;
    public PanelOrientation Orientation { get; set; } = PanelOrientation.Horizontal;
    public DisplayMode DisplayMode { get; set; } = DisplayMode.FollowSystem;
    public ThemeName Theme { get; set; } = ThemeName.YellowEyeCare;

    public int MaxItemsPerLine { get; set; } = 9;

    public bool AutoHideWhenDocked { get; set; } = true;
    public double AutoHideDelaySeconds { get; set; } = 2.0;

    // 各組態獨立記憶的面板位置與吸附狀態
    public double? LastPanelLeft { get; set; }
    public double? LastPanelTop { get; set; }
    public DockEdge LastDockEdge { get; set; } = DockEdge.None;

    public List<ToolItemConfig> Tools { get; set; } = CreateDefaultTools();

    public int GetMaxItemsPerLine() => Math.Max(1, MaxItemsPerLine);

    public double GetButtonSize() => Size switch
    {
        PanelSize.ExtraSmall => 24,
        PanelSize.Small => 30,
        PanelSize.Large => 46,
        _ => 38
    };

    public double GetIconSize() => Size switch
    {
        PanelSize.ExtraSmall => 12,
        PanelSize.Small => 15,
        PanelSize.Large => 22,
        _ => 18
    };

    public PanelProfile Clone(string? newName = null) => new()
    {
        Id = newName is null ? Id : Guid.NewGuid().ToString("N"),
        Name = newName ?? Name,
        Size = Size,
        Orientation = Orientation,
        DisplayMode = DisplayMode,
        Theme = Theme,
        MaxItemsPerLine = MaxItemsPerLine,
        AutoHideWhenDocked = AutoHideWhenDocked,
        AutoHideDelaySeconds = AutoHideDelaySeconds,
        LastPanelLeft = LastPanelLeft,
        LastPanelTop = LastPanelTop,
        LastDockEdge = LastDockEdge,
        Tools = Tools.Select(t => t.Clone()).ToList()
    };

    public static List<ToolItemConfig> CreateDefaultTools() =>
        Enum.GetValues<ToolType>()
            .Select(t => new ToolItemConfig { Tool = t, IsVisible = true })
            .ToList();
}
