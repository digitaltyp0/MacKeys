using System.Text;
using System.Text.RegularExpressions;

namespace MacKeys;

[Flags]
public enum MacMods { None = 0, Control = 1, Option = 2, Shift = 4, Command = 8 }

[Flags]
public enum WinMods { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }

/// <summary>A shortcut as written on a Mac, e.g. Cmd+Shift+Z. Always includes Cmd or Option.</summary>
public readonly record struct MacChord(MacMods Mods, int Key)
{
    public bool Has(MacMods mod) => (Mods & mod) != 0;

    public static bool TryParse(string? text, out MacChord chord)
    {
        chord = default;
        if (!KeyNames.TrySplit(text, out var modifiers, out int key)) return false;
        var mods = MacMods.None;
        foreach (var m in modifiers)
        {
            switch (m.ToLowerInvariant())
            {
                case "cmd" or "command" or "⌘": mods |= MacMods.Command; break;
                case "option" or "opt" or "⌥": mods |= MacMods.Option; break;
                case "ctrl" or "control" or "⌃": mods |= MacMods.Control; break;
                case "shift" or "⇧": mods |= MacMods.Shift; break;
                default: return false;
            }
        }
        if ((mods & (MacMods.Command | MacMods.Option)) == 0) return false;
        chord = new MacChord(mods, key);
        return true;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Has(MacMods.Control)) sb.Append("Ctrl+");
        if (Has(MacMods.Option)) sb.Append("Option+");
        if (Has(MacMods.Command)) sb.Append("Cmd+");
        if (Has(MacMods.Shift)) sb.Append("Shift+");
        return sb.Append(KeyNames.Name(Key)).ToString();
    }

    /// <summary>Mac menu style, e.g. ⇧⌘Z.</summary>
    public string ToSymbols()
    {
        var sb = new StringBuilder();
        if (Has(MacMods.Control)) sb.Append('⌃');
        if (Has(MacMods.Option)) sb.Append('⌥');
        if (Has(MacMods.Shift)) sb.Append('⇧');
        if (Has(MacMods.Command)) sb.Append('⌘');
        return sb.Append(KeyNames.MacSymbol(Key)).ToString();
    }

    /// <summary>The same physical keys as a Windows chord (⌘ → Alt, ⌥ → Win).</summary>
    public WinChord ToNative() => new(
        (Has(MacMods.Control) ? WinMods.Ctrl : 0) | (Has(MacMods.Option) ? WinMods.Win : 0) |
        (Has(MacMods.Command) ? WinMods.Alt : 0) | (Has(MacMods.Shift) ? WinMods.Shift : 0), Key);
}

/// <summary>A Windows key combination to send, e.g. Ctrl+Y.</summary>
public readonly record struct WinChord(WinMods Mods, int Key)
{
    public static bool TryParse(string? text, out WinChord chord)
    {
        chord = default;
        if (!KeyNames.TrySplit(text, out var modifiers, out int key)) return false;
        var mods = WinMods.None;
        foreach (var m in modifiers)
        {
            switch (m.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= WinMods.Ctrl; break;
                case "alt": mods |= WinMods.Alt; break;
                case "shift": mods |= WinMods.Shift; break;
                case "win" or "windows": mods |= WinMods.Win; break;
                default: return false;
            }
        }
        chord = new WinChord(mods, key);
        return true;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if ((Mods & WinMods.Win) != 0) sb.Append("Win+");
        if ((Mods & WinMods.Ctrl) != 0) sb.Append("Ctrl+");
        if ((Mods & WinMods.Alt) != 0) sb.Append("Alt+");
        if ((Mods & WinMods.Shift) != 0) sb.Append("Shift+");
        return sb.Append(KeyNames.Name(Key)).ToString();
    }
}

public enum ActionKind { Keys, Native, Minimize, Lock, Nothing }

/// <summary>What a shortcut does: send Windows keys (optionally several steps joined by "then") or a special action.</summary>
public sealed class ActionSpec
{
    public static readonly (ActionKind Kind, string Token, string Label)[] Specials =
    [
        (ActionKind.Native, "{Native}", "Native Alt / Win shortcut"),
        (ActionKind.Minimize, "{Minimize}", "Minimize window"),
        (ActionKind.Lock, "{Lock}", "Lock screen"),
        (ActionKind.Nothing, "{Nothing}", "Do nothing"),
    ];

    static readonly Regex StepSeparator = new(@"\s+then\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public ActionKind Kind { get; }
    public WinChord[] Steps { get; }

    ActionSpec(ActionKind kind, WinChord[] steps)
    {
        Kind = kind;
        Steps = steps;
    }

    public static ActionSpec Keys(params WinChord[] steps) => new(ActionKind.Keys, steps);

    public static ActionSpec Special(ActionKind kind) => new(kind, []);

    public static bool TryParse(string? text, out ActionSpec spec)
    {
        spec = Special(ActionKind.Nothing);
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        foreach (var s in Specials)
        {
            if (string.Equals(text, s.Token, StringComparison.OrdinalIgnoreCase))
            {
                spec = Special(s.Kind);
                return true;
            }
        }
        var parts = StepSeparator.Split(text);
        var steps = new WinChord[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!WinChord.TryParse(parts[i], out steps[i])) return false;
        spec = Keys(steps);
        return true;
    }

    public string Label => Kind == ActionKind.Keys ? ToString() : Specials.First(s => s.Kind == Kind).Label;

    public override string ToString() =>
        Kind == ActionKind.Keys ? string.Join(" then ", Steps) : Specials.First(s => s.Kind == Kind).Token;
}
