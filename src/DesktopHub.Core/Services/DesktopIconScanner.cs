using System.Collections.Generic;
using DesktopHub.Core.Models;
using DesktopHub.Shell;

namespace DesktopHub.Core.Services;

/// <summary>
/// 扫描桌面全部可见图标(Shell 命名空间视角),解析 .lnk 目标并按规则分类。
/// 桌面上的任何东西都不会被移动或修改。
/// 只产出纯数据模型 DesktopItem;图标句柄由 UI 层在展示时经 IconExtractor 按需提取。
/// </summary>
public sealed class DesktopIconScanner
{
    private readonly ClassificationEngine _classifier;

    public DesktopIconScanner(ClassificationEngine classifier) => _classifier = classifier;

    public IReadOnlyList<DesktopItem> Scan()
    {
        var items = new List<DesktopItem>();
        foreach (var shellItem in DesktopShell.EnumerateIcons())
        {
            var kind = shellItem.Kind;
            var targetPath = shellItem.FileSystemPath;

            if (kind == DesktopItemKind.Unknown && targetPath != null)
            {
                // .lnk:解析真实目标来决定类型与分类依据
                var info = LinkResolver.Resolve(targetPath);
                kind = info.Kind switch
                {
                    LinkTargetKind.Url => DesktopItemKind.Url,
                    LinkTargetKind.Folder => DesktopItemKind.Folder,
                    LinkTargetKind.Application => DesktopItemKind.Application,
                    LinkTargetKind.Document => DesktopItemKind.Document,
                    _ => DesktopItemKind.Unknown,
                };
                if (info.Kind == LinkTargetKind.Url || !string.IsNullOrEmpty(info.TargetPath))
                    targetPath = info.TargetPath;
            }

            items.Add(new DesktopItem
            {
                DisplayName = shellItem.DisplayName,
                FileSystemPath = shellItem.FileSystemPath,
                ParsingName = shellItem.ParsingName,
                Kind = kind,
                Category = _classifier.Classify(shellItem.DisplayName, kind, targetPath),
            });
        }
        return items;
    }
}
