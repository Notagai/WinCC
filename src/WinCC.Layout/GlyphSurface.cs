namespace WinCC.Layout;

/// <summary>Smooth height field for glass letters; coverage remains the original sharp glyph mask.</summary>
public static class GlyphSurface
{
    public static float[] Build(byte[] mask, int width, int height, double strokeRadius)
    {
        if (width < 2 || height < 2 || mask.Length != checked(width * height) ||
            !double.IsFinite(strokeRadius) || strokeRadius < 0)
            throw new ArgumentException("Invalid glyph surface.");
        var field = Array.ConvertAll(mask, value => value / 255f);
        var temporary = new float[field.Length];
        // Three separable box passes approximate a Gaussian in linear time. Unlike a
        // nearest-edge distance, this height has no medial-axis cusp inside a stroke.
        int radius = Math.Clamp((int)Math.Ceiling(strokeRadius * .55), 1, Math.Max(1, Math.Min(width, height) / 8));
        int diameter = radius * 2 + 1;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < height; y++)
            {
                double sum = 0;
                for (int x = 0; x <= Math.Min(radius, width - 1); x++) sum += field[y * width + x];
                for (int x = 0; x < width; x++)
                {
                    temporary[y * width + x] = (float)(sum / diameter);
                    if (x - radius >= 0) sum -= field[y * width + x - radius];
                    if (x + radius + 1 < width) sum += field[y * width + x + radius + 1];
                }
            }
            for (int x = 0; x < width; x++)
            {
                double sum = 0;
                for (int y = 0; y <= Math.Min(radius, height - 1); y++) sum += temporary[y * width + x];
                for (int y = 0; y < height; y++)
                {
                    field[y * width + x] = (float)(sum / diameter);
                    if (y - radius >= 0) sum -= temporary[(y - radius) * width + x];
                    if (y + radius + 1 < height) sum += temporary[(y + radius + 1) * width + x];
                }
            }
        }
        return field;
    }
}
