using Microsoft.Win32;

namespace MacKeys;

/// <summary>Owns the tray icon, the settings and the remapping engine for the life of the app.</summary>
sealed class TrayApp : ApplicationContext
{
    readonly AppSettings settings;
    readonly RemapEngine engine;
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem enabledItem;
    readonly Icon iconOn = Theme.CreateIcon(true);
    readonly Icon iconOff = Theme.CreateIcon(false);
    readonly Control invoker = new();
    readonly RegisteredWaitHandle showWait;
    SettingsForm? settingsForm;

    public TrayApp(EventWaitHandle showSignal)
    {
        settings = AppSettings.Load(out bool firstRun);
        engine = new RemapEngine(EngineConfig.From(settings));
        engine.Start();
        invoker.CreateControl();

        enabledItem = new ToolStripMenuItem("Remapping on", null, (_, _) =>
        {
            settings.Enabled = !settings.Enabled;
            Apply();
        });
        var menu = new ContextMenuStrip();
        menu.Items.Add(enabledItem);
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("Help", null, (_, _) => ShowSettings("Help"));
        menu.Items.Add("Send feedback…", null, (_, _) => ShowSettings("Feedback"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit MacKeys", null, (_, _) => ExitThread());

        tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowSettings();
        };

        SystemEvents.SessionSwitch += OnSessionSwitch;
        showWait = ThreadPool.RegisterWaitForSingleObject(
            showSignal, (_, _) => invoker.BeginInvoke(() => ShowSettings()), null, Timeout.Infinite, executeOnlyOnce: false);
        UpdateTray();

        if (firstRun)
        {
            Apply(); // write the defaults so they can be found and edited
            ShowSettings();
        }
    }

    void Apply()
    {
        try
        {
            settings.Save();
        }
        catch (Exception ex)
        {
            tray.ShowBalloonTip(5000, "MacKeys couldn't save settings", ex.Message, ToolTipIcon.Warning);
        }
        engine.Config = EngineConfig.From(settings);
        UpdateTray();
        settingsForm?.SyncFromSettings();
    }

    void UpdateTray()
    {
        enabledItem.Checked = settings.Enabled;
        tray.Icon = settings.Enabled ? iconOn : iconOff;
        tray.Text = settings.Enabled ? "MacKeys — remapping on" : "MacKeys — paused";
    }

    void ShowSettings(string? tab = null)
    {
        if (settingsForm is not { IsDisposed: false })
        {
            settingsForm = new SettingsForm(settings, engine, iconOn, Apply);
            settingsForm.FormClosed += (_, _) => settingsForm = null;
            settingsForm.Show();
        }
        else if (settingsForm.WindowState == FormWindowState.Minimized)
        {
            settingsForm.WindowState = FormWindowState.Normal;
        }
        if (tab != null) settingsForm.ShowTab(tab);
        settingsForm.Activate();
    }

    void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionUnlock) engine.ResetState();
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        showWait.Unregister(null);
        settingsForm?.Close();
        tray.Visible = false;
        tray.Dispose();
        engine.Dispose();
        iconOn.Dispose();
        iconOff.Dispose();
        invoker.Dispose();
        base.ExitThreadCore();
    }
}
