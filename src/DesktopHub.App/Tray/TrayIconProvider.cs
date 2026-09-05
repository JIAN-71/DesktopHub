using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopHub.App.Services;
using Hardcodet.Wpf.TaskbarNotification;

namespace DesktopHub.App.Tray;

/// <summary>系统托盘图标与右键菜单。</summary>
public sealed class TrayIconProvider : IDisposable
{
    private readonly DesktopController _controller;
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _iconsHiddenItem;

    public TrayIconProvider(DesktopController controller, Action showSettings, Action exit)
    {
        _controller = controller;

        var menu = new ContextMenu();

        var refresh = new MenuItem { Header = "重新扫描" };
        refresh.Click += (_, _) => _controller.RefreshAsync();
        menu.Items.Add(refresh);

        _iconsHiddenItem = new MenuItem { Header = "隐藏桌面图标", IsCheckable = true, IsChecked = _controller.AreDesktopIconsHidden() };
        _iconsHiddenItem.Click += (_, _) =>
        {
            // 勾选状态与实际状态不一致时才切换(避免菜单勾选本身的回调回环)
            if (_iconsHiddenItem.IsChecked != _controller.AreDesktopIconsHidden())
                _controller.ToggleDesktopIcons();
            _iconsHiddenItem.IsChecked = _controller.AreDesktopIconsHidden();
        };
        menu.Items.Add(_iconsHiddenItem);

        menu.Items.Add(new Separator());

        var settings = new MenuItem { Header = "设置…" };
        settings.Click += (_, _) => showSettings();
        menu.Items.Add(settings);

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(exitItem);

        _icon = new TaskbarIcon
        {
            ToolTipText = "DesktopHub - 桌面图标面板",
            IconSource = CreateIconImage(),
            ContextMenu = menu,
        };
        _icon.TrayLeftMouseUp += (_, _) => showSettings();
    }

    /// <summary>程序化绘制托盘图标:深色圆角方块 + 白色 "D"。</summary>
    private static ImageSource CreateIconImage()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x27)), null,
                new Rect(2, 2, 28, 28), 8, 8);
            var text = new FormattedText(
                "D", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                18, Brushes.White, 1.25);
            dc.DrawText(text, new Point((28 - text.Width) / 2 + 2, (28 - text.Height) / 2 + 2));
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public void Dispose() => _icon.Dispose();
}
