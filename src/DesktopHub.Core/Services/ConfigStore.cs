using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DesktopHub.Core.Models;

namespace DesktopHub.Core.Services;

public static class AppData
{
    /// <summary>可重定向(单元测试指向临时目录)。</summary>
    public static string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopHub");
}

/// <summary>config.json 读写。</summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private string ConfigPath => Path.Combine(AppData.Root, "config.json");

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOptions);
                if (config != null)
                {
                    // 补齐缺失的内置分类(旧版本配置升级时保留用户自定义规则)
                    foreach (var builtin in CategoryDefinition.BuiltIn())
                        if (!config.Categories.Any(c => c.Name == builtin.Name))
                            config.Categories.Add(builtin);
                    return config;
                }
            }
        }
        catch
        {
            // 配置损坏时回退默认值
        }
        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(AppData.Root);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
        }
        catch
        {
            // 写入失败不阻断 UI(下次启动仍可用内存中的配置;设置窗口已提示保存结果)
        }
    }
}
