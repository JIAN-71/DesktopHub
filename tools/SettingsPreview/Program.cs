// 设置窗口独立预览器(开发工具,不在 .sln 内):
// 复用真实的 SettingsWindow + SettingsViewModel + ThemeManager + DesktopController(无托盘/单实例),
// 用于在不干扰常驻胶囊实例的情况下预览/调试设置 UI 与主题切换。
// 运行: dotnet run --project tools/SettingsPreview
// 注意:主题字典用程序集限定 pack URI 加载(见 ThemeManager.LoadDictionary),任何宿主均可解析。
using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
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

            var settings = new SettingsWindow(controller) { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            var pill = new PillWindow(controller, () => settings.Activate());
            pill.Show();

            // 开发探针:记录消息是否到达胶囊窗口(验证 WndProc 钩子链路)
            var src = (HwndSource)PresentationSource.FromVisual(pill);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "pill-probe.txt"),
                $"attached hwnd={src.Handle}{Environment.NewLine}");
            src.AddHook((IntPtr probeHwnd, int probeMsg, IntPtr probeW, IntPtr probeL, ref bool probeHandled) =>
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "pill-probe.txt"),
                    $"msg=0x{probeMsg:X} w={probeW} l={probeL}{Environment.NewLine}");
                return IntPtr.Zero;
            });

            settings.Show();
        };
        app.Run();
    }
}
