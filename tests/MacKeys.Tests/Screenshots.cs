using MacKeys;

static class Screenshots
{
    public static void Render(string folder)
    {
        Directory.CreateDirectory(folder);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new Font("Segoe UI", 9f));
        var settings = new AppSettings();
        var engine = new RemapEngine(EngineConfig.From(settings)); // never started: no hooks
        using var icon = Theme.CreateIcon(true);

        // Render on the primary monitor (nearly invisible) so it uses that monitor's DPI.
        var origin = Screen.PrimaryScreen!.WorkingArea.Location;
        using var form = new SettingsForm(settings, engine, icon, () => { })
        {
            StartPosition = FormStartPosition.Manual,
            Location = origin,
            ShowInTaskbar = false,
            Opacity = 0.01,
        };
        form.Show();
        Application.DoEvents();
        Console.WriteLine($"settings form {form.Size} dpi {form.DeviceDpi} screen {Screen.FromControl(form).Bounds}");
        Save(form, Path.Combine(folder, "settings-shortcuts.png"));

        var tabs = form.Controls.OfType<TabControl>().Single();
        tabs.SelectedIndex = 1;
        Application.DoEvents();
        Save(form, Path.Combine(folder, "settings-general.png"));

        form.ShowTab("Help");
        Application.DoEvents();
        Save(form, Path.Combine(folder, "settings-help.png"));

        form.ShowTab("Feedback");
        Application.DoEvents();
        Save(form, Path.Combine(folder, "settings-feedback.png"));

        tabs.SelectedIndex = 0;
        var draft = settings.Shortcuts.First(s => s.Mac == "Cmd+Shift+Z").Clone();
        using var edit = new ShortcutEditForm(draft, draft, settings, engine, ["Editing", "Custom"])
        {
            StartPosition = FormStartPosition.Manual,
            Location = origin,
            Opacity = 0.01,
        };
        edit.Show();
        Application.DoEvents();
        Save(edit, Path.Combine(folder, "edit-shortcut.png"));

        using var bitmap = icon.ToBitmap();
        bitmap.Save(Path.Combine(folder, "icon.png"));
    }

    static void Save(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(path);
    }
}
