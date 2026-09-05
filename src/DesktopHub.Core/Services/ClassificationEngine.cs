using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesktopHub.Core.Models;
using DesktopHub.Shell;

namespace DesktopHub.Core.Services;

/// <summary>
/// 分类引擎:自定义规则优先(按配置顺序,关键字匹配显示名/目标文件名,路径片段匹配目标路径),
/// 不命中时按类型回退(Url→网页、Folder→文件夹、Application→应用程序、System→系统),最终兜底"其他"。
/// </summary>
public sealed class ClassificationEngine
{
    public const string FallbackCategory = "其他";
    private readonly IReadOnlyList<CategoryDefinition> _categories;

    public ClassificationEngine(IReadOnlyList<CategoryDefinition> categories)
    {
        _categories = categories.Count > 0 ? categories : CategoryDefinition.BuiltIn();
    }

    public string Classify(string displayName, DesktopItemKind kind, string? targetPath)
    {
        var targetFile = string.Empty;
        try
        {
            if (!string.IsNullOrEmpty(targetPath) && kind != DesktopItemKind.Url)
                targetFile = Path.GetFileName(targetPath);
        }
        catch
        {
            // 目标路径非法时只匹配显示名
        }

        foreach (var category in _categories)
        {
            if (category.Keywords.Count == 0 && category.PathFragments.Count == 0)
                continue; // 无规则的内置类别仅作回退目标
            if (Matches(category, displayName, targetFile, targetPath))
                return category.Name;
        }

        return kind switch
        {
            DesktopItemKind.Url => FindBuiltIn("网页快捷方式") ?? FallbackCategory,
            DesktopItemKind.Folder => FindBuiltIn("文件夹") ?? FallbackCategory,
            DesktopItemKind.Application => FindBuiltIn("应用程序") ?? FallbackCategory,
            DesktopItemKind.System => FindBuiltIn("系统") ?? FallbackCategory,
            _ => FallbackCategory,
        };
    }

    private string? FindBuiltIn(string name) =>
        _categories.FirstOrDefault(c => c.Name == name)?.Name;

    private static bool Matches(CategoryDefinition category, string displayName, string targetFile, string? targetPath)
    {
        foreach (var keyword in category.Keywords)
        {
            if (keyword.Length > 0 &&
                (displayName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true ||
                 targetFile?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true))
                return true;
        }
        foreach (var fragment in category.PathFragments)
        {
            if (fragment.Length > 0 &&
                targetPath?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }
        return false;
    }
}
