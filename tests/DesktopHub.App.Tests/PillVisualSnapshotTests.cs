/* ============================================================
 * PillVisualSnapshotTests.cs —— 小胶囊视觉快照:把真实控件渲染成 PNG
 * 供像素级目视检查(表情球与时间文本的相对位置)。
 * 产物: %TEMP%\pill-visual.png
 * ============================================================ */

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopHub.App.EmotionBall;

namespace DesktopHub.App.Tests;

public class PillVisualSnapshotTests
{
    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
    }

    [Fact]
    public void 渲染小胶囊视觉快照()
    {
        RunInSta(() =>
        {
            // 结构复刻 PillWindow.xaml 小胶囊态(含真实 EmotionBallControl)
            var ball = new EmotionBallControl();
            ball.Arrange(new Rect(0, 0, 44, 44));
            ball.UpdateLayout();

            // 引擎 Tick 驱动若干帧(反射调私有引擎,让姿态/眼环进入稳定态)
            var engineField = typeof(EmotionBallControl).GetField("_engine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var engine = engineField?.GetValue(ball);
            var tick = engine?.GetType().GetMethod("Tick");
            if (tick != null)
                for (var i = 0; i < 150; i++) tick.Invoke(engine, null);

            var small = new Grid();
            small.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            small.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var ballBorder = new Border
            {
                Width = 44, Height = 44, Margin = new Thickness(4, 0, 4, 0),
                Child = ball,
            };
            Grid.SetColumn(ballBorder, 0);
            small.Children.Add(ballBorder);

            var time = new TextBlock
            {
                Text = "12:34", FontSize = 21, FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(time, 1);
            small.Children.Add(time);

            var pill = new Border
            {
                Width = 240, Height = 58,
                Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(29),
                Child = small,
            };

            var root = new Grid { Width = 240, Height = 58 };
            root.Children.Add(pill);
            root.Measure(new Size(240, 58));
            root.Arrange(new Rect(0, 0, 240, 58));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(
                (int)(240 * 2), (int)(58 * 2), 192, 192, PixelFormats.Pbgra32);
            rtb.Render(root);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            var path = Path.Combine(Path.GetTempPath(), "pill-visual.png");
            using var fs = File.Create(path);
            enc.Save(fs);
        });
    }
}
