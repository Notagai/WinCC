namespace WinCC.Layout;

/// <summary>Native-resolution slope and rim cache: six bytes per pixel, no surface upscaling.</summary>
internal sealed class GlyphLens
{
    private readonly uint[] normals;
    private readonly Half[] rims;
    public int Width { get; }
    public int Height { get; }
    public byte[] Mask { get; }
    public double Displacement { get; }

    public GlyphLens(byte[] mask, int width, int height)
    {
        Width = width; Height = height; Mask = mask;
        var distance = GlyphDistance.Inside(mask, width, height);
        double strokeRadius = Math.Max(1, -distance.Min());
        var surface = GlyphSurface.Build(mask, width, height, strokeRadius);
        Displacement = Math.Min(Math.Min(width, height) * .16 * .18, strokeRadius * .45);
        double maxGradient = .00001;
        for (int y = 0; y < height; y++)
        {
            int above = Math.Max(0, y - 1) * width, below = Math.Min(height - 1, y + 1) * width, row = y * width;
            for (int x = 0; x < width; x++)
            {
                double gx = (surface[row + Math.Min(width - 1, x + 1)] - surface[row + Math.Max(0, x - 1)]) * .5;
                double gy = (surface[below + x] - surface[above + x]) * .5;
                maxGradient = Math.Max(maxGradient, Math.Sqrt(gx * gx + gy * gy));
            }
        }
        normals = new uint[mask.Length]; rims = new Half[mask.Length];
        for (int y = 0; y < height; y++)
        {
            int above = Math.Max(0, y - 1) * width, below = Math.Min(height - 1, y + 1) * width, row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                if (mask[i] == 0) continue;
                double nx = (surface[row + Math.Max(0, x - 1)] - surface[row + Math.Min(width - 1, x + 1)]) * .5 / maxGradient;
                double ny = (surface[above + x] - surface[below + x]) * .5 / maxGradient;
                short px = (short)Math.Round(nx * short.MaxValue), py = (short)Math.Round(ny * short.MaxValue);
                normals[i] = (uint)(ushort)px | ((uint)(ushort)py << 16);
                rims[i] = (Half)Math.Exp(-Math.Abs(distance[i] + 1.2) * 1.5);
            }
        }
    }

    public (double Nx, double Ny, double Rim) At(int x, int y)
    {
        int i = y * Width + x; uint packed = normals[i];
        return ((short)(packed & 0xffff) / (double)short.MaxValue, (short)(packed >> 16) / (double)short.MaxValue, (double)rims[i]);
    }
}
