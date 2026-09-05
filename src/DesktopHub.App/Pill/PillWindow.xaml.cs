using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DesktopHub.App.Helpers;
using DesktopHub.App.Services;

namespace DesktopHub.App.Pill;

/// <summary>
/// 灵动岛式胶囊窗。三态交互:
/// - Small:小胶囊常驻屏幕顶部居中(随鼠标所在屏幕移动),显示当前时间;
/// - Recent:悬停/触碰时向右展开一条"最近打开"扩展条(不打开面板);
/// - Expanded:点击胶囊后展开为分组图标面板,鼠标离开后自动收起。
/// 本类只保留纯 UI 行为(状态机/动画/定位/时钟);数据与命令绑定到 <see cref="PillViewModel"/>。
/// </summary>
public partial class PillWindow : Window
{
    private const double SmallWidth = 240, SmallHeight = 58;
    private const double RecentWidth = 412, RecentHeight = 58;
    private const double ExpandedWidth = 640, ExpandedHeight = 580;
    private const int AnimationMs = 280;

    // 三段式形变动画时长(收起/展开共用,保证节奏对称)
    private const int HeightStageMs = 200; // 高度段:面板向上压缩 / 向下展开
    private const int WidthStageMs = 220;  // 宽度段:两侧向中间收缩 / 向外展开
    private const int SettleStageMs = 140; // 收尾段:合拢小胶囊,内容淡入淡出

    private enum PillState { Small, Recent, Expanded }

    private readonly PillViewModel _viewModel;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _clockTimer;
    private Rect _workArea;
    private PillState _state = PillState.Small;
    private int _animSeq; // 动画代数:新一轮状态切换作废旧动画的 Completed 回调

    // 复用单个裁剪几何实例,动画期间只改 Rect/半径,避免逐帧分配
    private readonly RectangleGeometry _clipGeometry = new(new Rect(0, 0, 1, 1));

    public PillWindow(DesktopController controller)
    {
        InitializeComponent();
        _viewModel = new PillViewModel(controller);
        DataContext = _viewModel;

        // WPF Window 即便 WindowStyle=None,部分 Win 版本/驱动仍会保留 Win32 WS_BORDER / WS_THICKFRAME
        // 系统样式,DWM 据此沿窗口外侧画 1px 矩形外框。XAML 已设 BorderBrush={x:Null} + BorderThickness=0
        // (兜底 WPF 层),此处 code-behind 再兜底一次;真正的 Win32 样式剥离在 SourceInitialized
        // 走 AcrylicHelper.StripSystemBorder。
        BorderBrush = null;
        BorderThickness = new Thickness(0);

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _collapseTimer.Tick += (_, _) => { _collapseTimer.Stop(); Collapse(); };

        // 时钟:每秒刷新小胶囊时间,同时检查鼠标是否已跨屏(跨屏跟随)
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            _viewModel.TimeText = DateTime.Now.ToString("HH:mm");
            FollowCursorScreen();
        };
        _clockTimer.Start();
        _viewModel.TimeText = DateTime.Now.ToString("HH:mm");

        MouseEnter += (_, _) => { _collapseTimer.Stop(); if (_state == PillState.Small) ExpandToRecent(); };
        MouseLeave += (_, _) => _collapseTimer.Start();

        PositionPill();
        IsVisibleChanged += (_, _) => _viewModel.RefreshStatus();
        PillBorder.Clip = _clipGeometry;
        PillBorder.SizeChanged += (_, _) => { UpdateClip(); UpdateWindowRegion(); };
        UpdateClip();

        // 真亚克力:HWND 创建后立即挂 Accent/BlurBehind 策略(此时窗口尚未显示,无闪烁),
        // 并按初始尺寸裁出胶囊圆角区域。窗口须为 WS_EX_LAYERED(AllowsTransparency=True)。
        // 透明度 / 主题 / 启用由 ThemeManager.* 状态驱动,settings 改动即广播重设。
        SourceInitialized += (_, _) =>
        {
            // 显式剥离 Win32 WS_BORDER / WS_DLGFRAME / WS_THICKFRAME:WindowStyle=None 不动这些位,
            // 不清掉 DWM 就会在窗口外侧画 1px 矩形外框,盖在 SetWindowRgn 圆角裁剪之上。
            AcrylicHelper.StripSystemBorder(this);
            UpdateWindowRegion();
            RefreshBackdrop();
        };
        // 主题 / 透明度 / 启用 任一变更都重挂背景策略 + 同步 Win10 自绘 tint(确保 settings 改值实时生效)
        ThemeManager.ThemeChanged += _ => RefreshBackdrop();
        ThemeManager.AcrylicChanged += () => RefreshBackdrop();
        Loaded += (_, _) => RefreshBackdrop();
    }

    /// <summary>
    /// 重挂 DWM 背景策略并同步内容层 tint:
    ///  - Win11:Accent 亚克力 + DWM 系统 backdrop,GradientColor 自带 tint,不动 TintOverlay;
    ///  - Win10(blurbehind 配方):DWM 只负责按圆角区域模糊(无 tint、无矩形边缘),色调由
    ///    TintOverlay 以 AcrylicOpacity 为 alpha 自绘,观感与旧配方一致。
    /// 关闭(AcrylicEnabled=false)或 opacity≈0 时传 0,ApplyBackdrop 下 ACCENT_DISABLED、tint 全透明。
    /// </summary>
    private void RefreshBackdrop()
    {
        var opacity = ThemeManager.AcrylicEnabled ? ThemeManager.AcrylicOpacity : 0.0;
        // 传入当前圆角:ApplyBackdrop 末尾会重设窗口 region,若用默认 29,
        // 展开态(r=18)下收到主题/透明度广播会把面板圆角改回 29
        AcrylicHelper.ApplyBackdrop(this, ThemeManager.CurrentTheme, opacity, PillBorder.CornerRadius.TopLeft);
        if (AcrylicHelper.UseWpfTintOnWin10)
            TintOverlay.Background = AcrylicHelper.CreateTintBrush(ThemeManager.CurrentTheme, opacity);
    }

    /// <summary>按当前尺寸/圆角裁剪 HWND 圆角区域并同步 DWM 模糊区域,
    /// 让窗口外形与模糊都严格贴合胶囊(SetWindowRgn + DwmEnableBlurBehindWindow)。</summary>
    private void UpdateWindowRegion()
    {
        var corner = PillBorder.CornerRadius.TopLeft;
        AcrylicHelper.UpdateRoundedRegion(this, corner);
        AcrylicHelper.UpdateBlurRegion(this, corner);
    }

    // ---------- 定位 ----------

    private void PositionPill()
    {
        _workArea = ScreenInterop.GetWorkAreaAtCursor();
        // 按小胶囊宽度居中,修正此前按展开宽度预留导致的偏移
        Left = CenterLeft(SmallWidth);
        Top = _workArea.Top + 8;
    }

    private double CenterLeft(double width) => _workArea.Left + (_workArea.Width - width) / 2;

    /// <summary>跨屏跟随:鼠标移到另一块屏幕时,把胶囊平移到该屏顶部居中(展开面板态不打扰)。</summary>
    private void FollowCursorScreen()
    {
        if (_state == PillState.Expanded) return;
        var workArea = ScreenInterop.GetWorkAreaAtCursor();
        if (workArea.Equals(_workArea)) return;
        _workArea = workArea;
        AnimateMove(CenterLeft(SmallWidth), workArea.Top + 8);
    }

    /// <summary>平移窗口(不改变尺寸),用于跨屏跟随。Completed 同样先订阅再 BeginAnimation(见 AnimateSizePos)。</summary>
    private void AnimateMove(double toLeft, double toTop)
    {
        var seq = _animSeq;
        var duration = TimeSpan.FromMilliseconds(AnimationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var leftAnim = new DoubleAnimation(toLeft, duration) { EasingFunction = ease };
        var topAnim = new DoubleAnimation(toTop, duration) { EasingFunction = ease };
        leftAnim.Completed += (_, _) =>
        {
            if (seq != _animSeq) return;
            Left = toLeft;
            BeginAnimation(LeftProperty, null);
        };
        topAnim.Completed += (_, _) =>
        {
            if (seq != _animSeq) return;
            Top = toTop;
            BeginAnimation(TopProperty, null);
        };
        BeginAnimation(LeftProperty, leftAnim);
        BeginAnimation(TopProperty, topAnim);
    }

    /// <summary>按当前圆角裁剪内容,保证图标不超出胶囊圆角轮廓(复用几何实例,零分配)。</summary>
    private void UpdateClip()
    {
        if (PillBorder.ActualWidth <= 0 || PillBorder.ActualHeight <= 0) return;
        var r = PillBorder.CornerRadius.TopLeft;
        _clipGeometry.RadiusX = r;
        _clipGeometry.RadiusY = r;
        _clipGeometry.Rect = new Rect(0, 0, PillBorder.ActualWidth, PillBorder.ActualHeight);
    }

    // ---------- 状态切换(纯 UI 动画) ----------

    /// <summary>Small → Recent:窗口向右展开一条最近打开扩展条,不打开面板。</summary>
    private void ExpandToRecent()
    {
        _state = PillState.Recent;
        _animSeq++;
        RecentPanel.Visibility = Visibility.Visible;
        AnimateSizePos(RecentWidth, RecentHeight); // 左缘锚定,只向右展开
        AnimateOpacity(RecentPanel, 1);
    }

    /// <summary>Small / Recent → Expanded:点击胶囊展开完整面板。
    /// 与收起镜像的两段式:先两侧向外展开为长条胶囊,再向下展开为完整面板。</summary>
    private void Expand()
    {
        _state = PillState.Expanded;
        _animSeq++;
        var seq = _animSeq;
        // 阶段一:胶囊两侧同时向外展开为长条(宽度放大 + 中心不动)
        AnimateOpacity(SmallPanel, 0, WidthStageMs);
        AnimateOpacity(RecentPanel, 0, WidthStageMs);
        AnimateSizePos(ExpandedWidth, RecentHeight, CenterLeft(ExpandedWidth),
            WidthStageMs, EasingMode.EaseInOut, () =>
        {
            if (seq != _animSeq || _state != PillState.Expanded) return;
            // 阶段二:长条胶囊向下展开为完整面板(顶边锚定)
            SetCorner(18);
            ExpandedPanel.Visibility = Visibility.Visible;
            AnimateOpacity(ExpandedPanel, 1, HeightStageMs);
            AnimateSizePos(ExpandedWidth, ExpandedHeight, null, HeightStageMs, EasingMode.EaseOut);
            _viewModel.RefreshStatus();
        });
    }

    /// <summary>Recent / Expanded → 收起:按状态分发(面板三段式形变,扩展条直接缩回)。</summary>
    private void Collapse()
    {
        switch (_state)
        {
            case PillState.Expanded: CollapseStageHeight(); break;
            case PillState.Recent: CollapseToSmall(); break;
        }
    }

    /// <summary>收起阶段一:面板整体向上收起——高度逐渐压缩,过渡为长条形胶囊(宽度不变,顶边锚定)。</summary>
    private void CollapseStageHeight()
    {
        _state = PillState.Recent;
        _animSeq++;
        var seq = _animSeq;
        SetCorner(29); // 收起全程保持胶囊圆角一致
        AnimateOpacity(ExpandedPanel, 0, HeightStageMs);
        AnimateSizePos(ExpandedWidth, RecentHeight, null,
            HeightStageMs, EasingMode.EaseInOut, () =>
        {
            if (seq != _animSeq || _state != PillState.Recent) return;
            ExpandedPanel.Visibility = Visibility.Hidden;
            CollapseStageWidth(seq);
        });
    }

    /// <summary>收起阶段二:长条胶囊左右两侧同时向中间收缩(宽度收窄 + Left 同步移动,中心保持不动)。</summary>
    private void CollapseStageWidth(int seq)
    {
        AnimateSizePos(SmallWidth, SmallHeight, CenterLeft(SmallWidth),
            WidthStageMs, EasingMode.EaseInOut, () =>
        {
            if (seq != _animSeq || _state != PillState.Recent) return;
            CollapseStageSettle();
        });
    }

    /// <summary>收起阶段三:合拢为小胶囊——时间文本淡入收尾,作为收起后的最终形态。</summary>
    private void CollapseStageSettle()
    {
        _state = PillState.Small;
        AnimateOpacity(SmallPanel, 1, SettleStageMs);
        RecentPanel.Visibility = Visibility.Collapsed;
        ExpandedPanel.Visibility = Visibility.Hidden;
    }

    /// <summary>悬停扩展条直接收起:Recent → Small(左缘锚定,单段缩回)。</summary>
    private void CollapseToSmall()
    {
        _state = PillState.Small;
        _animSeq++;
        AnimateSizePos(SmallWidth, SmallHeight); // Left 未变,只收窄宽度
        AnimateOpacity(RecentPanel, 0);
        var seq = _animSeq;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AnimationMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (seq != _animSeq || _state != PillState.Small) return;
            RecentPanel.Visibility = Visibility.Collapsed;
            ExpandedPanel.Visibility = Visibility.Hidden;
        };
        timer.Start();
    }

    /// <summary>动画窗口尺寸(可选横向移动),动画完成后固化终值、清动画并触发链式回调。
    /// 链式阶段共享同一代 _animSeq,任何新状态切换都会作废旧阶段链。</summary>
    private void AnimateSizePos(double toWidth, double toHeight, double? toLeft = null,
        int durationMs = AnimationMs, EasingMode easingMode = EasingMode.EaseOut, Action? onCompleted = null)
    {
        var seq = _animSeq;
        var ease = new CubicEase { EasingMode = easingMode };
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var widthAnim = new DoubleAnimation(toWidth, duration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(toHeight, duration) { EasingFunction = ease };
        // 关键:Completed 必须在 BeginAnimation 之前订阅——时钟一旦分配,时间线的 Completed 不再派发给后订阅者
        widthAnim.Completed += (_, _) =>
        {
            if (seq != _animSeq) return;
            Width = toWidth;
            Height = toHeight;
            if (toLeft is { } l) Left = l;
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            onCompleted?.Invoke();
        };
        this.BeginAnimation(WidthProperty, widthAnim);
        this.BeginAnimation(HeightProperty, heightAnim);
        if (toLeft is { } left)
            this.BeginAnimation(LeftProperty, new DoubleAnimation(Left, left, duration) { EasingFunction = ease });
    }

    private void AnimateOpacity(UIElement element, double to, int? durationMs = null)
    {
        element.BeginAnimation(OpacityProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(durationMs ?? AnimationMs / 2)));
    }

    /// <summary>直接设置胶囊圆角并同步更新裁剪,避免离散关键帧动画造成的轮廓闪烁。</summary>
    private void SetCorner(double radius)
    {
        PillBorder.CornerRadius = new CornerRadius(radius);
        UpdateClip();
        UpdateWindowRegion();
    }

    // ---------- 交互 ----------

    /// <summary>点击胶囊(时间区或最近扩展条)→ 展开完整面板。</summary>
    private void Capsule_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != PillState.Expanded) Expand();
    }

    /// <summary>点击表情球 → 自旋一圈(不冒泡,不触发胶囊展开)。</summary>
    private void EmotionBall_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        EmotionBall.Spin(1);
    }

    /// <summary>点击最近打开图标 → 启动(单击),并阻止冒泡触发胶囊展开。</summary>
    private void RecentTile_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: DesktopIconVM icon })
            _viewModel.LaunchCommand.Execute(icon);
    }

    /// <summary>面板内图标双击启动(事件转发给 VM 命令)。</summary>
    private void Tile_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: DesktopIconVM icon })
            _viewModel.LaunchCommand.Execute(icon);
    }
}
