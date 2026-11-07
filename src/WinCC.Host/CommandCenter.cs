using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using WinCC.DesktopInterop;
using WinCC.Layout;

namespace WinCC.Host;

internal sealed class CommandCenter : Window
{
    private readonly Dashboard dashboard;
    internal bool AllowShutdown { get; set; }
    private readonly StackPanel content = new() { Margin = new Thickness(24) };
    private readonly DispatcherTimer topology = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly uint shellMessage = Desktop.ShellRestartMessage;
    public CommandCenter(Dashboard dashboard)
    {
        this.dashboard = dashboard;
        Title = "WinCC · Settings"; Width = 460; Height = 680; MinWidth = 360; MinHeight = 400;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;
        Height = Math.Min(680, SystemParameters.WorkArea.Height * .85);
        var shell = new DockPanel();
        var chrome = new DockPanel { Margin = new Thickness(16, 12, 16, 0) };
        var close = new Button { Content = "×", Padding = new Thickness(10, 4, 10, 4), ToolTip = "Minimize Settings" };
        close.Click += (_, _) => WindowState = WindowState.Minimized;
        DockPanel.SetDock(close, Dock.Right); chrome.Children.Add(close);
        var title = new TextBlock { Text = "Settings", FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
        title.MouseLeftButtonDown += (_, _) => DragMove(); chrome.Children.Add(title);
        DockPanel.SetDock(chrome, Dock.Top); shell.Children.Add(chrome);
        shell.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = new Border { CornerRadius = new CornerRadius(22), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 34, 44)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 255, 255, 255)), BorderThickness = new Thickness(1), Child = shell };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            BackdropCapture.Exclude(handle);
            HwndSource.FromHwnd(handle)?.AddHook(Hook);
        };
        Closing += (_, e) => { if (!AllowShutdown) { e.Cancel = true; WindowState = WindowState.Minimized; } };
        topology.Tick += (_, _) => { topology.Stop(); dashboard.DisplayChanged(); };
        Closed += (_, _) => topology.Stop(); Refresh();
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is 0x007E or 0x02E0 || (uint)message == shellMessage) { topology.Stop(); topology.Start(); }
        return 0;
    }
    public void RefreshStatus() { }
    public void Refresh()
    {
        content.Children.Clear();
        Heading("Settings");
        var mode = Row();
        if (dashboard.Editor.IsEditing)
        {
            Button(mode, "Done", dashboard.Done); Button(mode, "Cancel", dashboard.Cancel);
            Button(mode, "Undo", () => { dashboard.Editor.Undo(); dashboard.Render(); }, dashboard.Editor.CanUndo);
            Button(mode, "Redo", () => { dashboard.Editor.Redo(); dashboard.Render(); }, dashboard.Editor.CanRedo);
        }
        else Button(mode, "Edit clock", dashboard.Enter, !dashboard.ReadOnly);
        Heading("Clock");
        var clocks = dashboard.Editor.Current.Active.Widgets.Where(w => w.Type == "clock").ToArray();
        var actions = Row();
        Button(actions, "Show clock", dashboard.Render);
        if (dashboard.Editor.IsEditing)
        {
            Button(actions, "Add clock", () => dashboard.Add("clock", "primary"), clocks.Length == 0);
            foreach (var w in clocks)
            {
                var formats = Row();
                foreach (string format in new[] { "system", "12", "24" })
                    Button(formats, format == "system" ? "System time" : format + " hour", () =>
                    {
                        var settings = new Dictionary<string, string>(w.Settings) { ["hourFormat"] = format };
                        dashboard.Editor.Update(w with { Settings = settings }); dashboard.Render();
                    });
                Button(formats, "Remove", () => dashboard.Remove(w.Id));
                var displays = Row();
                foreach (var (display, i) in dashboard.Monitors.Select((m, i) => (m, i)))
                    Button(displays, "Display " + (i + 1), () => dashboard.MoveTo(w.Id, display.Key));
            }
        }
        Heading("Glass");
        var material = Row();
        Button(material, "Clear", () => dashboard.ChangeGlass("Clear", dashboard.Settings.ReduceGlassMotion));
        Button(material, "Regular", () => dashboard.ChangeGlass("Regular", dashboard.Settings.ReduceGlassMotion));
        var motion = Row();
        Button(motion, "Motion on", () => dashboard.ChangeGlass(dashboard.Settings.GlassVariant, false));
        Button(motion, "Reduce motion", () => dashboard.ChangeGlass(dashboard.Settings.GlassVariant, true));
        var transparency = Row();
        Button(transparency, "Transparency on", () => dashboard.ChangeGlass(dashboard.Settings.GlassVariant, dashboard.Settings.ReduceGlassMotion, false));
        Button(transparency, "Reduce transparency", () => dashboard.ChangeGlass(dashboard.Settings.GlassVariant, dashboard.Settings.ReduceGlassMotion, true));
        Heading("Typeface");
        var fonts = Row(); Button(fonts, "Lato", () => dashboard.ChangeFontFamily("Lato"));
        Button(fonts, "VT323", () => dashboard.ChangeFontFamily("VT323"));
        Heading("Clock text size");
        var sizes = Row();
        foreach (double size in new double[] { 12, 14, 16, 18, 20, 24, 28, 32 })
            Button(sizes, (size == dashboard.Settings.WidgetFontSize ? "✓ " : "") + size,
                () => dashboard.ChangeFontSizes(dashboard.Settings.InterfaceFontSize, size));
        Heading("Settings text size");
        var interfaceSizes = Row();
        foreach (double size in new double[] { 14, 18, 24 })
            Button(interfaceSizes, size.ToString(), () => dashboard.ChangeFontSizes(size, dashboard.Settings.WidgetFontSize));
        Heading("WinCC");
        var app = Row(); Button(app, "Minimize", () => WindowState = WindowState.Minimized);
        Button(app, "Exit", () => { if (dashboard.RequestExit()) { dashboard.Dispose(); Application.Current.Shutdown(); } });
        FontSize = dashboard.Settings.InterfaceFontSize;
    }
    private void Heading(string title) => content.Children.Add(new TextBlock
    { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 10) });
    private WrapPanel Row() { var row = new WrapPanel(); content.Children.Add(row); return row; }
    private void Button(Panel panel, string label, Action action, bool enabled = true)
    {
        var button = new Button { Content = label, IsEnabled = enabled };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => dashboard.Action(action); panel.Children.Add(button);
    }
}
