using System;
using System.Windows.Threading;

namespace DesktopHub.App.Services;

/// <summary>
/// UI 线程调度抽象:业务层(DesktopController)不再直接依赖 Application.Current.Dispatcher,
/// 便于单元测试注入假实现。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>在 UI 线程异步执行;若应用已关闭则安全忽略。</summary>
    void BeginInvoke(Action action);

    /// <summary>在 UI 线程同步执行;若应用已关闭则安全忽略。</summary>
    void Invoke(Action action);
}

/// <summary>基于 WPF Dispatcher 的默认实现。</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfUiDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public void BeginInvoke(Action action)
    {
        if (_dispatcher.HasShutdownStarted || action == null) return;
        _dispatcher.BeginInvoke(action);
    }

    public void Invoke(Action action)
    {
        if (_dispatcher.HasShutdownStarted || action == null) return;
        _dispatcher.Invoke(action);
    }
}
