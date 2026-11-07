namespace WinCC.Layout;

public sealed record GlassSampleRegion(double Left, double Top, double Width, double Height);
public sealed record GlassFramePlan(int Width, int Height, int CaptureWidth, int CaptureHeight,
    int PaddingX, int PaddingY, int SourceWidth, int SourceHeight, GlassSampleRegion SampleRegion);

public static class GlassResolution
{
    public const int MaxDimension = 16384;
    public const long MaxPixels = 64L * 1024 * 1024;
    public static bool Valid(int width, int height, int minimum = 2) =>
        width >= minimum && height >= minimum && width <= MaxDimension && height <= MaxDimension && (long)width * height <= MaxPixels;
    public static (int Width, int Height) Pixels(double widthDip, double heightDip, double dpiX, double dpiY, double scale = 1)
    {
        if (!new[] { widthDip, heightDip, dpiX, dpiY, scale }.All(double.IsFinite) ||
            widthDip <= 0 || heightDip <= 0 || dpiX <= 0 || dpiY <= 0 || scale is < .5 or > 1)
            throw new ArgumentOutOfRangeException(nameof(widthDip));
        double width = Math.Round(widthDip * dpiX * scale), height = Math.Round(heightDip * dpiY * scale);
        if (width > MaxDimension || height > MaxDimension || !Valid(Math.Max(2, (int)width), Math.Max(2, (int)height)))
            throw new ArgumentOutOfRangeException(nameof(widthDip), "Glass exceeds the supported pixel budget.");
        return (Math.Max(2, (int)width), Math.Max(2, (int)height));
    }
    public static GlassFramePlan Plan(double widthDip, double heightDip, double dpiX, double dpiY, double scale = 1)
    {
        var native = Pixels(widthDip, heightDip, dpiX, dpiY);
        var output = Pixels(widthDip, heightDip, dpiX, dpiY, scale);
        int px = (int)Math.Ceiling(native.Width / 8d), py = (int)Math.Ceiling(native.Height / 8d);
        int cw = native.Width + 2 * px, ch = native.Height + 2 * py;
        int sw = Math.Max(2, (int)Math.Round(cw * scale)), sh = Math.Max(2, (int)Math.Round(ch * scale));
        if (!Valid(sw, sh)) throw new ArgumentOutOfRangeException(nameof(widthDip), "Padded capture exceeds the pixel budget.");
        var sample = new GlassSampleRegion((double)px * sw / cw, (double)py * sh / ch,
            (double)native.Width * sw / cw, (double)native.Height * sh / ch);
        return new(output.Width, output.Height, cw, ch, px, py, sw, sh, sample);
    }
}
