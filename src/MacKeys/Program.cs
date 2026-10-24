namespace MacKeys;

static class Program
{
    [STAThread]
    static void Main()
    {
        // The named event doubles as the single-instance check: a second launch just asks the first to show settings.
        using var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\MacKeys.ShowSettings", out bool createdNew);
        if (!createdNew)
        {
            Native.AllowSetForegroundWindow(-1);
            showSettings.Set();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(showSettings));
    }
}
