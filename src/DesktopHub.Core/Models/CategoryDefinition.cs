using System.Collections.Generic;

namespace DesktopHub.Core.Models;

/// <summary>一个展示分组,由关键字(匹配显示名/目标文件名)和目标路径片段共同定义。</summary>
public sealed class CategoryDefinition
{
    public required string Name { get; set; }
    /// <summary>不区分大小写,匹配图标显示名或目标文件名的包含关系。</summary>
    public List<string> Keywords { get; set; } = new();
    /// <summary>不区分大小写,匹配目标完整路径的包含关系。</summary>
    public List<string> PathFragments { get; set; } = new();

    public static List<CategoryDefinition> BuiltIn() => new()
    {
        new()
        {
            Name = "游戏",
            Keywords = new() { "game", "游戏" },
            PathFragments = new() { "\\steam\\", "\\steamapps\\", "epic games", "riot games", "\\xboxgames\\", "wegame" },
        },
        new() { Name = "应用程序" },
        new() { Name = "文件夹" },
        new() { Name = "网页快捷方式" },
        new() { Name = "系统" },
        new() { Name = "其他" },
    };
}
