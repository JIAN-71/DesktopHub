using System.IO;
using DesktopHub.Core.Services;
using Xunit;

namespace DesktopHub.Core.Tests;

public class RecentStoreTests : TempAppDataTest
{
    [Fact]
    public void Roundtrip_Preserves_Order()
    {
        var store = new RecentStore();
        store.Save(new[] { "::{645FF040-5081-101B-9F08-00AA002F954E}", @"C:\Users\x\Desktop\a.lnk" });

        var loaded = store.Load();
        Assert.Equal(2, loaded.Count);
        Assert.StartsWith("::{645FF040", loaded[0]);
        Assert.EndsWith("a.lnk", loaded[1]);
    }

    [Fact]
    public void Load_Missing_File_Returns_Empty()
    {
        Assert.Empty(new RecentStore().Load());
    }

    [Fact]
    public void Load_Corrupt_File_Returns_Empty()
    {
        Directory.CreateDirectory(AppData.Root);
        File.WriteAllText(Path.Combine(AppData.Root, "recent.json"), "{ broken");
        Assert.Empty(new RecentStore().Load());
    }

    [Fact]
    public void Load_Filters_Blank_Entries()
    {
        Directory.CreateDirectory(AppData.Root);
        File.WriteAllText(Path.Combine(AppData.Root, "recent.json"), "[\"a\", \"\", \"  \"]");
        var loaded = new RecentStore().Load();
        Assert.Single(loaded);
        Assert.Equal("a", loaded[0]);
    }
}
