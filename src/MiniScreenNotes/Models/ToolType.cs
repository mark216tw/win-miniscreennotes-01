namespace Com.MiniScreenNotes.Models;

public enum ToolType
{
    // 標註工具（16 項 + 游標模式）
    Cursor,
    Pen,
    Highlighter,
    Line,
    Arrow,
    HollowRectangle,
    FilledRectangle,
    HollowEllipse,
    FilledEllipse,
    CheckMark,
    CrossMark,
    Text,
    StepNumber,
    Eraser,
    Laser,
    Spotlight,
    Magnifier,

    // 選項與功能項目（可自訂顯示與排序）
    ColorPicker,
    StrokeWidthPicker,
    ToggleVisibility,
    BackgroundMode,
    Undo,
    Redo,
    Clear,
    Copy,
    Save
}
