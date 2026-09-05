using System;
using System.Globalization;
using System.Windows.Data;

namespace DesktopHub.App.Settings;

/// <summary>
/// 一组 RadioButton ↔ 单个索引属性的绑定转换器:
/// Convert  = index == parameter → 勾选态;
/// ConvertBack = 勾选时把 parameter 写回索引(取消勾选不回写,避免清零)。
/// </summary>
public sealed class IndexEqualConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index && parameter is string raw && int.TryParse(raw, out var n) && index == n;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string raw && int.TryParse(raw, out var n) ? n : Binding.DoNothing;
}
