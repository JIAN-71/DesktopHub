using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using DesktopHub.Shell;
using Xunit;

namespace DesktopHub.Core.Tests;

/// <summary>IconExtractor 48px 大图标提取冒烟测试(真实 Shell 调用,STA 线程)。</summary>
public class IconExtractorTests
{
    /// <summary>Shell/图标提取在 STA 线程执行(与正式应用一致)。</summary>
    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
        return result!;
    }

    [Fact]
    public void Extract_File_Returns_48px_Icon_And_Free_Does_Not_Throw()
    {
        var notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");

        var hIcon = RunInSta(() => IconExtractor.Extract(notepad, isNamespaceItem: false));

        try
        {
            Assert.NotEqual(IntPtr.Zero, hIcon);
            var (width, height) = GetIconSize(hIcon);
            Assert.Equal(48, width);
            Assert.Equal(48, height);
        }
        finally
        {
            IconExtractor.Free(hIcon);
        }
    }

    [Fact]
    public void Extract_System_Namespace_Item_Returns_Icon()
    {
        // 回收站是公共桌面必有的系统命名空间项
        var hIcon = RunInSta(() => IconExtractor.Extract("::{645FF040-5081-101B-9F08-00AA002F954E}", isNamespaceItem: true));
        try
        {
            Assert.NotEqual(IntPtr.Zero, hIcon);
            Assert.Equal(48, GetIconSize(hIcon).width);
        }
        finally
        {
            IconExtractor.Free(hIcon);
        }
    }

    [Fact]
    public void Extract_Invalid_Path_Returns_Zero()
    {
        Assert.Equal(IntPtr.Zero, RunInSta(() =>
            IconExtractor.Extract(@"Z:\__definitely_not_exist__\none.exe", isNamespaceItem: false)));
    }

    private static (int width, int height) GetIconSize(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out var info)) return (0, 0);
        try
        {
            // hbmColor 可能为空(纯掩码图标),此时用掩码位图测宽
            var bmp = new BITMAP();
            var handle = info.hbmColor != IntPtr.Zero ? info.hbmColor : info.hbmMask;
            if (handle == IntPtr.Zero || GetObject(handle, Marshal.SizeOf<BITMAP>(), ref bmp) == 0)
                return (0, 0);
            return (bmp.bmWidth, bmp.bmHeight);
        }
        finally
        {
            if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
