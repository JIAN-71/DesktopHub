using System;
using System.IO;

namespace DesktopHub.Shell;

public enum LinkTargetKind
{
    None,
    Application,
    Document,
    Folder,
    Url
}

public sealed class LinkInfo
{
    public string TargetPath { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public LinkTargetKind Kind { get; init; }
}

/// <summary>
/// 解析桌面快捷方式(.lnk / .url)的真实目标。
/// .lnk 通过 WScript.Shell COM 晚绑定读取,无需额外 COM 引用。
/// </summary>
public static class LinkResolver
{
    public static LinkInfo Resolve(string shortcutPath)
    {
        var ext = Path.GetExtension(shortcutPath).ToLowerInvariant();
        try
        {
            return ext switch
            {
                ".lnk" => ResolveLnk(shortcutPath),
                ".url" => ResolveUrl(shortcutPath),
                _ => new LinkInfo { Kind = LinkTargetKind.None }
            };
        }
        catch
        {
            return new LinkInfo { Kind = LinkTargetKind.None };
        }
    }

    private static LinkInfo ResolveLnk(string shortcutPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell COM 不可用");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            string target = (string)shortcut.TargetPath;
            string args = (string)shortcut.Arguments;
            return new LinkInfo { TargetPath = target ?? string.Empty, Arguments = args ?? string.Empty, Kind = DetermineKind(target) };
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    private static LinkInfo ResolveUrl(string shortcutPath)
    {
        foreach (var line in File.ReadLines(shortcutPath))
        {
            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
            {
                var url = line["URL=".Length..].Trim();
                return new LinkInfo { TargetPath = url, Kind = string.IsNullOrEmpty(url) ? LinkTargetKind.None : LinkTargetKind.Url };
            }
        }
        return new LinkInfo { Kind = LinkTargetKind.None };
    }

    private static LinkTargetKind DetermineKind(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return LinkTargetKind.None;
        if (Directory.Exists(target)) return LinkTargetKind.Folder;
        if (File.Exists(target))
        {
            var ext = Path.GetExtension(target).ToLowerInvariant();
            return ext == ".exe" ? LinkTargetKind.Application : LinkTargetKind.Document;
        }
        return LinkTargetKind.None;
    }
}
