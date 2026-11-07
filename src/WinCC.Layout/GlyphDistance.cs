namespace WinCC.Layout;

/// <summary>Cached two-pass distance transform for glass glyph boundaries.</summary>
internal static class GlyphDistance
{
    public static float[] Build(byte[] mask, int width, int height)
    {
        float[] Transform(bool inside)
        {
            var d = new float[mask.Length];
            for (int i = 0; i < d.Length; i++) d[i] = (mask[i] >= 128) == inside ? 0 : width + height;
            void Relax(int x, int y, int dx, int dy, float cost)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                    d[y * width + x] = Math.Min(d[y * width + x], d[ny * width + nx] + cost);
            }
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            { Relax(x,y,-1,0,1); Relax(x,y,0,-1,1); Relax(x,y,-1,-1,1.414214f); Relax(x,y,1,-1,1.414214f); }
            for (int y = height - 1; y >= 0; y--) for (int x = width - 1; x >= 0; x--)
            { Relax(x,y,1,0,1); Relax(x,y,0,1,1); Relax(x,y,1,1,1.414214f); Relax(x,y,-1,1,1.414214f); }
            return d;
        }
        var toInside = Transform(true); var toOutside = Transform(false);
        for (int i = 0; i < toInside.Length; i++) toInside[i] = mask[i] >= 128 ? .5f - toOutside[i] : toInside[i] - .5f;
        return toInside;
    }
}
