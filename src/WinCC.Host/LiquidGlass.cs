using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinCC.DesktopInterop;
using WinCC.Layout;

namespace WinCC.Host;

/// <summary>Refracts a live local background sample. The child (clock text) is never distorted.</summary>
internal sealed class LiquidGlass : Decorator, IDisposable
{
    public event Action<double>? BackgroundLuminance;
    public bool ClearVariant { get; init; } = true;
    public bool ReduceTransparency { get; init; }
    public bool ReduceMotion { get; init; }
    private double interaction, targetInteraction, lightX = -.65, lightY = -.75, targetX = -.65, targetY = -.75;
    private readonly System.Diagnostics.Stopwatch age = System.Diagnostics.Stopwatch.StartNew();
    public string Material { get; set; } = "clock";
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background);
    private int frameRate = 60;
    private double frameStarted, previousFrame;
    private bool active;
    public int FrameRate
    {
        get => frameRate;
        set => frameRate = value is >= 0 and <= 500 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
    private BitmapSource? frame;
    private byte[]? capturePixels, opticalPixels;
    private bool busy, disposed, captureReady;
    private int generation;
    public LiquidGlass()
    {
        Loaded += (_, _) => { active = true; if (!disposed) Refresh(); };
        Unloaded += (_, _) => { active = false; timer.Stop(); generation++; frame = null; };
        SizeChanged += (_, _) => { generation++; frame = null; InvalidateVisual(); };
        timer.Tick += (_, _) => { timer.Stop(); Refresh(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) timer.Stop(); else if (active && !busy) Refresh(); };
        MouseMove += (_, e) =>
        {
            var point = e.GetPosition(this);
            targetX = (point.X / Math.Max(1, ActualWidth) - .5) * 1.5;
            targetY = (point.Y / Math.Max(1, ActualHeight) - .5) * 1.5;
            targetInteraction = 1;
        };
        MouseLeave += (_, _) => { targetInteraction = 0; targetX = -.65; targetY = -.75; };
    }
    public void EnableCapture(bool ready) => captureReady = ready;
    private void Schedule(TimeSpan delay)
    {
        if (disposed || !active || !captureReady || !IsVisible) return;
        timer.Interval = delay; timer.Start();
    }
    private async void Refresh()
    {
        timer.Stop();
        var window = Window.GetWindow(this);
        if (disposed || !active || busy || !captureReady || !IsVisible) return;
        if (window?.WindowState == WindowState.Minimized || ActualWidth < 2 || ActualHeight < 2)
        { Schedule(TimeSpan.FromMilliseconds(200)); return; }
        frameStarted = age.Elapsed.TotalSeconds;
        double elapsed = Math.Clamp(frameStarted - previousFrame, 0, .25); previousFrame = frameStarted;
        double lightingBlend = 1 - Math.Exp(-elapsed * 8);
        double energyBlend = 1 - Math.Exp(-elapsed * 10);
        bool reduced = ReduceMotion || !SystemParameters.ClientAreaAnimation;
        interaction += ((reduced ? 0 : targetInteraction) - interaction) * energyBlend;
        lightX += ((reduced ? -.65 : targetX) - lightX) * lightingBlend;
        lightY += ((reduced ? -.75 : targetY) - lightY) * lightingBlend;
        double emerge = reduced ? 1 : 1 - Math.Exp(-age.Elapsed.TotalSeconds * 5);
        var appearance = new GlassAppearance(ClearVariant, Math.Clamp(ActualWidth / 280, .1, 10), interaction, lightX, lightY, SystemParameters.HighContrast, ReduceTransparency, Organic: false, Phase: 0, CompositeToBackdrop: true);
        int version = generation;
        var origin = PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(this);
        double physicalWidth = ActualWidth * dpi.DpiScaleX, physicalHeight = ActualHeight * dpi.DpiScaleY;
        double ratio = Math.Min(1, 640 / Math.Max(physicalWidth, physicalHeight));
        int width = Math.Max(2, (int)(physicalWidth * ratio)), height = Math.Max(2, (int)(physicalHeight * ratio));
        int left = (int)Math.Floor(origin.X - physicalWidth * .125), top = (int)Math.Floor(origin.Y - physicalHeight * .125);
        busy = true;
        try
        {
            var image = await Task.Run(() =>
            {
                var captured = BackdropCapture.Read(left, top, (int)Math.Ceiling(physicalWidth * 1.25),
                    (int)Math.Ceiling(physicalHeight * 1.25), Math.Max(2, (int)(width * 1.25)), Math.Max(2, (int)(height * 1.25)), capturePixels);
                capturePixels = captured.Pixels;
                if (opticalPixels is null || opticalPixels.Length != width * height * 4) opticalPixels = new byte[width * height * 4];
                var pixels = GlassOptics.Render(captured.Pixels, captured.Width, captured.Height, width, height, emerge, appearance, opticalPixels);
                var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
                bitmap.Freeze();
                double luminance = 0; int samples = 0;
                for (int y = captured.Height / 4; y < captured.Height * 3 / 4; y += 8)
                    for (int x = captured.Width / 4; x < captured.Width * 3 / 4; x += 8)
                    {
                        int i = (y * captured.Width + x) * 4;
                        luminance += (.2126 * captured.Pixels[i + 2] + .7152 * captured.Pixels[i + 1] + .0722 * captured.Pixels[i]) / 255;
                        samples++;
                    }
                return (Bitmap: bitmap, Luminance: luminance / Math.Max(1, samples));
            });
            if (disposed || version != generation || !IsVisible) return;
            frame = image.Bitmap; BackgroundLuminance?.Invoke(image.Luminance); InvalidateVisual();
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            frame = null; InvalidateVisual();
            captureReady = false; timer.Stop(); // Do not display stale samples or silently pretend capture succeeded.
            Log.Write("glass.capture_failed", error.GetType().Name);
            window?.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(window,
                "Live glass capture is unavailable. Refraction is disabled; the clock will keep running. Restart WinCC to retry.", "WinCC")));
        }
        finally { busy = false; Schedule(FramePacing.Delay(frameStarted, age.Elapsed.TotalSeconds, FrameRate)); }
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (frame is not null) dc.DrawImage(frame, new Rect(RenderSize));
    }
    public void Dispose() { disposed = true; generation++; timer.Stop(); frame = null; }
}
