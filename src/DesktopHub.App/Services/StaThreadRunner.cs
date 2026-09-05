using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DesktopHub.App.Services;

/// <summary>
/// 专用 STA 工作线程,用于执行需要 STA COM 互操作(Shell.Application / WScript.Shell)
/// 的后台任务(桌面扫描、图标提取)。投递任务串行执行,避免在 UI 线程阻塞。
/// 所有任务异常在循环内兜底,线程持续服务。
/// </summary>
public sealed class StaThreadRunner : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private bool _disposed;

    public StaThreadRunner()
    {
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "DesktopHub.STA",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>异步投递一个任务到 STA 线程执行(不等待完成)。</summary>
    public void Post(Action action)
    {
        if (_disposed || action == null) return;
        _queue.Add(action);
    }

    /// <summary>投递并阻塞等待任务完成(调用方必须是非 STA 线程,避免死锁)。</summary>
    public void Invoke(Action action)
    {
        if (_disposed || action == null) return;
        using var done = new ManualResetEventSlim();
        _queue.Add(() =>
        {
            try { action(); }
            finally { done.Set(); }
        });
        done.Wait();
    }

    private void Loop()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try { action(); }
            catch
            {
                // 单个任务失败不影响线程持续服务;上层另有兜底
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
    }
}
