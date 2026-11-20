namespace Com.MiniScreenNotes.Helpers;

public static class HotkeyDisplayHelper
{
    public static string GetKeyName(uint virtualKey)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x7B)
        {
            return $"F{virtualKey - 0x70 + 1}";
        }

        return "A";
    }

    public static string Format(uint modifiers, uint virtualKey)
    {
        var parts = new List<string>(4);

        if ((modifiers & Win32Interop.ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & Win32Interop.ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & Win32Interop.ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & Win32Interop.ModWin) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(GetKeyName(virtualKey));
        return string.Join("+", parts);
    }
}
