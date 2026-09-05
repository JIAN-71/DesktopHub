// 设置窗口独立预览器(开发工具,不在 .sln 内):
// 复用真实的 SettingsWindow + SettingsViewModel + ThemeManager + DesktopController(无托盘/单实例),
// 用于在不干扰常驻胶囊实例的情况下预览/调试设置 UI 与主题切换。
// 运行: dotnet run --project tools/SettingsPreview
// 注意:pack URI 资源解析指向 DesktopHub.App 程序集(主题字典在其内)。
using System.Windows;
using DesktopHub.App.Helpers;
using DesktopHub.App.Pill;
using DesktopHub.App.Services;
using DesktopHub.App.Settings;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "SettingsPreview 启动失败");
        }
    }

    private static void Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
        app.Startup += (_, _) =>
        {
            // 与主程序 App.xaml 一致:合入主题无关控件样式(PillButton 等,PillWindow 依赖)
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/DesktopHub.App;component/Themes/Controls.xaml", UriKind.Absolute),
            });

            ThemeManager.Initialize(app);

            var controller = new DesktopController();
            ThemeManager.ApplyStartup(controller.Config);

            var pill = new PillWindow(controller);
            pill.Show();
            new SettingsWindow(controller) { WindowStartupLocation = WindowStartupLocation.CenterScreen }.Show();
        };
        app.Run();
    }
}
