using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WinCC.DesktopInterop;
using WinCC.Layout;
using Geometry = WinCC.Layout.Geometry;
using Calendar = System.Windows.Controls.Calendar;

namespace WinCC.Host;

internal sealed class WidgetWindow : Window, IDisposable
{
    private readonly Dashboard dashboard;
    private Widget widget;
    private readonly Border card;
    private readonly Grid grid = new();
    private readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer noteSave = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly SystemSampler sampler = new();
    private readonly TextBlock live = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private TextBox? note;
    private TextBlock? noteStatus;
    private bool noteDirty, disposed, placing;
    private Thumb? activeThumb;
    private Widget? gestureStart;
    private Point pointerStart;
    private DateTime calendarMonth = DateTime.Today;
    private bool expanded;
    private ProviderView? provider;
    private readonly List<double> cpuHistory = [];
    private readonly List<double> memoryHistory = [];
    private readonly Grid graph = new() { Height = 58, Margin = new Thickness(0, 8, 0, 0), ClipToBounds = true };
    private readonly Polyline cpuLine = new() { Stroke = Brushes.LightSkyBlue, StrokeThickness = 2 };
    private readonly Polyline memoryLine = new() { Stroke = Brushes.Plum, StrokeThickness = 2 };
    public nint Handle => new WindowInteropHelper(this).Handle;
    public int ZIndex => widget.ZIndex;

    public WidgetWindow(Dashboard dashboard, Widget widget)
    {
        this.dashboard = dashboard; this.widget = widget;
        Title = "WinCC · " + WidgetCatalog.Get(widget.Type).Name;
        Width = widget.RectDip.Width; Height = widget.RectDip.Height;
        ShowActivated = false; ShowInTaskbar = dashboard.Windowed;
        WindowStyle = dashboard.Windowed ? WindowStyle.SingleBorderWindow : WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = !dashboard.Windowed;
        Background = Brushes.Transparent;
        Topmost = false;
        var color = dashboard.Settings.Theme == "Midnight" ? Color.FromArgb(245, 20, 28, 44) : Color.FromArgb(220, 40, 53, 76);
        card = new Border
        {
            Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(18),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 194, 209, 245)), BorderThickness = new Thickness(1),
            Padding = new Thickness(16), Child = grid
        };
        Content = card;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (dashboard.Editor.IsEditing) BuildHandles();
        var body = new Grid { ClipToBounds = true };
        Grid.SetRow(body, 1); grid.Children.Add(body);
        BuildContent(body);
        SourceInitialized += (_, _) => HwndSource.FromHwnd(Handle)?.AddHook(Hook);
        Loaded += (_, _) => { ApplyPlacement(); UpdateLive(); tick.Start(); };
        tick.Tick += (_, _) => { if (IsVisible && WindowState != WindowState.Minimized) UpdateLive(); };
        noteSave.Tick += (_, _) => { noteSave.Stop(); FlushNote(); };
        PreviewKeyDown += OnKey;
        Closing += (_, e) =>
        {
            if (disposed) return;
            e.Cancel = true;
            dashboard.Center.Show(); dashboard.Center.Activate();
            dashboard.Notify("Use Remove in Edit Mode to remove a widget, or Exit WinCC to close everything.");
        };
    }

    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // Own-window system moves/resizes must not bypass the editor, including in fallback mode.
        if (message == 0x0112 && ((long)wParam & 0xFFF0) is 0xF000 or 0xF010)
        { handled = true; return 0; }
        if (!dashboard.Windowed && message == 0x0046) Desktop.KeepBehind(lParam, dashboard.AnchorFor(this));
        if (!dashboard.Windowed && message == 0x0047) dashboard.QueueStack();
        if (message == 0x02E0 && !placing)
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ApplyPlacement));
        return 0;
    }
    private MonitorArea Monitor => Geometry.Resolve(widget.MonitorKey, dashboard.Monitors);
    public void ApplyPlacement()
    {
        if (Handle == 0 || disposed || placing) return;
        placing = true;
        try
        {
            var m = Monitor;
            var r = Geometry.Clamp(widget.RectDip, WidgetCatalog.Get(widget.Type), m);
            if (expanded)
                r = Geometry.Clamp(r with { Width = Math.Max(460, r.Width), Height = Math.Max(520, r.Height) }, WidgetCatalog.Get(widget.Type), m);
            var p = Geometry.ToPhysical(r, m);
            Desktop.Place(Handle, (int)Math.Round(p.X), (int)Math.Round(p.Y),
                (int)Math.Round(r.Width * m.Scale), (int)Math.Round(r.Height * m.Scale), !dashboard.Windowed);
        }
        finally { placing = false; }
    }

    private void BuildHandles()
    {
        var header = new Thumb
        {
            Height = 30, Cursor = Cursors.SizeAll, Focusable = true,
            Template = ThumbTemplate("↕  " + WidgetCatalog.Get(widget.Type).Name + " · move"), Margin = new Thickness(0, 0, 0, 8)
        };
        System.Windows.Automation.AutomationProperties.SetName(header, "Move " + widget.Type + "; arrow keys nudge, Shift for larger steps");
        grid.Children.Add(header); WireGesture(header, resize: false);
        var resize = new Thumb
        {
            Width = 32, Height = 28, Cursor = Cursors.SizeNWSE, Focusable = true,
            HorizontalAlignment = HorizontalAlignment.Right, Template = ThumbTemplate("◢")
        };
        System.Windows.Automation.AutomationProperties.SetName(resize, "Resize " + widget.Type);
        Grid.SetRow(resize, 2); grid.Children.Add(resize); WireGesture(resize, resize: true);
    }
    private static ControlTemplate ThumbTemplate(string text)
    {
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetValue(TextBlock.TextProperty, text);
        label.SetValue(TextBlock.ForegroundProperty, Brushes.LightSteelBlue);
        label.SetValue(TextBlock.BackgroundProperty, new SolidColorBrush(Color.FromArgb(50, 180, 197, 230)));
        label.SetValue(TextBlock.PaddingProperty, new Thickness(4));
        return new ControlTemplate(typeof(Thumb)) { VisualTree = label };
    }
    private void WireGesture(Thumb thumb, bool resize)
    {
        thumb.DragStarted += (_, _) =>
        {
            if (!dashboard.Editor.IsEditing) { thumb.CancelDrag(); return; }
            activeThumb = thumb; gestureStart = widget; pointerStart = PointToScreen(Mouse.GetPosition(this));
            thumb.Focus();
        };
        thumb.DragDelta += (_, _) =>
        {
            if (gestureStart is null) return;
            Point pointer = PointToScreen(Mouse.GetPosition(this));
            var m = Geometry.Resolve(gestureStart.MonitorKey, dashboard.Monitors);
            double dx = (pointer.X - pointerStart.X) / m.Scale, dy = (pointer.Y - pointerStart.Y) / m.Scale;
            var r = gestureStart.RectDip;
            r = resize ? r with { Width = r.Width + dx, Height = r.Height + dy } : r with { X = r.X + dx, Y = r.Y + dy };
            widget = gestureStart with { RectDip = Geometry.Clamp(r, WidgetCatalog.Get(widget.Type), m) };
            ApplyPlacement();
        };
        thumb.DragCompleted += (_, e) =>
        {
            if (gestureStart is null) return;
            var original = gestureStart; gestureStart = null; activeThumb = null;
            if (e.Canceled) { widget = original; ApplyPlacement(); return; }
            dashboard.Action(() =>
            {
                try { dashboard.Editor.Update(widget); }
                catch { widget = original; ApplyPlacement(); throw; }
                dashboard.Center.Refresh();
            });
        };
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && activeThumb is not null)
        { activeThumb.CancelDrag(); e.Handled = true; return; }
        if (e.Key == Key.Escape && expanded) { expanded = false; RebuildBody(); ApplyPlacement(); e.Handled = true; return; }
        // Layout shortcuts only operate on explicit edit handles. Text controls keep their own keys.
        if (!dashboard.Editor.IsEditing || Keyboard.FocusedElement is not Thumb) return;
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        var r = widget.RectDip;
        var next = e.Key switch
        {
            Key.Left => r with { X = r.X - step }, Key.Right => r with { X = r.X + step },
            Key.Up => r with { Y = r.Y - step }, Key.Down => r with { Y = r.Y + step }, _ => r
        };
        if (next == r) return;
        e.Handled = true;
        dashboard.Action(() =>
        {
            var updated = widget with { RectDip = Geometry.Clamp(next, WidgetCatalog.Get(widget.Type), Monitor) };
            dashboard.Editor.Update(updated); widget = updated; ApplyPlacement(); dashboard.Center.Refresh();
        });
    }

    private void BuildContent(Grid body)
    {
        switch (widget.Type)
        {
            case "clock":
                var clock = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                live.FontSize = 36; clock.Children.Add(live); body.Children.Add(clock); break;
            case "calendar":
                BuildCalendar(body); break;
            case "notes":
                BuildNotes(body); break;
            case "system":
                var metrics = new StackPanel(); metrics.Children.Add(Label("SYSTEM", 13));
                live.FontSize = 17; metrics.Children.Add(live);
                graph.Children.Add(cpuLine); graph.Children.Add(memoryLine);
                graph.SizeChanged += (_, _) => DrawGraph(); metrics.Children.Add(graph);
                metrics.Children.Add(Label("Last 60 visible samples · CPU blue / RAM lavender", 11));
                body.Children.Add(Scrollable(metrics)); break;
            case "spotify":
                provider = new SpotifyView(dashboard); body.Children.Add(Scrollable(provider)); break;
            case "weather":
                provider = new WeatherView(dashboard); body.Children.Add(Scrollable(provider)); break;
            case "ai-island":
                var island = new StackPanel();
                var open = new Button { Content = expanded ? "✦  Close AI panel" : "✦  Open AI panel" };
                open.Click += (_, _) => { expanded = !expanded; RebuildBody(); ApplyPlacement(); };
                island.Children.Add(open);
                if (expanded)
                {
                    provider = new AiView(dashboard, widget.Id); island.Children.Add(provider);
                }
                body.Children.Add(Scrollable(island)); break;
        }
    }
    private void RebuildBody()
    {
        provider?.Dispose(); provider = null;
        var body = grid.Children.OfType<Grid>().Single(); body.Children.Clear(); BuildContent(body);
    }
    private void BuildCalendar(Grid body)
    {
        var calendar = new Calendar
        {
            DisplayDate = calendarMonth, SelectedDate = DateTime.Today,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)
        };
        calendar.DisplayDateChanged += (_, _) => calendarMonth = calendar.DisplayDate;
        var panel = new StackPanel(); panel.Children.Add(Label("CALENDAR · LOCAL", 13)); panel.Children.Add(calendar);
        panel.Children.Add(Label("No cloud calendar connected.", 12)); body.Children.Add(Scrollable(panel));
    }
    private void BuildNotes(Grid body)
    {
        var panel = new DockPanel();
        noteStatus = Label("Saved locally", 12); DockPanel.SetDock(noteStatus, Dock.Bottom); panel.Children.Add(noteStatus);
        var tools = new WrapPanel(); DockPanel.SetDock(tools, Dock.Bottom); panel.Children.Add(tools);
        var retry = new Button { Content = "Save now" }; retry.Click += (_, _) => FlushNote(); tools.Children.Add(retry);
        var export = new Button { Content = "Export" }; tools.Children.Add(export);
        note = new TextBox
        {
            AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = NotesStore.MaxCharacters,
            BorderThickness = new Thickness(0), FontSize = 15
        };
        System.Windows.Automation.AutomationProperties.SetName(note, "Editable note");
        try { note.Text = dashboard.Notes.Load(widget.Id); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        { note.IsReadOnly = true; noteStatus.Text = "Note unavailable. Original preserved."; }
        note.TextChanged += (_, _) => { noteDirty = true; noteStatus.Text = "Unsaved…"; noteSave.Stop(); noteSave.Start(); };
        export.Click += (_, _) => dashboard.Action(() =>
        {
            var picker = new SaveFileDialog { Filter = "Text file|*.txt", FileName = "WinCC-note.txt" };
            if (picker.ShowDialog(this) == true) File.WriteAllText(picker.FileName, note.Text);
        });
        panel.Children.Add(note); body.Children.Add(panel);
    }
    public bool FlushNote()
    {
        if (!noteDirty || note is null) return true;
        try
        {
            dashboard.Notes.Save(widget.Id, note.Text); noteDirty = false;
            if (noteStatus is not null) noteStatus.Text = "Saved locally";
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (noteStatus is not null) noteStatus.Text = "Save failed · use Save now or Export";
            dashboard.Notify("A note could not be saved. Keep WinCC open and retry or export the text.");
            Log.Write("note.save_failed", e.GetType().Name); return false;
        }
    }
    private void UpdateLive()
    {
        if (widget.Type == "clock")
        {
            string format = widget.Settings.GetValueOrDefault("hourFormat", "system") switch
            { "12" => "h:mm tt", "24" => "HH:mm", _ => "t" };
            live.Text = DateTime.Now.ToString(format, CultureInfo.CurrentCulture) + "\n" + DateTime.Today.ToString("ddd, d MMM", CultureInfo.CurrentCulture);
        }
        else if (widget.Type == "system")
        {
            var sample = sampler.Sample();
            string cpu = sample.CpuPercent is double value ? $"{value:F1}%" : "unavailable";
            string ram = sample.UsedMemory is ulong used && sample.TotalMemory is ulong total ?
                $"{used / 1073741824d:F1} / {total / 1073741824d:F1} GiB" : "unavailable";
            live.Text = $"CPU   {cpu}\nRAM   {ram}\nGPU   unavailable\nTemperature   unavailable";
            if (sample.CpuPercent is double cpuValue) cpuHistory.Add(cpuValue);
            if (sample.UsedMemory is ulong usedMemory && sample.TotalMemory is ulong totalMemory && totalMemory > 0)
                memoryHistory.Add(100d * usedMemory / totalMemory);
            if (cpuHistory.Count > 60) cpuHistory.RemoveAt(0);
            if (memoryHistory.Count > 60) memoryHistory.RemoveAt(0);
            DrawGraph();
        }
    }
    private void DrawGraph()
    {
        double width = Math.Max(1, graph.ActualWidth), height = graph.Height;
        cpuLine.Points = new PointCollection(cpuHistory.Select((value, i) =>
            new Point(i * width / 59, height * (1 - value / 100))));
        memoryLine.Points = new PointCollection(memoryHistory.Select((value, i) =>
            new Point(i * width / 59, height * (1 - value / 100))));
    }
    private static TextBlock Label(string text, double size = 16) => new()
    { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) };
    private static ScrollViewer Scrollable(UIElement element) => new()
    { Content = element, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private static StackPanel Message(string title, string state, string explanation)
    {
        var panel = new StackPanel(); panel.Children.Add(Label(title, 20));
        panel.Children.Add(new TextBlock { Text = state, Foreground = Brushes.LightSteelBlue, Margin = new Thickness(0, 4, 0, 8) });
        panel.Children.Add(Label(explanation, 13)); return panel;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        tick.Stop(); noteSave.Stop(); provider?.Dispose(); FlushNote();
        activeThumb?.CancelDrag(); Close();
    }
}
