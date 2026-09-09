using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopHub.Core.Services;

namespace DesktopHub.App.Services;

/// <summary>
/// 胶囊窗 ViewModel:分组列表、当前时间、最近打开、隐藏图标按钮文本,
/// 以及刷新/隐藏/启动命令。code-behind 只保留动画与窗口定位。
/// </summary>
public sealed partial class PillViewModel : ObservableObject, IDisposable
{
    private readonly DesktopController _controller;

    [ObservableProperty]
    private IReadOnlyList<IconGroup<DesktopIconVM>> _groups = Array.Empty<IconGroup<DesktopIconVM>>();

    /// <summary>小胶囊显示的当前时间(如 "14:08"),由 code-behind 时钟定时刷新。</summary>
    [ObservableProperty]
    private string _timeText = "";

    /// <summary>最近打开的软件(悬停扩展条,至多 3 个)。</summary>
    [ObservableProperty]
    private IReadOnlyList<DesktopIconVM> _recentApps = Array.Empty<DesktopIconVM>();

    /// <summary>是否有最近打开记录(无记录时扩展条显示占位提示)。</summary>
    [ObservableProperty]
    private bool _hasRecent;

    /// <summary>展开面板"隐藏/显示桌面图标"按钮文本(现为图标的 ToolTip)。</summary>
    [ObservableProperty]
    private string _toggleLabel = "隐藏桌面图标";

    /// <summary>桌面图标当前是否处于隐藏态(驱动睁眼/闭眼图标切换)。</summary>
    [ObservableProperty]
    private bool _desktopIconsHidden;

    /// <summary>刷新按钮反馈文本(刷新中显示"已刷新"1 秒,现为图标的 ToolTip)。</summary>
    [ObservableProperty]
    private string _refreshLabel = "刷新";

    /// <summary>音乐模式:歌名(无会话时显示占位提示)。</summary>
    [ObservableProperty]
    private string _nowPlayingTitle = "暂无正在播放的音乐";

    /// <summary>音乐模式:歌手名。</summary>
    [ObservableProperty]
    private string _nowPlayingArtist = "";

    /// <summary>音乐模式:专辑封面(无则显示音符占位)。</summary>
    [ObservableProperty]
    private System.Windows.Media.ImageSource? _nowPlayingArtwork;

    /// <summary>音乐模式:是否播放中(驱动 暂停/播放 图标)。</summary>
    [ObservableProperty]
    private bool _isMusicPlaying;

    /// <summary>音乐模式:是否存在媒体会话(决定播放/暂停按钮显隐)。</summary>
    [ObservableProperty]
    private bool _hasNowPlaying;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand ToggleIconsCommand { get; }
    public IRelayCommand<DesktopIconVM> LaunchCommand { get; }
    public IRelayCommand ShowSettingsCommand { get; }
    public IRelayCommand TogglePlayPauseCommand { get; }

    public PillViewModel(DesktopController controller, Action? showSettingsRequested = null,
        MediaSessionService? mediaSessions = null)
    {
        _controller = controller;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ToggleIconsCommand = new RelayCommand(ToggleIcons);
        LaunchCommand = new RelayCommand<DesktopIconVM>(Launch);
        ShowSettingsCommand = new RelayCommand(() => showSettingsRequested?.Invoke());
        TogglePlayPauseCommand = new RelayCommand(() => mediaSessions?.TogglePlayPause());
        _controller.StatusChanged += OnStatusChanged;
        if (mediaSessions != null)
        {
            _media = mediaSessions;
            _media.NowPlayingChanged += OnNowPlayingChanged;
        }
    }

    private readonly MediaSessionService? _media;

    private void OnNowPlayingChanged(NowPlayingInfo info)
    {
        HasNowPlaying = !string.IsNullOrEmpty(info.Title);
        NowPlayingTitle = HasNowPlaying ? info.Title : "暂无正在播放的音乐";
        NowPlayingArtist = info.Artist;
        IsMusicPlaying = info.IsPlaying;
        NowPlayingArtwork = info.Artwork;
    }

    /// <summary>从控制器读取最新分组与最近打开并刷新绑定属性(UI 线程调用)。</summary>
    public void RefreshStatus()
    {
        var (groups, _) = _controller.GetGroups();
        Groups = groups;
        RecentApps = _controller.RecentApps;
        HasRecent = RecentApps.Count > 0;
        DesktopIconsHidden = _controller.AreDesktopIconsHidden();
        ToggleLabel = DesktopIconsHidden ? "显示桌面图标" : "隐藏桌面图标";
    }

    private void OnStatusChanged() => RefreshStatus();

    private async Task RefreshAsync()
    {
        RefreshLabel = "已刷新";
        _controller.RefreshAsync(); // 后台重扫,完成后经 StatusChanged 刷新面板
        await Task.Delay(1000);
        RefreshLabel = "刷新";
    }

    private void ToggleIcons()
    {
        _controller.ToggleDesktopIcons();
        RefreshStatus();
    }

    private void Launch(DesktopIconVM? icon)
    {
        if (icon == null) return;
        _controller.RecordRecentLaunch(icon);
        icon.Launch();
    }

    public void Dispose()
    {
        _controller.StatusChanged -= OnStatusChanged;
        if (_media != null) _media.NowPlayingChanged -= OnNowPlayingChanged;
    }
}
