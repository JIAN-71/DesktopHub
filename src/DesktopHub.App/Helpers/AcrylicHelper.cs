using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopHub.App.Helpers;

/// <summary>应用主题(影响亚克力着色与胶囊画刷)。</summary>
public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>
/// 胶囊窗口样式辅助。按 OS 分支应用 DWM 模糊 + 可调 tint:
///
/// Win11(≥22000):
///   1. Window.AllowsTransparency=True(WS_EX_LAYERED 分层窗口)。
///   2. SetWindowCompositionAttribute 设 ACCENT_ENABLE_ACRYLICBLURBEHIND(4),AccentFlags=0;
///      GradientColor(ABGR)控制 tint 与不透明度。
///   3. DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE=38, DWMSBT_TRANSIENTWINDOW=2)
///      —— DWM 系统级 Acrylic,原生圆角 + 无矩形外框问题。
///
/// Win10(含 22H2)——配方演进:SWCA 两条路实测都会在胶囊四角露出矩形模糊面,已全部弃用:
///   - ACCENT_ENABLE_ACRYLICBLURBEHIND(4):WinUI AcrylicBrush 私有模拟,矩形材质面不被
///     SetWindowRgn 裁剪 + 自带 ~1px 描边 → 明显矩形框(2026-09-05 上午实拍确认)。
///   - ACCENT_ENABLE_BLURBEHIND(3):模糊面同样覆盖全窗口矩形、无视 SetWindowRgn——四边中段
///     与圆角帽重合看不出来,四个角落露出被抹平的模糊角(std 13.5 vs 壁纸 39.4,2026-09-05
///     下午角落取证确认),即用户看到的"隐约矩形"。
///   现行配方:公开 API DwmEnableBlurBehindWindow + DWM_BB_BLURREGION 显式指定**圆角模糊区域**
///   (与 SetWindowRgn 同参),模糊严格贴合胶囊形状;tint 由 WPF TintOverlay 自绘(见 CreateTintBrush)。
///
/// 前提:系统"透明效果"必须开启(HKCU ...\Personalize\EnableTransparency=1),否则 DWM
/// 不渲染模糊层。
/// </summary>
public static class AcrylicHelper
{
    // ---------- 主题 tint 颜色(ABGR 中的 R/G/B 通道;Alpha 由 Opacity 决定) ----------

    /// <summary>深色主题 tint 基色(RGB),偏冷蓝的暗灰(R=0x14 G=0x17 B=0x1E,Win10 Mica 深色调)。</summary>
    private static readonly (byte R, byte G, byte B) DarkTintColor = (0x14, 0x17, 0x1E);

    /// <summary>浅色主题 tint 基色,纯白。</summary>
    private static readonly (byte R, byte G, byte B) LightTintColor = (0xFF, 0xFF, 0xFF);

    // ---------- Win32 结构体与常量 ----------

    /// <summary>SetWindowCompositionAttribute 的模糊策略枚举(未公开 API)。</summary>
    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, // 亚克力模糊(Win10 1803+)
        ACCENT_ENABLE_HOSTBACKDROP = 5,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor; // ABGR:0xAABBGGRR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute; // 19 = WCA_ACCENT_POLICY
        public IntPtr Data;
        public int SizeOfData;
    }

    private const int WCA_ACCENT_POLICY = 19;

    /// <summary>AccentFlags 位组合:2 | 4 | 8 | 1 = 0x0F 表示四边全亮;
    /// 当前固定 = 0(纯 blurbehind,不画矩形 accent 边框,保留 SetWindowRgn 圆角)。</summary>
    private const int AccentFlagsNoBorder = 0;

    // DWM 系统背景材质(Win11 22H2+)
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_TRANSIENTWINDOW = 2; // Acrylic
    private const int DWMSBT_MAINWINDOW = 1;      // Mica(本项目暂用 Acrylic 透)

    // ---------- OS 版本探测(Win10 与 Win11 的亚克力走不同配方) ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RTL_OSVERSIONINFOEXW
    {
        public uint dwOSVersionInfoSize;
        public uint dwMajorVersion;
        public uint dwMinorVersion;
        public uint dwBuildNumber;
        public uint dwPlatformId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public char[] szCSDVersion;
        public ushort wServicePackMajor;
        public ushort wServicePackMinor;
        public ushort wSuiteMask;
        public byte wProductType;
        public byte wReserved;
    }

    [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
    private static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEXW info);

    /// <summary>真实 OS build(用 RtlGetVersion,Environment.OSVersion 在 Win10 上会被兼容层谎报)。</summary>
    public static int OSBuild { get; } = GetOSBuild();

    /// <summary>Win11 起(≥22000)才支持 DWM 系统圆角 + SYSTEMBACKDROP;Win10 全部走 blurbehind 配方。</summary>
    public static bool IsWindows11OrLater => OSBuild >= 22000;

    private static int GetOSBuild()
    {
        try
        {
            var info = new RTL_OSVERSIONINFOEXW
            {
                dwOSVersionInfoSize = (uint)Marshal.SizeOf<RTL_OSVERSIONINFOEXW>(),
                szCSDVersion = new char[128],
            };
            if (RtlGetVersion(ref info) == 0) return (int)(info.dwBuildNumber & 0xFFFF);
        }
        catch
        {
            // ntdll 不可达时退回 Environment(个别精简版系统)
        }
        return Environment.OSVersion.Version.Build;
    }

    /// <summary>
    /// Win10 上模糊由公开 API <see cref="DwmEnableBlurBehindWindow"/> 按圆角区域提供(无 tint),
    /// 着色一律由 WPF TintOverlay 自绘(见 <see cref="CreateTintBrush"/>)。
    /// </summary>
    public static bool UseWpfTintOnWin10 => !IsWindows11OrLater;

    /// <summary>Win10 上由 WPF TintOverlay 接管 tint 时,把 opacity 换算为画刷 alpha 的缩放系数。</summary>
    private const double TintAlphaScale = 0.80;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>DwmEnableBlurBehindWindow 的区域参数(DwFlags 决定哪些字段生效;BOOL 用 int 封送)。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public uint DwFlags;
        public int FEnable;
        public IntPtr HRgnBlur;
        public int FTransitionOnMaximized;
    }

    private const uint DWM_BB_ENABLE = 0x1;
    private const uint DWM_BB_BLURREGION = 0x2;

    /// <summary>公开 API:按 hRgnBlur 指定模糊区域(DWM_BB_BLURREGION)。文档未声明 DWM 接管
    /// region 句柄(对照 SetWindowRgn 会明确写出"系统接管"),按调用方持有处理,调用后立即
    /// DeleteObject(业界通行做法,见 UpdateBlurRegion 注释)。</summary>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    // ---------- Win32 窗口样式常量与 P/Invoke(剥离 WS_BORDER / WS_THICKFRAME) ----------

    private const int GWL_STYLE = -16;

    // WS_BORDER  = 0x00800000 → DWM 沿窗口外侧画的 1px 矩形描边,会盖在 SetWindowRgn 圆角裁剪之上
    // WS_DLGFRAME = 0x00400000 → 对话框外框位;WS_CAPTION(0x00C00000) = WS_BORDER | WS_DLGFRAME
    // WS_THICKFRAME = 0x00040000 → 同上,部分 Win 版本(尤其 Win11)即使 WindowStyle=None 也会保留
    private const long WS_BORDER = 0x00800000L;
    private const long WS_DLGFRAME = 0x00400000L;
    private const long WS_THICKFRAME = 0x00040000L;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020; // 触发非客户区重绘,使新 GWL_STYLE 立即生效

    // GetWindowLongPtr / SetWindowLongPtr:32 位 Windows 入口为 GetWindowLong / SetWindowLong,
    // 64 位入口为 GetWindowLongPtr / SetWindowLongPtr。按 IntPtr.Size 路由,一次性覆盖两种进程位宽。
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                         : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    // ---------- 公开 API ----------

    /// <summary>
    /// 对窗口应用亚克力背景。须在 Window.SourceInitialized 或 EnsureHandle 之后调用(HWND 已创建)。
    /// 入参 <paramref name="opacity"/> 范围 [0, 1]:0 = 完全透出(几近无 tint),1 = 完全覆盖糊白
    /// (类似 Win10 任务栏全展开效果);推荐区间 0.20~0.50,即"透出壁纸 + 微 tint"。
    /// </summary>
    public static void ApplyBackdrop(Window window, AppTheme theme, double opacity, double cornerRadiusDip = 29)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        if (hwnd == IntPtr.Zero) return;

        // 保险:把 WPF 渲染目标清屏色置为透明(分层窗口下默认透明,但驱动/GPU 兜底)
        var source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget != null)
            source.CompositionTarget.BackgroundColor = Colors.Transparent;

        // 关闭:清 Accent 策略 + 关模糊面,仍重裁 SetWindowRgn 保持胶囊外形
        if (opacity <= 0.001)
        {
            DisableAccent(hwnd);
            SetBlurBehindEnabled(hwnd, false, IntPtr.Zero);
            UpdateRoundedRegion(window, cornerRadiusDip);
            return;
        }

        if (!IsWindows11OrLater)
        {
            // Win10:SWCA 两条路(state=4 材质 / state=3 模糊)的 surface 都是全窗口矩形、
            // 不被 SetWindowRgn 裁剪(胶囊四角露出"隐约矩形",2026-09-05 角落取证),
            // 一律弃用;改公开 API DwmEnableBlurBehindWindow 显式指定圆角模糊区域,
            // 着色由 WPF TintOverlay 自绘(见 CreateTintBrush / UseWpfTintOnWin10)。
            DisableAccent(hwnd);
            UpdateRoundedRegion(window, cornerRadiusDip);
            UpdateBlurRegion(window, cornerRadiusDip);
            return;
        }

        // Win11:SWCA 亚克力 + DWM 系统 backdrop(支持原生圆角,无矩形框问题)
        var clampedOpacity = Math.Clamp(opacity, 0.0, 1.0);
        if (clampedOpacity < 0.01) clampedOpacity = 0.01;
        var alpha = (byte)Math.Round(clampedOpacity * byte.MaxValue);
        var (r, g, b) = theme == AppTheme.Light ? LightTintColor : DarkTintColor;
        var gradientColor = unchecked((alpha << 24) | (b << 16) | (g << 8) | r);

        var policy = new AccentPolicy
        {
            AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = AccentFlagsNoBorder,
            GradientColor = gradientColor,
            AnimationId = 0,
        };

        var size = Marshal.SizeOf(policy);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = ptr,
                SizeOfData = size,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        // Win11 22H2+ 兼容增强:同时挂 DWMWA_SYSTEMBACKDROP_TYPE=TRANSIENTWINDOW
        // (旧 Win11 忽略错误码,Accent 已兜底)
        try
        {
            var backdrop = DWMSBT_TRANSIENTWINDOW;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        catch
        {
            // 旧系统 dwmapi 不支持,忽略
        }

        // 末尾:强制重设一次 SetWindowRgn(SetWindowCompositionAttribute / DwmSetWindowAttribute
        // 内部会触发 DWM 重合成,常会重置窗口 region;每次 settings 改 Opacity/Theme/启用都会
        // 再走一遍,胶囊永远跟随状态。
        UpdateRoundedRegion(window, cornerRadiusDip);
    }

    /// <summary>
    /// 给 ThemeManager.AcrylicChanged 用的便捷代理:无参,把"主题+不透明度+启用"打成
    /// ApplyBackdrop 调用。订阅时直接 `() => ApplyBackdrop(this, ThemeManager.CurrentTheme,
    /// ThemeManager.AcrylicOpacity)` 即可,本方法保留以兼容简单接线的代码风格。
    /// </summary>
    public static void ApplyBackdrop(Window window)
        => ApplyBackdrop(window, ThemeManager.CurrentTheme, ThemeManager.AcrylicOpacity);

    /// <summary>
    /// 生成 Win10 blurbehind 配方下 WPF 层要自绘的 tint 画刷:颜色取主题基色(浅=白,深=冷灰蓝),
    /// alpha = opacity × 255 × <see cref="TintAlphaScale"/>,与原先 DWM GradientColor 提供的
    /// "tint 深浅随 AcrylicOpacity 变化"的观感保持一致。opacity≈0 或禁用时返回全透明。
    /// </summary>
    public static SolidColorBrush CreateTintBrush(AppTheme theme, double opacity)
    {
        if (opacity <= 0.001) return Brushes.Transparent;
        var clamped = Math.Clamp(opacity, 0.0, 1.0);
        var alpha = (byte)Math.Min(byte.MaxValue, Math.Round(clamped * byte.MaxValue * TintAlphaScale));
        var (r, g, b) = theme == AppTheme.Light ? LightTintColor : DarkTintColor;
        // 拖动不透明度滑杆时每次 tick 都会新建画刷,冻结以允许 WPF 跨线程复用并减免开销
        var brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static void DisableAccent(IntPtr hwnd)
    {
        var policy = new AccentPolicy { AccentState = AccentState.ACCENT_DISABLED };
        var size = Marshal.SizeOf(policy);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = ptr,
                SizeOfData = size,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// 按物理像素给窗口裁剪圆角矩形区域,裁出胶囊外形(亚克力模糊层跟随区域形状)。
    /// 窗口尺寸/圆角变化时需重新调用(动画期间每次 SizeChanged 都会触发)。
    /// </summary>
    /// <param name="cornerRadiusDip">圆角半径(DIP 单位,与 WPF CornerRadius 一致)。</param>
    public static void UpdateRoundedRegion(Window window, double cornerRadiusDip)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var scale = VisualTreeHelper.GetDpi(window).PixelsPerDip;
        if (scale <= 0) scale = 1.0;
        var w = (int)Math.Round(window.ActualWidth * scale);
        var h = (int)Math.Round(window.ActualHeight * scale);
        if (w <= 0 || h <= 0) return;
        var d = (int)Math.Round(cornerRadiusDip * 2 * scale);

        var region = CreateRoundRectRgn(0, 0, w + 1, h + 1, d, d);
        if (region == IntPtr.Zero) return;
        // SetWindowRgn 成功后系统接管 region 句柄(无需 DeleteObject);失败则自行释放
        if (SetWindowRgn(hwnd, region, true) == 0)
            DeleteObject(region);
    }

    /// <summary>
    /// 给窗口挂圆角模糊区域(公开 API DwmEnableBlurBehindWindow,与 SetWindowRgn 同参同坐标系)。
    /// DWM_BB_BLURREGION 的模糊严格按 hRgnBlur 形状渲染——胶囊四角之外不再有矩形模糊面。
    /// 窗口尺寸/圆角变化时需重新调用(PillWindow 的 SizeChanged / SetCorner 均会触发)。
    /// region 句柄所有权:文档未声明 DWM 接管(SetWindowRgn 有明确声明),按调用方持有处理,
    /// 调用后立即 DeleteObject——与主流实现(Chromium 旧 Aero 路径等)一致。
    /// </summary>
    public static void UpdateBlurRegion(Window window, double cornerRadiusDip)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var scale = VisualTreeHelper.GetDpi(window).PixelsPerDip;
        if (scale <= 0) scale = 1.0;
        var w = (int)Math.Round(window.ActualWidth * scale);
        var h = (int)Math.Round(window.ActualHeight * scale);
        if (w <= 0 || h <= 0) return;
        var d = (int)Math.Round(cornerRadiusDip * 2 * scale);

        var region = CreateRoundRectRgn(0, 0, w + 1, h + 1, d, d);
        if (region == IntPtr.Zero) return;
        try
        {
            SetBlurBehindEnabled(hwnd, true, region);
        }
        finally
        {
            DeleteObject(region);
        }
    }

    /// <summary>挂起/关闭模糊面。enabled=false 时只清 DWM_BB_ENABLE(亚克力关闭或透明度为 0)。</summary>
    private static void SetBlurBehindEnabled(IntPtr hwnd, bool enabled, IntPtr region)
    {
        var blur = new DWM_BLURBEHIND
        {
            DwFlags = enabled ? DWM_BB_ENABLE | DWM_BB_BLURREGION : DWM_BB_ENABLE,
            FEnable = enabled ? 1 : 0,
            HRgnBlur = region,
            FTransitionOnMaximized = 0,
        };
        DwmEnableBlurBehindWindow(hwnd, ref blur);
    }

    /// <summary>
    /// 剥离窗口的 WS_BORDER / WS_DLGFRAME / WS_THICKFRAME 系统样式,避免 DWM 在窗口外侧画
    /// 1px 矩形外框(WS_CAPTION = WS_BORDER | WS_DLGFRAME,一并清除即覆盖标题栏样式残留)。
    /// WPF 的 WindowStyle=None 只清 WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX |
    /// WS_MAXIMIZEBOX,并不动 WS_BORDER;且部分 Win 版本/驱动会在 InitializeComponent 之后
    /// 重置默认值。此处直接动 Win32 GWL_STYLE 清掉所有边框位,确保 SetWindowRgn 的圆角裁剪
    /// 不被任何系统矩形覆盖。
    /// 须在 Window.SourceInitialized / Loaded 之后调用(HWND 已创建)。
    /// </summary>
    public static void StripSystemBorder(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        if (hwnd == IntPtr.Zero) return;

        var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        var newStyle = new IntPtr(style & ~WS_BORDER & ~WS_DLGFRAME & ~WS_THICKFRAME);
        SetWindowLongPtr(hwnd, GWL_STYLE, newStyle);

        // SWP_FRAMECHANGED 触发非客户区重绘,新 GWL_STYLE 才会在画面上立即生效
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>
    /// 读取系统当前深浅色设置(HKCU\...\Personalize 的 AppsUseLightTheme)。
    /// 读不到时按深色处理(与桌面端工具类应用默认观感一致)。
    /// </summary>
    public static AppTheme DetectSystemTheme()
    {
        try
        {
            var value = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 0);
            return value is int i && i != 0 ? AppTheme.Light : AppTheme.Dark;
        }
        catch
        {
            return AppTheme.Dark;
        }
    }
}
