namespace WinCC.Layout;

public static class FramePacing
{
    public static TimeSpan Delay(double frameStarted, double now, int targetFps)
    {
        if (!double.IsFinite(frameStarted) || !double.IsFinite(now) || targetFps is < 0 or > 500)
            throw new ArgumentOutOfRangeException(nameof(targetFps));
        return targetFps == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Max(0, frameStarted + 1d / targetFps - now));
    }
}
