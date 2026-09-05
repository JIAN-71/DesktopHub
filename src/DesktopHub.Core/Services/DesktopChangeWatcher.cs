using System;
using System.Threading;
using DesktopHub.Shell;

namespace DesktopHub.Core.Services;

/// <summary>
/// 监听用户桌面与公共桌面的文件变化,去抖后通知上层刷新面板。
/// 只负责"有变化"这个信号,不接触文件内容。
/// </summary>
public sealed class DesktopChangeWatcher : IDisposable
{
    private readonly System.IO.FileSystemWatcher? _userWatcher;
    private readonly System.IO.FileSystemWatcher? _commonWatcher;
    private readonly Timer _debounceTimer;
    private readonly object _lock = new();
    private bool _dirty;
    private int _debounceSeconds = 2;
    private bool _started;

    /// <summary>桌面内容变化(UI 线程外触发,订阅方需自行调度)。</summary>
    public event Action? DesktopChanged;

    public DesktopChangeWatcher()
    {
        _debounceTimer = new Timer(_ => OnDebounceFired(), null, Timeout.Infinite, Timeout.Infinite);
        _userWatcher = CreateWatcher(DesktopPaths.UserDesktop);
        _commonWatcher = CreateWatcher(DesktopPaths.CommonDesktop);
        foreach (var watcher in new[] { _userWatcher, _commonWatcher })
        {
            if (watcher == null) continue;
            watcher.Created += OnFsEvent;
            watcher.Deleted += OnFsEvent;
            watcher.Renamed += OnFsRenamed;
            watcher.Error += OnFsError;
        }
    }

    private void OnFsEvent(object? sender, FileSystemEventArgs e) => Signal();
    private void OnFsRenamed(object? sender, RenamedEventArgs e) => Signal();
    private void OnFsError(object? sender, ErrorEventArgs e) => DesktopChanged?.Invoke(); // 缓冲溢出时直接刷新兜底

    public void Start(int debounceSeconds)
    {
        _debounceSeconds = Math.Max(1, debounceSeconds);
        SetWatching(true);
        _started = true;
    }

    public void Stop()
    {
        SetWatching(false);
        _started = false;
    }

    public bool IsRunning => _started;

    private void SetWatching(bool enabled)
    {
        if (_userWatcher != null) _userWatcher.EnableRaisingEvents = enabled;
        if (_commonWatcher != null) _commonWatcher.EnableRaisingEvents = enabled;
    }

    private static System.IO.FileSystemWatcher? CreateWatcher(string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path)) return null;
        var watcher = new System.IO.FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
        };
        return watcher;
    }

    private void Signal()
    {
        lock (_lock)
        {
            _dirty = true;
            _debounceTimer.Change(TimeSpan.FromSeconds(_debounceSeconds), Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceFired()
    {
        lock (_lock)
        {
            if (!_dirty) return;
            _dirty = false;
        }
        DesktopChanged?.Invoke();
    }

    public void Dispose()
    {
        Stop();
        _debounceTimer.Dispose();
        _userWatcher?.Dispose();
        _commonWatcher?.Dispose();
    }
}
