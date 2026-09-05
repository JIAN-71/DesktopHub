/* ============================================================
 * EmotionBallFacingTests.cs —— 朝向正面化回归:
 * 眼环数据把注视方向烘焙在位置里(待机环 0 双眼偏右上 +46.9px、环 8 偏左下 -34.1px),
 * 44px 胶囊下会读成"侧脸/背对"。EmotionBallControl.UpdateEyeTransform 在投影前把
 * 双眼对水平中点拉回面部中线(FaceFrontX)。本测试锁定该不变量。
 * ============================================================ */

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using DesktopHub.App.EmotionBall;

namespace DesktopHub.App.Tests;

public class EmotionBallFacingTests
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

    private static readonly FieldInfo EngineField = typeof(EmotionBallControl)
        .GetField("_engine", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo EngineTick = typeof(EmotionEngine)
        .GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance)!;
    private static readonly FieldInfo PoolNextField = typeof(EmotionEngine)
        .GetField("_poolNext", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo ExprIdxField = typeof(EmotionEngine)
        .GetField("_exprIdx", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo UpdateEye = typeof(EmotionBallControl)
        .GetMethod("UpdateEyeTransform", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo SilAt = typeof(EmotionBallControl)
        .GetMethod("SilAt", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static (double X, double Y) Centroid(EmotionEngine eng, int eye)
    {
        var flat = eng.RingCur;
        double x = 0, y = 0;
        var o = eye * EmotionBallData.EyePoints * 2;
        for (var i = 0; i < EmotionBallData.EyePoints; i++)
        {
            x += flat[o + i * 2];
            y += flat[o + i * 2 + 1];
        }
        return (x / EmotionBallData.EyePoints, y / EmotionBallData.EyePoints);
    }

    private static double NowMs() =>
        Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    private static void Frames(EmotionEngine eng, int n)
    {
        for (var i = 0; i < n; i++) { EngineTick.Invoke(eng, null); Thread.Sleep(16); }
    }

    /// <summary>双眼投影位置中点相对所在高度身体轮廓中线的水平偏移。
    /// 无正面化时:环 0 ≈ +47px、环 8 ≈ -34px;正面化后只剩动态张望 ±10px 的一半以内。</summary>
    private static double PairMidOffset(EmotionBallControl ball, EmotionEngine eng)
    {
        eng.Tick();
        var (bxL, byL) = Centroid(eng, 0);
        var (bxR, byR) = Centroid(eng, 1);
        var pairDx = (bxL + bxR) / 2 - EmotionBallData.HeadC;   // FaceFrontX = 1

        var posL = EyePosX(ball, eng.Pose.Left, 0, bxL, byL, pairDx);
        var posR = EyePosX(ball, eng.Pose.Right, 1, bxR, byR, pairDx);
        Assert.NotNull(posL);
        Assert.NotNull(posR);
        var by = (byL + byR) / 2;
        var (lo, hi) = ((double, double))SilAt.Invoke(null, new object[] { by })!;
        return (posL!.Value + posR!.Value) / 2 - (lo + hi) / 2;
    }

    private static double? EyePosX(EmotionBallControl ball, EyeState e, int eye,
        double bx, double by, double pairDx)
    {
        var visible = (bool?)UpdateEye.Invoke(ball,
            new object?[] { e, eye, 0.0, bx, by, pairDx });
        if (visible != true) return null;
        var arr = (TranslateTransform[])typeof(EmotionBallControl)
            .GetField("_eyePos", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(ball)!;
        return arr[eye].X;
    }

    [Fact]
    public void 待机环0投影后双眼对回正()
    {
        RunInSta(() =>
        {
            var ball = NewBall(out var eng);
            eng.SetEmotion("02");   // pool[0] = 环 0,9~16s 内不会池轮换
            Frames(eng, 60);
            Assert.Equal(0, (int)ExprIdxField.GetValue(eng)!);

            var offset = PairMidOffset(ball, eng);
            Assert.True(Math.Abs(offset) < 14,
                $"环 0 双眼对中点偏离面部中线 {offset:0.0}px(应 < 14px;未正面化时约 +47px)");
        });
    }

    [Fact]
    public void 待机环8投影后双眼对回正()
    {
        RunInSta(() =>
        {
            var ball = NewBall(out var eng);
            eng.SetEmotion("02");
            Frames(eng, 30);

            PoolNextField.SetValue(eng, NowMs() - 1);   // 触发一次池轮换:0 → 8
            Frames(eng, 60);
            Assert.Equal(8, (int)ExprIdxField.GetValue(eng)!);

            var offset = PairMidOffset(ball, eng);
            Assert.True(Math.Abs(offset) < 14,
                $"环 8 双眼对中点偏离面部中线 {offset:0.0}px(应 < 14px;未正面化时约 -34px)");
        });
    }

    private static EmotionBallControl NewBall(out EmotionEngine engine)
    {
        var ball = new EmotionBallControl();
        ball.Arrange(new Rect(0, 0, 232, 232));
        ball.UpdateLayout();
        engine = (EmotionEngine)EngineField.GetValue(ball)!;
        return ball;
    }
}
