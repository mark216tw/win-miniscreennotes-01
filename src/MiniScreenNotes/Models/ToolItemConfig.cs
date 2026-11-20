namespace Com.MiniScreenNotes.Models;

public sealed class ToolItemConfig
{
    public ToolType Tool { get; set; }
    public bool IsVisible { get; set; } = true;

    public ToolItemConfig Clone() => new()
    {
        Tool = Tool,
        IsVisible = IsVisible
    };
}
