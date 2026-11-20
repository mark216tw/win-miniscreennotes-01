using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;

namespace Com.MiniScreenNotes.ViewModels;

public sealed class ToolSettingItemViewModel : ObservableObject
{
    private bool _isVisible;

    public ToolSettingItemViewModel(ToolItemConfig source)
    {
        Tool = source.Tool;
        DisplayName = ToolMetadataHelper.GetDisplayName(source.Tool);
        _isVisible = source.IsVisible;
    }

    public ToolType Tool { get; }
    public string DisplayName { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public ToolItemConfig ToConfig() => new()
    {
        Tool = Tool,
        IsVisible = IsVisible
    };
}
