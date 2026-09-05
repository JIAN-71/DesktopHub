using DesktopHub.Core.Models;
using DesktopHub.Core.Services;

namespace DesktopHub.App.Services;

/// <summary>
/// 配置服务:负责 AppConfig 的加载、保存与开机自启同步。
/// 分组/自启等配置变化统一经此处落地。
/// </summary>
public sealed class ConfigService
{
    private readonly ConfigStore _configStore = new();

    public AppConfig Config { get; private set; }

    public ConfigService() => Config = _configStore.Load();

    /// <summary>应用新配置:持久化并同步开机自启注册表项。</summary>
    public void Apply(AppConfig newConfig)
    {
        Config = newConfig;
        _configStore.Save(newConfig);

        try
        {
            if (newConfig.StartWithWindows != AutoStartManager.IsEnabled())
                AutoStartManager.SetEnabled(newConfig.StartWithWindows);
        }
        catch
        {
            // 注册表写失败不阻断配置保存
        }
    }

    /// <summary>
    /// 就地持久化当前配置(不重建实例、不同步自启注册表)。
    /// 供外观等"即时生效 + 立即落盘"的局部修改使用,避免覆盖并发中的其他配置编辑。
    /// </summary>
    public void SaveCurrent() => _configStore.Save(Config);
}
