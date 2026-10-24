# MacKeys

macOS keyboard shortcuts on Windows. A small tray app that turns the key next to your spacebar into ⌘ Command, so your Mac muscle memory works: ⌘C copies, ⌘← jumps to the start of the line, ⌘Q quits, ⌘Tab switches apps.

| Mac key | Windows key | Why |
|---|---|---|
| ⌘ Command | Left Alt | same spot as ⌘ on a Mac keyboard |
| ⌥ Option | Left Windows key | same spot as ⌥ |
| ⌃ Control | Ctrl | unchanged |

Right Alt is left alone so AltGr characters (ą, ę, ł…) keep working.

## Run

Requires the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
dotnet publish src/MacKeys/MacKeys.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

Then run `dist\MacKeys.exe`. It lives in the tray (⌘ icon): left-click opens settings; right-click to pause, open Help, send feedback or exit. Launching it a second time just opens settings.

## Settings

**Shortcuts** lists all ~100 built-in shortcuts with the Mac shortcut, the keys you physically press, what Windows receives, and what it does. You can search, filter by category, switch any shortcut on or off, edit it, or add your own. Both key fields can be recorded by pressing the keys. A shortcut can send one key combination, several in a row (`Shift+Home then Backspace`), or do something special: keep the native Alt/Win shortcut, minimize the window, lock the screen, or nothing.

**General** covers which keys act as ⌘ and ⌥, whether other ⌘ shortcuts fall back to Ctrl (⌘J → Ctrl+J), ⌘-click as Ctrl-click, what a lone tap of ⌘ / ⌥ does, starting at sign-in, and apps to leave alone (games, Remote Desktop, VMs).

Changes apply immediately and are saved to `%APPDATA%\MacKeys\settings.json`.

## How it works

A low-level keyboard hook swallows the ⌘ key while it's held. Each key pressed with it is looked up in the shortcut list and the Windows equivalent is sent with `SendInput`. Unmapped combos are replayed with the real Alt / Win key, so Alt+Tab, Win+E, Win+V and similar keep working. Once a combo goes through natively (like ⌘Tab), the key stays native until you release it, so the app switcher behaves normally.

## Limitations

- Keys typed into apps running as administrator aren't visible to normal apps, so they aren't remapped there unless MacKeys also runs as administrator.
- Ctrl+Alt+Del and Win+L are handled by Windows itself (⌥L still locks).
- Per-app shortcut differences (e.g. Finder's ⌘⌫) aren't modelled. There's one global list.

## Feedback

MacKeys is made by **digitaltyp**.

- Feedback and ideas: [@digitaltyp on X](https://x.com/digitaltyp)
- Bugs and feature requests: [GitHub Issues](https://github.com/digitaltyp0/MacKeys/issues)

The app has the same links on its **Feedback** tab, plus a **Copy system info** button for bug reports. Its **Help** tab is a built-in version of this guide.

## License

[MIT](LICENSE) © digitaltyp

## Tests

```bash
dotnet run --project tests/MacKeys.Tests
```

These checks simulate key events against the engine (no real hooks) and confirm every default shortcut parses with no duplicates.
