using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Resources.Strings;

namespace Com.MiniScreenNotes.Helpers;

public static class ToolMetadataHelper
{
    public static string GetDisplayName(ToolType tool) => tool switch
    {
        ToolType.Cursor => Strings.Tool_Cursor,
        ToolType.Pen => Strings.Tool_Pen,
        ToolType.Highlighter => Strings.Tool_Highlighter,
        ToolType.Line => Strings.Tool_Line,
        ToolType.Arrow => Strings.Tool_Arrow,
        ToolType.HollowRectangle => Strings.Tool_HollowRectangle,
        ToolType.FilledRectangle => Strings.Tool_FilledRectangle,
        ToolType.HollowEllipse => Strings.Tool_HollowEllipse,
        ToolType.FilledEllipse => Strings.Tool_FilledEllipse,
        ToolType.CheckMark => Strings.Tool_CheckMark,
        ToolType.CrossMark => Strings.Tool_CrossMark,
        ToolType.Text => Strings.Tool_Text,
        ToolType.StepNumber => Strings.Tool_StepNumber,
        ToolType.Eraser => Strings.Tool_Eraser,
        ToolType.Laser => Strings.Tool_Laser,
        ToolType.Spotlight => Strings.Tool_Spotlight,
        ToolType.Magnifier => Strings.Tool_Magnifier,
        ToolType.ColorPicker => Strings.Option_Color,
        ToolType.StrokeWidthPicker => Strings.Option_StrokeWidth,
        ToolType.ToggleVisibility => Strings.Action_ToggleVisibility,
        ToolType.BackgroundMode => Strings.Action_Background,
        ToolType.Undo => Strings.Action_Undo,
        ToolType.Redo => Strings.Action_Redo,
        ToolType.Clear => Strings.Action_Clear,
        ToolType.Copy => Strings.Action_Copy,
        ToolType.Save => Strings.Action_Save,
        _ => tool.ToString()
    };

    public static string GetIconResourceKey(ToolType tool) => tool switch
    {
        ToolType.Cursor => "Icon_Cursor",
        ToolType.Pen => "Icon_Pen",
        ToolType.Highlighter => "Icon_Highlighter",
        ToolType.Line => "Icon_Line",
        ToolType.Arrow => "Icon_Arrow",
        ToolType.HollowRectangle => "Icon_HollowRectangle",
        ToolType.FilledRectangle => "Icon_FilledRectangle",
        ToolType.HollowEllipse => "Icon_HollowEllipse",
        ToolType.FilledEllipse => "Icon_FilledEllipse",
        ToolType.CheckMark => "Icon_CheckMark",
        ToolType.CrossMark => "Icon_CrossMark",
        ToolType.Text => "Icon_Text",
        ToolType.StepNumber => "Icon_StepNumber",
        ToolType.Eraser => "Icon_Eraser",
        ToolType.Laser => "Icon_Laser",
        ToolType.Spotlight => "Icon_Spotlight",
        ToolType.Magnifier => "Icon_Magnifier",
        ToolType.ColorPicker => "Icon_Color",
        ToolType.StrokeWidthPicker => "Icon_StrokeWidth",
        ToolType.ToggleVisibility => "Icon_ToggleVisibility",
        ToolType.BackgroundMode => "Icon_Background",
        ToolType.Undo => "Icon_Undo",
        ToolType.Redo => "Icon_Redo",
        ToolType.Clear => "Icon_Clear",
        ToolType.Copy => "Icon_Copy",
        ToolType.Save => "Icon_Save",
        _ => "Icon_Pen"
    };

    public static bool IsSelectableModeTool(ToolType tool) => tool is
        ToolType.Cursor or
        ToolType.Pen or
        ToolType.Highlighter or
        ToolType.Line or
        ToolType.Arrow or
        ToolType.HollowRectangle or
        ToolType.FilledRectangle or
        ToolType.HollowEllipse or
        ToolType.FilledEllipse or
        ToolType.CheckMark or
        ToolType.CrossMark or
        ToolType.Text or
        ToolType.StepNumber or
        ToolType.Eraser or
        ToolType.Laser or
        ToolType.Spotlight or
        ToolType.Magnifier;
}
