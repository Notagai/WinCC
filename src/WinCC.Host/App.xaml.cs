using System.IO;
using System.Windows;

namespace WinCC.Host;

public partial class App : Application
{
    private Mutex? instance;
    private Dashboard? dashboard;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Scope to this user's interactive session. A second process never creates another dashboard.
        string identity = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        instance = new Mutex(true, @"Local\WinCC-" + identity, out bool created);
        if (!created)
        {
            MessageBox.Show("WinCC is already running. Open its Command Center from the taskbar.", "WinCC");
            Shutdown(); return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write("host.failed", args.Exception.GetType().Name);
            MessageBox.Show("WinCC encountered an error and will exit. Your last saved layout is preserved.", "WinCC");
            args.Handled = true;
            dashboard?.Dispose();
            Shutdown(1);
        };
        try
        {
            Log.Write("host.started", Environment.OSVersion.VersionString);
            dashboard = new Dashboard(e.Args.Contains("--windowed"));
            MainWindow = dashboard.Center;
            dashboard.Start();
        }
        catch (Exception error)
        {
            Log.Write("host.start_failed", error.GetType().Name);
            MessageBox.Show("WinCC could not start. No Windows settings were changed. See local diagnostics.", "WinCC");
            dashboard?.Dispose();
            Shutdown(1);
        }
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        dashboard?.Dispose();
        base.OnSessionEnding(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        dashboard?.Dispose(); instance?.Dispose(); Log.Write("host.exiting", e.ApplicationExitCode.ToString());
        base.OnExit(e);
    }
}

internal static class Paths
{
    public static string Data => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinCC");
}

internal static class Log
{
    public static void Write(string name, string reason)
    {
        try
        {
            Directory.CreateDirectory(Paths.Data);
            var path = Path.Combine(Paths.Data, "diagnostics.jsonl");
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, path + ".old", true);
            File.AppendAllText(path, System.Text.Json.JsonSerializer.Serialize(new
                { timestamp = DateTimeOffset.UtcNow, @event = name, reason }) + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
