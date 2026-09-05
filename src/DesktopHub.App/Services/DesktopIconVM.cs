using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopHub.Core.Models;

namespace DesktopHub.App.Services;

/// <summary>面板里一个可启动的图标。</summary>
public sealed partial class DesktopIconVM : ObservableObject
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string ToolTip { get; init; }

    /// <summary>唯一标识(Shell 解析名),用于"最近打开"去重。</summary>
    public required string Key { get; init; }

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _icon;

    /// <summary>启动该图标(文件直接启动,系统项走 explorer shell:::)。</summary>
    public required Action Launch { get; init; }

    public static DesktopIconVM FromItem(DesktopItem item) => new()
    {
        Name = item.DisplayName,
        Category = item.Category,
        ToolTip = item.FileSystemPath ?? item.ParsingName,
        Key = item.ParsingName,
        Launch = () => LaunchItem(item),
    };

    private static void LaunchItem(DesktopItem item)
    {
        try
        {
            if (item.FileSystemPath is { } path)
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                });
            }
            else
            {
                // 系统命名空间项(如回收站):交给 explorer 用 shell:::{CLSID} 打开
                Process.Start(new ProcessStartInfo("explorer.exe")
                {
                    Arguments = $"\"shell:{item.ParsingName}\"",
                    UseShellExecute = true,
                });
            }
        }
        catch
        {
            // 目标可能已失效,忽略
        }
    }
}
