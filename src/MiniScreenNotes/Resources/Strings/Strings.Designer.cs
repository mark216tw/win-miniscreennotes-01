#nullable enable
using System.Globalization;
using System.Resources;

namespace Com.MiniScreenNotes.Resources.Strings;

public static class Strings
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager("Com.MiniScreenNotes.Resources.Strings.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture { get; set; }

    private static string Get(string name) => ResourceManager.GetString(name, Culture) ?? name;

    public static string AppTitle => Get(nameof(AppTitle));
    public static string Tool_Cursor => Get(nameof(Tool_Cursor));
    public static string Tool_Pen => Get(nameof(Tool_Pen));
    public static string Tool_Highlighter => Get(nameof(Tool_Highlighter));
    public static string Tool_Line => Get(nameof(Tool_Line));
    public static string Tool_Arrow => Get(nameof(Tool_Arrow));
    public static string Tool_HollowRectangle => Get(nameof(Tool_HollowRectangle));
    public static string Tool_FilledRectangle => Get(nameof(Tool_FilledRectangle));
    public static string Tool_HollowEllipse => Get(nameof(Tool_HollowEllipse));
    public static string Tool_FilledEllipse => Get(nameof(Tool_FilledEllipse));
    public static string Tool_CheckMark => Get(nameof(Tool_CheckMark));
    public static string Tool_CrossMark => Get(nameof(Tool_CrossMark));
    public static string Tool_Text => Get(nameof(Tool_Text));
    public static string Tool_StepNumber => Get(nameof(Tool_StepNumber));
    public static string Tool_Eraser => Get(nameof(Tool_Eraser));
    public static string Tool_Laser => Get(nameof(Tool_Laser));
    public static string Tool_Spotlight => Get(nameof(Tool_Spotlight));
    public static string Tool_Magnifier => Get(nameof(Tool_Magnifier));
    public static string Option_Color => Get(nameof(Option_Color));
    public static string Option_StrokeWidth => Get(nameof(Option_StrokeWidth));
    public static string Action_ToggleVisibility => Get(nameof(Action_ToggleVisibility));
    public static string Action_Background => Get(nameof(Action_Background));
    public static string Action_Undo => Get(nameof(Action_Undo));
    public static string Action_Redo => Get(nameof(Action_Redo));
    public static string Action_Clear => Get(nameof(Action_Clear));
    public static string Action_Copy => Get(nameof(Action_Copy));
    public static string Action_Save => Get(nameof(Action_Save));
    public static string Toolbar_Drag => Get(nameof(Toolbar_Drag));
    public static string Toolbar_Settings => Get(nameof(Toolbar_Settings));
    public static string Toolbar_Close => Get(nameof(Toolbar_Close));
    public static string Background_Transparent => Get(nameof(Background_Transparent));
    public static string Background_Whiteboard => Get(nameof(Background_Whiteboard));
    public static string Background_Blackboard => Get(nameof(Background_Blackboard));
    public static string Toast_Copied => Get(nameof(Toast_Copied));
    public static string Toast_Saved => Get(nameof(Toast_Saved));
    public static string Toast_StepReset => Get(nameof(Toast_StepReset));
    public static string Toast_CopyFailed => Get(nameof(Toast_CopyFailed));
    public static string Toast_SaveFailed => Get(nameof(Toast_SaveFailed));
    public static string Toast_HotkeyFailed => Get(nameof(Toast_HotkeyFailed));
    public static string Tray_ShowHide => Get(nameof(Tray_ShowHide));
    public static string Tray_Profiles => Get(nameof(Tray_Profiles));
    public static string Tray_ClearAll => Get(nameof(Tray_ClearAll));
    public static string Tray_AutoStart => Get(nameof(Tray_AutoStart));
    public static string Tray_Settings => Get(nameof(Tray_Settings));
    public static string Tray_About => Get(nameof(Tray_About));
    public static string Tray_Exit => Get(nameof(Tray_Exit));
    public static string Settings_Title => Get(nameof(Settings_Title));
    public static string About_Title => Get(nameof(About_Title));
    public static string About_Privacy => Get(nameof(About_Privacy));
    public static string About_License => Get(nameof(About_License));
    public static string Confirm_DeleteProfile => Get(nameof(Confirm_DeleteProfile));
    public static string Confirm_Title => Get(nameof(Confirm_Title));
    public static string Hotkey_NeedModifier => Get(nameof(Hotkey_NeedModifier));
    public static string Hotkey_Applied => Get(nameof(Hotkey_Applied));
    public static string Hotkey_ApplyFailed => Get(nameof(Hotkey_ApplyFailed));
    public static string Hotkey_ApplyFailedOldKept => Get(nameof(Hotkey_ApplyFailedOldKept));
    public static string Settings_MiddleClickToggle => Get(nameof(Settings_MiddleClickToggle));
    public static string Tooltip_StepNumberHint => Get(nameof(Tooltip_StepNumberHint));
    public static string Tooltip_AnnotationsVisible => Get(nameof(Tooltip_AnnotationsVisible));
    public static string Tooltip_AnnotationsHidden => Get(nameof(Tooltip_AnnotationsHidden));
    public static string HexColor_Invalid => Get(nameof(HexColor_Invalid));
    public static string Toast_OpenFolderHint => Get(nameof(Toast_OpenFolderHint));
    public static string Toast_QuickSaveFallback => Get(nameof(Toast_QuickSaveFallback));
    public static string Settings_About => Get(nameof(Settings_About));
    public static string About_Close => Get(nameof(About_Close));
    public static string About_SystemInfo => Get(nameof(About_SystemInfo));
    public static string About_Os => Get(nameof(About_Os));
    public static string About_Architecture => Get(nameof(About_Architecture));
    public static string About_Runtime => Get(nameof(About_Runtime));
    public static string About_Displays => Get(nameof(About_Displays));
    public static string About_DataFolder => Get(nameof(About_DataFolder));
    public static string About_GlobalHotkey => Get(nameof(About_GlobalHotkey));
    public static string About_Shortcuts => Get(nameof(About_Shortcuts));
    public static string About_Shortcut_TogglePanel => Get(nameof(About_Shortcut_TogglePanel));
    public static string About_Shortcut_ToggleCursor => Get(nameof(About_Shortcut_ToggleCursor));
    public static string About_Shortcut_Undo => Get(nameof(About_Shortcut_Undo));
    public static string About_Shortcut_Redo => Get(nameof(About_Shortcut_Redo));
    public static string About_Shortcut_Copy => Get(nameof(About_Shortcut_Copy));
    public static string About_Shortcut_Save => Get(nameof(About_Shortcut_Save));
    public static string About_Shortcut_ClosePanel => Get(nameof(About_Shortcut_ClosePanel));
    public static string About_Shortcut_CursorMode => Get(nameof(About_Shortcut_CursorMode));
    public static string About_Shortcut_ConfirmText => Get(nameof(About_Shortcut_ConfirmText));
    public static string About_Shortcut_Magnifier => Get(nameof(About_Shortcut_Magnifier));
    public static string About_Shortcut_Spotlight => Get(nameof(About_Shortcut_Spotlight));
    public static string About_Shortcut_StepNumber => Get(nameof(About_Shortcut_StepNumber));
    public static string About_VersionFormat => Get(nameof(About_VersionFormat));
    public static string About_FileVersionFormat => Get(nameof(About_FileVersionFormat));
    public static string About_LicenseFormat => Get(nameof(About_LicenseFormat));
    public static string About_ArchitectureValue => Get(nameof(About_ArchitectureValue));
    public static string About_DisplayEntry => Get(nameof(About_DisplayEntry));
    public static string About_PrimarySuffix => Get(nameof(About_PrimarySuffix));
    public static string About_HotkeyCustomizable => Get(nameof(About_HotkeyCustomizable));
    public static string About_Key_MiddleMouse => Get(nameof(About_Key_MiddleMouse));
    public static string About_Key_RightMouse => Get(nameof(About_Key_RightMouse));
    public static string About_Key_Wheel => Get(nameof(About_Key_Wheel));
    public static string About_Key_WheelCtrl => Get(nameof(About_Key_WheelCtrl));
}
