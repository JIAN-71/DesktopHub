using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DesktopHub.App.Helpers;
using DesktopHub.App.Pill;
using DesktopHub.App.Services;
using DesktopHub.App.Tray;

namespace DesktopHub.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private DesktopController _controller = null!;
    private TrayIconProvider _tray = null!;
    private Settings.SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, @"Local\DesktopHub.SingleInstance", out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("DesktopHub 已经在运行。", "DesktopHub", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"发生未处理的错误:{args.Exception.Message}", "DesktopHub",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);

        // 主题画刷字典(深浅色)须先于任何窗口创建完成合并,DynamicResource 才能解析
        ThemeManager.Initialize(this);

        _controller = new DesktopController(new WpfUiDispatcher(Dispatcher));

        // 外观持久化(主题模式/亚克力开关/不透明度)随配置装配,先于胶囊窗口创建
        ThemeManager.ApplyStartup(_controller.Config);

        _tray = new TrayIconProvider(
            _controller,
            showSettings: ShowSettings,
            exit: ExitApp);

        var pill = new PillWindow(_controller);
        pill.Show();
        MainWindow = pill;

        _controller.StartWatching();
    }

    private void ShowSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new Settings.SettingsWindow(_controller);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void ExitApp()
    {
        // 外观(主题模式/亚克力)即时生效但不即时落盘,退出时兜底固化一次
        _controller.SaveAppearance(
            ThemeManager.ModeKey(ThemeManager.Mode),
            ThemeManager.AcrylicEnabled,
            ThemeManager.AcrylicOpacity);
        _tray.Dispose();
        _controller.Dispose();
        Shutdown();
    }
}
