using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DesktopHub.Shell;

public enum DesktopItemKind
{
    Unknown,
    System,      // 此电脑 / 回收站等命名空间项
    Folder,
    Url,
    Application,
    Document,
}

/// <summary>桌面上可见的一个图标(Shell 桌面命名空间视角,与资源管理器所见一致)。纯数据,不含图标句柄。</summary>
public sealed class DesktopShellItem
{
    public required string DisplayName { get; init; }
    /// <summary>文件系统路径;系统图标(回收站等)为 null。</summary>
    public string? FileSystemPath { get; init; }
    /// <summary>Shell 解析名,系统图标形如 "::{CLSID}"。</summary>
    public required string ParsingName { get; init; }
    public DesktopItemKind Kind { get; init; }
}

/// <summary>
/// 枚举 Shell 桌面命名空间 —— 包括用户桌面、公共桌面文件与"此电脑""回收站"等系统图标,
/// 与用户在桌面上看到的完全一致。
/// 枚举走 Shell.Application 自动化对象(IDispatch,无 vtable 互操作);
/// 图标提取已拆分到 IconExtractor,展示时才按需提取。
/// </summary>
public static class DesktopShell
{
    public static IReadOnlyList<DesktopShellItem> EnumerateIcons()
    {
        var result = new List<DesktopShellItem>();
        var shellType = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new InvalidOperationException("Shell.Application COM 不可用");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic desktopFolder = shell.Namespace(0); // 0 = 桌面命名空间根
            if (desktopFolder == null) return result;
            dynamic items = desktopFolder.Items();
            var count = (int)items.Count;

            for (var i = 0; i < count; i++)
            {
                dynamic item = items.Item(i);
                var displayName = (string)item.Name;
                var parsingName = (string)item.Path;
                var isFolder = (bool)item.IsFolder;
                if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(parsingName))
                    continue;

                string? fileSystemPath = null;
                var kind = DesktopItemKind.Unknown;
                if (!parsingName.StartsWith("::"))
                {
                    fileSystemPath = parsingName;
                    if (isFolder && System.IO.Directory.Exists(parsingName))
                        kind = DesktopItemKind.Folder;
                    else
                    {
                        var ext = System.IO.Path.GetExtension(parsingName);
                        kind = ext switch
                        {
                            ".lnk" => DesktopItemKind.Unknown, // 目标类型由上层用 LinkResolver 解析
                            ".url" => DesktopItemKind.Url,
                            ".exe" => DesktopItemKind.Application,
                            _ => DesktopItemKind.Document,
                        };
                    }
                }
                else
                {
                    kind = DesktopItemKind.System;
                }

                result.Add(new DesktopShellItem
                {
                    DisplayName = displayName,
                    FileSystemPath = fileSystemPath,
                    ParsingName = parsingName,
                    Kind = kind,
                });
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
        return result;
    }
}
