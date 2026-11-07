namespace WinCC.Layout;

/// <summary>A rounded lens with a clear interior, thick curved perimeter, dispersion and Fresnel reflection.</summary>
public sealed record GlassAppearance(bool Clear = true, double MaterialScale = 1, double Interaction = 0,
    double LightX = -.65, double LightY = -.75, bool HighContrast = false, bool ReducedTransparency = false, bool Organic = false, double Phase = 0, bool CompositeToBackdrop = false, int TintRgb = 0xBFD5FF, double TintAmount = 0);

public static class GlassOptics
{
    private readonly record struct LensPoint(double Alpha, double Nx, double Ny, double Edge, double Displacement, double Fresnel, double Rim, double LabelDim);
    private sealed record LensGeometry(int Width, int Height, int OutputWidth, int OutputHeight, byte[]? Mask, LensPoint[] Points);
    private static readonly object CacheGate = new();
    private static LensGeometry? cached;
    private static LensPoint Mix(LensPoint a, LensPoint b, double t) => new(
        a.Alpha + (b.Alpha - a.Alpha) * t, a.Nx + (b.Nx - a.Nx) * t, a.Ny + (b.Ny - a.Ny) * t,
        a.Edge + (b.Edge - a.Edge) * t, a.Displacement + (b.Displacement - a.Displacement) * t,
        a.Fresnel + (b.Fresnel - a.Fresnel) * t, a.Rim + (b.Rim - a.Rim) * t, a.LabelDim + (b.LabelDim - a.LabelDim) * t);
    private static LensGeometry RigidGeometry(int width, int height, byte[]? mask)
    {
        lock (CacheGate)
        {
            if (cached is { } existing && existing.OutputWidth == width && existing.OutputHeight == height && ReferenceEquals(existing.Mask, mask)) return existing;
            int outputWidth = width, outputHeight = height; var originalMask = mask;
            // Bound surface math independently of image resolution. Native image pixels and
            // glyph coverage stay full resolution; the smooth slope field is interpolated.
            double ratio = Math.Min(1, 768d / Math.Max(width, height));
            width = Math.Max(2, (int)Math.Round(width * ratio)); height = Math.Max(2, (int)Math.Round(height * ratio));
            if (mask is not null && (width != outputWidth || height != outputHeight))
            {
                mask = new byte[width * height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    mask[y * width + x] = originalMask![Math.Min(outputHeight - 1, (int)((y + .5) * outputHeight / height)) * outputWidth + Math.Min(outputWidth - 1, (int)((x + .5) * outputWidth / width))];
            }
            var points = new LensPoint[width * height];
            var glyphDistance = mask is null ? null : GlyphDistance.Build(mask, width, height);
            double strokeRadius = glyphDistance is null ? 0 : Math.Max(1, -glyphDistance.Min());
            var glyphHeight = mask is null ? null : GlyphSurface.Build(mask, width, height, strokeRadius);
            double Height(int x, int y) => glyphHeight![Math.Clamp(y, 0, height - 1) * width + Math.Clamp(x, 0, width - 1)];
            double maxGradient = .00001;
            if (glyphHeight is not null)
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    double gx = (Height(x + 1, y) - Height(x - 1, y)) * .5;
                    double gy = (Height(x, y + 1) - Height(x, y - 1)) * .5;
                    maxGradient = Math.Max(maxGradient, Math.Sqrt(gx * gx + gy * gy));
                }
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
                double displacement = Math.Min(bevel * .22, edge / Math.Sqrt(Math.Max(.08, 1 - edge * edge)) * bevel * .12);
                if (glyphHeight is not null)
                {
                    // Use the continuous height gradient directly. Normalizing each pixel
                    // would reintroduce a sudden direction flip across stroke centres.
                    nx = (Height(x - 1, y) - Height(x + 1, y)) * .5 / maxGradient;
                    ny = (Height(x, y - 1) - Height(x, y + 1)) * .5 / maxGradient;
                    edge = Math.Min(1, Math.Sqrt(nx * nx + ny * ny));
                    displacement = Math.Min(bevel * .18, strokeRadius * .45);
                }
                double dx = (x - width * .5) / (width * .4), dy = (y - height * .5) / (height * .32);
                points[y * width + x] = new(alpha, nx, ny, edge, displacement, edge * edge * edge * edge * edge, Math.Exp(-Math.Abs(d + 1.2) * 1.5), 1 - .08 * Math.Exp(-2 * (dx * dx + dy * dy)));
            }
            cached = new(width, height, outputWidth, outputHeight, originalMask, points);
            return cached;
        }
    }

    public static byte[] Render(byte[] source, int sourceWidth, int sourceHeight, int width, int height, double strength = 1, GlassAppearance? appearance = null, byte[]? destination = null, byte[]? shapeMask = null, GlassSampleRegion? sampleRegion = null)
    {
        if (!GlassResolution.Valid(width, height) || !GlassResolution.Valid(sourceWidth, sourceHeight) ||
            source.Length != checked(sourceWidth * sourceHeight * 4) || !double.IsFinite(strength) || strength is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (destination is not null && (destination.Length != width * height * 4 || ReferenceEquals(destination, source)))
            throw new ArgumentException("Output buffer must match the output size and be separate from the source.", nameof(destination));
        if (shapeMask is not null && shapeMask.Length != width * height) throw new ArgumentException("Invalid glyph mask.");
        sampleRegion ??= new(sourceWidth * .1, sourceHeight * .1, sourceWidth * .8, sourceHeight * .8);
        if (!new[] { sampleRegion.Left, sampleRegion.Top, sampleRegion.Width, sampleRegion.Height }.All(double.IsFinite) ||
            sampleRegion.Left < 0 || sampleRegion.Top < 0 || sampleRegion.Width <= 0 || sampleRegion.Height <= 0 ||
            sampleRegion.Left + sampleRegion.Width > sourceWidth || sampleRegion.Top + sampleRegion.Height > sourceHeight)
            throw new ArgumentOutOfRangeException(nameof(sampleRegion));
        var output = destination ?? new byte[width * height * 4];
        if (destination is not null && appearance?.CompositeToBackdrop != true) Array.Clear(output);
        var material = appearance;
        if (material is not null && (!double.IsFinite(material.MaterialScale) || material.MaterialScale is < .1 or > 10 ||
            !double.IsFinite(material.Interaction) || material.Interaction is < 0 or > 1 ||
            !double.IsFinite(material.TintAmount) || material.TintAmount is < 0 or > .35 || material.TintRgb is < 0 or > 0xFFFFFF || !double.IsFinite(material.Phase) || !double.IsFinite(material.LightX) || !double.IsFinite(material.LightY))) throw new ArgumentOutOfRangeException(nameof(appearance));
        double tintAmount = material?.TintAmount ?? 0; int tintRgb = material?.TintRgb ?? 0xFFFFFF;
        double tintB = Math.Exp(-3 * tintAmount * (1 - (tintRgb & 255) / 255d));
        double tintG = Math.Exp(-3 * tintAmount * (1 - ((tintRgb >> 8) & 255) / 255d));
        double tintR = Math.Exp(-3 * tintAmount * (1 - ((tintRgb >> 16) & 255) / 255d));
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
        LensPoint Surface(int x, int y)
        {
            if (geometry!.Width == width && geometry.Height == height) return geometry.Points[y * width + x];
            double gx = Math.Clamp((x + .5) * geometry.Width / width - .5, 0, geometry.Width - 1.001);
            double gy = Math.Clamp((y + .5) * geometry.Height / height - .5, 0, geometry.Height - 1.001);
            int ix = (int)gx, iy = (int)gy; double tx = gx - ix, ty = gy - iy;
            int a = iy * geometry.Width + ix, b = a + geometry.Width;
            return Mix(Mix(geometry.Points[a], geometry.Points[a + 1], tx), Mix(geometry.Points[b], geometry.Points[b + 1], tx), ty);
        }
        double fieldScaleX = geometry is null ? 1 : (double)width / geometry.Width;
        double fieldScaleY = geometry is null ? 1 : (double)height / geometry.Height;
        double sourceScaleX = sampleRegion.Width / width, sourceScaleY = sampleRegion.Height / height;
        bool alignedSource = Math.Abs(sourceScaleX - 1) < .000001 && Math.Abs(sourceScaleY - 1) < .000001 &&
            Math.Abs(sampleRegion.Left - Math.Round(sampleRegion.Left)) < .000001 && Math.Abs(sampleRegion.Top - Math.Round(sampleRegion.Top)) < .000001;
        // Source includes a 12.5% margin on each side, supporting optical displacement beyond the window.
        void Row(int y)
        {
          for (int x = 0; x < width; x++)
          {
            double baseX = (x + .5) * sourceScaleX + sampleRegion.Left - .5;
            double baseY = (y + .5) * sourceScaleY + sampleRegion.Top - .5;
            double distance = shapeMask is null ? Distance(x + .5, y + .5) : 0;
            double alpha = shapeMask is null ? Math.Clamp(.5 - distance, 0, 1) : shapeMask[y * width + x] / 255d;
            if (alpha == 0)
            {
                if (material?.CompositeToBackdrop == true)
                {
                    int outside = (y * width + x) * 4;
                    if (alignedSource)
                    {
                        int behind = (((int)baseY) * sourceWidth + (int)baseX) * 4;
                        output[outside] = source[behind]; output[outside + 1] = source[behind + 1]; output[outside + 2] = source[behind + 2];
                    }
                    else for (int c = 0; c < 3; c++) output[outside + c] = (byte)Sample(baseX, baseY, c);
                    output[outside + 3] = 255;
                }
                continue;
            }
            var point = geometry is null ? default : Surface(x, y);
            double nx = geometry is null ? (Distance(x + 1, y) - Distance(x - 1, y)) * .5 : point.Nx;
            double ny = geometry is null ? (Distance(x, y + 1) - Distance(x, y - 1)) * .5 : point.Ny;
            double edge = geometry is null ? Math.Clamp(1 + distance / bevel, 0, 1) : point.Edge;
            double slope = geometry is null ? edge / Math.Sqrt(Math.Max(.08, 1 - edge * edge)) : 0;
            double displacement = (geometry is null ? Math.Min(bevel * .22, slope * bevel * .12) : point.Displacement) * strength * thickness;
            double fresnel = geometry is null ? edge * edge * edge * edge * edge : point.Fresnel;
            double light = Math.Max(0, nx * (material?.LightX ?? -.65) + ny * (material?.LightY ?? -.75));
            double energy = material?.Interaction ?? 0;
            double reflection = fresnel * (.06 + .18 * light + energy * .08);
            double rim = (geometry is null ? Math.Exp(-Math.Abs(distance + 1.2) * 1.5) : point.Rim) * (.16 + .45 * light);
            double labelDim = 1;
            if (material is { Clear: true, ReducedTransparency: false, HighContrast: false })
            {
                if (geometry is not null) labelDim = point.LabelDim;
                else
                {
                    double dx = (x - width * .5) / (width * .4), dy = (y - height * .5) / (height * .32);
                    labelDim -= .08 * Math.Exp(-2 * (dx * dx + dy * dy));
                }
            }
            int index = (y * width + x) * 4;
            for (int channel = 0; channel < 3; channel++)
            {
                double dispersion = channel == 2 ? 1.002 : channel == 0 ? .998 : 1;
                double sx = baseX - nx * displacement * dispersion * fieldScaleX * sourceScaleX;
                double sy = baseY - ny * displacement * dispersion * fieldScaleY * sourceScaleY;
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
                        pixel *= .96;
                    }
                    if (!material.HighContrast && material.TintAmount > 0)
                    {
                        // Selective transmission colors the sampled light, not an opaque RGB overlay.
                        pixel *= channel == 0 ? tintB : channel == 1 ? tintG : tintR;
                    }
                    pixel += energy * light * edge * 12;
                }
                if (material is { HighContrast: false }) pixel *= 1 - edge * .045 * (1 - light);
                // Keep the centre clear. Reflection grows toward the lens edge, not across the entire card.
                output[index + channel] = (byte)Math.Clamp(pixel * (1 - reflection) + 255 * (reflection + rim), 0, 255);
            }
            if (material?.CompositeToBackdrop == true && alpha < 1)
            {
                // Resolve edge coverage against the actual background, never an opaque black host.
                for (int c = 0; c < 3; c++)
                {
                    double behind = Sample(baseX, baseY, c);
                    output[index + c] = (byte)(output[index + c] * alpha + behind * (1 - alpha));
                }
                output[index + 3] = 255;
            }
            else output[index + 3] = material?.CompositeToBackdrop == true ? (byte)255 : (byte)(alpha * 255);
          }
        }
        if ((long)width * height >= 500000)
            Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) }, Row);
        else for (int y = 0; y < height; y++) Row(y);
        return output;
    }
}
