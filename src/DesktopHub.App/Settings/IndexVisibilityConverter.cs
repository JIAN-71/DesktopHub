using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DesktopHub.App.Settings;

/// <summary>
/// 分区索引 → 可见性:当前索引等于 parameter 指定的分区时返回 Visible,否则 Collapsed。
/// 与 <see cref="IndexEqualConverter"/> 配套,用于侧边栏分区内容面板的切换。
/// </summary>
public sealed class IndexVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index && parameter is string raw && int.TryParse(raw, out var n) && index == n
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
