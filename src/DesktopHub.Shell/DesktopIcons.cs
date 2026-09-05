using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DesktopHub.Shell;

/// <summary>切换桌面图标显示/隐藏(等效于桌面右键 → 查看 → 显示桌面图标)。</summary>
public static class DesktopIcons
{
    private const int WM_COMMAND = 0x0111;
    private const int ToggleDesktopIconsCmd = 0x7402; // Explorer 内部命令:切换桌面图标可见性

    private const string HideIconsValuePath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string HideIconsValueName = "HideIcons";

    /// <summary>当前桌面图标是否处于隐藏状态(读 Explorer 的 HideIcons 注册表值)。</summary>
    public static bool IsHidden()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(HideIconsValuePath);
            return key?.GetValue(HideIconsValueName) is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>切换桌面图标显示/隐藏,立即生效,无需重启 Explorer。</summary>
    public static void Toggle()
    {
        var defView = FindShellDefView();
        if (defView == IntPtr.Zero) return;
        SendMessage(defView, WM_COMMAND, ToggleDesktopIconsCmd, 0);
    }

    /// <summary>找到桌面 ListView(SHELLDLL_DefView):通常在 Progman 下,多屏/特殊场景在 WorkerW 下。</summary>
    private static IntPtr FindShellDefView()
    {
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero) return defView;
        }

        IntPtr worker = IntPtr.Zero;
        while ((worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null)) != IntPtr.Zero)
        {
            var defView = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero) return defView;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
