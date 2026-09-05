using System;
using System.Collections.Generic;
using System.Linq;
using DesktopHub.Core.Models;

namespace DesktopHub.Core.Services;

/// <summary>一个展示分组(纯数据,供 UI 绑定)。</summary>
public sealed class IconGroup<T>
{
    public required string Name { get; init; }
    public required IReadOnlyList<T> Icons { get; init; }
}

/// <summary>
/// 分组与排序:分组展示顺序以配置中的分类列表顺序为单一事实来源
/// (用户可在设置窗口调整顺序),未命中的未知分类排在最后,同序按名称排序。
/// </summary>
public static class IconGrouper
{
    public static IReadOnlyList<IconGroup<T>> Group<T>(
        IEnumerable<T> items,
        IReadOnlyList<CategoryDefinition> categories,
        Func<T, string> categorySelector)
    {
        return items
            .GroupBy(categorySelector)
            .Select(g => new IconGroup<T> { Name = g.Key, Icons = g.ToList() })
            .OrderBy(g => IndexOf(categories, g.Name))
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int IndexOf(IReadOnlyList<CategoryDefinition> categories, string name)
    {
        for (var i = 0; i < categories.Count; i++)
        {
            if (string.Equals(categories[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return categories.Count;
    }
}
