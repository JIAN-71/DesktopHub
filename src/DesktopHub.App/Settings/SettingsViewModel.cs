using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopHub.App.Helpers;
using DesktopHub.Core.Models;

namespace DesktopHub.App.Services;

/// <summary>设置窗口编辑用行(DataGrid 直接编辑属性,无需通知)。</summary>
public sealed class RuleRow
{
    public string Name { get; set; } = string.Empty;
    public string KeywordsText { get; set; } = string.Empty;
    public string PathFragmentsText { get; set; } = string.Empty;
}

/// <summary>
/// 设置窗口 ViewModel:
///   - 通用项(开机自启/隐藏图标)与分组规则走「保存并应用」;
///   - 外观项(主题模式/亚克力开关/不透明度)改动即经 <see cref="ThemeManager"/> 实时生效并落盘,
///     不透明度滑杆拖动期间连续生效、停止 500ms 后才写配置(避免逐 tick 写盘);
///   - 校验失败触发 <see cref="Warned"/>,成功反馈触发 <see cref="Notified"/>,由 code-behind 弹窗。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly DesktopController _controller;
    private readonly DispatcherTimer _opacitySaveTimer;
    private bool _loading; // LoadConfig 期间抑制「改动即生效/落盘」

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _hideDesktopIcons;

    /// <summary>外观模式:0 跟随系统 / 1 深色 / 2 浅色(与 <see cref="ThemeMode"/> 枚举值一致)。</summary>
    [ObservableProperty]
    private int _themeModeIndex;

    /// <summary>侧边栏当前分区:0 个性化 / 1 通用 / 2 分组规则(仅 UI 导航态,不持久化)。</summary>
    [ObservableProperty]
    private int _selectedSectionIndex;

    /// <summary>亚克力背景总开关。</summary>
    [ObservableProperty]
    private bool _acrylicEnabled;

    /// <summary>亚克力不透明度,UI 用 0~100(%),内部换算为 [0,1]。</summary>
    [ObservableProperty]
    private double _acrylicOpacityPercent;

    /// <summary>分组规则行集合(DataGrid 可增删改)。</summary>
    public ObservableCollection<RuleRow> Rules { get; } = new();

    /// <summary>校验失败提示(警告框)。</summary>
    public event Action<string>? Warned;

    /// <summary>操作成功提示(信息框)。</summary>
    public event Action<string>? Notified;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand RefreshNowCommand { get; }

    public SettingsViewModel(DesktopController controller)
    {
        _controller = controller;
        SaveCommand = new RelayCommand(Save);
        RefreshNowCommand = new RelayCommand(RefreshNow);

        _opacitySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _opacitySaveTimer.Tick += (_, _) =>
        {
            _opacitySaveTimer.Stop();
            PersistAppearance();
        };

        LoadConfig();
    }

    private void LoadConfig()
    {
        _loading = true;
        try
        {
            var config = _controller.Config;
            StartWithWindows = config.StartWithWindows;
            HideDesktopIcons = _controller.AreDesktopIconsHidden();
            ThemeModeIndex = (int)ThemeManager.ParseMode(config.ThemeMode);
            ThemeManager.AcrylicEnabled = config.AcrylicEnabled; // 与配置对齐(值未变时 setter 不广播)
            ThemeManager.AcrylicOpacity = config.AcrylicOpacity;
            AcrylicEnabled = config.AcrylicEnabled;
            AcrylicOpacityPercent = Math.Round(ThemeManager.AcrylicOpacity * 100.0);
            Rules.Clear();
            foreach (var c in config.Categories)
            {
                Rules.Add(new RuleRow
                {
                    Name = c.Name,
                    KeywordsText = string.Join(", ", c.Keywords),
                    PathFragmentsText = string.Join(", ", c.PathFragments),
                });
            }
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnThemeModeIndexChanged(int value)
    {
        if (_loading) return;
        ThemeManager.SetThemeMode((ThemeMode)value);
        PersistAppearance();
    }

    partial void OnAcrylicEnabledChanged(bool value)
    {
        if (_loading) return;
        ThemeManager.AcrylicEnabled = value;
        PersistAppearance();
    }

    partial void OnAcrylicOpacityPercentChanged(double value)
    {
        if (_loading) return;
        ThemeManager.AcrylicOpacity = value / 100.0; // 拖动期间实时生效
        _opacitySaveTimer.Stop();
        _opacitySaveTimer.Start(); // 停止拖动 500ms 后再落盘
    }

    /// <summary>把当前外观状态(ThemeManager 即事实来源)写入配置文件。</summary>
    public void PersistAppearance() =>
        _controller.SaveAppearance(
            ThemeManager.ModeKey(ThemeManager.Mode),
            ThemeManager.AcrylicEnabled,
            ThemeManager.AcrylicOpacity);

    private void Save()
    {
        var names = Rules.Where(r => !string.IsNullOrWhiteSpace(r.Name)).Select(r => r.Name.Trim()).ToList();
        if (names.Count == 0)
        {
            Warned?.Invoke("至少保留一个分组。");
            return;
        }
        if (names.Count != names.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            Warned?.Invoke("存在重复的分组名,请修改后再保存。");
            return;
        }

        if (HideDesktopIcons != _controller.AreDesktopIconsHidden())
            _controller.ToggleDesktopIcons();

        _controller.ApplyConfig(new AppConfig
        {
            StartWithWindows = StartWithWindows,
            Categories = Rules
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => new CategoryDefinition
                {
                    Name = r.Name.Trim(),
                    Keywords = SplitCsv(r.KeywordsText),
                    PathFragments = SplitCsv(r.PathFragmentsText),
                })
                .ToList(),
            // 外观随保存一并固化(以 ThemeManager 当前状态为准)
            ThemeMode = ThemeManager.ModeKey(ThemeManager.Mode),
            AcrylicEnabled = ThemeManager.AcrylicEnabled,
            AcrylicOpacity = ThemeManager.AcrylicOpacity,
        });

        Notified?.Invoke("设置已保存并应用。");
    }

    private void RefreshNow()
    {
        _controller.RefreshAsync();
        Notified?.Invoke("已重新扫描桌面。");
    }

    private static List<string> SplitCsv(string text) =>
        text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}
