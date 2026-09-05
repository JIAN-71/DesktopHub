using System;
using System.IO;
using DesktopHub.Core.Services;

namespace DesktopHub.Core.Tests;

/// <summary>把 AppData 重定向到临时目录,测试间互不污染真实 %APPDATA%。</summary>
public abstract class TempAppDataTest : IDisposable
{
    protected string TempRoot { get; }

    protected TempAppDataTest()
    {
        TempRoot = Path.Combine(Path.GetTempPath(), "DesktopHubTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRoot);
        AppData.Root = TempRoot;
    }

    public void Dispose()
    {
        try { Directory.Delete(TempRoot, recursive: true); } catch { }
    }
}
