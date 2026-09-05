using System;
using System.IO;
using System.Threading;
using DesktopHub.Core.Models;
using DesktopHub.Core.Services;
using DesktopHub.Shell;
using Xunit;

namespace DesktopHub.Core.Tests;

public class ShellAndConfigTests : TempAppDataTest
{
    [Fact]
    public void Config_Roundtrip()
    {
        var store = new ConfigStore();
        var config = new AppConfig
        {
            SchemaVersion = 2,
            StartWithWindows = true,
            Categories = { new CategoryDefinition { Name = "工作", Keywords = { "work" } } },
            ThemeMode = "Dark",
            AcrylicEnabled = false,
            AcrylicOpacity = 0.65,
        };
        store.Save(config);

        var loaded = store.Load();
        Assert.Equal(2, loaded.SchemaVersion);
        Assert.True(loaded.StartWithWindows);
        Assert.Contains(loaded.Categories, c => c.Name == "工作");
        Assert.Contains(loaded.Categories, c => c.Name == "游戏" && c.Keywords.Contains("game"));
        Assert.Equal("Dark", loaded.ThemeMode);
        Assert.False(loaded.AcrylicEnabled);
        Assert.Equal(0.65, loaded.AcrylicOpacity, precision: 3);
    }

    [Fact]
    public void Config_Without_Appearance_Fields_Falls_Back_To_Defaults()
    {
        // 旧版本 config.json(无外观字段)升级:缺失字段落默认值,不抛异常
        Directory.CreateDirectory(AppData.Root);
        File.WriteAllText(Path.Combine(AppData.Root, "config.json"),
            """{"schemaVersion":1,"startWithWindows":false,"categories":[]}""");
        var config = new ConfigStore().Load();

        Assert.Equal("System", config.ThemeMode);
        Assert.True(config.AcrylicEnabled);
        Assert.Equal(0.40, config.AcrylicOpacity, precision: 3);
        // 内置分类补齐逻辑不受影响
        Assert.Contains(config.Categories, c => c.Name == "游戏");
    }

    [Fact]
    public void Config_Corrupt_File_Falls_Back_To_Default()
    {
        Directory.CreateDirectory(AppData.Root);
        File.WriteAllText(Path.Combine(AppData.Root, "config.json"), "{ broken");
        var config = new ConfigStore().Load();
        Assert.False(config.StartWithWindows);
        Assert.Equal(CategoryDefinition.BuiltIn().Count, config.Categories.Count);
    }

    /// <summary>Shell COM 交互在 STA 线程执行(正式应用即 STA);并打印调用轨迹便于崩溃定位。</summary>
    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
        return result!;
    }

    [Fact]
    public void DesktopShell_Enumerates_Real_Desktop()
    {
        // 真实桌面冒烟:本机公共桌面必有若干图标(Chrome/Edge 等)
        var items = RunInSta(() => DesktopShell.EnumerateIcons());

        Assert.NotEmpty(items);
        Assert.All(items, i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(i.ParsingName));
        });
    }

    [Fact]
    public void DesktopIconScanner_Classifies_Real_Desktop_Without_Touching_Files()
    {
        var items = RunInSta(() =>
            new DesktopIconScanner(new ClassificationEngine(CategoryDefinition.BuiltIn())).Scan());

        Assert.NotEmpty(items);
        // 每个图标都有合法分组名
        Assert.All(items, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Category)));
        // 扫描只读:System 项没有文件路径
        Assert.All(items, entry =>
        {
            if (entry.Kind == DesktopItemKind.System)
                Assert.Null(entry.FileSystemPath);
        });
    }

    [Fact]
    public void LinkResolver_Reads_Real_Lnk()
    {
        var temp = Path.Combine(TempRoot, "lnk");
        Directory.CreateDirectory(temp);
        var lnkPath = Path.Combine(temp, "Notepad.lnk");

        var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            shortcut.TargetPath = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\notepad.exe";
            shortcut.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }

        var info = LinkResolver.Resolve(lnkPath);
        Assert.Equal(LinkTargetKind.Application, info.Kind);
        Assert.EndsWith("notepad.exe", info.TargetPath, StringComparison.OrdinalIgnoreCase);
    }
}
