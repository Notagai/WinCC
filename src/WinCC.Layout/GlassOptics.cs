namespace WinCC.Layout;

/// <summary>A rounded lens with a clear interior, thick curved perimeter, dispersion and Fresnel reflection.</summary>
public sealed record GlassAppearance(bool Clear = true, double MaterialScale = 1, double Interaction = 0,
    double LightX = -.65, double LightY = -.75, bool HighContrast = false, bool ReducedTransparency = false, bool Organic = false, double Phase = 0, bool CompositeToBackdrop = false);

public static class GlassOptics
{
    private readonly record struct LensPoint(double Alpha, double Nx, double Ny, double Edge, double Displacement, double Fresnel, double Rim, double LabelDim);
    private sealed record LensGeometry(int Width, int Height, byte[]? Mask, LensPoint[] Points);
    private static readonly object CacheGate = new();
    private static LensGeometry? cached;
    private static LensPoint[] RigidGeometry(int width, int height, byte[]? mask)
    {
        lock (CacheGate)
        {
            if (cached is { } existing && existing.Width == width && existing.Height == height && ReferenceEquals(existing.Mask, mask)) return existing.Points;
            var points = new LensPoint[width * height];
            var glyphDistance = mask is null ? null : GlyphDistance.Build(mask, width, height);
            double radius = Math.Min(width, height) * .48, bevel = Math.Min(width, height) * .16;
            double Distance(double x, double y)
            {
                if (glyphDistance is not null) return glyphDistance[Math.Clamp((int)y, 0, height - 1) * width + Math.Clamp((int)x, 0, width - 1)];
                double qx = Math.Abs(x - width * .5) - (width * .5 - radius - 1);
                double qy = Math.Abs(y - height * .5) - (height * .5 - radius - 1);
                double ax = Math.Max(qx, 0), ay = Math.Max(qy, 0);
                return Math.Sqrt(ax * ax + ay * ay) + Math.Min(Math.Max(qx, qy), 0) - radius;
            }
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                double d = Distance(x + .5, y + .5), alpha = mask is null ? Math.Clamp(.5 - d, 0, 1) : mask[y * width + x] / 255d;
                double nx = (Distance(x + 1, y) - Distance(x - 1, y)) * .5;
                double ny = (Distance(x, y + 1) - Distance(x, y - 1)) * .5;
                double edge = Math.Clamp(1 + d / bevel, 0, 1);
                double displacement = Math.Min(bevel * .65, edge / Math.Sqrt(Math.Max(.08, 1 - edge * edge)) * bevel * .35);
                double dx = (x - width * .5) / (width * .4), dy = (y - height * .5) / (height * .32);
                points[y * width + x] = new(alpha, nx, ny, edge, displacement, edge * edge * edge * edge * edge, Math.Exp(-Math.Abs(d + 1.2) * 1.5), 1 - .18 * Math.Exp(-2 * (dx * dx + dy * dy)));
            }
            cached = new(width, height, mask, points); // One bounded cache; resizing replaces the old geometry.
            return points;
        }
    }

    public static byte[] Render(byte[] source, int sourceWidth, int sourceHeight, int width, int height, double strength = 1, GlassAppearance? appearance = null, byte[]? destination = null, byte[]? shapeMask = null)
    {
        if (width is < 2 or > 1024 || height is < 2 or > 1024 || sourceWidth < 2 || sourceHeight < 2 ||
            source.Length != checked(sourceWidth * sourceHeight * 4) || !double.IsFinite(strength) || strength is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (destination is not null && (destination.Length != width * height * 4 || ReferenceEquals(destination, source)))
            throw new ArgumentException("Output buffer must match the output size and be separate from the source.", nameof(destination));
        if (shapeMask is not null && shapeMask.Length != width * height) throw new ArgumentException("Invalid glyph mask.");
        var output = destination ?? new byte[width * height * 4];
        if (destination is not null && appearance?.CompositeToBackdrop != true) Array.Clear(output);
        var material = appearance;
        if (material is not null && (!double.IsFinite(material.MaterialScale) || material.MaterialScale is < .1 or > 10 ||
            !double.IsFinite(material.Interaction) || material.Interaction is < 0 or > 1 ||
            !double.IsFinite(material.Phase) || !double.IsFinite(material.LightX) || !double.IsFinite(material.LightY))) throw new ArgumentOutOfRangeException(nameof(appearance));
        double thickness = material is null ? 1 : Math.Clamp(Math.Sqrt(material.MaterialScale), .8, 1.6);
        double radius = Math.Min(width, height) * .48, bevel = Math.Min(width, height) * .16;
        double Distance(double x, double y)
        {
            if (material?.Organic == true) return GlassShape.Distance(x, y, width, height, material.Phase, material.Interaction);
            double qx = Math.Abs(x - width * .5) - (width * .5 - radius - 1);
            double qy = Math.Abs(y - height * .5) - (height * .5 - radius - 1);
            double ax = Math.Max(qx, 0), ay = Math.Max(qy, 0);
            return Math.Sqrt(ax * ax + ay * ay) + Math.Min(Math.Max(qx, qy), 0) - radius;
        }
        double Sample(double x, double y, int channel)
        {
            x = Math.Clamp(x, 0, sourceWidth - 1.001); y = Math.Clamp(y, 0, sourceHeight - 1.001);
            int ix = (int)x, iy = (int)y; double tx = x - ix, ty = y - iy;
            double a = source[(iy * sourceWidth + ix) * 4 + channel], b = source[(iy * sourceWidth + ix + 1) * 4 + channel];
            double c = source[((iy + 1) * sourceWidth + ix) * 4 + channel], d = source[((iy + 1) * sourceWidth + ix + 1) * 4 + channel];
            return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty;
        }
        var geometry = material?.Organic == true ? null : RigidGeometry(width, height, shapeMask);
        // Source includes a 12.5% margin on each side, supporting optical displacement beyond the window.
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            var point = geometry is null ? default : geometry[y * width + x];
            double distance = geometry is null ? Distance(x + .5, y + .5) : 0, alpha = geometry is null ? Math.Clamp(.5 - distance, 0, 1) : point.Alpha;
            if (alpha == 0)
            {
                if (material?.CompositeToBackdrop == true)
                {
                    int outside = (y * width + x) * 4;
                    for (int c = 0; c < 3; c++) output[outside + c] = (byte)Sample((x + .5) / width * sourceWidth * .8 + sourceWidth * .1, (y + .5) / height * sourceHeight * .8 + sourceHeight * .1, c);
                    output[outside + 3] = 255;
                }
                continue;
            }
            double nx = geometry is null ? (Distance(x + 1, y) - Distance(x - 1, y)) * .5 : point.Nx;
            double ny = geometry is null ? (Distance(x, y + 1) - Distance(x, y - 1)) * .5 : point.Ny;
            double edge = geometry is null ? Math.Clamp(1 + distance / bevel, 0, 1) : point.Edge;
            double slope = geometry is null ? edge / Math.Sqrt(Math.Max(.08, 1 - edge * edge)) : 0;
            double displacement = (geometry is null ? Math.Min(bevel * .65, slope * bevel * .35) : point.Displacement) * strength * thickness;
            double fresnel = geometry is null ? edge * edge * edge * edge * edge : point.Fresnel;
            double light = Math.Max(0, nx * (material?.LightX ?? -.65) + ny * (material?.LightY ?? -.75));
            double energy = material?.Interaction ?? 0;
            double reflection = fresnel * (.06 + .30 * light + energy * .12);
            double rim = (geometry is null ? Math.Exp(-Math.Abs(distance + 1.2) * 1.5) : point.Rim) * (.12 + .5 * light);
            double labelDim = 1;
            if (material is { Clear: true, ReducedTransparency: false, HighContrast: false })
            {
                if (geometry is not null) labelDim = point.LabelDim;
                else
                {
                    double dx = (x - width * .5) / (width * .4), dy = (y - height * .5) / (height * .32);
                    labelDim -= .18 * Math.Exp(-2 * (dx * dx + dy * dy));
                }
            }
            int index = (y * width + x) * 4;
            for (int channel = 0; channel < 3; channel++)
            {
                double dispersion = channel == 2 ? 1.025 : channel == 0 ? .975 : 1;
                double sx = (x + .5 - nx * displacement * dispersion) / width * sourceWidth * .8 + sourceWidth * .1;
                double sy = (y + .5 - ny * displacement * dispersion) / height * sourceHeight * .8 + sourceHeight * .1;
                double pixel = Sample(sx, sy, channel);
                if (material is not null)
                {
                    if (material.HighContrast) pixel = 12;
                    else if (material.ReducedTransparency)
                    {
                        double spread = Math.Min(width, height) * .035;
                        double frosted = 0;
                        for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                            frosted += Sample(sx + ox * spread, sy + oy * spread, channel);
                        pixel = frosted / 9 * .55 + 30 * .45;
                    }
                    else if (material.Clear)
                    {
                        // WWDC25 Clear: permanently transparent; dim only beneath the bright clock label.
                        pixel *= labelDim;
                    }
                    else
                    {
                        // Regular approximation: thicker surfaces softly scatter background detail.
                        double spread = Math.Max(1, thickness * 1.5);
                        double diffuse = (Sample(sx - spread, sy, channel) + Sample(sx + spread, sy, channel) +
                            Sample(sx, sy - spread, channel) + Sample(sx, sy + spread, channel)) * .25;
                        pixel = pixel * .72 + diffuse * .28;
                        pixel = pixel * .88 + 24 * .12;
                    }
                    pixel += energy * light * edge * 12;
                }
                // Keep the centre clear. Reflection grows toward the lens edge, not across the entire card.
                output[index + channel] = (byte)Math.Clamp(pixel * (1 - reflection) + 255 * (reflection + rim), 0, 255);
            }
            if (material?.CompositeToBackdrop == true && alpha < 1)
            {
                // Resolve edge coverage against the actual background, never an opaque black host.
                for (int c = 0; c < 3; c++)
                {
                    double behind = Sample((x + .5) / width * sourceWidth * .8 + sourceWidth * .1, (y + .5) / height * sourceHeight * .8 + sourceHeight * .1, c);
                    output[index + c] = (byte)(output[index + c] * alpha + behind * (1 - alpha));
                }
                output[index + 3] = 255;
            }
            else output[index + 3] = material?.CompositeToBackdrop == true ? (byte)255 : (byte)(alpha * 255);
        }
        return output;
    }
}
