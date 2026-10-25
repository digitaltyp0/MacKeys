namespace MacKeys;

/// <summary>The Help (in-app readme) and Feedback tabs of the settings window.</summary>
static class InfoPages
{
    public static TabPage Help()
    {
        var doc = new Doc();
        doc.Title("How MacKeys works");
        doc.Para("MacKeys makes your Windows keyboard behave like a Mac keyboard. Hold the key next to your spacebar " +
            "and use the shortcuts you already know — ⌘C copies, ⌘Z undoes, ⌘← jumps to the start of a line.");

        doc.Heading("Your keys");
        doc.Add(KeyTable(
            ("⌘", "Command", "Left Alt"),
            ("⌥", "Option", "Left Windows key"),
            ("⌃", "Control", "Ctrl"),
            ("⇧", "Shift", "Shift")));
        doc.Para("These are the defaults. You can move ⌘ and ⌥ to other keys on the General tab. " +
            "Right Alt (AltGr) is left alone, so letters like ą, ę and ł keep working.");

        doc.Heading("Getting started");
        doc.Bullets(
            "MacKeys runs in the system tray — look for the ⌘ icon (it may be hidden under the ^ arrow).",
            "Left-click the icon to open these settings. Right-click it to pause remapping or exit.",
            "Try ⌘C and ⌘V (Alt+C, Alt+V), ⌘← and ⌘→ to jump to the start or end of a line, and ⌥← and ⌥→ to jump by word.",
            "⌘Tab switches apps just like on a Mac — keep holding ⌘ and press Tab to move along.");

        doc.Heading("Changing shortcuts");
        doc.Bullets(
            "The Shortcuts tab lists every shortcut. Search by name, keys or symbols, or pick a category.",
            "Untick a shortcut to turn it off. Double-click it to edit, or click Add… to create your own.",
            "Click Record and press the keys instead of typing their names.",
            "A shortcut can send several steps in a row, e.g. “Shift+Home then Backspace”.",
            "It can also keep the native Alt / Win shortcut, minimize the window, lock the screen, or do nothing.",
            "Any ⌘ shortcut that isn't in the list is sent as Ctrl (⌘J → Ctrl+J). You can turn this off on the General tab.");

        doc.Heading("Tips");
        doc.Bullets(
            "Tap ⌘ on its own to reach an app's menus (like pressing Alt). Tap ⌥ on its own to open Start.",
            "⌘-click works like Ctrl-click: open links in a new tab or select several files.",
            "Add games, Remote Desktop (mstsc.exe) or virtual machines to Excluded apps so MacKeys stays out of their way.");

        doc.Heading("Troubleshooting");
        doc.Bullets(
            "A shortcut doesn't work in one app: that app probably runs as administrator. Windows doesn't let regular " +
                "apps see keys typed there, so MacKeys would need to run as administrator too.",
            "A key feels stuck: press and release Alt and the Windows key once, or pause and resume remapping from the tray icon.",
            "Need a normal Windows keyboard for a moment: right-click the tray icon and untick “Remapping on”.",
            "Shortcuts got messy: click Restore defaults on the Shortcuts tab.",
            @"Settings are saved in %APPDATA%\MacKeys\settings.json — the “Open settings folder” link on the General tab takes you there.");

        return doc.Page("Help");
    }

    public static TabPage Feedback(AppSettings settings)
    {
        var doc = new Doc();
        doc.Add(new Label
        {
            Text = Brand.Publisher,
            AutoSize = true,
            Font = Theme.Semibold(22f),
            Margin = new Padding(0, 0, 0, 0),
        });
        doc.Add(new Label
        {
            Text = $"MacKeys {Brand.Version} is made by {Brand.Publisher}.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Margin = new Padding(1, 0, 0, 18),
        });
        doc.Para("Ideas, bugs, or a Mac shortcut you miss? I'd love to hear about it.").Margin = new Padding(0, 0, 0, 16);

        doc.Add(LinkRow("Share feedback on X", Brand.XHandle, Brand.XUrl, "Open X"));
        doc.Add(LinkRow("Report a bug or request a feature", "GitHub Issues", Brand.IssuesUrl, "Open Issues"));
        doc.Add(LinkRow("Source code", Brand.RepoUrl.Replace("https://", ""), Brand.RepoUrl, "Open GitHub"));

        doc.Heading("Reporting a bug");
        doc.Para("It helps to include which app you were in, the shortcut you pressed, and what happened. " +
            "This copies your MacKeys version, Windows version and key setup so you can paste it in:");
        var copy = new Button { Text = "Copy system info", AutoSize = true, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 4, 0, 0) };
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(SystemInfo(settings));
            copy.Text = "Copied ✓";
        };
        doc.Add(copy);

        return doc.Page("Feedback");
    }

    static string SystemInfo(AppSettings settings) => string.Join(Environment.NewLine,
        $"MacKeys {Brand.Version}",
        $"Windows {Environment.OSVersion.Version}",
        $"Keyboard layout: {InputLanguage.CurrentInputLanguage.LayoutName}",
        $"⌘ key: {settings.CommandKey}, ⌥ key: {settings.OptionKey}",
        $"Shortcuts: {settings.Shortcuts.Count(s => s.Enabled)} on of {settings.Shortcuts.Count}, " +
            $"Ctrl fallback: {(settings.CommandFallbackToCtrl ? "on" : "off")}",
        $"Excluded apps: {(settings.ExcludedApps.Count == 0 ? "none" : string.Join(", ", settings.ExcludedApps))}");

    static Control KeyTable(params (string Symbol, string Mac, string Windows)[] rows)
    {
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 2, 0, 8) };
        for (int i = 0; i < 3; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var (symbol, mac, windows) in rows)
        {
            table.Controls.Add(new Label { Text = symbol, AutoSize = true, Font = new Font("Segoe UI Symbol", 13f), Margin = new Padding(0, 1, 10, 1) });
            table.Controls.Add(new Label { Text = mac, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 1, 24, 1) });
            table.Controls.Add(new Label { Text = windows, AutoSize = true, Anchor = AnchorStyles.Left, Font = Theme.Semibold(9f), Margin = new Padding(0, 1, 0, 1) });
        }
        return table;
    }

    static Control LinkRow(string title, string linkText, string url, string buttonText)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 12),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var heading = new Label { Text = title, AutoSize = true, Font = Theme.Semibold(10f), Margin = new Padding(0) };
        var link = new LinkLabel { Text = linkText, AutoSize = true, Margin = new Padding(1, 2, 0, 0) };
        link.LinkClicked += (_, _) => Brand.Open(url);
        var button = new Button { Text = buttonText, AutoSize = true, Padding = new Padding(8, 2, 8, 2), Anchor = AnchorStyles.Left };
        button.Click += (_, _) => Brand.Open(url);
        row.Controls.Add(heading, 0, 0);
        row.Controls.Add(link, 0, 1);
        row.Controls.Add(button, 1, 0);
        row.SetRowSpan(button, 2);
        return row;
    }

    /// <summary>Stacks headings, paragraphs and bullets into a scrollable, readable column.</summary>
    sealed class Doc
    {
        const int TextWidth = 720;
        readonly TableLayoutPanel body = new() { AutoSize = true, ColumnCount = 1, Location = new Point(24, 18), Padding = new Padding(0, 0, 0, 24) };

        public void Add(Control control) => body.Controls.Add(control);

        public void Title(string text) => Add(new Label
        {
            Text = text,
            AutoSize = true,
            Font = Theme.Semibold(14f),
            Margin = new Padding(0, 0, 0, 6),
        });

        public void Heading(string text) => Add(new Label
        {
            Text = text,
            AutoSize = true,
            Font = Theme.Semibold(10.5f),
            Margin = new Padding(0, 16, 0, 6),
        });

        public Label Para(string text)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(TextWidth, 0),
                Margin = new Padding(0, 0, 0, 6),
            };
            Add(label);
            return label;
        }

        public void Bullets(params string[] items)
        {
            var list = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 4) };
            list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            foreach (var item in items)
            {
                list.Controls.Add(new Label { Text = "•", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(2, 0, 8, 5) });
                list.Controls.Add(new Label { Text = item, AutoSize = true, MaximumSize = new Size(TextWidth - 20, 0), Margin = new Padding(0, 0, 0, 5) });
            }
            Add(list);
        }

        public TabPage Page(string title)
        {
            var page = new TabPage(title) { BackColor = Color.White, AutoScroll = true };
            page.Controls.Add(body);
            return page;
        }
    }
}
