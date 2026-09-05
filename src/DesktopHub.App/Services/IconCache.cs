using System;
using System.Collections.Concurrent;
using System.Windows.Media;

namespace DesktopHub.App.Services;

/// <summary>
/// 桌面图标缓存:按 Shell 解析名(ParsingName)缓存冻结的 ImageSource,
/// 避免每次展开/刷新重复调用 SHGetFileInfo 提取并重建位图。
/// 键不区分大小写;冻结的 ImageSource 可安全跨线程使用。
/// </summary>
public sealed class IconCache
{
    private readonly ConcurrentDictionary<string, ImageSource> _map = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _map.Count;

    /// <summary>取缓存;未命中时用 factory 生成并存入。factory 返回 null 时不缓存。</summary>
    public ImageSource? GetOrAdd(string key, Func<ImageSource?> factory)
    {
        if (string.IsNullOrEmpty(key) || factory == null) return null;
        if (_map.TryGetValue(key, out var cached)) return cached;

        var created = factory();
        if (created == null) return null;

        return _map.TryAdd(key, created) ? created : _map[key];
    }

    /// <summary>配置或桌面内容变化后清空,下次访问重新提取。</summary>
    public void Clear() => _map.Clear();
}
