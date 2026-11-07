namespace WinCC.Layout;

/// <summary>One implicit surface shared by refraction, coverage and the native hit region.</summary>
public static class GlassShape
{
    public static double Distance(double x, double y, double width, double height, double phase, double energy)
    {
        double unit = Math.Min(width, height);
        double breathe = Math.Sin(phase);
        double cx = width * (.5 + .006 * Math.Sin(phase * .73));
        double cy = height * (.5 + .008 * Math.Cos(phase * .81));
        double warp = unit * (.014 + energy * .016) * Math.Sin(y / height * Math.PI * 1.6 + phase);
        double radius = unit * .34;
        double qx = Math.Abs(x - cx + warp) - (width * (.455 + .008 * breathe) - radius);
        double qy = Math.Abs(y - cy) - (height * (.445 - .012 * breathe) - radius);
        double ax = Math.Max(qx, 0), ay = Math.Max(qy, 0);
        return Math.Sqrt(ax * ax + ay * ay) + Math.Min(Math.Max(qx, qy), 0) - radius;
    }
    public static (double X, double Y)[] Outline(double width, double height, double phase, double energy)
    {
        var points = new (double X, double Y)[128];
        for (int i = 0; i < points.Length; i++)
        {
            double angle = i * Math.PI * 2 / points.Length, dx = Math.Cos(angle), dy = Math.Sin(angle);
            double low = 0, high = Math.Max(width, height);
            for (int j = 0; j < 18; j++)
            {
                double r = (low + high) * .5;
                if (Distance(width * .5 + dx * r, height * .5 + dy * r, width, height, phase, energy) > 0) high = r;
                else low = r;
            }
            double radius = (low + high) * .5;
            points[i] = ((width * .5 + dx * radius) / width, (height * .5 + dy * radius) / height);
        }
        return points;
    }
}
