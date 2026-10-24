namespace MacKeys;

public static class DefaultShortcuts
{
    const string Editing = "Editing";
    const string Navigation = "Text navigation";
    const string Formatting = "Formatting";
    const string Files = "Files & documents";
    const string Browser = "Browser & tabs";
    const string Windows = "Windows & apps";
    const string Screenshots = "Screenshots";
    const string Developer = "Developer";

    public static List<Shortcut> Create()
    {
        var list = new List<Shortcut>
        {
            S(Editing, "Cmd+C", "Ctrl+C", "Copy"),
            S(Editing, "Cmd+X", "Ctrl+X", "Cut"),
            S(Editing, "Cmd+V", "Ctrl+V", "Paste"),
            S(Editing, "Cmd+Shift+V", "Ctrl+Shift+V", "Paste without formatting"),
            S(Editing, "Option+Cmd+Shift+V", "Ctrl+Shift+V", "Paste and match style"),
            S(Editing, "Cmd+Z", "Ctrl+Z", "Undo"),
            S(Editing, "Cmd+Shift+Z", "Ctrl+Y", "Redo"),
            S(Editing, "Cmd+A", "Ctrl+A", "Select all"),
            S(Editing, "Cmd+F", "Ctrl+F", "Find"),
            S(Editing, "Cmd+G", "F3", "Find next"),
            S(Editing, "Cmd+Shift+G", "Shift+F3", "Find previous"),
            S(Editing, "Option+Cmd+F", "Ctrl+H", "Find and replace"),
            S(Editing, "Cmd+Backspace", "Shift+Home then Backspace", "Delete to start of line"),
            S(Editing, "Cmd+Delete", "Shift+End then Delete", "Delete to end of line"),
            S(Editing, "Option+Backspace", "Ctrl+Backspace", "Delete previous word"),
            S(Editing, "Option+Delete", "Ctrl+Delete", "Delete next word"),

            S(Navigation, "Cmd+Left", "Home", "Move to start of line"),
            S(Navigation, "Cmd+Right", "End", "Move to end of line"),
            S(Navigation, "Cmd+Up", "Ctrl+Home", "Move to start of document"),
            S(Navigation, "Cmd+Down", "Ctrl+End", "Move to end of document"),
            S(Navigation, "Cmd+Shift+Left", "Shift+Home", "Select to start of line"),
            S(Navigation, "Cmd+Shift+Right", "Shift+End", "Select to end of line"),
            S(Navigation, "Cmd+Shift+Up", "Ctrl+Shift+Home", "Select to start of document"),
            S(Navigation, "Cmd+Shift+Down", "Ctrl+Shift+End", "Select to end of document"),
            S(Navigation, "Option+Left", "Ctrl+Left", "Move one word left"),
            S(Navigation, "Option+Right", "Ctrl+Right", "Move one word right"),
            S(Navigation, "Option+Shift+Left", "Ctrl+Shift+Left", "Select one word left"),
            S(Navigation, "Option+Shift+Right", "Ctrl+Shift+Right", "Select one word right"),
            S(Navigation, "Option+Up", "Ctrl+Up", "Move to previous paragraph"),
            S(Navigation, "Option+Down", "Ctrl+Down", "Move to next paragraph"),
            S(Navigation, "Option+Shift+Up", "Ctrl+Shift+Up", "Select to previous paragraph"),
            S(Navigation, "Option+Shift+Down", "Ctrl+Shift+Down", "Select to next paragraph"),

            S(Formatting, "Cmd+B", "Ctrl+B", "Bold"),
            S(Formatting, "Cmd+I", "Ctrl+I", "Italic"),
            S(Formatting, "Cmd+U", "Ctrl+U", "Underline"),
            S(Formatting, "Cmd+K", "Ctrl+K", "Insert link"),

            S(Files, "Cmd+N", "Ctrl+N", "New document or window"),
            S(Files, "Cmd+O", "Ctrl+O", "Open"),
            S(Files, "Cmd+S", "Ctrl+S", "Save"),
            S(Files, "Cmd+Shift+S", "Ctrl+Shift+S", "Save as"),
            S(Files, "Cmd+P", "Ctrl+P", "Print"),
            S(Files, "Cmd+Shift+N", "Ctrl+Shift+N", "New folder / private window"),
            S(Files, "Cmd+,", "Ctrl+,", "Settings / preferences"),

            S(Browser, "Cmd+T", "Ctrl+T", "New tab"),
            S(Browser, "Cmd+Shift+T", "Ctrl+Shift+T", "Reopen closed tab"),
            S(Browser, "Cmd+W", "Ctrl+W", "Close tab or window"),
            S(Browser, "Cmd+Shift+W", "Ctrl+Shift+W", "Close all tabs in window"),
            S(Browser, "Cmd+L", "Ctrl+L", "Focus address bar"),
            S(Browser, "Cmd+R", "Ctrl+R", "Reload page"),
            S(Browser, "Cmd+Shift+R", "Ctrl+Shift+R", "Hard reload"),
            S(Browser, "Cmd+D", "Ctrl+D", "Bookmark page / select next match"),
            S(Browser, "Cmd+Shift+B", "Ctrl+Shift+B", "Show bookmarks bar"),
            S(Browser, "Option+Cmd+B", "Ctrl+Shift+O", "Bookmark manager"),
            S(Browser, "Cmd+Y", "Ctrl+H", "History"),
            S(Browser, "Cmd+[", "Alt+Left", "Back"),
            S(Browser, "Cmd+]", "Alt+Right", "Forward"),
            S(Browser, "Cmd+Shift+]", "Ctrl+Tab", "Next tab"),
            S(Browser, "Cmd+Shift+[", "Ctrl+Shift+Tab", "Previous tab"),
            S(Browser, "Option+Cmd+Right", "Ctrl+Tab", "Next tab"),
            S(Browser, "Option+Cmd+Left", "Ctrl+Shift+Tab", "Previous tab"),
            S(Browser, "Cmd+=", "Ctrl+=", "Zoom in"),
            S(Browser, "Cmd+-", "Ctrl+-", "Zoom out"),
            S(Browser, "Cmd+0", "Ctrl+0", "Actual size"),
            S(Browser, "Option+Cmd+I", "Ctrl+Shift+I", "Developer tools"),
            S(Browser, "Option+Cmd+J", "Ctrl+Shift+J", "JavaScript console"),
            S(Browser, "Option+Cmd+U", "Ctrl+U", "View page source"),

            S(Windows, "Cmd+Tab", "{Native}", "Switch apps"),
            S(Windows, "Cmd+Shift+Tab", "{Native}", "Switch apps backwards"),
            S(Windows, "Cmd+Q", "Alt+F4", "Quit app"),
            S(Windows, "Cmd+M", "{Minimize}", "Minimize window"),
            S(Windows, "Cmd+H", "{Minimize}", "Hide app"),
            S(Windows, "Option+Cmd+H", "Win+Home", "Hide others"),
            S(Windows, "Option+Cmd+M", "Win+M", "Minimize all windows"),
            S(Windows, "Cmd+Space", "Win+S", "Spotlight → Windows Search"),
            S(Windows, "Option+Cmd+Escape", "Ctrl+Shift+Escape", "Force quit → Task Manager"),
            S(Windows, "Ctrl+Cmd+Q", "{Lock}", "Lock screen"),
            S(Windows, "Ctrl+Cmd+F", "F11", "Toggle full screen"),
            S(Windows, "Ctrl+Cmd+Space", "Win+.", "Emoji & symbols"),
            S(Windows, "Ctrl+Option+Left", "Win+Left", "Snap window left"),
            S(Windows, "Ctrl+Option+Right", "Win+Right", "Snap window right"),
            S(Windows, "Ctrl+Option+Up", "Win+Up", "Maximize window"),
            S(Windows, "Ctrl+Option+Down", "Win+Down", "Restore window"),

            S(Screenshots, "Cmd+Shift+3", "Win+PrintScreen", "Screenshot of whole screen (saved to Pictures)"),
            S(Screenshots, "Cmd+Shift+4", "Win+Shift+S", "Screenshot of selection"),
            S(Screenshots, "Cmd+Shift+5", "Win+Shift+R", "Record screen (Snipping Tool)"),

            S(Developer, "Cmd+/", "Ctrl+/", "Toggle comment"),
            S(Developer, "Cmd+Shift+P", "Ctrl+Shift+P", "Command palette"),
            S(Developer, "Cmd+Shift+F", "Ctrl+Shift+F", "Find in files"),
            S(Developer, "Cmd+Enter", "Ctrl+Enter", "Send / run"),
            S(Developer, "Option+Cmd+Up", "Ctrl+Alt+Up", "Add cursor above"),
            S(Developer, "Option+Cmd+Down", "Ctrl+Alt+Down", "Add cursor below"),
        };

        var tabs = Enumerable.Range(1, 9)
            .Select(i => S(Browser, $"Cmd+{i}", $"Ctrl+{i}", i == 9 ? "Go to last tab" : $"Go to tab {i}"));
        list.InsertRange(list.FindIndex(s => s.Category == Windows), tabs);

        return list;
    }

    static Shortcut S(string category, string mac, string windows, string description) =>
        new() { Category = category, Mac = mac, Windows = windows, Description = description };
}
