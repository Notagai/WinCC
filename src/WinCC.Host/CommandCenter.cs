using System.ComponentModel;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WinCC.DesktopInterop;
using WinCC.Layout;

namespace WinCC.Host;

internal sealed class CommandCenter : Window
{
    private readonly Dashboard dashboard;
    internal bool AllowShutdown { get; set; }
    private readonly StackPanel content = new() { Margin = new Thickness(22) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 16) };
    private readonly DispatcherTimer topology = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly uint shellMessage = Desktop.ShellRestartMessage;
    public CommandCenter(Dashboard dashboard)
    {
        this.dashboard = dashboard;
        Title = "WinCC · Command Center"; Width = 740; Height = 780; MinWidth = 500; MinHeight = 450;
        Content = new LiquidGlass { Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(Hook);
        Closing += OnClosing;
        topology.Tick += (_, _) => { topology.Stop(); dashboard.DisplayChanged(); };
        Closed += (_, _) => topology.Stop();
        Refresh();
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is 0x007E or 0x02E0 || (uint)message == shellMessage)
        {
            topology.Stop(); topology.Start();
            if ((uint)message == shellMessage) Log.Write("shell.recreated", "normal_top_level_windows_retained");
        }
        return 0;
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AllowShutdown) return;
        e.Cancel = true;
        WindowState = WindowState.Minimized;
    }
    public void RefreshStatus() => status.Text = dashboard.Status;
    public void Refresh()
    {
        content.Children.Clear();
        content.Children.Add(new TextBlock { Text = "Windows Command Center", FontSize = 27, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "Your desktop. Your layout.", Foreground = Brushes.LightSteelBlue, Margin = new Thickness(0, 4, 0, 0) });
        RefreshStatus(); content.Children.Add(status);
        var mode = new WrapPanel(); content.Children.Add(mode);
        if (dashboard.Editor.IsEditing)
        {
            AddButton(mode, "Done · Save layout", dashboard.Done);
            AddButton(mode, "Cancel", dashboard.Cancel);
            AddButton(mode, "Undo", () => { dashboard.Editor.Undo(); dashboard.Render(); }, dashboard.Editor.CanUndo);
            AddButton(mode, "Redo", () => { dashboard.Editor.Redo(); dashboard.Render(); }, dashboard.Editor.CanRedo);
        }
        else AddButton(mode, "Edit Dashboard", dashboard.Enter, !dashboard.ReadOnly);
        AddButton(mode, "Show widgets", dashboard.Render);
        AddButton(mode, "Exit WinCC", () =>
        {
            if (!dashboard.RequestExit()) return;
            dashboard.Dispose();
            Application.Current.Shutdown();
        });
        if (!dashboard.Windowed)
            Paragraph("Desktop positioning is experimental until verified on your Windows 11 build. " +
                      "If layering or input is wrong, exit and relaunch with --windowed for ordinary app windows.");
        Heading("Appearance");
        var appearance = new WrapPanel(); content.Children.Add(appearance);
        foreach (string theme in new[] { "Winter Glass", "Midnight" })
            AddButton(appearance, theme == dashboard.Settings.Theme ? "✓ " + theme : theme, () => dashboard.ChangeTheme(theme));
        var fonts = new WrapPanel(); content.Children.Add(fonts);
        fonts.Children.Add(new TextBlock { Text = "Interface / widgets", VerticalAlignment = VerticalAlignment.Center });
        var interfaceSize = new ComboBox { ItemsSource = new double[] { 12, 14, 16, 18, 20, 24, 28, 32 }, SelectedItem = dashboard.Settings.InterfaceFontSize };
        var widgetSize = new ComboBox { ItemsSource = new double[] { 12, 14, 16, 18, 20, 24, 28, 32 }, SelectedItem = dashboard.Settings.WidgetFontSize };
        System.Windows.Automation.AutomationProperties.SetName(interfaceSize, "Interface font size");
        System.Windows.Automation.AutomationProperties.SetName(widgetSize, "Widget font size");
        fonts.Children.Add(interfaceSize); fonts.Children.Add(widgetSize);
        AddButton(fonts, "Apply text size", () => dashboard.ChangeFontSizes((double)interfaceSize.SelectedItem, (double)widgetSize.SelectedItem));
        AddButton(fonts, "Reset text size", () => dashboard.ChangeFontSizes(18, 18));
        Heading("Optional connections");
        Paragraph("Weather sends the coordinates you enter to Open-Meteo (15-minute refresh). Spotify reads your local Windows media session. AI sends only the conversation you submit to OpenAI; API usage may incur charges.");
        var weather = new CheckBox { Content = "Connect weather · Open-Meteo", IsChecked = dashboard.Settings.WeatherEnabled, Margin = new Thickness(3) };
        var media = new CheckBox { Content = "Connect local Spotify media session", IsChecked = dashboard.Settings.SpotifyEnabled, Margin = new Thickness(3) };
        content.Children.Add(weather); content.Children.Add(media);
        var coordinates = new WrapPanel(); content.Children.Add(coordinates);
        var lat = new TextBox { Text = dashboard.Settings.Latitude.ToString(CultureInfo.InvariantCulture), Width = 100, Margin = new Thickness(3) };
        var lon = new TextBox { Text = dashboard.Settings.Longitude.ToString(CultureInfo.InvariantCulture), Width = 100, Margin = new Thickness(3) };
        var fahrenheit = new CheckBox { Content = "°F", IsChecked = dashboard.Settings.Fahrenheit, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
        coordinates.Children.Add(new TextBlock { Text = "Latitude / longitude", VerticalAlignment = VerticalAlignment.Center });
        coordinates.Children.Add(lat); coordinates.Children.Add(lon); coordinates.Children.Add(fahrenheit);
        System.Windows.Automation.AutomationProperties.SetName(lat, "Weather latitude");
        System.Windows.Automation.AutomationProperties.SetName(lon, "Weather longitude");
        var model = new TextBox { Text = dashboard.Settings.AiModel, Width = 180, Margin = new Thickness(3) };
        System.Windows.Automation.AutomationProperties.SetName(model, "OpenAI model");
        var ai = new WrapPanel(); content.Children.Add(ai);
        ai.Children.Add(new TextBlock { Text = "AI model", VerticalAlignment = VerticalAlignment.Center }); ai.Children.Add(model);
        AddButton(ai, "Save connections", () =>
        {
            if (!double.TryParse(lat.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(lon.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
                throw new InvalidOperationException("Enter coordinates using a decimal point.");
            dashboard.ConfigureProviders(weather.IsChecked == true, latitude, longitude, fahrenheit.IsChecked == true,
                media.IsChecked == true, model.Text.Trim());
        });
        Paragraph("AI reads WINCC_AI_API_KEY from the process environment. WinCC never saves the key or chat to disk. Restart after setting the variable. Weather attribution: Open-Meteo.com, CC BY 4.0; review its terms before commercial use.");

        var targets = new ComboBox { DisplayMemberPath = "Label", SelectedValuePath = "Key", MinWidth = 240 };
        foreach (var (m, i) in dashboard.Monitors.Select((m, i) => (m, i)))
            targets.Items.Add(new { m.Key, Label = $"Display {i + 1}{(m.Primary ? " · Primary" : "")} · {m.WidthDip:F0} × {m.HeightDip:F0} DIP" });
        targets.SelectedIndex = Math.Max(0, dashboard.Monitors.ToList().FindIndex(m => m.Primary));
        if (dashboard.Editor.IsEditing)
        {
            Heading("Add widgets");
            content.Children.Add(targets);
            var catalog = new WrapPanel(); content.Children.Add(catalog);
            foreach (var spec in WidgetCatalog.All)
                AddButton(catalog, "+ " + spec.Name, () => dashboard.Add(spec.Type, (string)targets.SelectedValue));
            var presets = new WrapPanel(); content.Children.Add(presets);
            AddButton(presets, "Winter Glass preset", dashboard.Preset);
            AddButton(presets, "Reset to blank", () =>
            {
                if (MessageBox.Show(this, "Remove all widgets from the draft? Undo or Cancel can restore them. Notes files are kept.",
                        "Reset layout", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                dashboard.Editor.Change(d => d.Active.Widgets.Clear()); dashboard.Render();
            });
            Paragraph("Drag the widget header or resize its lower-right handle. Arrow keys nudge a focused header by 1 DIP; Shift uses 10 DIP. Escape cancels only the active gesture. Use the list below to select obscured widgets.");
        }
        Heading("Your widgets");
        var widgets = dashboard.Editor.Current.Active.Widgets.OrderBy(w => w.ZIndex).ToList();
        if (widgets.Count == 0) Paragraph("Your canvas is blank. Enter Edit Mode to add widgets or apply an optional preset.");
        foreach (var w in widgets)
        {
            var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(new TextBlock { Text = WidgetCatalog.Get(w.Type).Name + " · " + w.Id[..Math.Min(6, w.Id.Length)], Width = 150, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(row);
            if (dashboard.Editor.IsEditing)
            {
                AddButton(row, "Back", () => dashboard.Reorder(w.Id, -1));
                AddButton(row, "Forward", () => dashboard.Reorder(w.Id, 1));
                AddButton(row, "Move to display", () => dashboard.MoveTo(w.Id, (string)targets.SelectedValue));
                AddButton(row, "Remove", () => dashboard.Remove(w.Id));
                if (w.Type == "clock")
                    foreach (string format in new[] { "system", "12", "24" })
                        AddButton(row, format, () =>
                        {
                            var settings = new Dictionary<string, string>(w.Settings) { ["hourFormat"] = format };
                            dashboard.Editor.Update(w with { Settings = settings }); dashboard.Render();
                        });
            }
        }
        Heading("App launcher");
        Paragraph("Add local programs or shortcuts yourself. WinCC passes no command arguments and does not register startup tasks.");
        var launchers = new WrapPanel(); content.Children.Add(launchers);
        AddButton(launchers, "+ Choose app", () =>
        {
            var picker = new OpenFileDialog { Filter = "Programs and shortcuts|*.exe;*.lnk", CheckFileExists = true, DereferenceLinks = false };
            if (picker.ShowDialog(this) == true) dashboard.AddLauncher(picker.FileName);
        });
        foreach (var path in dashboard.Settings.Launchers.ToArray())
        {
            var row = new WrapPanel(); content.Children.Add(row);
            AddButton(row, Path.GetFileNameWithoutExtension(path), () => dashboard.Launch(path));
            AddButton(row, "Remove", () => dashboard.RemoveLauncher(path));
        }
        Heading("Local data and recovery");
        Paragraph("Layout, independent notes, settings and bounded local diagnostics are stored in %LOCALAPPDATA%\\WinCC. " +
                  "No telemetry, auto-start or wallpaper integration. Optional providers stay disconnected until you configure or enable them.");
        FontSize = dashboard.Settings.InterfaceFontSize;
        Typography.Apply(content, dashboard.Settings.InterfaceFontSize / 14);
    }
    private void Heading(string text) => content.Children.Add(new TextBlock
        { Text = text, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 20, 0, 6) });
    private void Paragraph(string text) => content.Children.Add(new TextBlock
        { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSteelBlue, Margin = new Thickness(0, 6, 0, 8) });
    private void AddButton(Panel panel, string text, Action action, bool enabled = true)
    {
        var button = new Button { Content = text, IsEnabled = enabled };
        System.Windows.Automation.AutomationProperties.SetName(button, text);
        button.Click += (_, _) => dashboard.Action(action);
        panel.Children.Add(button);
    }
}
