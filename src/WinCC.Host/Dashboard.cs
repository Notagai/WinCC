using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using WinCC.DesktopInterop;
using WinCC.Layout;

namespace WinCC.Host;

public sealed class UserSettings
{
    public double InterfaceFontSize { get; set; } = 18;
    public double WidgetFontSize { get; set; } = 18;
    public string FontFamily { get; set; } = "Lato";
    public bool TimeShapedGlass { get; set; }
    public int GlassFps { get; set; } = 60;
    public string GlassVariant { get; set; } = "Clear";
    public bool ReduceGlassTransparency { get; set; }
    public bool ReduceGlassMotion { get; set; }
    public string Theme { get; set; } = "Winter Glass";
    public List<string> Launchers { get; set; } = [];
    public bool WeatherEnabled { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool Fahrenheit { get; set; }
    public bool SpotifyEnabled { get; set; }
    public string AiModel { get; set; } = "gpt-5-mini";
}

internal sealed class Dashboard : IDisposable
{
    private readonly LayoutStore store = new(Path.Combine(Paths.Data, "layout.json"));
    private readonly List<WidgetWindow> windows = [];
    private readonly DispatcherTimer lifecycle = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly string settingsFile = Path.Combine(Paths.Data, "settings.json");
    private bool disposing;
    private bool stacking, stackPending;
    private bool settingsWritable = true;
    public bool Windowed { get; }
    public bool ReadOnly { get; }
    public EditorSession Editor { get; }
    public UserSettings Settings { get; private set; } = new();
    public IReadOnlyList<MonitorArea> Monitors { get; private set; } = [];
    public CommandCenter Center { get; }
    public string Status { get; private set; } = "Normal Mode · Your layout is locked.";

    public Dashboard(bool windowed)
    {
        Windowed = windowed;
        var loaded = store.Load(); ReadOnly = loaded.ReadOnly;
        Editor = new(loaded.Document);
        if (loaded.Warning is not null) Status = loaded.Warning;
        try
        {
            if (File.Exists(settingsFile))
            {
                if (new FileInfo(settingsFile).Length > 65536) throw new InvalidDataException();
                Settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllBytes(settingsFile)) ?? throw new InvalidDataException();
                if (Settings.GlassFps is < 0 or > 500 || !double.IsFinite(Settings.InterfaceFontSize) || Settings.InterfaceFontSize is < 12 or > 32 ||
                    !double.IsFinite(Settings.WidgetFontSize) || Settings.WidgetFontSize is < 12 or > 32 || Settings.Launchers is null || Settings.Launchers.Count > 100 ||
                    Settings.Theme is not ("Winter Glass" or "Midnight") ||
                    !double.IsFinite(Settings.Latitude) || Settings.Latitude is < -90 or > 90 ||
                    !double.IsFinite(Settings.Longitude) || Settings.Longitude is < -180 or > 180 ||
                    string.IsNullOrWhiteSpace(Settings.AiModel) || Settings.AiModel.Length > 100) throw new InvalidDataException();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Settings = new(); settingsWritable = false;
            Status = "Settings could not be read; original preserved. Settings changes are disabled.";
        }
        ApplyFont();
        RefreshMonitors();
        Center = new(this);
        lifecycle.Tick += (_, _) => CheckLifecycle();
    }
    public void Start()
    {
        Center.Show(); Render(); lifecycle.Start();
    }
    public void Action(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or
                                     InvalidOperationException or System.ComponentModel.Win32Exception or JsonException)
        {
            Notify(e is InvalidOperationException ? e.Message : "Operation failed. Your saved data is preserved. " + e.Message);
            MessageBox.Show(Center, e.Message, "WinCC");
            Log.Write("operation.failed", e.GetType().Name);
        }
    }
    public void Notify(string text) { Status = text; Center.RefreshStatus(); }
    public void Enter()
    {
        if (ReadOnly) { Notify("Layout saving is disabled to protect the original file. See recovery instructions."); return; }
        Editor.Enter(); Notify("Edit Mode · Changes are drafts until you choose Done."); Render();
    }
    public void Done()
    {
        // Require saved geometry to fit a present target; absent monitors retain their saved association.
        foreach (var w in Editor.Current.Active.Widgets.Where(w => w.Type == "clock"))
        {
            var m = Monitors.FirstOrDefault(m => m.Key == w.MonitorKey || w.MonitorKey == "primary" && m.Primary);
            if (m is not null && (w.RectDip.X + w.RectDip.Width > m.WidthDip + 0.1 ||
                                  w.RectDip.Y + w.RectDip.Height > m.HeightDip + 0.1))
                throw new InvalidOperationException("A widget exceeds its display work area. Move or resize it before saving.");
        }
        Editor.Done(store); Log.Write("layout.saved", "done");
        Notify("Normal Mode · Layout saved."); Render();
    }
    public void Cancel()
    {
        if (Editor.IsDirty && MessageBox.Show(Center, "Discard all unsaved clock changes?",
                "Cancel editing", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Editor.Cancel(); Notify("Normal Mode · Draft discarded."); Render();
    }
    public void Render()
    {
        foreach (var window in windows) window.Dispose();
        windows.Clear();
        foreach (var widget in Editor.Current.Active.Widgets.Where(w => w.Type == "clock").OrderByDescending(w => w.ZIndex))
        {
            var window = new WidgetWindow(this, widget);
            windows.Add(window); window.Show(); window.ApplyPlacement();
        }
        Center.Refresh();
    }
    public void Add(string type, string monitorKey)
    {
        if (type != "clock") throw new InvalidOperationException("Only the clock is available.");
        var spec = WidgetCatalog.Get(type);
        var monitor = Geometry.Resolve(monitorKey, Monitors);
        if (monitor.WidthDip < spec.MinWidth || monitor.HeightDip < spec.MinHeight)
            throw new InvalidOperationException("This display is too small for that widget at its current scale.");
        var rect = Geometry.Clamp(new((monitor.WidthDip - spec.Width) / 2,
            (monitor.HeightDip - spec.Height) / 2, spec.Width, spec.Height), spec, monitor);
        Editor.Change(d => d.Active.Widgets.Add(new(Guid.NewGuid().ToString("N"), type, monitor.Key, rect,
            d.Active.Widgets.Count, [])));
        Render();
    }
    public void Remove(string id)
    {
        Editor.Change(d => d.Active.Widgets.RemoveAll(w => w.Id == id)); Render();
    }
    public void Reorder(string id, int direction)
    {
        Editor.Change(d =>
        {
            var ordered = d.Active.Widgets.OrderBy(w => w.ZIndex).ToList();
            int from = ordered.FindIndex(w => w.Id == id);
            int to = Math.Clamp(from + direction, 0, ordered.Count - 1);
            (ordered[from], ordered[to]) = (ordered[to], ordered[from]);
            d.Active.Widgets.Clear();
            d.Active.Widgets.AddRange(ordered.Select((w, i) => w with { ZIndex = i }));
        }); Render();
    }
    public void MoveTo(string id, string monitorKey)
    {
        var w = Editor.Current.Active.Widgets.Single(w => w.Id == id);
        var m = Geometry.Resolve(monitorKey, Monitors); var spec = WidgetCatalog.Get(w.Type);
        if (m.WidthDip < spec.MinWidth || m.HeightDip < spec.MinHeight)
            throw new InvalidOperationException("The target display is too small for this widget.");
        Editor.Update(w with { MonitorKey = m.Key, RectDip = Geometry.Clamp(w.RectDip, spec, m) }); Render();
    }
    public void Preset()
    {
        var m = Geometry.Resolve("primary", Monitors);
        // This is an optional, undoable draft operation. Never apply it on startup.
        string[] types = ["clock"];
        Editor.Change(d =>
        {
            d.Active.Widgets.Clear();
            double leftY = 24, rightY = 24;
            foreach (var type in types)
            {
                var s = WidgetCatalog.Get(type);
                if (m.WidthDip < s.MinWidth || m.HeightDip < s.MinHeight) continue;
                bool left = type is "clock" or "calendar" or "spotify";
                double x = type == "ai-island" ? (m.WidthDip - s.Width) / 2 : left ? 24 : m.WidthDip - s.Width - 24;
                double y = type == "ai-island" ? 20 : left ? leftY : rightY;
                var r = Geometry.Clamp(new(x, y, s.Width, s.Height), s, m);
                d.Active.Widgets.Add(new(Guid.NewGuid().ToString("N"), type, m.Key, r, d.Active.Widgets.Count, []));
                if (left) leftY += s.Height + 14; else rightY += s.Height + 14;
            }
        }); Render();
    }
    private void ApplyFont() => Application.Current.Resources["DesktopFont"] =
        new System.Windows.Media.FontFamily("/WinCC;component/Assets/Fonts/#" + (Settings.FontFamily == "VT323" ? "VT323" : "Lato"));
    public PresetStore Presets { get; } = new(Path.Combine(Paths.Data, "presets"));
    public void SavePreset(int slot)
    {
        if (Presets.Exists(slot) && MessageBox.Show(Center, "Replace this preset?", "WinCC", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Presets.Save(slot, Editor.Current); Center.Refresh();
    }
    public void LoadPreset(int slot)
    {
        if (Editor.IsDirty && MessageBox.Show(Center, "Replace the unsaved layout draft?", "WinCC", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var preset = Presets.Load(slot);
        Editor.Enter(); Editor.Change(d => { d.Active.Widgets.Clear(); d.Active.Widgets.AddRange(preset.Active.Widgets.Where(w => w.Type == "clock")); });
        Editor.Done(store); Render();
    }
    public void ChangeClockShape(bool shaped)
    {
        if (!settingsWritable) throw new InvalidOperationException("Settings are protected.");
        bool old = Settings.TimeShapedGlass; Settings.TimeShapedGlass = shaped;
        try { SaveSettings(); } catch { Settings.TimeShapedGlass = old; throw; }
        Render();
    }
    public void ChangeFrameRate(int fps)
    {
        if (!settingsWritable) throw new InvalidOperationException("Settings are protected.");
        if (fps is < 0 or > 500) throw new InvalidOperationException("Frame rate must be 0 (uncapped) or 1–500.");
        int previous = Settings.GlassFps; Settings.GlassFps = fps;
        try { SaveSettings(); } catch { Settings.GlassFps = previous; throw; }
        foreach (var window in windows) window.SetFrameRate(fps);
        Center.Refresh();
    }
    public void ChangeGlass(string variant, bool reduceMotion, bool? reduceTransparency = null)
    {
        if (!settingsWritable) throw new InvalidOperationException("Settings are protected.");
        if (variant is not ("Clear" or "Regular")) throw new InvalidOperationException("Unknown glass variant.");
        var oldVariant = Settings.GlassVariant; bool oldMotion = Settings.ReduceGlassMotion, oldTransparency = Settings.ReduceGlassTransparency;
        Settings.GlassVariant = variant; Settings.ReduceGlassMotion = reduceMotion;
        Settings.ReduceGlassTransparency = reduceTransparency ?? oldTransparency;
        try { SaveSettings(); } catch { Settings.GlassVariant = oldVariant; Settings.ReduceGlassMotion = oldMotion; Settings.ReduceGlassTransparency = oldTransparency; throw; }
        Render();
    }
    public void ChangeFontFamily(string family)
    {
        if (!settingsWritable) throw new InvalidOperationException("Settings are protected.");
        if (family is not ("Lato" or "VT323")) throw new InvalidOperationException("Unknown font.");
        string previous = Settings.FontFamily; Settings.FontFamily = family;
        try { SaveSettings(); } catch { Settings.FontFamily = previous; throw; }
        ApplyFont(); Render();
    }
    public void ChangeFontSizes(double interfaceSize, double widgetSize)
    {
        if (!settingsWritable) throw new InvalidOperationException("Settings are protected.");
        if (!double.IsFinite(interfaceSize) || interfaceSize is < 12 or > 32 ||
            !double.IsFinite(widgetSize) || widgetSize is < 12 or > 32)
            throw new InvalidOperationException("Font sizes must be between 12 and 32.");
        var previousInterface = Settings.InterfaceFontSize;
        var previousWidget = Settings.WidgetFontSize;
        Settings.InterfaceFontSize = interfaceSize; Settings.WidgetFontSize = widgetSize;
        try { SaveSettings(); }
        catch { Settings.InterfaceFontSize = previousInterface; Settings.WidgetFontSize = previousWidget; throw; }
        Render();
    }
    public void ChangeTheme(string theme)
    {
        if (!settingsWritable) throw new InvalidOperationException("Settings are protected; archive the original settings file to recover.");
        string previous = Settings.Theme; Settings.Theme = theme;
        try { SaveSettings(); } catch { Settings.Theme = previous; throw; }
        Render();
    }
    private void SaveSettings() => AtomicFile.Write(settingsFile, JsonSerializer.SerializeToUtf8Bytes(Settings), backup: true);
    public bool RequestExit()
    {
        if (Editor.IsDirty && MessageBox.Show(Center, "Exit and discard the unsaved layout draft?",
                "Unsaved layout", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
        return true;
    }
    private void RefreshMonitors()
    {
        Monitors = Desktop.Displays().Select(d => new MonitorArea(d.Key, d.Left, d.Top,
            d.Width / d.Scale, d.Height / d.Scale, d.Scale, d.Primary)).ToArray();
        if (Monitors.Count == 0) throw new InvalidOperationException("No usable displays were found.");
    }
    public void DisplayChanged() => Action(() =>
    {
        RefreshMonitors();
        foreach (var window in windows) window.ApplyPlacement();
        Center.Refresh(); Log.Write("display.topology.changed", "reprojected_without_saving");
    });
    public nint AnchorFor(WidgetWindow window)
    {
        var ordered = windows.Where(w => w.Handle != 0).OrderByDescending(w => w.ZIndex).ToList();
        int index = ordered.IndexOf(window);
        return index <= 0 ? new nint(1) : ordered[index - 1].Handle;
    }
    public void QueueStack()
    {
        if (Windowed || disposing || stacking || stackPending) return;
        stackPending = true;
        Center.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            stackPending = false; if (disposing) return;
            stacking = true;
            try { Action(() => { foreach (var window in windows.OrderByDescending(w => w.ZIndex)) window.ApplyPlacement(); }); }
            finally { stacking = false; }
        }));
    }
    private void CheckLifecycle()
    {
        foreach (var window in windows)
        {
            bool hidden = !Windowed && Desktop.IsForegroundFullscreen(window.Handle);
            if (hidden && window.IsVisible) window.Hide();
            else if (!hidden && !window.IsVisible) { window.Show(); window.ApplyPlacement(); }
        }
    }
    public void Dispose()
    {
        if (disposing) return; disposing = true;
        Center.AllowShutdown = true;
        Center.DisposeTray();
        lifecycle.Stop();
        foreach (var window in windows) window.Dispose();
        windows.Clear();
    }
}
