using System;
using System.Threading.Tasks;
using DesktopHub.App.Services;
using Xunit;

namespace DesktopHub.App.Tests;

/// <summary>SMTC 系统媒体检测冒烟:能连接管理器并完成首次刷新(有无会话取决于测试机)。</summary>
public class MediaSessionServiceTests
{
    private sealed class InlineDispatcher : IUiDispatcher
    {
        public void BeginInvoke(Action action) => action();
        public void Invoke(Action action) => action();
    }

    [Fact]
    public async Task Start_Completes_And_Publishes_Snapshot()
    {
        var service = new MediaSessionService(new InlineDispatcher());
        NowPlayingInfo? received = null;
        service.NowPlayingChanged += info => received = info;

        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        var start = service.StartAsync();
        var finished = await Task.WhenAny(start, timeout);
        Assert.Same(start, finished); // 10s 内未完成视为 SMTC 异常

        // 无断言会话内容:测试机可能在放音乐也可能没有;快照字段非空即契约成立
        Assert.NotNull(service.Current);
    }
}
