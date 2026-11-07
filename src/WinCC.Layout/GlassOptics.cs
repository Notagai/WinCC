namespace WinCC.Layout;

/// <summary>A rounded lens with a clear interior, thick curved perimeter, dispersion and Fresnel reflection.</summary>
public static class GlassOptics
{
    public static byte[] Render(byte[] source, int sourceWidth, int sourceHeight, int width, int height, double strength = 1)
    {
        if (width is < 2 or > 1024 || height is < 2 or > 1024 || sourceWidth < 2 || sourceHeight < 2 ||
            source.Length != checked(sourceWidth * sourceHeight * 4) || !double.IsFinite(strength) || strength is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(width));
        var output = new byte[width * height * 4];
        double radius = Math.Min(width, height) * .22, bevel = Math.Min(width, height) * .16;
        double Distance(double x, double y)
        {
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
        // Source includes a 12.5% margin on each side, supporting optical displacement beyond the window.
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            double distance = Distance(x + .5, y + .5), alpha = Math.Clamp(.5 - distance, 0, 1);
            if (alpha == 0) continue;
            double nx = (Distance(x + 1, y) - Distance(x - 1, y)) * .5;
            double ny = (Distance(x, y + 1) - Distance(x, y - 1)) * .5;
            double edge = Math.Clamp(1 + distance / bevel, 0, 1);
            double slope = edge / Math.Sqrt(Math.Max(.08, 1 - edge * edge));
            double displacement = Math.Min(bevel * .65, slope * bevel * .35) * strength;
            double fresnel = edge * edge * edge * edge * edge;
            double light = Math.Max(0, -nx * .65 - ny * .75);
            double reflection = fresnel * (.06 + .30 * light);
            double rim = Math.Exp(-Math.Abs(distance + 1.2) * 1.5) * (.12 + .5 * light);
            int index = (y * width + x) * 4;
            for (int channel = 0; channel < 3; channel++)
            {
                double dispersion = channel == 2 ? 1.025 : channel == 0 ? .975 : 1;
                double sx = (x + .5 - nx * displacement * dispersion) / width * sourceWidth * .8 + sourceWidth * .1;
                double sy = (y + .5 - ny * displacement * dispersion) / height * sourceHeight * .8 + sourceHeight * .1;
                double pixel = Sample(sx, sy, channel);
                // Keep the centre clear. Reflection grows toward the lens edge, not across the entire card.
                output[index + channel] = (byte)Math.Clamp(pixel * (1 - reflection) + 255 * (reflection + rim), 0, 255);
            }
            output[index + 3] = (byte)(alpha * 255);
        }
        return output;
    }
}
