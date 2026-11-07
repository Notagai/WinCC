using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinCC.DesktopInterop;

public sealed class TrayIcon : IDisposable
{
    public const int CallbackMessage = 0x8007;
    private Data data;
    public TrayIcon(nint window)
    {
        data = new Data { Size = (uint)Marshal.SizeOf<Data>(), Window = window, Id = 1, Flags = 7,
            Callback = CallbackMessage, Icon = CreateClockIcon(), Tip = "WinCC · Double-click for Settings", Info = "", Title = "" };
        try { Restore(); } catch { DestroyIcon(data.Icon); throw; }
    }
    public void Restore() { if (!Shell_NotifyIcon(0, ref data)) throw new Win32Exception(); }
    public void Dispose()
    {
        Shell_NotifyIcon(2, ref data);
        if (data.Icon != 0) { DestroyIcon(data.Icon); data.Icon = 0; }
    }
    private static nint CreateClockIcon()
    {
        const int size = 32;
        var mask = new byte[size * size / 8]; var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            int i = (y * size + x) * 4; double dx = x - 15.5, dy = y - 15.5, r = Math.Sqrt(dx * dx + dy * dy);
            if (r > 14) { mask[(y * size + x) / 8] |= (byte)(128 >> (x % 8)); continue; }
            bool rim = r > 12;
            bool hand = (Math.Abs(dx) < 1 && dy is > -8 and < 1) || (Math.Abs(dy) < 1 && dx is > -1 and < 7);
            byte shade = rim || hand ? (byte)240 : (byte)45;
            pixels[i] = rim ? (byte)255 : shade; pixels[i + 1] = shade; pixels[i + 2] = shade; pixels[i + 3] = 255;
        }
        nint icon = CreateIcon(0, size, size, 1, 32, mask, pixels);
        if (icon == 0) throw new Win32Exception(); return icon;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Data
    {
        public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Shell_NotifyIcon(uint message, ref Data data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreateIcon(nint instance, int width, int height, byte planes, byte bits, byte[] andMask, byte[] xorPixels);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
}
