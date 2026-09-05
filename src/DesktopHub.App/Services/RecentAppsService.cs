using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopHub.App.Services;

/// <summary>
/// "最近打开"记录:追踪用户经胶囊(Dock)启动的软件,最新在前、去重、最多保留 3 个。
/// 供胶囊右侧悬停扩展条展示。启动时按 recent.json 记录的 Key(Shell 解析名)从扫描结果恢复,
/// 记录变化时由 DesktopController 落盘。
/// </summary>
public sealed class RecentAppsService
{
    private const int MaxRecent = 3;
    private readonly List<DesktopIconVM> _items = new();

    /// <summary>最近打开的软件(最新在前,至多 3 个)。每次变更返回新数组,便于绑定刷新。</summary>
    public IReadOnlyList<DesktopIconVM> Recent { get; private set; } = Array.Empty<DesktopIconVM>();

    public void Record(DesktopIconVM icon)
    {
        _items.RemoveAll(i => string.Equals(i.Key, icon.Key, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, icon);
        if (_items.Count > MaxRecent)
            _items.RemoveRange(MaxRecent, _items.Count - MaxRecent);
        Recent = _items.ToArray();
    }

    /// <summary>启动恢复:按持久化的顺序(最新在前)把扫描结果中的图标装回"最近打开"。</summary>
    public void Restore(IEnumerable<DesktopIconVM> itemsNewestFirst)
    {
        _items.Clear();
        _items.AddRange(itemsNewestFirst.Take(MaxRecent));
        Recent = _items.ToArray();
    }
}
