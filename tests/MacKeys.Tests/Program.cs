// Dependency-free checks for the remapping logic. Run with: dotnet run --project tests/MacKeys.Tests
// The engine runs without installing hooks; key state and injected input are simulated.
using MacKeys;
using static MacKeys.Native;

// `--screenshot <folder>` renders the settings windows to PNGs instead of running checks (no hooks installed).
if (args is ["--screenshot", var folder])
{
    var thread = new Thread(() => Screenshots.Render(folder));
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    return 0;
}

int failures = 0;
int passed = 0;

void Check(string name, bool condition, string detail = "")
{
    if (condition) passed++;
    else
    {
        failures++;
        Console.WriteLine($"FAIL  {name}  {detail}");
    }
}

// ---------------------------------------------------------------- defaults

var defaults = DefaultShortcuts.Create();
var seen = new Dictionary<MacChord, string>();
foreach (var s in defaults)
{
    Check($"default parses: {s.Mac}", MacChord.TryParse(s.Mac, out var chord));
    Check($"default action parses: {s.Windows}", ActionSpec.TryParse(s.Windows, out _));
    Check($"default not duplicated: {s.Mac}", seen.TryAdd(chord, s.Description), $"also used by {seen.GetValueOrDefault(chord)}");
}
Console.WriteLine($"{defaults.Count} default shortcuts");

// ---------------------------------------------------------------- parsing

Check("round trip", MacChord.TryParse("shift+cmd+z", out var rt) && rt.ToString() == "Cmd+Shift+Z", rt.ToString());
Check("symbols", rt.ToSymbols() == "⇧⌘Z", rt.ToSymbols());
Check("needs cmd or option", !MacChord.TryParse("Ctrl+C", out _));
Check("comma key", MacChord.TryParse("Cmd+,", out var comma) && comma.Key == 0xBC);
Check("sequence", ActionSpec.TryParse("Shift+Home then Backspace", out var seq) && seq.Steps.Length == 2 && seq.ToString() == "Shift+Home then Backspace");
Check("special", ActionSpec.TryParse("{minimize}", out var min) && min.Kind == ActionKind.Minimize);
Check("bad key", !WinChord.TryParse("Ctrl+Nope", out _));

// ---------------------------------------------------------------- engine

var held = new HashSet<int>();
var sent = new List<(int vk, bool up)>();

RemapEngine NewEngine(Action<AppSettings>? tweak = null)
{
    var settings = new AppSettings();
    tweak?.Invoke(settings);
    var engine = new RemapEngine(EngineConfig.From(settings))
    {
        IsKeyDown = vk => held.Contains(vk)
            || (vk == VK_CONTROL && (held.Contains(VK_LCONTROL) || held.Contains(VK_RCONTROL)))
            || (vk == VK_SHIFT && (held.Contains(VK_LSHIFT) || held.Contains(VK_RSHIFT))),
        Inject = inputs => sent.AddRange(inputs.Select(i => i.type == INPUT_MOUSE
            ? (-1, (i.u.mi.dwFlags & MOUSEEVENTF_LEFTUP) != 0)
            : ((int)i.u.ki.wVk, (i.u.ki.dwFlags & KEYEVENTF_KEYUP) != 0))),
        IsForegroundExcluded = _ => false,
    };
    held.Clear();
    sent.Clear();
    return engine;
}

// Simulates a physical key event; returns whether the engine swallowed it.
// Pass-through keys update the simulated key state like Windows would.
bool Key(RemapEngine e, int vk, bool down)
{
    bool swallowed = e.OnKey(vk, MapVirtualKey((uint)vk, 0), 0, down);
    if (!swallowed)
    {
        if (down) held.Add(vk);
        else held.Remove(vk);
    }
    return swallowed;
}

string Sent() => string.Join(" ", sent.Select(s => (s.up ? "-" : "+") + KeyNames.Name(s.vk) switch
{
    _ when s.vk == -1 => "Click",
    var n when s.vk == VK_LCONTROL => "LCtrl",
    var n when s.vk == VK_LSHIFT => "LShift",
    var n when s.vk == VK_LMENU => "LAlt",
    var n when s.vk == VK_LWIN => "LWin",
    var n => n,
}));

{
    var e = NewEngine();
    Check("cmd down swallowed", Key(e, VK_LMENU, true));
    Check("cmd+c swallowed", Key(e, 'C', true));
    Check("cmd+c → ctrl+c", Sent() == "+LCtrl +C -C -LCtrl", Sent());
    Check("c up swallowed", Key(e, 'C', false));
    sent.Clear();
    Check("cmd up swallowed", Key(e, VK_LMENU, false));
    Check("no alt tap after use", sent.Count == 0, Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LMENU, true);
    Key(e, VK_LMENU, false);
    Check("lone cmd tap → alt tap", Sent() == "+LAlt -LAlt", Sent());
}

{
    var e = NewEngine(s => s.CommandTapSendsAlt = false);
    Key(e, VK_LMENU, true);
    Key(e, VK_LMENU, false);
    Check("lone cmd tap disabled", sent.Count == 0, Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LMENU, true);
    Check("cmd+tab swallowed and replayed", Key(e, 0x09, true));
    Check("cmd+tab → alt down + tab", Sent() == "+LAlt +Tab", Sent());
    held.Add(VK_LMENU);
    held.Add(0x09); // the replayed events are now "down" as far as Windows is concerned
    Check("tab up passes", !Key(e, 0x09, false));
    sent.Clear();
    Check("second tab passes natively", !Key(e, 0x09, true));
    Check("cmd+c while switching stays native", !Key(e, 'C', true) && sent.Count == 0, Sent());
    Key(e, 'C', false);
    Check("cmd up passes to release alt", !Key(e, VK_LMENU, false));
}

{
    var e = NewEngine();
    Key(e, VK_LSHIFT, true);
    Key(e, VK_LMENU, true);
    Check("cmd+shift+z swallowed", Key(e, 'Z', true));
    Check("⇧⌘Z → Ctrl+Y with shift lifted", Sent() == "-LShift +LCtrl +Y -Y -LCtrl +LShift", Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LMENU, true);
    Key(e, 'J', true);
    Check("fallback ⌘J → Ctrl+J", Sent() == "+LCtrl +J -J -LCtrl", Sent());
}

{
    var e = NewEngine(s => s.CommandFallbackToCtrl = false);
    Key(e, VK_LMENU, true);
    Key(e, 'J', true);
    Check("no fallback → native Alt+J", Sent() == "+LAlt +J", Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LCONTROL, true);
    Key(e, VK_LMENU, true);
    Key(e, 'A', true);
    Check("Ctrl+Alt+A stays native (AltGr emulation)", Sent() == "+LAlt +A", Sent());
}

{
    var e = NewEngine();
    Check("AltGr passes through", !Key(e, VK_RMENU, true));
    Check("AltGr+A passes through", !Key(e, 'A', true));
    Check("AltGr nothing sent", sent.Count == 0, Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LWIN, true);
    Key(e, 0x25, true);
    Check("⌥← → Ctrl+Left", Sent() == "+LCtrl +Left -Left -LCtrl", Sent());
    sent.Clear();
    Key(e, 0x25, false);
    Key(e, VK_LWIN, false);
    Check("no start menu after ⌥←", sent.Count == 0, Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LWIN, true);
    Key(e, VK_LWIN, false);
    Check("lone ⌥ tap → Start", Sent() == "+LWin -LWin", Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LWIN, true);
    Key(e, 'E', true);
    Check("Win+E stays native", Sent() == "+LWin +E", Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LMENU, true);
    Key(e, 0x08, true);
    Check("⌘⌫ → sequence", Sent() == "+LShift +Home -Home -LShift +Backspace -Backspace", Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LMENU, true);
    Key(e, 0x25, true);
    sent.Clear();
    Key(e, 0x25, true); // auto-repeat
    Check("auto-repeat repeats action", Sent() == "+Home -Home", Sent());
    Key(e, VK_LMENU, false);
    sent.Clear();
    Check("repeat after cmd released is swallowed", Key(e, 0x25, true) && sent.Count == 0);
    Check("release after cmd released is swallowed", Key(e, 0x25, false));
    Check("next plain press passes", !Key(e, 0x25, true));
}

{
    var e = NewEngine(s => s.Enabled = false);
    Check("disabled: alt passes", !Key(e, VK_LMENU, true));
    Check("disabled: alt+c passes", !Key(e, 'C', true));
    Check("disabled: nothing sent", sent.Count == 0);
}

{
    var e = NewEngine();
    e.IsForegroundExcluded = _ => true;
    Check("excluded app: alt passes", !Key(e, VK_LMENU, true));
    Check("excluded app: alt+c passes", !Key(e, 'C', true));
}

{
    var e = NewEngine();
    MacChord? captured = null;
    e.CaptureNext(c => captured = c);
    Key(e, VK_LMENU, true);
    Key(e, VK_LSHIFT, true);
    Key(e, 'K', true);
    Check("capture records chord", captured?.ToString() == "Cmd+Shift+K", captured?.ToString() ?? "null");
    Check("capture sends nothing", sent.Count == 0, Sent());
}

{
    var e = NewEngine();
    Key(e, VK_LMENU, true);
    Check("⌘-click swallowed", e.OnMouse(WM_LBUTTONDOWN));
    Check("⌘-click up swallowed", e.OnMouse(WM_LBUTTONUP));
    Check("⌘-click → ctrl-click", Sent() == "+LCtrl +Click -Click -LCtrl", Sent());
    sent.Clear();
    Key(e, VK_LMENU, false);
    Check("no alt tap after click", sent.Count == 0, Sent());
}

Console.WriteLine(failures == 0 ? $"All {passed} checks passed." : $"{failures} failed, {passed} passed.");
return failures == 0 ? 0 : 1;
