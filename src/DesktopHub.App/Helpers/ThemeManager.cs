using System;
using System.Windows;
using DesktopHub.Core.Models;

namespace DesktopHub.App.Helpers;

/// <summary>外观模式:跟随系统(启动时解析一次) / 固定深色 / 固定浅色。</summary>
public enum ThemeMode
{
    System,
    Dark,
    Light,
}

/// <summary>
/// 胶囊主题与亚克力参数管理:
///   - 维护 Application 级合并资源字典中的深浅色画刷;
///   - <see cref="Mode"/> 三态(跟随系统/深色/浅色),<see cref="SetThemeMode"/> 换字典并广播;
///   - 维护可调亚克力不透明度 + 是否启用,改值时触发 <see cref="AcrylicChanged"/>;
///   - <see cref="ApplyStartup"/> 在组合根把 AppConfig 的外观三字段装配进本管理器;
///   - 持久化映射用 <see cref="ParseMode"/> / <see cref="ModeKey"/>(AppConfig.ThemeMode 字符串)。
/// 全部画刷一律以 DynamicResource 引用。
/// </summary>
public static class ThemeManager
{
    private const string DarkUri = "Themes/Capsule.Dark.xaml";
    private const string LightUri = "Themes/Capsule.Light.xaml";

    /// <summary>外观模式,默认跟随系统。<see cref="SetThemeMode"/> 修改。</summary>
    public static ThemeMode Mode { get; private set; } = ThemeMode.System;

    /// <summary>当前生效主题(Mode=System 时为启动时解析出的系统主题)。</summary>
    public static AppTheme CurrentTheme { get; private set; } = AcrylicHelper.DetectSystemTheme();

    /// <summary>主题切换后触发(参数为新主题)。UI 线程触发。</summary>
    public static event Action<AppTheme>? ThemeChanged;

    /// <summary>亚克力不透明度 / 启用状态变更后触发(无参,取 <see cref="AcrylicOpacity"/> /
    /// <see cref="AcrylicEnabled"/>);UI 线程触发。PillWindow 订阅此事件 +
    /// <see cref="ThemeChanged"/> 即可同步应用。</summary>
    public static event Action? AcrylicChanged;

    // ---------- 亚克力参数(供 settings UI / 测试注入) ----------

    private static double _acrylicOpacity = 0.40;

    /// <summary>亚克力 tint 不透明度,范围 [0, 1]。0 = 完全透出、1 = 全覆盖糊白;
    /// 推荐 0.20~0.50(透出壁纸 + 微微暖/冷 tint)。</summary>
    public static double AcrylicOpacity
    {
        get => _acrylicOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_acrylicOpacity - clamped) < 0.001) return;
            _acrylicOpacity = clamped;
            AcrylicChanged?.Invoke();
        }
    }

    private static bool _acrylicEnabled = true;

    /// <summary>亚克力总开关。false 时 ApplyBackdrop 会下 ACCENT_DISABLED,窗口回到无 tint 状态。</summary>
    public static bool AcrylicEnabled
    {
        get => _acrylicEnabled;
        set
        {
            if (_acrylicEnabled == value) return;
            _acrylicEnabled = value;
            AcrylicChanged?.Invoke();
        }
    }

    // ---------- 生命周期 ----------

    /// <summary>应用启动时调用:把当前主题字典合入 Application 资源(须先于任何窗口创建)。</summary>
    public static void Initialize(Application app)
    {
        app.Resources.MergedDictionaries.Add(LoadDictionary(CurrentTheme));
    }

    /// <summary>
    /// 组合根(App.OnStartup)装配:把配置文件里的外观三字段(ThemeMode/AcrylicEnabled/AcrylicOpacity)
    /// 应用到本管理器。须在 <see cref="Initialize"/> 之后、任何窗口创建之前调用。
    /// </summary>
    public static void ApplyStartup(AppConfig config)
    {
        SetThemeMode(ParseMode(config.ThemeMode));
        AcrylicEnabled = config.AcrylicEnabled;
        AcrylicOpacity = config.AcrylicOpacity;
    }

    // ---------- 模式与主题切换 ----------

    /// <summary>
    /// 切换外观模式:System 立即按系统深浅色解析,Dark/Light 固定;随后广播
    /// <see cref="ThemeChanged"/> + <see cref="AcrylicChanged"/>(窗口重挂 Accent)。
    /// </summary>
    public static void SetThemeMode(ThemeMode mode)
    {
        Mode = mode;
        var resolved = mode switch
        {
            ThemeMode.Dark => AppTheme.Dark,
            ThemeMode.Light => AppTheme.Light,
            _ => AcrylicHelper.DetectSystemTheme(),
        };
        SetTheme(resolved);
    }

    /// <summary>MVVM 友好重载:ViewModel 不直接引用 Application.Current。</summary>
    public static void SetTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return; // 无 Application 宿主(如单元测试)时静默跳过
        SetTheme(app, theme);
    }

    /// <summary>切换深浅色主题:换字典 + 广播 ThemeChanged + AcrylicChanged(让窗口重挂 Accent)。</summary>
    public static void SetTheme(Application app, AppTheme theme)
    {
        if (theme == CurrentTheme && app.Resources.MergedDictionaries.Count > 0)
        {
            return;
        }

        CurrentTheme = theme;

        var dictionaries = app.Resources.MergedDictionaries;
        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var src = dictionaries[i].Source?.OriginalString;
            if (src != null && (src.EndsWith(DarkUri, StringComparison.OrdinalIgnoreCase)
                                || src.EndsWith(LightUri, StringComparison.OrdinalIgnoreCase)))
                dictionaries.RemoveAt(i);
        }
        dictionaries.Add(LoadDictionary(theme));

        ThemeChanged?.Invoke(theme);
        AcrylicChanged?.Invoke();
    }

    // ---------- AppConfig.ThemeMode 字符串映射(持久化单一事实) ----------

    /// <summary>配置字符串 → 模式;空/未知值回退 <see cref="ThemeMode.System"/>。</summary>
    public static ThemeMode ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "dark" => ThemeMode.Dark,
        "light" => ThemeMode.Light,
        "system" => ThemeMode.System,
        _ => ThemeMode.System,
    };

    /// <summary>模式 → 配置字符串(与 <see cref="ParseMode"/> 互逆)。</summary>
    public static string ModeKey(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => "Dark",
        ThemeMode.Light => "Light",
        _ => "System",
    };

    /// <summary>
    /// 主题字典用程序集限定 pack URI 加载:相对 URI("Themes/...")按入口程序集解析,
    /// 若宿主不是本程序集(如 tools/SettingsPreview 预览器)会解析失败;
    /// 限定 DesktopHub.App;component 后任何宿主都能解析。
    /// </summary>
    private static ResourceDictionary LoadDictionary(AppTheme theme)
    {
        var asm = typeof(ThemeManager).Assembly.GetName().Name;
        var path = theme == AppTheme.Light ? LightUri : DarkUri;
        return new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/{asm};component/{path}", UriKind.Absolute),
        };
    }
}
