namespace Com.MiniScreenNotes.Models;

public sealed class AppConfig
{
    public string ActiveProfileId { get; set; } = string.Empty;
    public List<PanelProfile> Profiles { get; set; } = [];

    // 上次面板位置與吸附狀態
    public double? LastPanelLeft { get; set; }
    public double? LastPanelTop { get; set; }
    public DockEdge LastDockEdge { get; set; } = DockEdge.None;

    // 全域快捷鍵（預設 Ctrl + Alt + A）
    public uint HotkeyModifiers { get; set; } = 0x0001 | 0x0002; // MOD_ALT | MOD_CONTROL
    public uint HotkeyVirtualKey { get; set; } = 0x41; // 'A'

    // 一鍵快速儲存
    public bool QuickSaveEnabled { get; set; }
    public string QuickSaveDirectory { get; set; } =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "MiniScreenNotes");
    public ImageSaveFormat QuickSaveFormat { get; set; } = ImageSaveFormat.Png;
    public string? LastSaveDirectory { get; set; }

    // 開機自動啟動
    public bool LaunchAtStartup { get; set; }

    // 面板顯示時以滑鼠中鍵切換游標模式
    public bool MiddleClickToggleEnabled { get; set; } = true;

    // 繪圖偏好記憶
    public string SelectedColorHex { get; set; } = "#EF4444";
    public double StrokeWidth { get; set; } = 4.0;
}
