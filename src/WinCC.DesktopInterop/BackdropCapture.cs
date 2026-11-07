using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinCC.DesktopInterop;

public sealed record BackdropFrame(int Width, int Height, byte[] Pixels);

/// <summary>Local, in-memory capture. Exclusion prevents the renderer sampling its own previous frame.</summary>
public static class BackdropCapture
{
    public static void ShapeClock(nint window, int width, int height, bool timeShaped = false)
    {
        int diameter = timeShaped ? 0 : Math.Max(2, (int)(Math.Min(width, height) * .96));
        nint region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
        if (region == 0) throw new Win32Exception();
        // SetWindowRgn transfers ownership on success; delete only when that transfer fails.
        if (SetWindowRgn(window, region, true) == 0) { DeleteObject(region); throw new Win32Exception(); }
    }
    public static void ShapeBlob(nint window, int width, int height, (double X, double Y)[] outline)
    {
        var points = outline.Select(p => new NativePoint { X = (int)Math.Round(p.X * width), Y = (int)Math.Round(p.Y * height) }).ToArray();
        nint region = CreatePolygonRgn(points, points.Length, 1);
        if (region == 0) throw new Win32Exception();
        if (SetWindowRgn(window, region, true) == 0) { DeleteObject(region); throw new Win32Exception(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("gdi32.dll")] private static extern nint CreatePolygonRgn([In] NativePoint[] points, int count, int mode);
    public static bool Exclude(nint window, bool excluded = true) => SetWindowDisplayAffinity(window, excluded ? 0x11u : 0u);
    public static BackdropFrame Read(int left, int top, int width, int height, int outputWidth, int outputHeight, byte[]? reusablePixels = null)
    {
        if (width <= 0 || height <= 0 || outputWidth is < 1 or > 16384 || outputHeight is < 1 or > 16384 || (long)outputWidth * outputHeight > 64L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(width));
        nint screen = GetDC(0), dc = 0, bitmap = 0, previous = 0;
        try
        {
            if (screen == 0) throw new Win32Exception();
            dc = CreateCompatibleDC(screen);
            var info = new BitmapInfo { Size = 40, Width = outputWidth, Height = -outputHeight, Planes = 1, Bits = 32 };
            bitmap = CreateDIBSection(screen, ref info, 0, out nint bits, 0, 0);
            if (dc == 0 || bitmap == 0 || bits == 0) throw new Win32Exception();
            previous = SelectObject(dc, bitmap);
            if (previous == 0 || previous == -1) throw new Win32Exception();
            SetStretchBltMode(dc, 4);
            if (!StretchBlt(dc, 0, 0, outputWidth, outputHeight, screen, left, top, width, height, 0x40CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            int length = checked(outputWidth * outputHeight * 4);
            var pixels = reusablePixels is not null && reusablePixels.Length == length ? reusablePixels : new byte[length];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return new(outputWidth, outputHeight, pixels);
        }
        finally
        {
            if (previous != 0 && previous != -1 && dc != 0) SelectObject(dc, previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (dc != 0) DeleteDC(dc);
            if (screen != 0) ReleaseDC(0, screen);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, Bits;
        public uint Compression, ImageSize; public int XPels, YPels; public uint ColorsUsed, ColorsImportant;
    }
    [DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowRgn(nint window, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(nint dc, int mode);
    [DllImport("gdi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool StretchBlt(nint dest, int x, int y, int w, int h, nint source, int sx, int sy, int sw, int sh, uint operation);
}
