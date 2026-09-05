using System.Runtime.InteropServices;
using System.Windows;

namespace DesktopHub.App.Services;

/// <summary>获取鼠标所在显示器的可用区域(换算为 WPF DIP),用于胶囊窗多屏定位。</summary>
public static class ScreenInterop
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int CbSize;
        public RECT RcMonitor;
        public RECT RcWork;
        public uint DwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>鼠标所在显示器的工作区(DIP)。获取失败时返回主屏工作区。</summary>
    public static Rect GetWorkAreaAtCursor()
    {
        var wa = SystemParameters.WorkArea;
        var fallback = new Rect(wa.Left, wa.Top, wa.Width, wa.Height);

        try
        {
            if (!GetCursorPos(out var point)) return fallback;
            var monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return fallback;

            var info = new MONITORINFO { CbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info)) return fallback;

            double scale = 1.0;
            if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
                scale = dpiX / 96.0;

            return new Rect(
                info.RcWork.Left / scale,
                info.RcWork.Top / scale,
                (info.RcWork.Right - info.RcWork.Left) / scale,
                (info.RcWork.Bottom - info.RcWork.Top) / scale);
        }
        catch
        {
            return fallback;
        }
    }
}
