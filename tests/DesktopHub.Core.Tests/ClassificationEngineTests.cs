using System.Collections.Generic;
using DesktopHub.Core.Models;
using DesktopHub.Core.Services;
using DesktopHub.Shell;
using Xunit;

namespace DesktopHub.Core.Tests;

public class ClassificationEngineTests
{
    private static string Classify(string name, DesktopItemKind kind, string? target = null) =>
        new ClassificationEngine(CategoryDefinition.BuiltIn()).Classify(name, kind, target);

    [Fact]
    public void Game_Keyword_In_Name_Wins()
    {
        Assert.Equal("游戏", Classify("MyGameLauncher", DesktopItemKind.Application, @"C:\Tools\game.exe"));
    }

    [Fact]
    public void Steam_Path_Fragment_Classifies_As_Game()
    {
        Assert.Equal("游戏", Classify("Hades", DesktopItemKind.Application, @"D:\Steam\steamapps\common\Hades\Hades.exe"));
    }

    [Fact]
    public void Plain_Application_Falls_Back_By_Kind()
    {
        Assert.Equal("应用程序", Classify("Notepad", DesktopItemKind.Application, @"C:\Windows\notepad.exe"));
    }

    [Fact]
    public void Url_Goes_To_Web_Category()
    {
        Assert.Equal("网页快捷方式", Classify("Zhihu", DesktopItemKind.Url, "https://www.zhihu.com"));
    }

    [Fact]
    public void Folder_Goes_To_Folder_Category()
    {
        Assert.Equal("文件夹", Classify("Projects", DesktopItemKind.Folder, @"D:\Work\Projects"));
    }

    [Fact]
    public void System_Item_Goes_To_System_Category()
    {
        // 回收站:系统命名空间项,无文件路径
        Assert.Equal("系统", Classify("回收站", DesktopItemKind.System, null));
    }

    [Fact]
    public void Unknown_Falls_Back_To_Others()
    {
        Assert.Equal("其他", Classify("Mystery", DesktopItemKind.Unknown, null));
    }

    [Fact]
    public void Custom_Rule_Takes_Priority_Over_BuiltIn()
    {
        var categories = CategoryDefinition.BuiltIn();
        categories.Insert(0, new CategoryDefinition { Name = "工作", Keywords = new() { "work" } });
        var engine = new ClassificationEngine(categories);
        // "work" 命中自定义规则,即使名字里也含 "game"(游戏关键字)
        Assert.Equal("工作", engine.Classify("workgame", DesktopItemKind.Application, @"C:\x.exe"));
    }
}
