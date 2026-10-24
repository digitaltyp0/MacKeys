using System.Diagnostics;

namespace MacKeys;

/// <summary>Browse, search, toggle and edit every shortcut, plus general options. Changes apply immediately.</summary>
sealed class SettingsForm : Form
{
    const string AllCategories = "All categories";

    readonly AppSettings settings;
    readonly RemapEngine engine;
    readonly Action apply;

    readonly TabControl tabs = new();
    readonly Label subtitle = new();
    readonly CheckBox enabledBox = new();
    readonly TextBox search = new();
    readonly ComboBox category = new();
    readonly DataGridView grid = new();
    readonly Label status = new();
    bool populating;

    public SettingsForm(AppSettings settings, RemapEngine engine, Icon icon, Action apply)
    {
        this.settings = settings;
        this.engine = engine;
        this.apply = apply;

        Text = "MacKeys Settings";
        Icon = icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1040, 660);
        MinimumSize = new Size(780, 480);
        BackColor = Color.White;
        ForeColor = Theme.Text;

        tabs.Dock = DockStyle.Fill;
        tabs.Padding = new Point(16, 5);
        tabs.TabPages.Add(BuildShortcutsPage());
        tabs.TabPages.Add(BuildGeneralPage());
        tabs.TabPages.Add(InfoPages.Help());
        tabs.TabPages.Add(InfoPages.Feedback(settings));
        Controls.Add(tabs);
        Controls.Add(BuildHeader()); // added last so it docks above the tabs

        RefreshCategories();
        Populate();
        UpdateSubtitle();
        Shown += (_, _) => search.Focus();
    }

    public void ShowTab(string title)
    {
        var page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Text == title);
        if (page != null) tabs.SelectedTab = page;
    }

    /// <summary>Picks up changes made elsewhere (the tray menu's on/off toggle).</summary>
    public void SyncFromSettings()
    {
        if (enabledBox.Checked != settings.Enabled) enabledBox.Checked = settings.Enabled;
        UpdateSubtitle();
    }

    void Changed()
    {
        apply();
        UpdateSubtitle();
    }

    Control BuildHeader()
    {
        var title = new Label
        {
            Text = "MacKeys",
            AutoSize = true,
            Font = Theme.Semibold(16f),
            Margin = new Padding(0),
        };
        subtitle.AutoSize = true;
        subtitle.ForeColor = Theme.Muted;
        subtitle.Margin = new Padding(2, 2, 0, 0);

        enabledBox.Text = "Remapping on";
        enabledBox.AutoSize = true;
        enabledBox.Font = Theme.Semibold(10f);
        enabledBox.Anchor = AnchorStyles.Right;
        enabledBox.Checked = settings.Enabled;
        enabledBox.CheckedChanged += (_, _) =>
        {
            if (settings.Enabled == enabledBox.Checked) return;
            settings.Enabled = enabledBox.Checked;
            Changed();
        };

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(18, 14, 22, 12),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(subtitle, 0, 1);
        header.Controls.Add(enabledBox, 1, 0);
        header.SetRowSpan(enabledBox, 2);
        return header;
    }

    void UpdateSubtitle()
    {
        string command = settings.CommandKey switch
        {
            CommandKeyChoice.RightAlt => "Right Alt",
            CommandKeyChoice.BothAlt => "Alt keys",
            _ => "Left Alt",
        };
        string option = settings.OptionKey switch
        {
            OptionKeyChoice.LeftWin => "⌥ Option is your Left Windows key",
            OptionKeyChoice.BothWin => "⌥ Option is your Windows keys",
            _ => "⌥ Option is off",
        };
        subtitle.Text = settings.Enabled
            ? $"macOS shortcuts on Windows.  ⌘ Command is your {command} · {option}."
            : "Paused — your keyboard behaves like a normal Windows keyboard until you turn remapping back on.";
    }

    // ---------------------------------------------------------------- Shortcuts tab

    TabPage BuildShortcutsPage()
    {
        var page = new TabPage("Shortcuts") { BackColor = Color.White, Padding = new Padding(14, 12, 14, 10) };

        search.PlaceholderText = "Search — try “copy”, “tab”, “⇧⌘” or “Ctrl+Y”";
        search.Width = 340;
        search.Margin = new Padding(0, 1, 8, 0);
        search.TextChanged += (_, _) => Populate();

        category.DropDownStyle = ComboBoxStyle.DropDownList;
        category.Width = 190;
        category.Margin = new Padding(0, 1, 8, 0);
        category.SelectedIndexChanged += (_, _) => Populate();

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Right };
        buttons.Controls.Add(MakeButton("Add…", (_, _) => Edit(null)));
        buttons.Controls.Add(MakeButton("Edit…", (_, _) => { if (Selected is { } s) Edit(s); }));
        buttons.Controls.Add(MakeButton("Remove", (_, _) => Remove()));
        buttons.Controls.Add(MakeButton("Restore defaults", (_, _) => RestoreDefaults()));

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(0, 0, 0, 10) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.Controls.Add(search, 0, 0);
        toolbar.Controls.Add(category, 1, 0);
        toolbar.Controls.Add(buttons, 3, 0);

        BuildGrid();
        var gridFrame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Theme.Border };
        gridFrame.Controls.Add(grid);

        status.Dock = DockStyle.Bottom;
        status.AutoSize = true;
        status.ForeColor = Theme.Muted;
        status.Padding = new Padding(0, 8, 0, 0);

        page.Controls.Add(gridFrame);
        page.Controls.Add(status);
        page.Controls.Add(toolbar);
        return page;
    }

    void BuildGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Color.White;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(238, 238, 242);
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.EnableHeadersVisualStyles = false;
        grid.ShowCellToolTips = true;

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.HeaderBack,
            ForeColor = Theme.Muted,
            SelectionBackColor = Theme.HeaderBack,
            SelectionForeColor = Theme.Muted,
            Font = Theme.Semibold(9f),
            Padding = new Padding(6, 7, 6, 7),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Theme.Text,
            Font = Font,
            Padding = new Padding(6, 5, 6, 5),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "On",
            FillWeight = 5,
            MinimumWidth = 44,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
        });
        AddTextColumn("Mac", 10, new Font("Segoe UI Symbol", 11f));
        AddTextColumn("You press", 17);
        AddTextColumn("Windows sends", 22);
        AddTextColumn("What it does", 30);
        AddTextColumn("Category", 16);

        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell?.ColumnIndex == 0)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) =>
        {
            if (!populating && e.RowIndex >= 0 && e.ColumnIndex == 0) OnToggled(grid.Rows[e.RowIndex]);
        };
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex != 0 && Selected is { } s) Edit(s);
        };
        grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && Selected is { } s)
            {
                e.Handled = e.SuppressKeyPress = true;
                Edit(s);
            }
            else if (e.KeyCode == Keys.Delete)
            {
                e.Handled = e.SuppressKeyPress = true;
                Remove();
            }
        };
    }

    void AddTextColumn(string header, float weight, Font? font = null)
    {
        var column = new DataGridViewTextBoxColumn { HeaderText = header, FillWeight = weight, ReadOnly = true };
        if (font != null) column.DefaultCellStyle.Font = font;
        grid.Columns.Add(column);
    }

    Shortcut? Selected => grid.CurrentRow?.Tag as Shortcut;

    void RefreshCategories()
    {
        var current = category.SelectedItem as string;
        var names = settings.Shortcuts.Select(s => s.Category).Distinct().ToList();
        category.BeginUpdate();
        category.Items.Clear();
        category.Items.Add(AllCategories);
        foreach (var name in names) category.Items.Add(name);
        category.SelectedItem = current != null && names.Contains(current) ? current : AllCategories;
        category.EndUpdate();
    }

    void Populate()
    {
        var selected = Selected;
        string query = search.Text.Trim();
        string? onlyCategory = category.SelectedItem is string c && c != AllCategories ? c : null;

        var rows = new List<DataGridViewRow>();
        foreach (var s in settings.Shortcuts)
        {
            if (onlyCategory != null && s.Category != onlyCategory) continue;

            bool valid = MacChord.TryParse(s.Mac, out var chord);
            string symbols = valid ? chord.ToSymbols() : s.Mac;
            string macText = valid ? chord.ToString() : s.Mac;
            string press = valid ? settings.PhysicalKeys(chord) : "—";
            string sends = DescribeAction(s.Windows, valid ? chord : null);
            if (query.Length > 0 && !Matches(query, s.Description, s.Category, macText, symbols, press, sends)) continue;

            var row = new DataGridViewRow { Tag = s };
            row.CreateCells(grid, s.Enabled, symbols, press, sends, s.Description, s.Category);
            row.Cells[1].ToolTipText = macText;
            StyleRow(row, s);
            rows.Add(row);
        }

        int scroll = Math.Max(grid.FirstDisplayedScrollingRowIndex, 0);
        populating = true;
        grid.Rows.Clear();
        grid.Rows.AddRange([.. rows]);
        if (grid.SortedColumn != null)
            grid.Sort(grid.SortedColumn, grid.SortOrder == SortOrder.Descending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending);
        populating = false;

        var match = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag == selected);
        if (match != null) grid.CurrentCell = match.Cells[1];
        ScrollTo(scroll);

        UpdateStatus(rows.Count, query);
    }

    void UpdateStatus(int shown, string query)
    {
        int total = settings.Shortcuts.Count;
        int on = settings.Shortcuts.Count(s => s.Enabled);
        status.Text = shown == 0 && query.Length > 0
            ? $"No shortcuts match “{query}”."
            : $"Showing {shown} of {total} shortcuts · {on} on.   Double-click a row to edit — changes apply instantly.";
    }

    static string DescribeAction(string text, MacChord? chord)
    {
        if (!ActionSpec.TryParse(text, out var action)) return text + "  (invalid)";
        if (action.Kind == ActionKind.Native && chord is { } c) return $"{c.ToNative()} (native)";
        return action.Label;
    }

    static bool Matches(string query, params string[] fields)
    {
        foreach (var term in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (!fields.Any(f => f.Contains(term, StringComparison.OrdinalIgnoreCase))) return false;
        return true;
    }

    static void StyleRow(DataGridViewRow row, Shortcut s) =>
        row.DefaultCellStyle.ForeColor = s.Enabled ? Color.Empty : Theme.Faint;

    void OnToggled(DataGridViewRow row)
    {
        if (row.Tag is not Shortcut s) return;
        bool on = row.Cells[0].Value is true;
        Shortcut? turnedOff = null;
        if (on && !ConfirmNoClash(this, s, s.Mac, settings.Shortcuts, out turnedOff))
        {
            BeginInvoke(() =>
            {
                populating = true;
                row.Cells[0].Value = false;
                populating = false;
            });
            return;
        }
        s.Enabled = on;
        StyleRow(row, s);
        Changed();
        UpdateStatus(grid.Rows.Count, search.Text.Trim());
        if (turnedOff != null) BeginInvoke(Populate); // refresh the row that was switched off; not re-entrantly
    }

    /// <summary>
    /// If another enabled shortcut uses the same Mac keys, asks whether to turn it off.
    /// Returns false if the user wants to keep the existing one.
    /// </summary>
    internal static bool ConfirmNoClash(IWin32Window owner, Shortcut candidate, string mac, IEnumerable<Shortcut> all, out Shortcut? turnedOff)
    {
        turnedOff = null;
        if (!MacChord.TryParse(mac, out var chord)) return true;
        var clash = all.FirstOrDefault(o => o != candidate && o.Enabled && MacChord.TryParse(o.Mac, out var other) && other == chord);
        if (clash == null) return true;

        var answer = MessageBox.Show(owner,
            $"{chord.ToSymbols()} is already used by “{clash.Description}”.\n\nTurn that one off and use this one instead?",
            "Shortcut already in use", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return false;
        clash.Enabled = false;
        turnedOff = clash;
        return true;
    }

    void Edit(Shortcut? existing)
    {
        var draft = existing?.Clone() ?? new Shortcut
        {
            Category = category.SelectedItem is string c && c != AllCategories ? c : "Custom",
        };
        var categories = settings.Shortcuts.Select(s => s.Category).Append("Custom").Distinct();

        using (var dialog = new ShortcutEditForm(draft, existing, settings, engine, categories))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
        }

        if (existing == null)
        {
            settings.Shortcuts.Add(draft);
            existing = draft;
        }
        else
        {
            existing.Mac = draft.Mac;
            existing.Windows = draft.Windows;
            existing.Description = draft.Description;
            existing.Category = draft.Category;
            existing.Enabled = draft.Enabled;
        }

        Changed();
        RefreshCategories();
        Populate();
        var row = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag == existing);
        if (row != null)
        {
            grid.CurrentCell = row.Cells[1];
            ScrollTo(row.Index);
        }
    }

    void ScrollTo(int index)
    {
        // The grid throws if it has no room to display rows yet (before the window is laid out).
        if (grid.Rows.Count == 0 || grid.DisplayedRowCount(includePartialRow: true) == 0) return;
        grid.FirstDisplayedScrollingRowIndex = Math.Clamp(index, 0, grid.Rows.Count - 1);
    }

    void Remove()
    {
        if (Selected is not { } s) return;
        var answer = MessageBox.Show(this, $"Remove “{s.Description}”?", "Remove shortcut",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (answer != DialogResult.OK) return;
        settings.Shortcuts.Remove(s);
        Changed();
        RefreshCategories();
        Populate();
    }

    void RestoreDefaults()
    {
        var answer = MessageBox.Show(this,
            "Put back the built-in shortcut list? Your edits and any shortcuts you added will be removed.",
            "Restore defaults", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
        if (answer != DialogResult.OK) return;
        settings.Shortcuts = DefaultShortcuts.Create();
        Changed();
        RefreshCategories();
        Populate();
    }

    // ---------------------------------------------------------------- General tab

    TabPage BuildGeneralPage()
    {
        var page = new TabPage("General") { BackColor = Color.White, AutoScroll = true, Padding = new Padding(22, 16, 22, 16) };
        var layout = new TableLayoutPanel { Location = new Point(22, 16), AutoSize = true, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        int row = 0;

        void Section(string text)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                Font = Theme.Semibold(10.5f),
                Margin = new Padding(0, row == 0 ? 0 : 22, 0, 8),
            };
            Full(label);
        }

        void Field(string text, Control control)
        {
            layout.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 18, 4) }, 0, row);
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(0, 4, 0, 4);
            layout.Controls.Add(control, 1, row);
            row++;
        }

        void Full(Control control)
        {
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 2);
            row++;
        }

        Label Note(string text) => new()
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = Theme.Muted,
            Margin = new Padding(0, 0, 0, 8),
        };

        CheckBox Check(string text, bool value, Action<bool> set)
        {
            var box = new CheckBox { Text = text, AutoSize = true, Checked = value, Margin = new Padding(0, 3, 0, 3) };
            box.CheckedChanged += (_, _) =>
            {
                set(box.Checked);
                Changed();
            };
            return box;
        }

        ComboBox Choice(string[] items, int selected, Action<int> set)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
            box.Items.AddRange(items);
            box.SelectedIndex = selected;
            box.SelectedIndexChanged += (_, _) =>
            {
                set(box.SelectedIndex);
                Changed();
                Populate(); // the "You press" column depends on these
            };
            return box;
        }

        Section("Modifier keys");
        Field("⌘ Command key", Choice(["Left Alt (recommended)", "Right Alt", "Both Alt keys"],
            (int)settings.CommandKey, i => settings.CommandKey = (CommandKeyChoice)i));
        Full(Note("Left Alt sits exactly where ⌘ is on a Mac keyboard. On layouts with AltGr (Polish, German, French…) " +
            "Right Alt types characters like ą, ę, ł — keep ⌘ on Left Alt so those keep working."));
        Field("⌥ Option key", Choice(["Off", "Left Windows key", "Both Windows keys"],
            (int)settings.OptionKey, i => settings.OptionKey = (OptionKeyChoice)i));
        Full(Note("The Windows key sits where ⌥ is on a Mac. Windows-key shortcuts that aren't remapped (Win+E, Win+V, Win+L…) keep working."));

        Section("Behavior");
        Full(Check("Send any other ⌘ shortcut as Ctrl   (e.g. ⌘J → Ctrl+J)",
            settings.CommandFallbackToCtrl, v => settings.CommandFallbackToCtrl = v));
        Full(Check("⌘-click acts as Ctrl-click   (open links in a new tab, select several files)",
            settings.CommandClickIsCtrlClick, v => settings.CommandClickIsCtrlClick = v));
        Full(Check("Tapping ⌘ on its own presses Alt   (shows the app's menu shortcuts)",
            settings.CommandTapSendsAlt, v => settings.CommandTapSendsAlt = v));
        Full(Check("Tapping ⌥ on its own opens Start",
            settings.OptionTapSendsWin, v => settings.OptionTapSendsWin = v));

        Section("Startup");
        Full(Check("Start MacKeys when I sign in to Windows", Startup.IsEnabled, v =>
        {
            try
            {
                Startup.Set(v);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Couldn't change startup setting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }));

        Section("Excluded apps");
        Full(Note("MacKeys stays out of the way while one of these apps is in front — handy for games, " +
            "Remote Desktop (mstsc.exe) and virtual machines. One app per line."));
        var excluded = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(420, 110),
            Margin = new Padding(0, 0, 10, 0),
            Lines = [.. settings.ExcludedApps],
        };
        excluded.TextChanged += (_, _) =>
        {
            settings.ExcludedApps = excluded.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            Changed();
        };
        var pick = MakeButton("Add running app…", null);
        pick.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        pick.Click += (_, _) => ShowRunningApps(pick, excluded);
        var excludedRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        excludedRow.Controls.Add(excluded);
        excludedRow.Controls.Add(pick);
        Full(excludedRow);

        Section("Good to know");
        Full(Note("Windows doesn't show keys typed into apps running as administrator to regular apps, so shortcuts " +
            "aren't remapped there unless MacKeys also runs as administrator."));
        var folder = new LinkLabel { Text = "Open settings folder", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        folder.LinkClicked += (_, _) =>
        {
            Directory.CreateDirectory(AppSettings.Folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppSettings.Folder}\"") { UseShellExecute = true });
        };
        Full(folder);

        page.Controls.Add(layout);
        return page;
    }

    static void ShowRunningApps(Control anchor, TextBox target)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero)
                        names.Add(process.ProcessName + ".exe");
                }
                catch (Exception)
                {
                    // Processes can exit or deny access while we look; skip them.
                }
            }
        }

        var menu = new ContextMenuStrip();
        foreach (var name in names)
        {
            menu.Items.Add(name, null, (_, _) =>
            {
                if (target.Lines.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
                var prefix = target.TextLength == 0 || target.Text.EndsWith(Environment.NewLine) ? "" : Environment.NewLine;
                target.AppendText(prefix + name);
            });
        }
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    static Button MakeButton(string text, EventHandler? click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(6, 0, 0, 0),
            UseVisualStyleBackColor = true,
        };
        if (click != null) button.Click += click;
        return button;
    }
}
