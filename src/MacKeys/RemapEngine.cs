using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static MacKeys.Native;

namespace MacKeys;

/// <summary>Immutable snapshot of the settings the hook reads; replaced as a whole when settings change.</summary>
public sealed class EngineConfig
{
    public bool Enabled { get; init; }
    public HashSet<int> CommandKeys { get; init; } = [];
    public HashSet<int> OptionKeys { get; init; } = [];
    public bool CommandFallbackToCtrl { get; init; }
    public bool CommandClickIsCtrlClick { get; init; }
    public bool CommandTapSendsAlt { get; init; }
    public bool OptionTapSendsWin { get; init; }
    public HashSet<string> ExcludedApps { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<MacChord, ActionSpec> Map { get; init; } = [];

    public static EngineConfig From(AppSettings s)
    {
        var map = new Dictionary<MacChord, ActionSpec>();
        foreach (var shortcut in s.Shortcuts)
        {
            if (shortcut.Enabled && MacChord.TryParse(shortcut.Mac, out var chord)
                && ActionSpec.TryParse(shortcut.Windows, out var action))
                map.TryAdd(chord, action);
        }

        return new EngineConfig
        {
            Enabled = s.Enabled,
            CommandKeys = s.CommandKey switch
            {
                CommandKeyChoice.RightAlt => new HashSet<int> { VK_RMENU },
                CommandKeyChoice.BothAlt => new HashSet<int> { VK_LMENU, VK_RMENU },
                _ => new HashSet<int> { VK_LMENU },
            },
            OptionKeys = s.OptionKey switch
            {
                OptionKeyChoice.LeftWin => new HashSet<int> { VK_LWIN },
                OptionKeyChoice.BothWin => new HashSet<int> { VK_LWIN, VK_RWIN },
                _ => new HashSet<int>(),
            },
            CommandFallbackToCtrl = s.CommandFallbackToCtrl,
            CommandClickIsCtrlClick = s.CommandClickIsCtrlClick,
            CommandTapSendsAlt = s.CommandTapSendsAlt,
            OptionTapSendsWin = s.OptionTapSendsWin,
            ExcludedApps = s.ExcludedApps
                .Select(a => a.Trim())
                .Select(a => a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? a[..^4] : a)
                .Where(a => a.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            Map = map,
        };
    }
}

/// <summary>
/// Turns the ⌘ key (Alt) and ⌥ key (Win) into Mac-style modifiers using low-level keyboard and mouse hooks.
/// The modifier key itself is swallowed while held; each key pressed with it is either translated through the
/// shortcut map or replayed with the real Alt / Win key so native shortcuts like Alt+Tab keep working.
/// </summary>
public sealed class RemapEngine : IDisposable
{
    // Tags input we synthesize so it is never mistaken for the user's own key presses.
    static readonly IntPtr Marker = new(0x4D4B4559);

    sealed class Layer
    {
        public bool Held;       // the physical key is down
        public bool Used;       // another key or click happened while held (so releasing it is not a "tap")
        public bool NativeDown; // we replayed the real Alt / Win key down, so the system sees it as held
        public bool Bypass;     // pressed while an excluded app was in front: everything passes through
        public int Vk;

        public void Reset() => Held = Used = NativeDown = Bypass = false;
    }

    readonly Layer cmd = new();
    readonly Layer opt = new();
    readonly HashSet<int> consumed = []; // keys whose press we replaced; their release must be swallowed too
    readonly Dictionary<uint, string> processNames = [];
    bool ctrlClick;

    volatile EngineConfig config;
    volatile bool suspended;
    volatile bool resetRequested;
    volatile Action<MacChord>? capture;

    Thread? thread;
    uint threadId;
    IntPtr keyboardHook, mouseHook;
    HookProc? keyboardProc, mouseProc;

    // Seams so the decision logic can be exercised without touching real input.
    internal Func<int, bool> IsKeyDown { get; set; } = vk => GetAsyncKeyState(vk) < 0;
    internal Action<INPUT[]> Inject { get; set; } = inputs => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    internal Func<EngineConfig, bool> IsForegroundExcluded { get; set; }

    public RemapEngine(EngineConfig config)
    {
        this.config = config;
        IsForegroundExcluded = ForegroundAppIsExcluded;
    }

    public EngineConfig Config
    {
        get => config;
        set => config = value;
    }

    /// <summary>While suspended, keys pass through untouched (used while recording Windows shortcuts).</summary>
    public bool Suspended
    {
        get => suspended;
        set => suspended = value;
    }

    /// <summary>Delivers the next ⌘ / ⌥ chord to <paramref name="callback"/> (on the hook thread) instead of running it.</summary>
    public void CaptureNext(Action<MacChord>? callback) => capture = callback;

    /// <summary>Forgets held keys, e.g. after the session was locked and key releases went to the lock screen.</summary>
    public void ResetState() => resetRequested = true;

    public void Start()
    {
        using var ready = new ManualResetEventSlim();
        int error = 0;
        thread = new Thread(() =>
        {
            threadId = GetCurrentThreadId();
            keyboardProc = KeyboardHook;
            mouseProc = MouseHook;
            var module = GetModuleHandle(null);
            keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc, module, 0);
            if (keyboardHook == IntPtr.Zero) error = Marshal.GetLastWin32Error();
            mouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseProc, module, 0);
            ready.Set();
            if (keyboardHook == IntPtr.Zero) return;

            // Low-level hooks are called from inside GetMessage on the installing thread.
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }

            UnhookWindowsHookEx(keyboardHook);
            if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
        })
        {
            IsBackground = true,
            Name = "MacKeys input hook",
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
        ready.Wait();
        if (keyboardHook == IntPtr.Zero) throw new Win32Exception(error, "Could not install the keyboard hook.");
    }

    public void Dispose()
    {
        if (thread == null) return;
        PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        thread.Join(1000);
        thread = null;
    }

    IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if ((k.flags & LLKHF_INJECTED) == 0)
            {
                int msg = (int)wParam;
                bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                try
                {
                    if (OnKey((int)k.vkCode, k.scanCode, k.flags, down)) return 1;
                }
                catch (Exception ex)
                {
                    // Never let an exception escape into the hook chain; worst case the key passes through.
                    Debug.WriteLine(ex);
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        int msg = (int)wParam;
        if (nCode >= 0 && msg is WM_LBUTTONDOWN or WM_LBUTTONUP or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
        {
            var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            try
            {
                if ((m.flags & LLMHF_INJECTED) == 0 && OnMouse(msg)) return 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    /// <summary>Handles one physical key event. Returns true to swallow it.</summary>
    internal bool OnKey(int vk, uint scan, uint flags, bool down)
    {
        ApplyPendingReset();
        var cfg = config;
        bool active = (cfg.Enabled && !suspended) || capture != null;

        var layer = LayerFor(vk, cfg, active);
        if (layer != null) return OnLayerKey(layer, vk, down, cfg);

        if (!down) return consumed.Remove(vk);

        bool cmdOn = cmd.Held && !cmd.Bypass;
        bool optOn = opt.Held && !opt.Bypass;
        if (!active || !(cmdOn || optOn)) return consumed.Contains(vk); // auto-repeat of a key we replaced
        if (IsPassThroughKey(vk)) return false;

        if (cmdOn) cmd.Used = true;
        if (optOn) opt.Used = true;

        bool ctrlHeld = IsKeyDown(VK_CONTROL);
        bool shiftHeld = IsKeyDown(VK_SHIFT);
        var chord = new MacChord(
            (cmdOn ? MacMods.Command : 0) | (optOn ? MacMods.Option : 0) |
            (ctrlHeld ? MacMods.Control : 0) | (shiftHeld ? MacMods.Shift : 0), vk);

        if (capture is { } callback)
        {
            capture = null;
            consumed.Add(vk);
            callback(chord);
            return true;
        }

        // Once something went through as a real Alt / Win combo (⌘Tab), stay native until the key is released
        // so the app switcher keeps working.
        bool nativeMode = (cmdOn && cmd.NativeDown) || (optOn && opt.NativeDown);
        ActionSpec? action = null;
        if (!nativeMode && !cfg.Map.TryGetValue(chord, out action)
            && cfg.CommandFallbackToCtrl && cmdOn && !optOn && !ctrlHeld) // Ctrl+Alt+key is AltGr on many layouts
        {
            action = ActionSpec.Keys(new WinChord(WinMods.Ctrl | (shiftHeld ? WinMods.Shift : 0), vk));
        }

        if (action == null || action.Kind == ActionKind.Native)
            return SendNative(vk, scan, flags, cmdOn, optOn);

        consumed.Add(vk);
        Execute(action);
        return true;
    }

    Layer? LayerFor(int vk, EngineConfig cfg, bool active)
    {
        if (cmd.Held && vk == cmd.Vk) return cmd;
        if (opt.Held && vk == opt.Vk) return opt;
        if (!active) return null;
        if (cfg.CommandKeys.Contains(vk)) return cmd;
        if (cfg.OptionKeys.Contains(vk)) return opt;
        return null;
    }

    bool OnLayerKey(Layer layer, int vk, bool down, EngineConfig cfg)
    {
        if (down)
        {
            if (layer.Held)
            {
                if (vk != layer.Vk) return true; // the other Alt / Win key while one is already held
                return !(layer.Bypass || layer.NativeDown); // auto-repeat: only let it through if the system sees the key
            }
            layer.Reset();
            layer.Held = true;
            layer.Vk = vk;
            layer.Bypass = IsForegroundExcluded(cfg);
            return !layer.Bypass;
        }

        if (!layer.Held) return false;
        if (vk != layer.Vk) return true;

        layer.Held = false;
        if (layer.Bypass || layer.NativeDown)
        {
            layer.Reset();
            return false; // release the real key the system has seen go down
        }

        bool tap = layer == cmd ? cfg.CommandTapSendsAlt : cfg.OptionTapSendsWin;
        if (!layer.Used && tap && cfg.Enabled && !suspended)
            Inject([KeyInput(vk, up: false), KeyInput(vk, up: true)]);
        return true;
    }

    bool SendNative(int vk, uint scan, uint flags, bool cmdOn, bool optOn)
    {
        // Win+L can't be synthesized; lock directly instead.
        if (optOn && vk == 'L' && !IsKeyDown(VK_CONTROL))
        {
            consumed.Add(vk);
            LockWorkStation();
            resetRequested = true;
            return true;
        }

        var inputs = new List<INPUT>();
        if (cmdOn && !cmd.NativeDown)
        {
            inputs.Add(KeyInput(cmd.Vk, up: false));
            cmd.NativeDown = true;
        }
        if (optOn && !opt.NativeDown)
        {
            inputs.Add(KeyInput(opt.Vk, up: false));
            opt.NativeDown = true;
        }
        if (inputs.Count == 0) return false; // the system already sees Alt / Win held: let the key through

        // Replay the key after the modifier so the order is right.
        inputs.Add(KeyInput(vk, up: false, scan, (flags & LLKHF_EXTENDED) != 0));
        Inject([.. inputs]);
        return true;
    }

    void Execute(ActionSpec action)
    {
        switch (action.Kind)
        {
            case ActionKind.Keys:
                SendChords(action.Steps);
                break;
            case ActionKind.Minimize:
                var window = GetForegroundWindow();
                if (window != IntPtr.Zero) ShowWindowAsync(window, SW_MINIMIZE);
                break;
            case ActionKind.Lock:
                LockWorkStation();
                resetRequested = true;
                break;
        }
    }

    static readonly int[] PhysicalModifiers = [VK_LCONTROL, VK_RCONTROL, VK_LSHIFT, VK_RSHIFT];

    void SendChords(WinChord[] steps)
    {
        // Ctrl / Shift the user is holding (e.g. the Shift in ⇧⌘Z) would leak into the output,
        // so lift them for the duration and put them back afterwards.
        var held = PhysicalModifiers.Where(IsKeyDown).ToArray();
        var inputs = new List<INPUT>();
        foreach (var m in held) inputs.Add(KeyInput(m, up: true));
        foreach (var step in steps)
        {
            var mods = ModifierKeys(step.Mods);
            foreach (var m in mods) inputs.Add(KeyInput(m, up: false));
            inputs.Add(KeyInput(step.Key, up: false));
            inputs.Add(KeyInput(step.Key, up: true));
            for (int i = mods.Count - 1; i >= 0; i--) inputs.Add(KeyInput(mods[i], up: true));
        }
        foreach (var m in held) inputs.Add(KeyInput(m, up: false));
        Inject([.. inputs]);
    }

    static List<int> ModifierKeys(WinMods mods)
    {
        var keys = new List<int>(4);
        if ((mods & WinMods.Win) != 0) keys.Add(VK_LWIN);
        if ((mods & WinMods.Ctrl) != 0) keys.Add(VK_LCONTROL);
        if ((mods & WinMods.Alt) != 0) keys.Add(VK_LMENU);
        if ((mods & WinMods.Shift) != 0) keys.Add(VK_LSHIFT);
        return keys;
    }

    internal bool OnMouse(int msg)
    {
        ApplyPendingReset();
        if (msg == WM_LBUTTONUP)
        {
            if (!ctrlClick) return false;
            ctrlClick = false;
            Inject([MouseInput(MOUSEEVENTF_LEFTUP), KeyInput(VK_LCONTROL, up: true)]);
            return true;
        }

        bool cmdOn = cmd.Held && !cmd.Bypass;
        if (cmdOn) cmd.Used = true;
        if (opt.Held && !opt.Bypass) opt.Used = true;

        var cfg = config;
        if (msg != WM_LBUTTONDOWN || !cmdOn || cmd.NativeDown) return false;
        if (!cfg.CommandClickIsCtrlClick || !cfg.Enabled || suspended) return false;

        ctrlClick = true;
        Inject([KeyInput(VK_LCONTROL, up: false), MouseInput(MOUSEEVENTF_LEFTDOWN)]);
        return true;
    }

    void ApplyPendingReset()
    {
        if (!resetRequested) return;
        resetRequested = false;
        var release = new List<INPUT>();
        foreach (var layer in new[] { cmd, opt })
        {
            if (layer.NativeDown) release.Add(KeyInput(layer.Vk, up: true));
            layer.Reset();
        }
        if (ctrlClick) release.Add(KeyInput(VK_LCONTROL, up: true));
        ctrlClick = false;
        consumed.Clear();
        if (release.Count > 0) Inject([.. release]);
    }

    bool ForegroundAppIsExcluded(EngineConfig cfg)
    {
        if (cfg.ExcludedApps.Count == 0) return false;
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        if (!processNames.TryGetValue(pid, out var name))
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                name = process.ProcessName;
            }
            catch (Exception)
            {
                name = "";
            }
            if (processNames.Count > 256) processNames.Clear();
            processNames[pid] = name;
        }
        return cfg.ExcludedApps.Contains(name);
    }

    static bool IsPassThroughKey(int vk) => vk is VK_SHIFT or VK_CONTROL or VK_MENU
        or VK_LSHIFT or VK_RSHIFT or VK_LCONTROL or VK_RCONTROL or VK_LMENU or VK_RMENU
        or VK_LWIN or VK_RWIN or VK_CAPITAL or VK_NUMLOCK or VK_SCROLL;

    static bool IsExtendedKey(int vk) => vk is (>= 0x21 and <= 0x28) or 0x2C or 0x2D or 0x2E
        or VK_LWIN or VK_RWIN or 0x5D or 0x6F or VK_NUMLOCK or VK_RCONTROL or VK_RMENU or (>= 0xA6 and <= 0xB7);

    internal static INPUT KeyInput(int vk, bool up, uint? scan = null, bool? extended = null)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (extended ?? IsExtendedKey(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = (ushort)(scan ?? MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC)),
                    dwFlags = flags,
                    dwExtraInfo = Marker,
                },
            },
        };
    }

    static INPUT MouseInput(uint flags) => new()
    {
        type = INPUT_MOUSE,
        u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags, dwExtraInfo = Marker } },
    };
}
