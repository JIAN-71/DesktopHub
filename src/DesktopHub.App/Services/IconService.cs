using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopHub.Core.Models;
using DesktopHub.Core.Services;
using DesktopHub.Shell;

namespace DesktopHub.App.Services;

/// <summary>
/// 图标服务:桌面扫描编排 + 图标缓存 + 后台 STA 刷新。
/// 产出可直接绑定的分组结果(冻结 ImageSource),句柄契约 Extract/Free 成对落实在本层。
/// 桌面变化监听由上层(DesktopController)持有,变化时调用 RefreshAsync(分类)。
/// </summary>
public sealed class IconService : IDisposable
{
    private readonly StaThreadRunner _sta = new();
    private readonly IconCache _iconCache = new();
    private readonly IUiDispatcher _dispatcher;

    /// <summary>最近一次成功的分组结果(缓存)。</summary>
    private IReadOnlyList<IconGroup<DesktopIconVM>>? _cachedGroups;
    private int _cachedTotal;

    /// <summary>配置或桌面变化后置脏,下次 GetGroups 强制重扫。</summary>
    private bool _dirty = true;

    public IconService(IUiDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>分组数据或桌面显隐状态变化(UI 线程触发)。</summary>
    public event Action? StatusChanged;

    /// <summary>扫描桌面、分类分组并把 HICON 转为冻结的 ImageSource。命中缓存时立即返回。</summary>
    public (IReadOnlyList<IconGroup<DesktopIconVM>> Groups, int Total) GetGroups(
        IReadOnlyList<CategoryDefinition> categories)
    {
        if (!_dirty && _cachedGroups != null)
            return (_cachedGroups, _cachedTotal);

        IReadOnlyList<DesktopItem> scanned;
        try
        {
            scanned = new DesktopIconScanner(new ClassificationEngine(categories)).Scan();
        }
        catch
        {
            return (Array.Empty<IconGroup<DesktopIconVM>>(), 0); // 枚举失败时返回空面板
        }

        var vms = BuildVms(scanned);
        var groups = IconGrouper.Group(vms, categories, i => i.Category);
        _cachedGroups = groups;
        _cachedTotal = vms.Count;
        _dirty = false;
        return (groups, vms.Count);
    }

    /// <summary>在后台 STA 线程重扫桌面并重建分组缓存,完成后在 UI 线程广播 StatusChanged。</summary>
    public void RefreshAsync(IReadOnlyList<CategoryDefinition> categories)
    {
        _sta.Post(() =>
        {
            IReadOnlyList<DesktopItem> scanned;
            try
            {
                scanned = new DesktopIconScanner(new ClassificationEngine(categories)).Scan();
            }
            catch
            {
                return; // 后台失败保留旧缓存
            }

            var vms = BuildVms(scanned);
            var groups = IconGrouper.Group(vms, categories, i => i.Category);
            _cachedGroups = groups;
            _cachedTotal = vms.Count;
            _dirty = false;

            _dispatcher.BeginInvoke(() => StatusChanged?.Invoke());
        });
    }

    /// <summary>分类规则变化后置脏并清空图标缓存(下次访问重新提取)。</summary>
    public void Invalidate()
    {
        _dirty = true;
        _iconCache.Clear();
    }

    private IReadOnlyList<DesktopIconVM> BuildVms(IReadOnlyList<DesktopItem> scanned)
    {
        var vms = new List<DesktopIconVM>(scanned.Count);
        foreach (var item in scanned)
        {
            ImageSource? icon = _iconCache.GetOrAdd(item.ParsingName, () => ExtractIcon(item));
            var vm = DesktopIconVM.FromItem(item);
            vm.Icon = icon;
            vms.Add(vm);
        }
        return vms;
    }

    private static ImageSource? ExtractIcon(DesktopItem item)
    {
        var hIcon = IntPtr.Zero;
        try
        {
            hIcon = IconExtractor.Extract(item.ParsingName, item.Kind == DesktopItemKind.System);
            if (hIcon == IntPtr.Zero) return null;
            var source = Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze(); // 允许跨线程/绑定使用
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            IconExtractor.Free(hIcon);
        }
    }

    public void Dispose()
    {
        _sta.Dispose();
        _iconCache.Clear();
    }
}
