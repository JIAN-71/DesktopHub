using System;
using System.Runtime.InteropServices;

namespace DesktopHub.Shell;

/// <summary>
/// 提取桌面图标的 48×48 HICON(SHGetFileInfo 拿系统图标索引 + SHGetImageList(SHIL_EXTRALARGE) 取大图),
/// 系统命名空间项("::{CLSID}")先经 SHParseDisplayName 解析为 PIDL 再取图标。
/// 返回的句柄由调用方用完调用 Free 释放。
/// 枚举与取图标分离:枚举返回纯数据,只有展示时才按需提取图标。
/// </summary>
public static class IconExtractor
{
    /// <param name="parsingName">Shell 解析名;文件为完整路径,系统图标形如 "::{CLSID}"。</param>
    /// <param name="isNamespaceItem">是否为系统命名空间项(非文件系统路径)。</param>
    /// <returns>48×48 的 HICON,失败返回 IntPtr.Zero;调用方负责 Free。</returns>
    public static IntPtr Extract(string parsingName, bool isNamespaceItem)
    {
        var info = new SHFILEINFOW();
        var size = (uint)Marshal.SizeOf<SHFILEINFOW>();
        var flags = SHGFI_SYSICONINDEX | (isNamespaceItem ? SHGFI_PIDL : 0u);

        var found = false;
        if (!isNamespaceItem)
        {
            found = SHGetFileInfo(parsingName, FILE_ATTRIBUTE_NORMAL, ref info, size, flags) != IntPtr.Zero;
        }
        else
        {
            // 系统命名空间项:先解析为 PIDL 再取图标索引
            if (SHParseDisplayName(parsingName, IntPtr.Zero, out var pidl, 0, IntPtr.Zero) != 0 || pidl == IntPtr.Zero)
                return IntPtr.Zero;
            try
            {
                found = SHGetFileInfo(pidl, 0, ref info, size, flags) != IntPtr.Zero;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        if (!found || info.iIcon < 0) return IntPtr.Zero;
        var imageList = GetExtraLargeImageList();
        return imageList == IntPtr.Zero ? IntPtr.Zero : GetIconFromList(imageList, info.iIcon, ILD_TRANSPARENT);
    }

    /// <summary>释放 Extract 返回的 HICON。</summary>
    public static void Free(IntPtr hIcon)
    {
        if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
    }

    // ---------- 系统额外大图标列表(SHIL_EXTRALARGE,48px) ----------
    //
    // 缓存裸指针(永久持有一个引用,系统资源进程退出即回收),每次调用在当前线程重新 QI 出 RCW 调 GetIcon:
    // - RCW 绑定创建它的 COM apartment,跨线程复用会抛 InvalidComObjectException(UI 线程与 STA 后台线程都会取图标);
    // - 手工 vtable 调用在本项目测试宿主会 AccessViolation(见 DesktopHub.md §7,IShellFolder 同样问题)。

    private static IntPtr _extraLargeList;
    private static readonly object _imageListLock = new();

    private static IntPtr GetExtraLargeImageList()
    {
        if (_extraLargeList != IntPtr.Zero) return _extraLargeList;
        lock (_imageListLock)
        {
            if (_extraLargeList != IntPtr.Zero) return _extraLargeList;
            var iid = new Guid(IID_IImageList);
            if (SHGetImageList(SHIL_EXTRALARGE, ref iid, out var ppv) != 0 || ppv == IntPtr.Zero)
                return IntPtr.Zero;
            _extraLargeList = ppv;
            return ppv;
        }
    }

    private static IntPtr GetIconFromList(IntPtr imageList, int index, int flags)
    {
        try
        {
            var list = (IImageList)Marshal.GetObjectForIUnknown(imageList);
            if (list.GetIcon(index, flags, out var hIcon) == 0)
            {
                Marshal.FinalReleaseComObject(list); // 本调用周期内释放,RCW 不跨线程存活
                return hIcon;
            }
            Marshal.FinalReleaseComObject(list);
            return IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero; // QI 失败等异常兜底,返回空态
        }
    }

    /// <summary>IImageList 声明到用到的 GetIcon 即可,方法顺序即 vtable 顺序,不可增删调换。</summary>
    [ComImport, Guid(IID_IImageList), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
    }

    // ---------- Win32 ----------

    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint SHGFI_PIDL = 0x8;
    private const uint SHGFI_SYSICONINDEX = 0x4000;
    private const int SHIL_EXTRALARGE = 0x2;
    private const int ILD_TRANSPARENT = 0x1;
    private const string IID_IImageList = "46EB5926-582E-4017-9FDF-E8998DAA0950";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFOW
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFOW psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("shell32.dll")]
    private static extern IntPtr SHGetFileInfo(IntPtr pszPath, uint dwFileAttributes, ref SHFILEINFOW psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, IntPtr psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
