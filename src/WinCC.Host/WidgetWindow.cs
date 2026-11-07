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

namespace WinCC.Host;

internal sealed class WidgetWindow : Window, IDisposable
{
    private readonly Dashboard dashboard;
    private Widget widget;
    private readonly LiquidGlass card;
    private readonly WidgetViewport viewport;
    private readonly Grid grid = new();
    private readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock live = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock dateLine = new() { FontSize = 13, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(2, 8, 0, 0) };
    private bool disposed, placing;
    private (int Width, int Height) shapedSize;
    private Thumb? activeThumb;
    private Widget? gestureStart;
    private Point pointerStart;
    public nint Handle => new WindowInteropHelper(this).Handle;
    public int ZIndex => widget.ZIndex;

    public WidgetWindow(Dashboard dashboard, Widget widget)
    {
        this.dashboard = dashboard; this.widget = widget;
        FontSize = dashboard.Settings.WidgetFontSize;
        Title = "WinCC · " + WidgetCatalog.Get(widget.Type).Name;
        Width = widget.RectDip.Width; Height = widget.RectDip.Height;
        ShowActivated = false; ShowInTaskbar = false;
        WindowStyle = dashboard.Windowed ? WindowStyle.SingleBorderWindow : WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        Topmost = false;
        var composition = WidgetCatalog.Get(widget.Type);
        viewport = new WidgetViewport(composition.Width, composition.Height)
        {
            Child = new Border { Padding = new Thickness(18), Child = grid }
        };
        card = new LiquidGlass { Material = widget.Type, Child = viewport, ClearVariant = dashboard.Settings.GlassVariant != "Regular", ReduceMotion = dashboard.Settings.ReduceGlassMotion, ReduceTransparency = dashboard.Settings.ReduceGlassTransparency };
        bool darkText = false;
        card.BackgroundLuminance += luminance =>
        {
            bool next = !card.ClearVariant && !SystemParameters.HighContrast && (darkText ? luminance > .48 : luminance > .65);
            if (next == darkText) return;
            darkText = next;
            live.Foreground = dateLine.Foreground = darkText ? new SolidColorBrush(Color.FromRgb(20, 25, 32)) : Brushes.White;
            foreach (var text in new[] { live, dateLine })
                if (text.Effect is System.Windows.Media.Effects.DropShadowEffect shadow) shadow.Color = darkText ? Colors.White : Colors.Black;
        };
        Content = card;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (dashboard.Editor.IsEditing) BuildHandles();
        var body = new Grid { ClipToBounds = true };
        Grid.SetRow(body, 1); grid.Children.Add(body);
        BuildContent(body);
        SourceInitialized += (_, _) =>
        {
            Desktop.ConfigureWidgetWindow(Handle);
            bool ready = BackdropCapture.Exclude(Handle);
            card.EnableCapture(ready);
            if (!ready) MessageBox.Show("Live glass capture exclusion is unavailable on this Windows setup. Refraction is disabled to avoid feedback.", "WinCC");
            HwndSource.FromHwnd(Handle)?.AddHook(Hook);
        };
        Loaded += (_, _) => { ApplyPlacement(); UpdateLive(); tick.Start(); };
        tick.Tick += (_, _) => { if (IsVisible && WindowState != WindowState.Minimized) UpdateLive(); };
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
            var size = ((int)Math.Round(r.Width * m.Scale), (int)Math.Round(r.Height * m.Scale));
            if (!dashboard.Windowed && shapedSize != size)
            {
                BackdropCapture.ShapeClock(Handle, size.Item1, size.Item2); shapedSize = size;
            }
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
            Height = 32, Cursor = Cursors.SizeNWSE, Focusable = true, Tag = "resize",
            HorizontalAlignment = HorizontalAlignment.Stretch, Template = ThumbTemplate("Resize  ↘")
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
        // Layout shortcuts only operate on explicit edit handles. Text controls keep their own keys.
        if (!dashboard.Editor.IsEditing || Keyboard.FocusedElement is not Thumb) return;
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        var r = widget.RectDip;
        bool resizing = Keyboard.FocusedElement is Thumb { Tag: "resize" };
        var next = resizing ? e.Key switch
        {
            Key.Left => r with { Width = r.Width - step }, Key.Right => r with { Width = r.Width + step },
            Key.Up => r with { Height = r.Height - step }, Key.Down => r with { Height = r.Height + step }, _ => r
        } : e.Key switch
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
        var clock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        live.FontSize = 44; live.FontWeight = FontWeights.SemiBold; live.TextAlignment = TextAlignment.Center;
        dateLine.TextAlignment = TextAlignment.Center;
        live.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 5, ShadowDepth = 1, Opacity = .65 };
        dateLine.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = .7 };
        clock.Children.Add(live); clock.Children.Add(dateLine); body.Children.Add(clock);
        Typography.Apply(body, dashboard.Settings.WidgetFontSize / 14);
    }
    private void UpdateLive()
    {
        string format = widget.Settings.GetValueOrDefault("hourFormat", "system") switch
        { "12" => "h:mm tt", "24" => "HH:mm", _ => "t" };
        live.Text = DateTime.Now.ToString(format, CultureInfo.CurrentCulture);
        dateLine.Text = DateTime.Today.ToString("dddd, MMMM d", CultureInfo.CurrentCulture);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        tick.Stop(); card.Dispose(); activeThumb?.CancelDrag(); Close();
    }
}
