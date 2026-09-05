using System.Collections.Generic;
using System.Linq;
using DesktopHub.Core.Models;
using DesktopHub.Core.Services;
using Xunit;

namespace DesktopHub.Core.Tests;

public class IconGrouperTests
{
    private sealed record Fake(string Category);

    private static readonly IReadOnlyList<CategoryDefinition> Categories =
        CategoryDefinition.BuiltIn();

    [Fact]
    public void Groups_By_Category_In_Config_Order()
    {
        var items = new[]
        {
            new Fake("其他"),
            new Fake("应用程序"),
            new Fake("游戏"),
            new Fake("应用程序"),
        };

        var groups = IconGrouper.Group(items, Categories, i => i.Category);

        // 内置分类定义顺序:游戏 → 应用程序 → … → 其他
        Assert.Equal(new[] { "游戏", "应用程序", "其他" }, groups.Select(g => g.Name));
        Assert.Equal(2, groups[1].Icons.Count);
    }

    [Fact]
    public void Unknown_Category_Goes_Last()
    {
        var items = new[] { new Fake("自定义分组"), new Fake("应用程序") };

        var groups = IconGrouper.Group(items, Categories, i => i.Category);

        Assert.Equal(new[] { "应用程序", "自定义分组" }, groups.Select(g => g.Name));
    }

    [Fact]
    public void Empty_Input_Returns_Empty()
    {
        var groups = IconGrouper.Group(Enumerable.Empty<Fake>(), Categories, i => i.Category);
        Assert.Empty(groups);
    }

    [Fact]
    public void Same_Order_Groups_Sorted_By_Name()
    {
        var items = new[] { new Fake("b"), new Fake("a") };
        // 两个未知分类同处末尾,按名称排序
        var groups = IconGrouper.Group(items, Categories, i => i.Category);
        Assert.Equal(new[] { "a", "b" }, groups.Select(g => g.Name));
    }
}
