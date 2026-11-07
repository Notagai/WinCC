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
    private TrayIcon? tray;
    private readonly TabControl tabs = new() { Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0) };
    private string selectedTab = "Clock";
    private StackPanel panel = null!;
    internal bool AllowShutdown { get; set; }
    private readonly StackPanel content = new() { Margin = new Thickness(24) };
    private readonly DispatcherTimer topology = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly uint shellMessage = Desktop.ShellRestartMessage;
    public CommandCenter(Dashboard dashboard)
    {
        this.dashboard = dashboard;
        Title = "WinCC · Settings"; Width = 860; Height = 760; MinWidth = 640; MinHeight = 480;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;
        Height = Math.Min(760, SystemParameters.WorkArea.Height * .85);
        var shell = new DockPanel();
        var chrome = new DockPanel { Margin = new Thickness(16, 12, 16, 0) };
        var close = new Button { Content = "×", Padding = new Thickness(10, 4, 10, 4), ToolTip = "Hide Settings" };
        close.Click += (_, _) => Hide();
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
            tray = new TrayIcon(handle);
        };
        Closing += (_, e) => { if (!AllowShutdown) { e.Cancel = true; Hide(); } };
        topology.Tick += (_, _) => { topology.Stop(); dashboard.DisplayChanged(); };
        Closed += (_, _) => topology.Stop(); Refresh();
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == TrayIcon.CallbackMessage)
        {
            int mouse = (int)lParam;
            if (mouse is 0x0203 or 0x0202) ShowSettings();
            if (mouse == 0x0205)
            {
                var menu = new ContextMenu { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 34, 44)), Foreground = System.Windows.Media.Brushes.White };
                var open = new MenuItem { Header = "Settings" }; open.Click += (_, _) => ShowSettings(); menu.Items.Add(open);
                var capture = new MenuItem { Header = dashboard.ScreenshotMode ? "Resume live glass" : "Screenshot mode" };
                capture.Click += (_, _) => dashboard.Action(() => dashboard.SetScreenshotMode(!dashboard.ScreenshotMode)); menu.Items.Add(capture);
                var exit = new MenuItem { Header = "Exit WinCC" }; exit.Click += (_, _) => { if (dashboard.RequestExit()) { dashboard.Dispose(); Application.Current.Shutdown(); } }; menu.Items.Add(exit);
                menu.IsOpen = true;
            }
        }
        if ((uint)message == shellMessage) tray?.Restore();
        if (message is 0x007E or 0x02E0 || (uint)message == shellMessage) { topology.Stop(); topology.Start(); }
        return 0;
    }
    public void DisposeTray() => tray?.Dispose();
    public void ShowSettings() { Show(); WindowState = WindowState.Normal; Activate(); }
    public void RefreshStatus() { }
    public void Refresh()
    {
        if (tabs.SelectedItem is TabItem previous) selectedTab = previous.Header.ToString() ?? "Clock";
        tabs.Items.Clear(); content.Children.Clear();
        panel = content;
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
        Heading("Position · % of usable display");
        var position = Row();
        var xPosition = new TextBox { Text = "50", Width = 80, Margin = new Thickness(4) };
        var yPosition = new TextBox { Text = "50", Width = 80, Margin = new Thickness(4) };
        System.Windows.Automation.AutomationProperties.SetName(xPosition, "Horizontal percent");
        System.Windows.Automation.AutomationProperties.SetName(yPosition, "Vertical percent");
        position.Children.Add(new TextBlock { Text = "X", VerticalAlignment = VerticalAlignment.Center }); position.Children.Add(xPosition);
        position.Children.Add(new TextBlock { Text = "Y", VerticalAlignment = VerticalAlignment.Center }); position.Children.Add(yPosition);
        double Read(TextBox field) => double.Parse(field.Text, System.Globalization.CultureInfo.InvariantCulture);
        Button(position, "Position", () => dashboard.SnapClock(Read(xPosition), Read(yPosition)));
        Button(position, "X only", () => dashboard.SnapClock(Read(xPosition), null));
        Button(position, "Y only", () => dashboard.SnapClock(null, Read(yPosition)));
        Button(position, "Center", () => dashboard.SnapClock(50, 50));
        Button(position, "Fill display", dashboard.FitClockToDisplay);
        var shape = Row(); Button(shape, "Glass lens", () => dashboard.ChangeClockShape(false)); Button(shape, "Time-shaped glass", () => dashboard.ChangeClockShape(true));
        Heading("Presets");
        for (int slot = 1; slot <= 3; slot++)
        {
            int captured = slot; var preset = Row();
            Button(preset, "Save " + slot, () => dashboard.SavePreset(captured));
            Button(preset, "Load " + slot, () => dashboard.LoadPreset(captured), dashboard.Presets.Exists(slot));
        }
        Heading("Frame rate · " + (dashboard.Settings.GlassFps == 0 ? "Uncapped" : dashboard.Settings.GlassFps + " FPS"));
        var rates = Row();
        foreach (int fps in new[] { 15, 30, 60, 90, 120, 144, 165, 240, 360, 0 })
            Button(rates, (fps == dashboard.Settings.GlassFps ? "✓ " : "") + (fps == 0 ? "Uncapped" : fps.ToString()), () => dashboard.ChangeFrameRate(fps));
        var adjust = Row();
        Button(adjust, "−5 FPS", () => dashboard.ChangeFrameRate(Math.Max(1, (dashboard.Settings.GlassFps == 0 ? 60 : dashboard.Settings.GlassFps) - 5)));
        Button(adjust, "+5 FPS", () => dashboard.ChangeFrameRate(Math.Min(500, (dashboard.Settings.GlassFps == 0 ? 60 : dashboard.Settings.GlassFps) + 5)));
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
        BuildDesign();
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
        var captureActions = Row();
        Button(captureActions, dashboard.ScreenshotMode ? "Resume live glass" : "Screenshot mode", () => dashboard.SetScreenshotMode(!dashboard.ScreenshotMode));
        var app = Row(); Button(app, "Minimize", () => WindowState = WindowState.Minimized);
        Button(app, "Exit", () => { if (dashboard.RequestExit()) { dashboard.Dispose(); Application.Current.Shutdown(); } });
        content.Children.Add(tabs);
        FontSize = dashboard.Settings.InterfaceFontSize;
    }
    private string designPage = "Typography";
    private void BuildDesign()
    {
        Heading("Text design");
        var host = panel;
        var pages = new TabControl(); host.Children.Add(pages);
        string wanted = designPage;
        void Group(string name)
        {
            panel = new StackPanel { Margin = new Thickness(8) };
            pages.Items.Add(new TabItem { Header = name, Content = panel });
        }
        var d = dashboard.Settings.Design;
        TextBox Field(string label, string value)
        {
            var row = Row(); row.Children.Add(new TextBlock { Text = label, Width = 220, VerticalAlignment = VerticalAlignment.Center });
            var field = new TextBox { Text = value, Width = 240, Margin = new Thickness(4) };
            System.Windows.Automation.AutomationProperties.SetName(field, label); row.Children.Add(field); return field;
        }
        string N(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ComboBox FontField(string label, string value)
        {
            var row = Row(); row.Children.Add(new TextBlock { Text = label, Width = 220, VerticalAlignment = VerticalAlignment.Center });
            var picker = new ComboBox { ItemsSource = FontLibrary.Names, Text = value, IsEditable = true, IsTextSearchEnabled = true, Width = 240, MaxDropDownHeight = 320 };
            var option = new DataTemplate();
            var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
            text.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Black); option.VisualTree = text;
            picker.ItemTemplate = option;
            System.Windows.Automation.AutomationProperties.SetName(picker, label); row.Children.Add(picker);
            var download = new Button { Content = "Get Google font" }; row.Children.Add(download);
            download.Click += async (_, _) =>
            {
                download.IsEnabled = false;
                try { await FontLibrary.Download(picker.Text.Trim()); download.Content = "Downloaded"; }
                catch (Exception error) when (error is System.IO.IOException or System.Net.Http.HttpRequestException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
                { MessageBox.Show(this, error.Message, "Google Fonts"); }
                finally { download.IsEnabled = true; }
            };
            return picker;
        }
        Group("Typography");
        var font = FontField("Glass font", d.Font); var weight = Field("Weight · 100–900", N(d.Weight));
        var size = Field("Size · 25–200%", N(d.SizePercent));
        var sx = Field("Horizontal stretch · 0.4–2.5", N(d.StretchX)); var sy = Field("Vertical stretch · 0.4–2.5", N(d.StretchY));
        var tracking = Field("Letter spacing · −4–20", N(d.Tracking)); var stroke = Field("Stroke thickness · 0–8", N(d.Stroke));
        var timeFormat = Field("Custom time format", d.TimeFormat);
        bool show = d.ShowDate, italic = d.Italic;
        var italicToggle = new Button { Content = italic ? "Italic on" : "Italic off" }; italicToggle.Click += (_, _) => { italic = !italic; italicToggle.Content = italic ? "Italic on" : "Italic off"; }; Row().Children.Add(italicToggle);
        Group("Material & tint");
        var refraction = Field("Lens thickness · 0–2", N(d.Refraction));
        var tint = Field("Glass tint · #RRGGBB", d.Tint); var amount = Field("Tint strength · 0–35%", N(d.TintAmount * 100));
        var swatches = Row();
        foreach (var choice in new[] { ("Ice", "#BFD5FF"), ("Rose", "#FFC0DA"), ("Amber", "#FFD599"), ("Mint", "#A4FFE0") })
            Button(swatches, choice.Item1, () => { tint.Text = choice.Item2; amount.Text = "18"; });
        Button(swatches, "Tint off", () => amount.Text = "0");
        Group("Date");
        var dateFormat = Field("Date format", d.DateFormat); var dateFont = FontField("Date font", d.DateFont);
        var dateSize = Field("Date size · 8–40", N(d.DateSize)); var dateLift = Field("Date lift · 0–25", N(d.DateLift));
        var dateColor = Field("Date color · #RRGGBB", d.DateColor);
        var dateToggle = new Button { Content = show ? "Date shown" : "Date hidden" }; dateToggle.Click += (_, _) => { show = !show; dateToggle.Content = show ? "Date shown" : "Date hidden"; }; Row().Children.Add(dateToggle);
        foreach (TabItem tab in pages.Items) if ((string)tab.Header == wanted) pages.SelectedItem = tab;
        pages.SelectionChanged += (_, _) => { if (pages.SelectedItem is TabItem selected) designPage = (string)selected.Header; };
        panel = host;
        double Read(TextBox field) => double.Parse(field.Text, System.Globalization.CultureInfo.InvariantCulture);
        var actions = Row();
        Button(actions, "Apply design", () => dashboard.ApplyDesign(new ClockDesign
        {
            Font = font.Text.Trim(), Weight = int.Parse(weight.Text, System.Globalization.CultureInfo.InvariantCulture), SizePercent = Read(size), StretchX = Read(sx), StretchY = Read(sy),
            Tracking = Read(tracking), Stroke = Read(stroke), Refraction = Read(refraction), Tint = tint.Text.Trim(), TintAmount = Read(amount) / 100,
            ShowDate = show, Italic = italic, TimeFormat = timeFormat.Text, DateFormat = dateFormat.Text,
            DateFont = dateFont.Text.Trim(), DateSize = Read(dateSize), DateLift = Read(dateLift), DateColor = dateColor.Text.Trim()
        }));
        Button(actions, "Reset design", () => dashboard.ApplyDesign(new ClockDesign()));
    }
    private void Heading(string title)
    {
        string? section = title == "Clock" ? "Clock" : title.StartsWith("Frame rate") ? "Performance" : title == "Glass" ? "Appearance" : title == "Text design" ? "Design" : title == "Typeface" ? "Text" : title == "WinCC" ? "App" : null;
        if (section is not null)
        {
            panel = new StackPanel { Margin = new Thickness(12) };
            var tab = new TabItem { Header = section, Foreground = System.Windows.Media.Brushes.White, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
            tabs.Items.Add(tab); if (section == selectedTab) tabs.SelectedItem = tab;
        }
        panel.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 10) });
    }
    private WrapPanel Row() { var row = new WrapPanel(); panel.Children.Add(row); return row; }
    private void Button(Panel panel, string label, Action action, bool enabled = true)
    {
        var button = new Button { Content = label, IsEnabled = enabled };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => dashboard.Action(action); panel.Children.Add(button);
    }
}
