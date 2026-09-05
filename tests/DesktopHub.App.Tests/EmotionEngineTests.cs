/* ============================================================
 * EmotionEngineTests.cs —— 表情球引擎冒烟测试(纯逻辑,不碰 WPF 控件)
 *
 * 覆盖:注册表完整性 / 未知 ID 回退 / 数据几何完整性 /
 *      每帧合成管线稳定性 / 表情切换 / 自旋与弹跳 API / 眨眼开合。
 * 注:引擎内部用 Stopwatch 计时,时间驱动行为(眨眼/池轮换/弹簧收敛)
 *    用真实时间等待验证,断言取宽松边界避免 flaky。
 * ============================================================ */

using DesktopHub.App.EmotionBall;

namespace DesktopHub.App.Tests;

public class EmotionEngineTests
{
    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    private static void AssertPoseFinite(PoseState p)
    {
        var b = p.Body;
        Assert.True(IsFinite(b.X) && IsFinite(b.Y) && IsFinite(b.Scale) && IsFinite(b.Rotate)
            && IsFinite(b.Breathe) && IsFinite(b.Yaw), "Body 字段必须全为有限值");
        foreach (var (name, e) in new[] { ("Left", p.Left), ("Right", p.Right) })
        {
            Assert.True(IsFinite(e.X) && IsFinite(e.Y) && IsFinite(e.ScaleX) && IsFinite(e.ScaleY)
                && IsFinite(e.Rotate) && IsFinite(e.Open) && IsFinite(e.LookX) && IsFinite(e.LookY),
                $"{name} 字段必须全为有限值");
        }
    }

    // ---------- 注册表与数据完整性 ----------

    [Fact]
    public void 注册表含32个表情且全部可切换()
    {
        var seed = EmotionSeed.Create();
        Assert.Equal(32, seed.Count);

        var engine = new EmotionEngine("01");
        foreach (var raw in seed)
        {
            Assert.True(engine.SetEmotion(raw.Id), $"SetEmotion({raw.Id}) 应成功");
            Assert.Equal(raw.Id, engine.EmotionId);
        }
    }

    [Fact]
    public void 未知表情ID回退到待机02()
    {
        var engine = new EmotionEngine("01");
        Assert.True(engine.SetEmotion("does-not-exist"));
        Assert.Equal("02", engine.EmotionId); // 兜底待机
    }

    [Fact]
    public void 几何数据尺寸与范围正确()
    {
        Assert.Equal(25, EmotionBallData.RingCount);
        Assert.Equal(48, EmotionBallData.EyePoints);
        Assert.Equal(96, EmotionBallData.BodyPoints);

        // 眼环:25 组 × 2 眼 × 48 点 × 2 坐标
        Assert.Equal(25 * 2 * 48 * 2, EmotionBallData.Rings.Length);
        // 身体:96 点 × 2 坐标
        Assert.Equal(96 * 2, EmotionBallData.BodyRing.Length);

        foreach (var v in EmotionBallData.Rings)
            Assert.InRange(v, 0, 260); // viewBox -15..244,眼环实际 0~230
        foreach (var v in EmotionBallData.BodyRing)
            Assert.InRange(v, 0, 230);
    }

    // ---------- 每帧合成管线 ----------

    [Fact]
    public void 连续Tick600帧不抛异常且状态有限()
    {
        var engine = new EmotionEngine("01");
        for (var i = 0; i < 600; i++)
        {
            engine.Tick();
            AssertPoseFinite(engine.Pose);
            Assert.Equal(192, engine.RingCur.Length);
            foreach (var v in engine.RingCur)
                Assert.True(IsFinite(v), $"RingCur[{i}] 必须有限");
        }
    }

    [Fact]
    public void 快速切换多个表情不抛异常()
    {
        var engine = new EmotionEngine("01");
        foreach (var id in new[] { "10", "21", "30", "00", "05", "13", "02" })
        {
            engine.SetEmotion(id);
            for (var i = 0; i < 30; i++) engine.Tick();
            AssertPoseFinite(engine.Pose);
        }
    }

    // ---------- 对外交互 API ----------

    [Fact]
    public void 自旋后Tick稳定()
    {
        var engine = new EmotionEngine("01");
        engine.Spin(1);
        for (var i = 0; i < 60; i++)
        {
            engine.Tick();
            AssertPoseFinite(engine.Pose);
        }
    }

    [Fact]
    public void 弹跳后Tick稳定()
    {
        var engine = new EmotionEngine("01");
        engine.Bounce();
        for (var i = 0; i < 60; i++)
        {
            engine.Tick();
            AssertPoseFinite(engine.Pose);
        }
    }

    // ---------- 时间驱动行为(真实时间等待,宽松断言) ----------

    [Fact]
    public void 自旋一圈后偏航角收敛回零()
    {
        var engine = new EmotionEngine("01");
        engine.Spin(1);
        // 弹簧频率 6.2Hz 临界阻尼,约 1s 内收敛;跑 1.6s 留余量
        for (var i = 0; i < 100; i++)
        {
            engine.Tick();
            Thread.Sleep(16);
        }
        Assert.InRange(engine.Pose.Body.Yaw, -0.05, 0.05);
    }

    [Fact]
    public void 表情切换触发眨眼后开合度下降()
    {
        var engine = new EmotionEngine("01");
        engine.SetEmotion("10"); // prevId="01" ≠ "10" → BlinkNow 入队(合上 0.05)
        engine.Tick();           // 消费队列首键(0.05) → 弹簧目标 0.05
        Thread.Sleep(100);       // 让弹簧(26Hz 临界阻尼)向 0.05 移动;
                                 // 需 <150ms 避开 +150ms 的"过冲 1.08"关键帧
        engine.Tick();
        Assert.True(engine.Pose.Left.Open < 0.9, $"眨眼后 Open 应显著小于 1,实际 {engine.Pose.Left.Open}");
    }
}
