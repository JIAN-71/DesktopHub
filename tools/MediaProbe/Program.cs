// SMTC 探针(开发工具,不在 .sln 内):注册一个真实的系统媒体会话,模拟播放器行为——
// 元数据(歌名/歌手/封面)、播放状态、时间轴推进、响应 播放/暂停 按钮。
// 用于端到端验证胶囊的音乐模式(自动切换/封面/歌词同步/播放暂停),全程不发声。
// 运行: dotnet run --project tools/MediaProbe
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media;
using Windows.Storage.Streams;

internal static class Program
{
    private static readonly (string Title, string Artist, TimeSpan Duration)[] Songs =
    {
        ("晴天", "周杰伦", TimeSpan.FromSeconds(269)),
        ("七里香", "周杰伦", TimeSpan.FromSeconds(289)),
    };

    private static SystemMediaTransportControls? _smtc;
    private static SystemMediaTransportControlsDisplayUpdater? _updater;
    private static DispatcherTimer? _positionTimer;
    private static TextBlock? _status;
    private static bool _playing = true;
    private static int _songIndex;
    private static TimeSpan _position;

    [STAThread]
    private static void Main()
    {
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
            app.Startup += (_, _) => BuildUi();
            app.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "MediaProbe 启动失败");
        }
    }

    private static void BuildUi()
    {
        var win = new Window
        {
            Title = "SMTC Probe",
            Width = 300,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        _status = new TextBlock { Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap, Text = "初始化..." };
        var toggle = new Button { Content = "播放/暂停", Margin = new Thickness(12, 0, 12, 6), Padding = new Thickness(8, 4, 8, 4) };
        var next = new Button { Content = "下一首", Padding = new Thickness(8, 4, 8, 4) };
        toggle.Click += (_, _) => SetPlaying(!_playing);
        next.Click += (_, _) => NextSong();
        var stack = new StackPanel();
        stack.Children.Add(_status);
        stack.Children.Add(toggle);
        stack.Children.Add(next);
        win.Content = stack;
        win.Loaded += async (_, _) => await InitAsync(win);
        win.Show();
    }

    private static async Task InitAsync(Window win)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(win).Handle;
        _smtc = SystemMediaTransportControlsInterop.GetForWindow(hwnd);
        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
        _smtc.ButtonPressed += (_, args) =>
        {
            if (args.Button == SystemMediaTransportControlsButton.Play) SetPlaying(true);
            else if (args.Button == SystemMediaTransportControlsButton.Pause) SetPlaying(false);
        };
        _updater = _smtc.DisplayUpdater;
        _updater.Type = MediaPlaybackType.Music;

        ApplySong();

        // 时间轴推进:模拟播放进度(歌词同步依赖 Position)
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _positionTimer.Tick += (_, _) =>
        {
            if (!_playing) return;
            _position += _positionTimer.Interval;
            if (_position > Songs[_songIndex].Duration)
            {
                NextSong();
                return;
            }
            UpdateTimeline();
        };
        _positionTimer.Start();
        await Task.CompletedTask;
    }

    private static void NextSong()
    {
        _songIndex = (_songIndex + 1) % Songs.Length;
        _position = TimeSpan.Zero;
        ApplySong();
    }

    private static void ApplySong()
    {
        var song = Songs[_songIndex];
        if (_updater != null)
        {
            _updater.Type = MediaPlaybackType.Music;
            _updater.MusicProperties.Title = song.Title;
            _updater.MusicProperties.Artist = song.Artist;
            _updater.MusicProperties.AlbumTitle = "DesktopHub Probe";
            _updater.Thumbnail = MakeCoverReference(song.Title);
            _updater.Update();
        }
        UpdateTimeline();
        SetPlaying(_playing);
    }

    /// <summary>生成封面 PNG 到 %TEMP%,以文件引用提供给 SMTC(最接近真实播放器的路径)。</summary>
    private static RandomAccessStreamReference MakeCoverReference(string title)
    {
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            var brush = new LinearGradientBrush(Colors.SteelBlue, Colors.MidnightBlue, 90);
            ctx.DrawRectangle(brush, null, new Rect(0, 0, 200, 200));
            var text = title.Length > 0 ? title[..1] : "?";
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei"), 96, Brushes.White,
                VisualTreeHelper.GetDpi(visual).PixelsPerDip);
            ctx.DrawText(ft, new Point(100 - ft.Width / 2, 100 - ft.Height / 2));
        }
        var rtb = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        var path = Path.Combine(Path.GetTempPath(), "DesktopHubCover.png");
        using (var fs = File.Create(path))
        {
            encoder.Save(fs);
        }
        var file = Windows.Storage.StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult();
        return RandomAccessStreamReference.CreateFromFile(file);
    }

    private static void UpdateTimeline()
    {
        if (_smtc == null) return;
        _smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
        {
            Position = _position,
            StartTime = TimeSpan.Zero,
            EndTime = Songs[_songIndex].Duration,
            MinSeekTime = TimeSpan.Zero,
            MaxSeekTime = Songs[_songIndex].Duration,
        });
    }

    private static void SetPlaying(bool playing)
    {
        _playing = playing;
        if (_smtc != null)
            _smtc.PlaybackStatus = playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
        UpdateTimeline();
        if (_status != null)
            _status.Text = $"playing={_playing}\nsong={Songs[_songIndex].Title} - {Songs[_songIndex].Artist}\nposition={_position:mm\\:ss}";
    }

    /// <summary>程序化画一张 200x200 封面(渐变底 + 歌名首字),编码 PNG 写入内存流。</summary>
}
