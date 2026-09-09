using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DesktopHub.App.Services;

/// <summary>当前播放快照:歌名 / 歌手 / 是否播放中 / 专辑封面(可为 null)。Title 为空 = 无会话。</summary>
public sealed record NowPlayingInfo(string Title, string Artist, bool IsPlaying, ImageSource? Artwork)
{
    public static NowPlayingInfo Empty { get; } = new("", "", false, null);
}

/// <summary>
/// 系统媒体检测:包装 SMTC(GlobalSystemMediaTransportControlsSessionManager,Win10 1903+)。
/// 聚合所有媒体会话,优先挑"正在播放"的,否则保持上一个会话(暂停态也能显示歌名)。
/// 歌名/歌手经 TryGetMediaPropertiesAsync 获取;专辑封面经 Thumbnail 流解码(按曲目缓存);
/// 播放/暂停经 TryTogglePlayPauseAsync。
/// WinRT 事件在线程池触发,结果一律经 IUiDispatcher 回 UI 线程。
/// </summary>
public sealed class MediaSessionService
{
    private readonly IUiDispatcher _dispatcher;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _current;
    private bool _started;

    // 封面缓存:同一曲目在 播放/暂停/进度 事件中反复刷新,避免重复解码
    private (string Title, string Artist)? _artworkKey;
    private ImageSource? _artworkCache;

    /// <summary>当前快照变化(UI 线程触发)。</summary>
    public event Action<NowPlayingInfo>? NowPlayingChanged;

    /// <summary>最近一次发布(任意线程读取)。</summary>
    public NowPlayingInfo Current { get; private set; } = NowPlayingInfo.Empty;

    public MediaSessionService(IUiDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>连接 SMTC 并做首次刷新。幂等;失败(旧系统/服务异常)时静默保持空态。</summary>
    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += OnManagerChanged;
            _manager.CurrentSessionChanged += OnManagerChanged;
            await RefreshAsync();
        }
        catch
        {
            // 拿不到 SMTC(个别精简系统)时保持无音乐态,不影响胶囊其余功能
        }
    }

    /// <summary>对当前会话切换 播放/暂停(无会话时忽略)。</summary>
    public async void TogglePlayPause()
    {
        try
        {
            if (_current != null)
                await _current.TryTogglePlayPauseAsync();
        }
        catch
        {
            // 会话可能已被播放器关闭
        }
    }

    private void OnManagerChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args) => Refresh();

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) => Refresh();

    /// <summary>线程池侧重算当前快照(事件与属性获取存在竞态,整轮 try-catch 兜底)。</summary>
    private void Refresh()
    {
        var _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var manager = _manager;
        if (manager == null) return;
        try
        {
            var sessions = manager.GetSessions();
            var session =
                sessions.FirstOrDefault(s =>
                    s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ?? sessions.FirstOrDefault(s => ReferenceEquals(s, _current))
                ?? sessions.FirstOrDefault();

            AttachSession(session);

            if (session == null)
            {
                _artworkKey = null;
                _artworkCache = null;
                Publish(NowPlayingInfo.Empty);
                return;
            }

            var props = await session.TryGetMediaPropertiesAsync();
            var playing = session.GetPlaybackInfo().PlaybackStatus
                          == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var key = (props.Title ?? "", props.Artist ?? "");
            ImageSource? artwork;
            if (_artworkKey == key && _artworkCache != null)
            {
                artwork = _artworkCache;
            }
            else
            {
                artwork = await TryGetArtworkAsync(props);
                _artworkKey = key;
                _artworkCache = artwork;
            }

            Publish(new NowPlayingInfo(props.Title ?? "", props.Artist ?? "", playing, artwork));
        }
        catch
        {
            // 会话在读取途中被销毁等竞态:跳过本轮,下一个事件会再次刷新
        }
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (ReferenceEquals(_current, session)) return;
        if (_current != null)
        {
            _current.PlaybackInfoChanged -= OnSessionChanged;
            _current.MediaPropertiesChanged -= OnSessionChanged;
        }
        _current = session;
        if (_current != null)
        {
            _current.PlaybackInfoChanged += OnSessionChanged;
            _current.MediaPropertiesChanged += OnSessionChanged;
        }
    }

    /// <summary>解码专辑封面缩略图(字节流 → 冻结的 BitmapImage);失败返回 null(显示音符占位)。</summary>
    private static async Task<ImageSource?> TryGetArtworkAsync(GlobalSystemMediaTransportControlsSessionMediaProperties props)
    {
        try
        {
            if (props.Thumbnail == null) return null;
            using var stream = await props.Thumbnail.OpenReadAsync();
            var size = (int)Math.Min(stream.Size, 20_000_000L);
            if (size <= 0) return null;
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)size);
            var bytes = new byte[size];
            reader.ReadBytes(bytes);

            using var ms = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            // 封面读取/解码失败(会话销毁、不支持格式):回退音符占位
            return null;
        }
    }

    private void Publish(NowPlayingInfo info)
    {
        Current = info;
        _dispatcher.BeginInvoke(() => NowPlayingChanged?.Invoke(info));
    }
}
