using System.Windows;
using DesktopHub.App.Services;

namespace DesktopHub.App.Settings;

/// <summary>
/// 设置窗口:数据与命令绑定到 <see cref="SettingsViewModel"/>,
/// code-behind 只负责装配 DataContext 与弹窗反馈(校验警告/成功提示)。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(DesktopController controller)
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel(controller);
        DataContext = _viewModel;

        _viewModel.Warned += message =>
            MessageBox.Show(this, message, "DesktopHub", MessageBoxButton.OK, MessageBoxImage.Warning);
        _viewModel.Notified += message =>
            MessageBox.Show(this, message, "DesktopHub", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
