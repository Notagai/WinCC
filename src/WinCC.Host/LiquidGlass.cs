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
    public ClockDesign Design { get; init; } = new();
    public double RenderScale { get; init; } = 1;
    private static FontFamily ResolveFont(string name) => FontLibrary.Resolve(name);
    public bool TimeShaped { get; init; }
    private string glyphText = "00:00";
    private byte[]? glyphMask;
    private (int Width, int Height) glyphSize;
    public string GlyphText { get => glyphText; set { if (glyphText != value) { glyphText = value; glyphMask = null; generation++; } } }
    private byte[]? GetMask(int width, int height)
    {
        if (!TimeShaped) return null;
        if (glyphMask is not null && glyphSize == (width, height)) return glyphMask;
        var typeface = new Typeface(ResolveFont(Design.Font), Design.Italic ? FontStyles.Italic : FontStyles.Normal, FontWeight.FromOpenTypeWeight(Design.Weight), FontStretches.Normal);
        var outline = new GeometryGroup(); double advance = 0;
        foreach (char c in glyphText)
        {
            var text = new FormattedText(c.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, height * .58, Brushes.White, 1);
            outline.Children.Add(text.BuildGeometry(new Point(advance, 0)));
            advance += Math.Max(1, text.WidthIncludingTrailingWhitespace + Design.Tracking * height / 150);
        }
        System.Windows.Media.Geometry ink = outline;
        if (Design.Stroke > 0) ink = System.Windows.Media.Geometry.Combine(outline, outline.GetWidenedPathGeometry(new Pen(Brushes.White, Design.Stroke * height / 150)), GeometryCombineMode.Union, null);
        var bounds = ink.Bounds;
        double scale = Math.Min(width * .88 / Math.Max(1, bounds.Width), height * .55 / Math.Max(1, bounds.Height)) * Design.SizePercent / 100;
        double sx = scale * Design.StretchX, sy = scale * Design.StretchY;
        double availableHeight = height * (Design.ShowDate ? .78 : .96);
        if (Design.FillFrame)
        {
            sx = width * .96 / Math.Max(1, bounds.Width);
            sy = availableHeight / Math.Max(1, bounds.Height);
        }
        double fit = Math.Min(1, Math.Min(width * .96 / Math.Max(1, bounds.Width * sx), availableHeight / Math.Max(1, bounds.Height * sy)));
        sx *= fit; sy *= fit;
        ink.Transform = new MatrixTransform(sx, 0, 0, sy, width * .5 - (bounds.X + bounds.Width * .5) * sx,
            height * (Design.ShowDate ? .41 : .5) - (bounds.Y + bounds.Height * .5) * sy);
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawGeometry(Brushes.White, null, ink);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0);
        glyphMask = new byte[width * height]; for (int i = 0; i < glyphMask.Length; i++) glyphMask[i] = pixels[i * 4 + 3];
        glyphSize = (width, height); return glyphMask;
    }
    public bool ClearVariant { get; init; } = true;
    public bool ReduceTransparency { get; init; }
    public bool ReduceMotion { get; init; }
    private double interaction, targetInteraction, lightX = -.65, lightY = -.75, targetX = -.65, targetY = -.75;
    private readonly System.Diagnostics.Stopwatch age = System.Diagnostics.Stopwatch.StartNew();
    public string Material { get; set; } = "clock";
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background);
    private int frameRate = 60;
    private double frameStarted, previousFrame;
    private bool active, suspended;
    public bool HasFrame => frame is not null;
    public void SuspendSampling(bool suspend)
    {
        suspended = suspend; generation++; timer.Stop();
        if (!suspend && active && !busy) Refresh();
    }
    public int FrameRate
    {
        get => frameRate;
        set => frameRate = value is >= 0 and <= 500 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
    private BitmapSource? frame;
    private WriteableBitmap? renderBitmap;
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
        if (disposed || suspended || !active || !captureReady || !IsVisible) return;
        timer.Interval = delay; timer.Start();
    }
    private async void Refresh()
    {
        timer.Stop();
        var window = Window.GetWindow(this);
        if (disposed || suspended || !active || busy || !captureReady || !IsVisible) return;
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
        GlassFramePlan plan;
        try
        {
            plan = GlassResolution.Plan(ActualWidth, ActualHeight, dpi.DpiScaleX, dpi.DpiScaleY, RenderScale);
        }
        catch (ArgumentOutOfRangeException)
        {
            captureReady = false;
            MessageBox.Show(window, "This clock exceeds the supported render pixel budget. Select 75% or 50% resolution in Performance, or reduce its size.", "WinCC");
            return;
        }
        int width = plan.Width, height = plan.Height;
        int left = (int)Math.Round(origin.X) - plan.PaddingX, top = (int)Math.Round(origin.Y) - plan.PaddingY;
        var mask = GetMask(width, height);
        busy = true;
        try
        {
            var image = await Task.Run(() =>
            {
                var captured = BackdropCapture.Read(left, top, plan.CaptureWidth,
                    plan.CaptureHeight, plan.SourceWidth, plan.SourceHeight, capturePixels);
                capturePixels = captured.Pixels;
                if (opticalPixels is null || opticalPixels.Length != width * height * 4) opticalPixels = new byte[width * height * 4];
                var pixels = GlassOptics.Render(captured.Pixels, captured.Width, captured.Height, width, height, emerge * Design.Refraction, appearance with { TintRgb = Convert.ToInt32(Design.Tint[1..], 16), TintAmount = Design.TintAmount }, opticalPixels, mask, plan.SampleRegion);
                double luminance = 0; int samples = 0;
                for (int y = captured.Height / 4; y < captured.Height * 3 / 4; y += 8)
                    for (int x = captured.Width / 4; x < captured.Width * 3 / 4; x += 8)
                    {
                        int i = (y * captured.Width + x) * 4;
                        luminance += (.2126 * captured.Pixels[i + 2] + .7152 * captured.Pixels[i + 1] + .0722 * captured.Pixels[i]) / 255;
                        samples++;
                    }
                return (Pixels: pixels, Luminance: luminance / Math.Max(1, samples));
            });
            if (disposed || version != generation || !IsVisible) return;
            if (renderBitmap is null || renderBitmap.PixelWidth != width || renderBitmap.PixelHeight != height)
                renderBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            renderBitmap.WritePixels(new Int32Rect(0, 0, width, height), image.Pixels, width * 4, 0);
            frame = renderBitmap; BackgroundLuminance?.Invoke(image.Luminance); InvalidateVisual();
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            if (disposed || version != generation) return;
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
    public void Dispose() { disposed = true; generation++; timer.Stop(); frame = null; renderBitmap = null; }
}
