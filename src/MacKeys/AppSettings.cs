using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacKeys;

public enum CommandKeyChoice { LeftAlt, RightAlt, BothAlt }

public enum OptionKeyChoice { Off, LeftWin, BothWin }

public sealed class Shortcut
{
    public string Mac { get; set; } = "";
    public string Windows { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Custom";
    public bool Enabled { get; set; } = true;

    public Shortcut Clone() => (Shortcut)MemberwiseClone();
}

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public CommandKeyChoice CommandKey { get; set; } = CommandKeyChoice.LeftAlt;
    public OptionKeyChoice OptionKey { get; set; } = OptionKeyChoice.LeftWin;
    public bool CommandFallbackToCtrl { get; set; } = true;
    public bool CommandClickIsCtrlClick { get; set; } = true;
    public bool CommandTapSendsAlt { get; set; } = true;
    public bool OptionTapSendsWin { get; set; } = true;
    public List<string> ExcludedApps { get; set; } = [];
    public List<Shortcut> Shortcuts { get; set; } = DefaultShortcuts.Create();

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacKeys");

    public static string FilePath => Path.Combine(Folder, "settings.json");

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load(out bool firstRun)
    {
        firstRun = !File.Exists(FilePath);
        if (firstRun) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Keep the broken file for the user and start over with defaults.
            File.Copy(FilePath, FilePath + ".broken", overwrite: true);
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, FilePath, overwrite: true);
    }

    /// <summary>The keys to press on a Windows keyboard for a Mac shortcut, e.g. Alt+Shift+Z for ⇧⌘Z.</summary>
    public string PhysicalKeys(MacChord chord)
    {
        var parts = new List<string>();
        if (chord.Has(MacMods.Control)) parts.Add("Ctrl");
        if (chord.Has(MacMods.Option))
        {
            if (OptionKey == OptionKeyChoice.Off) return "— (⌥ is off)";
            parts.Add("Win");
        }
        if (chord.Has(MacMods.Command)) parts.Add(CommandKey == CommandKeyChoice.RightAlt ? "Right Alt" : "Alt");
        if (chord.Has(MacMods.Shift)) parts.Add("Shift");
        parts.Add(KeyNames.Name(chord.Key));
        return string.Join("+", parts);
    }
}
