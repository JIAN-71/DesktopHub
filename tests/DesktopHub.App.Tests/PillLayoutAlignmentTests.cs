/* ============================================================
 * PillLayoutAlignmentTests.cs —— 小胶囊内部居中对齐的像素级回归测试。
 *
 * 复刻 PillWindow.xaml 小胶囊态的结构(2026-09-03 定稿):
 *   PillBorder(58, BorderThickness=1) → Grid → SmallPanel
 *     ├ Border(44×44, Margin 4,0,0,0, 水平贴左) ← 表情球容器
 *     └ TextBlock(HorizontalAlignment=Center, FontSize=21, 垂直居中) ← 时间文本
 * 时间文本相对整个 240px 胶囊居中(中心 = 120),而非球右侧剩余空间居中。
 * 测量实际位置与视觉中心,防止未来布局改动破坏对齐。
 * 背景:52px球列+188星列 → 文本中心146 偏右26px;整组居中方案 → 球不贴左。
 * 定稿:球贴左(灵动岛式)、时间单独居中(中心120)。
 * ============================================================ */

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopHub.App.Tests;

public class PillLayoutAlignmentTests
{
    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
        return result!;
    }

    private sealed class LayoutProbe
    {
        public Border PillBorder = null!;
        public Grid InnerGrid = null!;
        public Grid SmallPanel = null!;
        public Border BallBorder = null!;
        public TextBlock TimeText = null!;

        public double BallTop, BallBottom, BallCenter;      // 相对 PillBorder
        public double BallLeft, BallRight;
        public double TextLeft, TextWidth, TextCenter;
        public double TextTop, TextActualHeight;
        public double DigitTop, DigitHeight, DigitVisualCenter; // 数字字形视觉中心
        public double ContentTop;                            // 边框内侧内容区起点
    }

    private static LayoutProbe MeasureLayout()
    {
        var probe = new LayoutProbe();

        // 结构与 PillWindow.xaml 小胶囊态一致(含 1px 边框)
        probe.PillBorder = new Border
        {
            Width = 240,
            Height = 58,
            BorderThickness = new Thickness(1),
        };
        probe.InnerGrid = new Grid();
        probe.PillBorder.Child = probe.InnerGrid;

        probe.SmallPanel = new Grid();
        probe.InnerGrid.Children.Add(probe.SmallPanel);

        probe.BallBorder = new Border
        {
            Width = 44,
            Height = 44,
            Margin = new Thickness(4, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        probe.SmallPanel.Children.Add(probe.BallBorder);

        probe.TimeText = new TextBlock
        {
            FontSize = 21,
            FontWeight = FontWeights.Medium,
            Text = "12:34",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        probe.SmallPanel.Children.Add(probe.TimeText);

        var root = new Grid { Width = 240, Height = 58 };
        root.Children.Add(probe.PillBorder);
        root.Measure(new Size(240, 58));
        root.Arrange(new Rect(0, 0, 240, 58));
        root.UpdateLayout();

        var toRoot = probe.PillBorder.TransformToAncestor(root);
        var pillOrigin = toRoot.Transform(new Point(0, 0));

        var ballToPill = probe.BallBorder.TransformToAncestor(probe.PillBorder);
        var ballPos = ballToPill.Transform(new Point(0, 0));
        probe.BallTop = pillOrigin.Y + ballPos.Y;
        probe.BallBottom = probe.BallTop + probe.BallBorder.ActualHeight;
        probe.BallCenter = (probe.BallTop + probe.BallBottom) / 2;
        probe.BallLeft = pillOrigin.X + ballPos.X;
        probe.BallRight = probe.BallLeft + probe.BallBorder.ActualWidth;

        var textToPill = probe.TimeText.TransformToAncestor(probe.PillBorder);
        var textPos = textToPill.Transform(new Point(0, 0));
        probe.TextLeft = pillOrigin.X + textPos.X;
        probe.TextWidth = probe.TimeText.ActualWidth;
        probe.TextCenter = probe.TextLeft + probe.TextWidth / 2;
        probe.TextTop = pillOrigin.Y + textPos.Y;
        probe.TextActualHeight = probe.TimeText.ActualHeight;

        // 数字字形视觉中心:基线 - 数字高度的一半(数字无下伸部)
        var ft = new FormattedText(
            "0", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            probe.TimeText.FontFamily != null
                ? new Typeface(probe.TimeText.FontFamily, probe.TimeText.FontStyle, probe.TimeText.FontWeight, FontStretches.Normal)
                : new Typeface("Segoe UI"),
            probe.TimeText.FontSize, Brushes.Black,
            VisualTreeHelper.GetDpi(probe.TimeText).PixelsPerDip);
        probe.DigitHeight = ft.Height;      // 数字字形高度(cap 高)
        probe.DigitTop = probe.TextTop + ft.Baseline - ft.Height;
        probe.DigitVisualCenter = probe.DigitTop + probe.DigitHeight / 2;

        // 内容区起点 = 边框厚度
        probe.ContentTop = probe.PillBorder.BorderThickness.Top;
        return probe;
    }

    [Fact]
    public void 表情球容器在小胶囊内垂直居中且贴左()
    {
        var p = RunInSta(MeasureLayout);
        // 内容区高 56(58 - 上下各 1px 边框),44px 居中 → 上边距 6
        Assert.Equal(p.ContentTop + (56 - 44) / 2, p.BallTop, 1);
        // 贴左:左缘 = 内容区起点(1px 边框) + 4px Margin
        Assert.Equal(1 + 4, p.BallLeft, 1);
    }

    [Fact]
    public void 时间文本水平居中于整个胶囊()
    {
        var p = RunInSta(MeasureLayout);
        // TextBlock 相对 240px 整窗居中 → 中心必须落在 120(旧结构在 188px 剩余区居中=146,偏右 26)
        Assert.Equal(120, p.TextCenter, 1);
        // 时间不得与贴左的球重叠(文本左缘 > 球右缘 48)
        Assert.True(p.TextLeft > p.BallRight, $"时间左缘({p.TextLeft:F1}) 应大于球右缘({p.BallRight:F1})");
    }

    [Fact]
    public void 输出对齐诊断数据()
    {
        var p = RunInSta(MeasureLayout);
        var report = $"""
            === 小胶囊布局实测(单位 DIP) ===
            内容区: y {p.ContentTop} ~ {p.ContentTop + 56}
            表情球: y {p.BallTop:F2} ~ {p.BallBottom:F2}  中心 {p.BallCenter:F2}
                     x {p.BallLeft:F2} ~ {p.BallRight:F2}  (贴左)
            时间行框: y {p.TextTop:F2} ~ {p.TextTop + p.TextActualHeight:F2}  (行高 {p.TextActualHeight:F2})
                      x {p.TextLeft:F2} ~ {p.TextLeft + p.TextWidth:F2}  (宽 {p.TextWidth:F2})  中心 {p.TextCenter:F2}  (胶囊中心 120)
            数字字形: y {p.DigitTop:F2} ~ {p.DigitTop + p.DigitHeight:F2}  视觉中心 {p.DigitVisualCenter:F2}
            差值: 球心 - 文字行框中心 = {p.BallCenter - (p.TextTop + p.TextActualHeight / 2):F2}
            文本中心 - 胶囊中心 = {p.TextCenter - 120:F2}
            """;
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "pill-align.txt"), report);
    }
}
