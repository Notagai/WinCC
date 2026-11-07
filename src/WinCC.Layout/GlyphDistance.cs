namespace WinCC.Layout;

/// <summary>Exact Euclidean distance, avoiding the faceted normals of the former eight-direction approximation.</summary>
public static class GlyphDistance
{
    public static float[] Build(byte[] mask, int width, int height)
    {
        if (width < 1 || height < 1 || mask.Length != checked(width * height)) throw new ArgumentException("Invalid glyph mask.");
        float[] Transform(bool inside)
        {
            double far = 4d * (width * width + height * height);
            var intermediate = new double[mask.Length]; var result = new float[mask.Length];
            int capacity = Math.Max(width, height);
            var input = new double[capacity]; var output = new double[capacity];
            var sites = new int[capacity]; var crossings = new double[capacity + 1];
            void Line(int count)
            {
                int k = 0; sites[0] = 0; crossings[0] = double.NegativeInfinity; crossings[1] = double.PositiveInfinity;
                for (int q = 1; q < count; q++)
                {
                    double cross;
                    while (true)
                    {
                        int site = sites[k];
                        cross = ((input[q] + q * q) - (input[site] + site * site)) / (2d * (q - site));
                        if (cross > crossings[k] || k == 0) break;
                        k--;
                    }
                    k++; sites[k] = q; crossings[k] = cross; crossings[k + 1] = double.PositiveInfinity;
                }
                k = 0;
                for (int q = 0; q < count; q++)
                {
                    while (crossings[k + 1] < q) k++;
                    double delta = q - sites[k]; output[q] = delta * delta + input[sites[k]];
                }
            }
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++) input[y] = (mask[y * width + x] >= 128) == inside ? 0 : far;
                Line(height);
                for (int y = 0; y < height; y++) intermediate[y * width + x] = output[y];
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) input[x] = intermediate[y * width + x];
                Line(width);
                for (int x = 0; x < width; x++) result[y * width + x] = (float)Math.Sqrt(output[x]);
            }
            return result;
        }
        var toInside = Transform(true); var toOutside = Transform(false);
        for (int i = 0; i < toInside.Length; i++) toInside[i] = mask[i] >= 128 ? .5f - toOutside[i] : toInside[i] - .5f;
        return toInside;
    }
}
