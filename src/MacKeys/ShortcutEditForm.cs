namespace MacKeys;

/// <summary>Add or edit one shortcut. Both key fields can be typed or recorded.</summary>
sealed class ShortcutEditForm : Form
{
    enum Recording { None, Mac, Windows }

    readonly Shortcut draft;
    readonly Shortcut? original;
    readonly AppSettings settings;
    readonly RemapEngine engine;

    readonly TextBox macBox = new();
    readonly TextBox winBox = new();
    readonly TextBox descriptionBox = new();
    readonly ComboBox kindBox = new();
    readonly ComboBox categoryBox = new();
    readonly CheckBox enabledBox = new();
    readonly Button macRecord;
    readonly Button winRecord;
    readonly Label macHint = new();
    readonly Label winHint = new();
    readonly Label error = new();
    Recording recording;

    const string MacHintText = "Click Record and press the shortcut, or type it like Cmd+Shift+Z.";
    const string WinHintText = "Type keys like Ctrl+Y. Chain steps with “then”, e.g. Shift+Home then Backspace.";

    public ShortcutEditForm(Shortcut draft, Shortcut? original, AppSettings settings, RemapEngine engine, IEnumerable<string> categories)
    {
        this.draft = draft;
        this.original = original;
        this.settings = settings;
        this.engine = engine;

        Text = original == null ? "Add shortcut" : "Edit shortcut";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.White;
        ForeColor = Theme.Text;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Padding = new Padding(20, 18, 20, 16),
        };
        for (int i = 0; i < 3; i++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        int row = 0;

        void Field(string label, Control field, Control? extra = null)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 16, 2) }, 0, row);
            field.Margin = new Padding(0, 4, 6, 2);
            layout.Controls.Add(field, 1, row);
            if (extra != null) layout.Controls.Add(extra, 2, row);
            row++;
        }

        void Hint(Label hint, string text)
        {
            hint.Text = text;
            hint.AutoSize = true;
            hint.ForeColor = Theme.Muted;
            hint.MaximumSize = new Size(380, 0);
            hint.Margin = new Padding(0, 2, 0, 10);
            layout.Controls.Add(hint, 1, row);
            layout.SetColumnSpan(hint, 2);
            row++;
        }

        macBox.Width = 300;
        macBox.Font = new Font("Segoe UI Symbol", 10f);
        macBox.Text = MacChord.TryParse(draft.Mac, out var chord) ? chord.ToString() : draft.Mac;
        macBox.TextChanged += (_, _) => UpdateMacHint();
        macRecord = RecordButton(() => ToggleRecording(Recording.Mac));
        Field("Mac shortcut", macBox, macRecord);
        Hint(macHint, MacHintText);

        kindBox.DropDownStyle = ComboBoxStyle.DropDownList;
        kindBox.Width = 300;
        kindBox.Items.Add("Send Windows keys");
        foreach (var special in ActionSpec.Specials) kindBox.Items.Add(special.Label);
        bool parsed = ActionSpec.TryParse(draft.Windows, out var action);
        kindBox.SelectedIndex = parsed && action.Kind != ActionKind.Keys
            ? Array.FindIndex(ActionSpec.Specials, s => s.Kind == action.Kind) + 1
            : 0;
        kindBox.SelectedIndexChanged += (_, _) => UpdateKind();
        Field("Action", kindBox);

        winBox.Width = 300;
        winBox.Text = kindBox.SelectedIndex == 0 ? draft.Windows : "";
        winRecord = RecordButton(() => ToggleRecording(Recording.Windows));
        Field("Windows keys", winBox, winRecord);
        Hint(winHint, WinHintText);

        descriptionBox.Width = 300;
        descriptionBox.PlaceholderText = "What it does, e.g. Redo";
        descriptionBox.Text = draft.Description;
        Field("Description", descriptionBox);

        categoryBox.DropDownStyle = ComboBoxStyle.DropDown;
        categoryBox.Width = 300;
        categoryBox.Items.AddRange([.. categories]);
        categoryBox.Text = draft.Category;
        Field("Category", categoryBox);

        enabledBox.Text = "Enabled";
        enabledBox.AutoSize = true;
        enabledBox.Checked = draft.Enabled;
        Field("", enabledBox);

        error.AutoSize = true;
        error.ForeColor = Theme.Error;
        error.MaximumSize = new Size(440, 0);
        error.Margin = new Padding(0, 6, 0, 0);
        layout.Controls.Add(error, 0, row);
        layout.SetColumnSpan(error, 3);
        row++;

        var save = new Button { Text = "Save", AutoSize = true, Padding = new Padding(10, 2, 10, 2), Margin = new Padding(8, 0, 0, 0) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, Padding = new Padding(6, 2, 6, 2), DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 14, 0, 0),
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, row);
        layout.SetColumnSpan(buttons, 3);

        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;

        UpdateKind();
        UpdateMacHint();
    }

    static Button RecordButton(Action click)
    {
        var button = new Button { Text = "Record", AutoSize = true, Padding = new Padding(6, 0, 6, 0), Margin = new Padding(0, 3, 0, 0) };
        button.Click += (_, _) => click();
        return button;
    }

    void UpdateKind()
    {
        bool keys = kindBox.SelectedIndex == 0;
        winBox.Enabled = winRecord.Enabled = keys;
        if (!keys && recording == Recording.Windows) StopRecording();
    }

    void UpdateMacHint()
    {
        if (recording == Recording.Mac) return;
        macHint.Text = MacChord.TryParse(macBox.Text, out var chord)
            ? $"{chord.ToSymbols()}  —  on your keyboard press {settings.PhysicalKeys(chord)}"
            : MacHintText;
    }

    void ToggleRecording(Recording which)
    {
        var was = recording;
        StopRecording();
        if (was == which) return;

        recording = which;
        error.Text = "";
        if (which == Recording.Mac)
        {
            macRecord.Text = "Stop";
            macHint.Text = "Press the shortcut now, holding ⌘ (Alt) or ⌥ (Win). Esc cancels.";
            macBox.Focus();
            // The engine sees ⌘ / ⌥ exactly as it will when the shortcut is used, including Win-key combos.
            engine.CaptureNext(chord => BeginInvoke(() =>
            {
                if (recording != Recording.Mac) return;
                macBox.Text = chord.ToString();
                StopRecording();
            }));
        }
        else
        {
            winRecord.Text = "Stop";
            winHint.Text = "Press the Windows keys to send now. Esc cancels.";
            winBox.Focus();
            engine.Suspended = true; // let real Alt / Ctrl combos reach this window untranslated
        }
    }

    void StopRecording()
    {
        if (recording == Recording.Mac) engine.CaptureNext(null);
        if (recording == Recording.Windows) engine.Suspended = false;
        recording = Recording.None;
        macRecord.Text = winRecord.Text = "Record";
        winHint.Text = WinHintText;
        UpdateMacHint();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (recording != Recording.None && keyData == Keys.Escape)
        {
            StopRecording();
            return true;
        }
        if (recording != Recording.Windows) return base.ProcessCmdKey(ref msg, keyData);

        var key = keyData & Keys.KeyCode;
        if (key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.None)
            return true; // wait for the actual key

        var mods = WinMods.None;
        if ((keyData & Keys.Control) != 0) mods |= WinMods.Ctrl;
        if ((keyData & Keys.Alt) != 0) mods |= WinMods.Alt;
        if ((keyData & Keys.Shift) != 0) mods |= WinMods.Shift;
        if (Native.GetKeyState(Native.VK_LWIN) < 0 || Native.GetKeyState(Native.VK_RWIN) < 0) mods |= WinMods.Win;
        winBox.Text = new WinChord(mods, (int)key).ToString();
        StopRecording();
        return true;
    }

    void Save()
    {
        StopRecording();
        error.Text = "";

        if (!MacChord.TryParse(macBox.Text, out var chord))
        {
            error.Text = "The Mac shortcut needs Cmd or Option plus a key, e.g. Cmd+Shift+Z.";
            macBox.Focus();
            return;
        }

        ActionSpec action;
        if (kindBox.SelectedIndex == 0)
        {
            if (!ActionSpec.TryParse(winBox.Text, out action) || action.Kind != ActionKind.Keys)
            {
                error.Text = "Enter the Windows keys to send, e.g. Ctrl+Y. Key names: A–Z, 0–9, F1–F24, Left, Home, " +
                    "Enter, Escape, Backspace, Delete, Space, Tab, PrintScreen, and symbols like [ ] , . / = -";
                winBox.Focus();
                return;
            }
        }
        else
        {
            action = ActionSpec.Special(ActionSpec.Specials[kindBox.SelectedIndex - 1].Kind);
        }

        var self = original ?? draft;
        if (enabledBox.Checked && !SettingsForm.ConfirmNoClash(this, self, chord.ToString(), settings.Shortcuts, out _))
            return;

        draft.Mac = chord.ToString();
        draft.Windows = action.ToString();
        draft.Description = descriptionBox.Text.Trim() is { Length: > 0 } d ? d : action.Label;
        draft.Category = categoryBox.Text.Trim() is { Length: > 0 } c ? c : "Custom";
        draft.Enabled = enabledBox.Checked;
        DialogResult = DialogResult.OK;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        StopRecording();
        base.OnFormClosed(e);
    }
}
