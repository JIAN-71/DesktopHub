using System;
using System.Collections.Generic;
using System.IO;
using DesktopHub.Shell;

namespace DesktopHub.Core.Models;

public sealed class AppConfig
{
    /// <summary>配置结构版本,便于未来迁移(当前为 1)。</summary>
    public int SchemaVersion { get; set; } = 1;

    public bool StartWithWindows { get; set; } = false;
    public List<CategoryDefinition> Categories { get; set; } = CategoryDefinition.BuiltIn();

    /// <summary>
    /// 外观主题模式:"System" = 启动时跟随系统深浅色,"Dark" / "Light" = 固定深/浅色。
    /// 用字符串而非枚举:Core 层不依赖 App 层的 AppTheme,JSON 亦向前兼容(未知值按 System 处理)。
    /// </summary>
    public string ThemeMode { get; set; } = "System";

    /// <summary>亚克力背景总开关(关闭后胶囊仅保留轻衬底,无模糊)。</summary>
    public bool AcrylicEnabled { get; set; } = true;

    /// <summary>亚克力 tint 不透明度,范围 [0,1](设置页以 0~100% 展示)。</summary>
    public double AcrylicOpacity { get; set; } = 0.40;
}

/// <summary>分类后待展示的桌面图标。</summary>
public sealed class DesktopItem
{
    public required string DisplayName { get; init; }
    /// <summary>文件系统路径;系统图标(此电脑/回收站等)为 null。</summary>
    public string? FileSystemPath { get; init; }
    /// <summary>Shell 解析名,系统图标形如 "::{CLSID}",启动时用。</summary>
    public required string ParsingName { get; init; }
    public DesktopItemKind Kind { get; init; }
    public required string Category { get; init; }
}
