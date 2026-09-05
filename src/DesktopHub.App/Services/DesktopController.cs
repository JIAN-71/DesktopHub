using System;
using System.Collections.Generic;
using System.Linq;
using DesktopHub.Core.Models;
using DesktopHub.Core.Services;
using DesktopHub.Shell;

namespace DesktopHub.App.Services;

/// <summary>
/// App 层门面:组合 IconService(扫描/缓存/刷新)与 ConfigService(配置/自启),
/// 并持有桌面变化监听。UI 层(Pill/Settings/Tray)只依赖本门面,不感知内部服务拆分。
/// </summary>
public sealed class DesktopController : IDisposable
{
    private readonly ConfigService _configService;
    private readonly IconService _iconService;
    private readonly RecentAppsService _recentApps = new();
    private readonly RecentStore _recentStore = new();
    private readonly IReadOnlyList<string> _persistedRecentKeys;
    private bool _recentRestored;
    private DesktopChangeWatcher? _watcher;

    public DesktopController(IUiDispatcher? dispatcher = null)
    {
        dispatcher ??= new WpfUiDispatcher(System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("UI Dispatcher 不可用"));
        _configService = new ConfigService();
        _iconService = new IconService(dispatcher);
        _iconService.StatusChanged += () => StatusChanged?.Invoke();
        _persistedRecentKeys = _recentStore.Load();
    }

    public AppConfig Config => _configService.Config;

    /// <summary>桌面内容或配置变化(UI 线程触发)。</summary>
    public event Action? StatusChanged;

    /// <summary>启动桌面变化监听(需在订阅 StatusChanged 之后调用)。</summary>
    public void StartWatching()
    {
        if (_watcher != null) return;
        _watcher = new DesktopChangeWatcher();
        _watcher.DesktopChanged += OnDesktopChanged;
        _watcher.Start(2);
    }

    /// <summary>扫描桌面、分类分组(命中缓存立即返回);首次成功扫描时恢复持久化的"最近打开"。</summary>
    public (IReadOnlyList<IconGroup<DesktopIconVM>> Groups, int Total) GetGroups()
    {
        var result = _iconService.GetGroups(Config.Categories);
        TryRestoreRecent(result.Groups);
        return result;
    }

    /// <summary>按持久化 Key(最新在前)从扫描结果中找回"最近打开"的图标;桌面图标已删除的条目自然失效。</summary>
    private void TryRestoreRecent(IReadOnlyList<IconGroup<DesktopIconVM>> groups)
    {
        if (_recentRestored || _persistedRecentKeys.Count == 0 || groups.Count == 0) return;
        _recentRestored = true;
        var byKey = groups
            .SelectMany(g => g.Icons)
            .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var restored = _persistedRecentKeys
            .Where(byKey.ContainsKey)
            .Select(k => byKey[k])
            .ToList();
        if (restored.Count > 0)
            _recentApps.Restore(restored);
    }

    /// <summary>最近打开的软件(最新在前,至多 3 个),供胶囊悬停扩展条展示。</summary>
    public IReadOnlyList<DesktopIconVM> RecentApps => _recentApps.Recent;

    /// <summary>记录一次经胶囊启动的软件,并广播刷新。</summary>
    public void RecordRecentLaunch(DesktopIconVM icon)
    {
        _recentApps.Record(icon);
        _recentStore.Save(_recentApps.Recent.Select(i => i.Key).ToList());
        StatusChanged?.Invoke();
    }

    /// <summary>后台重扫桌面,完成后在 UI 线程广播 StatusChanged。</summary>
    public void RefreshAsync() => _iconService.RefreshAsync(Config.Categories);

    public bool AreDesktopIconsHidden() => DesktopIcons.IsHidden();

    public void ToggleDesktopIcons()
    {
        DesktopIcons.Toggle();
        StatusChanged?.Invoke();
    }

    public void ApplyConfig(AppConfig newConfig)
    {
        _configService.Apply(newConfig);
        _iconService.Invalidate(); // 分类规则变化后强制重扫
        StatusChanged?.Invoke();
    }

    /// <summary>
    /// 落盘外观设置(主题模式 / 亚克力开关 / 不透明度),不动分组规则与自启。
    /// 外观本身经 ThemeManager 即时生效,此处只负责持久化;settings 保存与退出时兜底调用。
    /// </summary>
    public void SaveAppearance(string themeModeKey, bool acrylicEnabled, double acrylicOpacity)
    {
        Config.ThemeMode = themeModeKey;
        Config.AcrylicEnabled = acrylicEnabled;
        Config.AcrylicOpacity = acrylicOpacity;
        _configService.SaveCurrent();
    }

    private void OnDesktopChanged()
    {
        RefreshAsync();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _iconService.Dispose();
    }
}
