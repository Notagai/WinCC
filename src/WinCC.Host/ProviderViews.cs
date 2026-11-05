using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinCC.DesktopInterop;
using WinCC.Integrations;

namespace WinCC.Host;

internal abstract class ProviderView : StackPanel, IDisposable
{
    protected readonly CancellationTokenSource Lifetime = new();
    protected readonly TextBlock State = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
    protected bool Busy, Disposed;
    protected bool VisibleForPolling => IsVisible && Window.GetWindow(this)?.WindowState != WindowState.Minimized;
    protected readonly DispatcherTimer Timer = new();
    protected ProviderView(string title)
    {
        Children.Add(new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeights.SemiBold });
        Children.Add(State);
    }
    protected void Failure(Exception error)
    {
        State.Text = error switch
        {
            ProviderException p => p.Message,
            UnauthorizedAccessException => "Permission denied. Provider access is unavailable.",
            OperationCanceledException => "Request canceled or timed out. Retry when ready.",
            HttpRequestException => "Offline or provider unreachable. Check your connection.",
            _ => "Provider unavailable. Try again later."
        };
        Log.Write("provider.failed", error.GetType().Name);
    }
    protected static bool Expected(Exception e) => e is IOException or HttpRequestException or JsonException or
        OperationCanceledException or UnauthorizedAccessException or COMException or InvalidOperationException;
    public virtual void Dispose()
    {
        if (Disposed) return; Disposed = true; Timer.Stop(); Lifetime.Cancel(); Lifetime.Dispose();
    }
}

internal sealed class WeatherView : ProviderView
{
    private readonly Dashboard dashboard;
    public WeatherView(Dashboard dashboard) : base("Weather")
    {
        this.dashboard = dashboard;
        var refresh = new Button { Content = "Refresh weather", IsEnabled = dashboard.Settings.WeatherEnabled };
        refresh.Click += async (_, _) => await Refresh(); Children.Add(refresh);
        Children.Add(new TextBlock { Text = "Weather data: Open-Meteo.com · CC BY 4.0", FontSize = 11, TextWrapping = TextWrapping.Wrap });
        if (!dashboard.Settings.WeatherEnabled) State.Text = "Not connected. Enable weather and choose coordinates in the Command Center.";
        Timer.Interval = TimeSpan.FromMinutes(15);
        Timer.Tick += async (_, _) => { if (VisibleForPolling) await Refresh(); };
        Loaded += async (_, _) => { if (dashboard.Settings.WeatherEnabled) { await Refresh(); if (!Disposed) Timer.Start(); } };
    }
    private async Task Refresh()
    {
        if (Busy || Disposed || !dashboard.Settings.WeatherEnabled) return;
        Busy = true; State.Text = "Loading weather…";
        try
        {
            var s = dashboard.Settings;
            var reading = await new WeatherClient(dashboard.Http).Fetch(s.Latitude, s.Longitude, s.Fahrenheit, Lifetime.Token);
            if (Disposed) return;
            State.Text = $"{reading.Temperature:F1}{reading.Unit} · {reading.Conditions}\nObserved {reading.ObservedAt}\n" +
                string.Join("\n", reading.Forecast.Select(d => $"{d.Date}: {d.Minimum:F0}–{d.Maximum:F0}{reading.Unit}"));
        }
        catch (Exception e) when (Expected(e)) { if (!Disposed) Failure(e); }
        finally { Busy = false; }
    }
}

internal sealed class SpotifyView : ProviderView
{
    private readonly Dashboard dashboard;
    private readonly MediaSession media = new();
    private readonly Button previous = new() { Content = "Previous", IsEnabled = false };
    private readonly Button toggle = new() { Content = "Play / pause", IsEnabled = false };
    private readonly Button next = new() { Content = "Next", IsEnabled = false };
    private readonly Button shuffle = new() { Content = "Shuffle", IsEnabled = false };
    private readonly Button repeat = new() { Content = "Repeat", IsEnabled = false };
    private readonly Image artwork = new() { Height = 56, Width = 56, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
    private byte[]? shownArtwork;
    private readonly Slider seek = new() { Minimum = 0, Maximum = 1, IsEnabled = false, Margin = new Thickness(4) };
    private bool seeking;
    public SpotifyView(Dashboard dashboard) : base("Spotify")
    {
        this.dashboard = dashboard;
        var buttons = new WrapPanel(); buttons.Children.Add(previous); buttons.Children.Add(toggle); buttons.Children.Add(next);
        Children.Insert(1, artwork);
        Children.Add(buttons); Children.Add(seek);
        var modes = new WrapPanel(); modes.Children.Add(shuffle); modes.Children.Add(repeat); Children.Add(modes);
        System.Windows.Automation.AutomationProperties.SetName(seek, "Playback position");
        previous.Click += async (_, _) => await Control(media.Previous);
        toggle.Click += async (_, _) => await Control(media.Toggle);
        next.Click += async (_, _) => await Control(media.Next);
        shuffle.Click += async (_, _) => await Control(media.ToggleShuffle);
        repeat.Click += async (_, _) => await Control(media.CycleRepeat);
        seek.PreviewMouseLeftButtonDown += (_, _) => seeking = true;
        seek.PreviewMouseLeftButtonUp += async (_, _) => { seeking = false; await Control(() => media.Seek(seek.Value)); };
        seek.LostMouseCapture += (_, _) => seeking = false;
        seek.KeyUp += async (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End) await Control(() => media.Seek(seek.Value)); };
        State.Text = dashboard.Settings.SpotifyEnabled ? "Connecting local media session…" : "Disconnected. Enable local Spotify access in the Command Center.";
        Timer.Interval = TimeSpan.FromSeconds(2);
        Timer.Tick += async (_, _) => { if (VisibleForPolling && !seeking) await Refresh(); };
        Loaded += async (_, _) => { if (dashboard.Settings.SpotifyEnabled) { await Refresh(); if (!Disposed) Timer.Start(); } };
    }
    private async Task Control(Func<Task<bool>> action)
    {
        if (Busy || Disposed) return;
        try
        {
            bool accepted = await action().WaitAsync(Lifetime.Token);
            if (Disposed) return;
            if (!accepted) State.Text = "Spotify did not accept that control.";
            else await Refresh();
        }
        catch (Exception e) when (Expected(e)) { if (!Disposed) Failure(e); }
    }
    private async Task Refresh()
    {
        if (Busy || Disposed || !dashboard.Settings.SpotifyEnabled) return; Busy = true;
        try
        {
            var reading = await media.Read().WaitAsync(Lifetime.Token);
            if (Disposed) return;
            if (reading is null)
            {
                State.Text = "No active Spotify session. Open Spotify and start playback.";
                previous.IsEnabled = toggle.IsEnabled = next.IsEnabled = seek.IsEnabled = shuffle.IsEnabled = repeat.IsEnabled = false;
                artwork.Source = null; artwork.Visibility = Visibility.Collapsed; shownArtwork = null;
                return;
            }
            State.Text = $"{reading.Title}\n{reading.Artist}\n{TimeSpan.FromSeconds(reading.Position):m\\:ss} / {TimeSpan.FromSeconds(reading.Duration):m\\:ss}";
            previous.IsEnabled = reading.CanPrevious; toggle.IsEnabled = reading.CanToggle; next.IsEnabled = reading.CanNext;
            toggle.Content = reading.Playing ? "Pause" : "Play";
            seek.IsEnabled = reading.CanSeek && reading.Duration > 0; seek.Maximum = Math.Max(1, reading.Duration); seek.Value = Math.Min(reading.Position, seek.Maximum);
            shuffle.IsEnabled = reading.CanShuffle; repeat.IsEnabled = reading.CanRepeat;
            shuffle.Content = reading.Shuffle ? "Shuffle on" : "Shuffle off"; repeat.Content = "Repeat: " + reading.Repeat;
            if (!ReferenceEquals(reading.Artwork, shownArtwork))
            {
                shownArtwork = reading.Artwork; artwork.Source = null; artwork.Visibility = Visibility.Collapsed;
                if (shownArtwork is not null)
                {
                    try
                    {
                        using var stream = new MemoryStream(shownArtwork);
                        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 112; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                        artwork.Source = bitmap; artwork.Visibility = Visibility.Visible;
                    }
                    catch (Exception e) when (e is NotSupportedException or IOException or ArgumentException) { artwork.Source = null; }
                }
            }
        }
        catch (Exception e) when (Expected(e))
        { if (!Disposed) { Failure(e); previous.IsEnabled = toggle.IsEnabled = next.IsEnabled = seek.IsEnabled = shuffle.IsEnabled = repeat.IsEnabled = false; } }
        finally { Busy = false; }
    }
}

internal sealed class AiView : ProviderView
{
    private readonly Dashboard dashboard;
    private readonly string id;
    private readonly TextBox prompt = new()
    {
        AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 4000, MinHeight = 54, MaxHeight = 90,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };
    private readonly TextBlock history = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button send = new() { Content = "Send" };
    private readonly Button clear = new() { Content = "Clear" };
    private CancellationTokenSource? request;
    public AiView(Dashboard dashboard, string id) : base("AI chat")
    {
        this.dashboard = dashboard; this.id = id;
        dashboard.Chats.TryAdd(id, []);
        prompt.Text = dashboard.ChatDrafts.GetValueOrDefault(id, "");
        prompt.TextChanged += (_, _) => dashboard.ChatDrafts[id] = prompt.Text;
        Children.Add(new ScrollViewer { Content = history, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Children.Add(prompt);
        var buttons = new WrapPanel(); buttons.Children.Add(send); buttons.Children.Add(clear);
        var cancel = new Button { Content = "Stop" }; buttons.Children.Add(cancel); Children.Add(buttons);
        cancel.Click += (_, _) => request?.Cancel();
        clear.Click += (_, _) => { dashboard.Chats[id].Clear(); prompt.Clear(); RefreshHistory(); };
        send.Click += async (_, _) => await Send();
        prompt.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            { e.Handled = true; await Send(); }
        };
        bool connected = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WINCC_AI_API_KEY"));
        State.Text = connected ? "Messages are sent to OpenAI when you choose Send. Chat is kept in memory only." :
            "Disconnected. Set WINCC_AI_API_KEY before starting WinCC; no key is saved by this app.";
        send.IsEnabled = connected;
        System.Windows.Automation.AutomationProperties.SetName(prompt, "AI chat message");
        RefreshHistory();
    }
    private void RefreshHistory() => history.Text = string.Join("\n\n", dashboard.Chats[id].Select(m => (m.Role == "user" ? "You: " : "AI: ") + m.Text));
    private async Task Send()
    {
        if (Busy || Disposed || string.IsNullOrWhiteSpace(prompt.Text) || !send.IsEnabled) return;
        Busy = true; send.IsEnabled = clear.IsEnabled = false;
        string text = prompt.Text.Trim(); State.Text = "Waiting for AI…";
        request = CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token);
        var messages = dashboard.Chats[id].TakeLast(18).Append(new ChatMessage("user", text)).ToArray();
        try
        {
            var answer = await new AiClient(dashboard.Http).Send(Environment.GetEnvironmentVariable("WINCC_AI_API_KEY") ?? "",
                dashboard.Settings.AiModel, messages, request.Token);
            if (Disposed) return;
            dashboard.Chats[id] = messages.Append(new ChatMessage("assistant", answer)).ToList();
            prompt.Clear(); RefreshHistory(); State.Text = "Ready · conversation stays in memory.";
        }
        catch (Exception e) when (Expected(e)) { if (!Disposed) Failure(e); }
        finally
        {
            request.Dispose(); request = null; Busy = false;
            if (!Disposed) send.IsEnabled = clear.IsEnabled = true;
        }
    }
    public override void Dispose() { request?.Cancel(); base.Dispose(); }
}
