using System.Globalization;

namespace MacKeys;

/// <summary>Maps virtual-key codes to the names used in shortcut strings like "Cmd+Shift+Z".</summary>
public static class KeyNames
{
    static readonly Dictionary<string, int> byName = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<int, string> byVk = new();

    static readonly Dictionary<int, string> macSymbols = new()
    {
        [0x25] = "←", [0x26] = "↑", [0x27] = "→", [0x28] = "↓",
        [0x08] = "⌫", [0x2E] = "⌦", [0x0D] = "↩", [0x09] = "⇥", [0x1B] = "⎋",
        [0x21] = "⇞", [0x22] = "⇟", [0x24] = "↖", [0x23] = "↘",
    };

    static KeyNames()
    {
        for (char c = 'A'; c <= 'Z'; c++) Add(c.ToString(), c);
        for (char c = '0'; c <= '9'; c++) Add(c.ToString(), c);
        for (int i = 1; i <= 24; i++) Add("F" + i, 0x6F + i);

        Add("Backspace", 0x08, "Back", "BS");
        Add("Tab", 0x09);
        Add("Enter", 0x0D, "Return");
        Add("Pause", 0x13, "Break");
        Add("CapsLock", 0x14);
        Add("Escape", 0x1B, "Esc");
        Add("Space", 0x20, "Spacebar");
        Add("PageUp", 0x21, "PgUp");
        Add("PageDown", 0x22, "PgDn");
        Add("End", 0x23);
        Add("Home", 0x24);
        Add("Left", 0x25, "←");
        Add("Up", 0x26, "↑");
        Add("Right", 0x27, "→");
        Add("Down", 0x28, "↓");
        Add("PrintScreen", 0x2C, "PrtSc", "PrintScrn");
        Add("Insert", 0x2D, "Ins");
        Add("Delete", 0x2E, "Del");
        Add("Menu", 0x5D, "Apps");
        for (int i = 0; i <= 9; i++) Add("Num" + i, 0x60 + i, "Numpad" + i);
        Add("NumMultiply", 0x6A);
        Add("NumAdd", 0x6B);
        Add("NumSubtract", 0x6D);
        Add("NumDecimal", 0x6E);
        Add("NumDivide", 0x6F);
        Add("NumLock", 0x90);
        Add("ScrollLock", 0x91);
        Add("BrowserBack", 0xA6);
        Add("BrowserForward", 0xA7);
        Add("VolumeMute", 0xAD);
        Add("VolumeDown", 0xAE);
        Add("VolumeUp", 0xAF);
        Add("MediaNext", 0xB0);
        Add("MediaPrevious", 0xB1);
        Add("MediaStop", 0xB2);
        Add("MediaPlayPause", 0xB3);
        Add(";", 0xBA);
        Add("=", 0xBB);
        Add(",", 0xBC);
        Add("-", 0xBD);
        Add(".", 0xBE);
        Add("/", 0xBF);
        Add("`", 0xC0);
        Add("[", 0xDB);
        Add("\\", 0xDC);
        Add("]", 0xDD);
        Add("'", 0xDE);
        Add("OEM102", 0xE2); // extra key next to Left Shift on ISO keyboards
    }

    static void Add(string name, int vk, params string[] aliases)
    {
        byName[name] = vk;
        byVk.TryAdd(vk, name);
        foreach (var alias in aliases) byName[alias] = vk;
    }

    public static bool TryGetVk(string name, out int vk)
    {
        name = name.Trim();
        if (byName.TryGetValue(name, out vk)) return true;
        // Keys without a friendly name round-trip as "VK" + hex code.
        return name.Length > 2 && name.StartsWith("VK", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(name.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vk)
            && vk is > 0 and < 0xFF;
    }

    public static string Name(int vk) => byVk.TryGetValue(vk, out var name) ? name : $"VK{vk:X2}";

    public static string MacSymbol(int vk) => macSymbols.TryGetValue(vk, out var symbol) ? symbol : Name(vk);

    /// <summary>Splits "Mod+Mod+Key" into its modifier names and key code.</summary>
    public static bool TrySplit(string? text, out string[] modifiers, out int key)
    {
        modifiers = [];
        key = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (!TryGetVk(parts[^1], out key)) return false;
        modifiers = parts[..^1];
        return true;
    }
}
