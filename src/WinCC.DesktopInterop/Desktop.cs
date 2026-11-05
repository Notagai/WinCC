using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinCC.DesktopInterop;

public sealed record Display(string Key, string Name, int Left, int Top, int Width, int Height, double Scale, bool Primary);
public sealed record SystemSample(double? CpuPercent, ulong? UsedMemory, ulong? TotalMemory);

/// <summary>Only this assembly calls user32/shcore/kernel32. No Explorer mutation or global input hooks.</summary>
public static class Desktop
{
    public static IReadOnlyList<Display> Displays()
    {
        var displays = new List<Display>();
        Native.MonitorCallback callback = (nint monitor, nint dc, nint rect, nint data) =>
        {
            var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
            if (!Native.GetMonitorInfo(monitor, ref info)) return true;
            uint dpi = 96;
            if (Native.GetDpiForMonitor(monitor, 0, out uint x, out _) == 0 && x > 0) dpi = x;
            var device = new Native.DisplayDevice { Size = Marshal.SizeOf<Native.DisplayDevice>() };
            string key = info.Device;
            if (Native.EnumDisplayDevices(info.Device, 0, ref device, 1) && !string.IsNullOrWhiteSpace(device.DeviceId))
                key = device.DeviceId;
            var r = info.Work;
            displays.Add(new(key, info.Device, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                dpi / 96d, (info.Flags & 1) != 0));
            return true;
        };
        if (!Native.EnumDisplayMonitors(0, 0, callback, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return displays;
    }

    /// <summary>Keep widget surfaces out of Alt+Tab and Task View without changing their owner or focus behavior.</summary>
    public static void ConfigureWidgetWindow(nint handle)
    {
        const int extendedStyle = -20;
        const long toolWindow = 0x80, appWindow = 0x40000;
        long style = Native.GetWindowLongPtr(handle, extendedStyle).ToInt64();
        Marshal.SetLastPInvokeError(0);
        var previous = Native.SetWindowLongPtr(handle, extendedStyle, new nint((style | toolWindow) & ~appWindow));
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    public static void Place(nint handle, int x, int y, int width, int height, bool desktop)
    {
        // No activation, ownership, parenting, style changes or topmost windows.
        const uint flags = 0x0010 | 0x0200; // SWP_NOACTIVATE | SWP_NOOWNERZORDER
        if (!Native.SetWindowPos(handle, desktop ? new nint(1) : 0, x, y, width, height,
                flags | (desktop ? 0u : 0x0004u))) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static void KeepBehind(nint position, nint anchor)
    {
        var p = Marshal.PtrToStructure<Native.WindowPosition>(position);
        // Preserve activation semantics for focused text controls, but never lift above ordinary apps.
        p.InsertAfter = anchor; p.Flags &= ~0x0004u;
        Marshal.StructureToPtr(p, position, false);
    }
    public static uint ShellRestartMessage => Native.RegisterWindowMessage("TaskbarCreated");

    public static bool IsForegroundFullscreen(nint ownWindow)
    {
        nint foreground = Native.GetForegroundWindow();
        if (foreground == 0 || foreground == ownWindow) return false;
        var cls = new System.Text.StringBuilder(256);
        Native.GetClassName(foreground, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!Native.GetWindowRect(foreground, out var rect)) return false;
        var monitor = Native.MonitorFromWindow(foreground, 2);
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        return Native.GetMonitorInfo(monitor, ref info) && rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top &&
            rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom;
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MonitorInfo
        {
            public int Size; public Rect Monitor, Work; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DisplayDevice
        {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }
        [StructLayout(LayoutKind.Sequential)] public struct WindowPosition
        { public nint Window, InsertAfter; public int X, Y, Width, Height; public uint Flags; }
        public delegate bool MonitorCallback(nint monitor, nint dc, nint rect, nint data);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        public static extern nint GetWindowLongPtr(nint handle, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        public static extern nint SetWindowLongPtr(nint handle, int index, nint value);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
        [return: MarshalAs(UnmanagedType.Bool)] public static extern bool EnumDisplayDevices(string device, uint number, ref DisplayDevice info, uint flags);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(nint window, System.Text.StringBuilder text, int count);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(nint window, out Rect rect);
        [DllImport("user32.dll")] public static extern nint MonitorFromWindow(nint window, uint flags);
    }
}

public sealed class SystemSampler
{
    private ulong previousIdle, previousTotal;
    public SystemSample Sample()
    {
        double? cpu = null;
        if (GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
        {
            ulong total = kernel + user;
            if (previousTotal > 0 && total > previousTotal && idle >= previousIdle)
                cpu = Math.Clamp(100d * (1d - (idle - previousIdle) / (double)(total - previousTotal)), 0, 100);
            previousIdle = idle; previousTotal = total;
        }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (GlobalMemoryStatusEx(ref memory)) return new(cpu, memory.TotalPhysical - memory.AvailablePhysical, memory.TotalPhysical);
        return new(cpu, null, null);
    }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtended;
    }
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
