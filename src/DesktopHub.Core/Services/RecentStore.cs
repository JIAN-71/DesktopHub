using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopHub.Core.Services;

/// <summary>recent.json 读写:持久化"最近打开"的 Shell 解析名(最新在前)。失败一律兜底为空。</summary>
public sealed class RecentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private string RecentPath => Path.Combine(AppData.Root, "recent.json");

    /// <summary>读取最近打开记录(Shell 解析名列表,最新在前);文件缺失或损坏返回空列表。</summary>
    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(RecentPath)) return Array.Empty<string>();
            var keys = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentPath), JsonOptions);
            return keys?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? new List<string>();
        }
        catch
        {
            // 文件损坏时回退空态
            return Array.Empty<string>();
        }
    }

    /// <summary>保存最近打开记录(最新在前,至多数条,写入失败静默兜底)。</summary>
    public void Save(IReadOnlyList<string> parsingNames)
    {
        try
        {
            Directory.CreateDirectory(AppData.Root);
            File.WriteAllText(RecentPath, JsonSerializer.Serialize(parsingNames, JsonOptions));
        }
        catch
        {
            // 写入失败不阻断 UI(下次启动丢失最近记录,可接受)
        }
    }
}
