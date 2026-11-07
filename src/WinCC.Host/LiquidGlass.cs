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
    public string Material { get; set; } = "clock";
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private BitmapSource? frame;
    private bool busy, disposed, captureReady;
    private int generation;
    public LiquidGlass()
    {
        Loaded += (_, _) => { if (!disposed) { timer.Start(); Refresh(); } };
        Unloaded += (_, _) => { timer.Stop(); generation++; frame = null; };
        SizeChanged += (_, _) => { generation++; frame = null; InvalidateVisual(); };
        timer.Tick += (_, _) => Refresh();
    }
    public void EnableCapture(bool ready) => captureReady = ready;
    private async void Refresh()
    {
        var window = Window.GetWindow(this);
        if (disposed || busy || !captureReady || !IsVisible || window?.WindowState == WindowState.Minimized || ActualWidth < 2 || ActualHeight < 2) return;
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
                    (int)Math.Ceiling(physicalHeight * 1.25), Math.Max(2, (int)(width * 1.25)), Math.Max(2, (int)(height * 1.25)));
                var pixels = GlassOptics.Render(captured.Pixels, captured.Width, captured.Height, width, height);
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
            timer.Stop(); // Do not display stale samples or silently pretend capture succeeded.
            Log.Write("glass.capture_failed", error.GetType().Name);
            window?.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(window,
                "Live glass capture is unavailable. The clock will remain clear. Restart WinCC to retry.", "WinCC")));
        }
        finally { busy = false; }
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (frame is not null) dc.DrawImage(frame, new Rect(RenderSize));
    }
    public void Dispose() { disposed = true; generation++; timer.Stop(); frame = null; }
}
