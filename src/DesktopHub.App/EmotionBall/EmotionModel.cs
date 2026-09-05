/* ============================================================
 * EmotionModel.cs —— 表情球运行时数据模型(移植自 aora-bot emotion-ball)
 *
 * EmotionRaw/BodySpec/EyeSpec/AnimRaw/SeqFrameRaw 对应 emotions.js 的原始配置
 * 字段(由工具生成的 EmotionSeed.cs 消费);PoseState/BodyState/EyeState 对应
 * engine.js 的 pose(每帧合成的工作姿态),弹簧结构对应 engine.js 的 spring。
 * 上游: https://github.com/sam70361/aora-bot (社区许可,非商业使用需注明出处)
 * ============================================================ */

using System.Globalization;

namespace DesktopHub.App.EmotionBall;

#region 原始配置模型(emotions.js 字段映射)

/// <summary>body 覆盖片段,字段为 null 表示不覆盖。</summary>
public sealed class BodySpec
{
    public double? X, Y, Scale, Rotate, Breathe, Zzz, Orbit, Ribbons, Confetti;
    public string? Color;
}

/// <summary>eyes 覆盖片段(both/left/right 共用),字段为 null 表示不覆盖。</summary>
public sealed class EyeSpec
{
    public double? X, Y, ScaleX, ScaleY, Rotate, Open, LookX, LookY;
    public string? Color;
}

/// <summary>待机动画原语(对应 engine.js ANIM_TYPES 的参数包)。</summary>
public sealed class AnimRaw
{
    public string Target = "";   // eyes / body / left / right
    public string Prop = "";     // x / y / scale / lookX / lookY / open / rotate / breathe ...
    public string Type = "";     // sine / pulse / jitter / scan / glance / blink
    public double? Amp, Period, Phase, PhaseMs, Speed, Decay, Interval, Dur, Depth;
}

/// <summary>sequence 关键帧(对应 engine.js sequence.frames 的元素)。</summary>
public sealed class SeqFrameRaw
{
    public double At;
    public BodySpec? Body;
    public EyeSpec? Both, Left, Right;
}

/// <summary>表情原始定义(emotions.js 一个表情条目)。</summary>
public sealed class EmotionRaw
{
    public string Id = "", Name = "", Group = "", Desc = "";
    public double Transition = -1;   // <0 未指定 → 默认 500
    public bool Gaze = true;         // gaze: false 时为 false
    public int[]? Pool;
    public double[]? PoolMs;
    public double? PoolSpeed;
    public bool BlinkOff;            // blinkMs: null(不眨眼)
    public double[]? BlinkMs;        // blinkMs: [min, max]
    public double? Openness;
    public bool Antics;
    public BodySpec? Body;
    public EyeSpec? Both, Left, Right;
    public AnimRaw[]? Anims;
    public string? Settle;           // 'base' | 'hold'
    public string? SettleNext;       // settle: { next: id }
    public SeqFrameRaw[]? Frames;
}

#endregion

#region 归一化后的表情定义

/// <summary>关键帧:时间点 + 应用过覆盖的完整姿态。</summary>
public sealed class SeqFrame
{
    public required double At { get; init; }
    public required PoseState Pose { get; init; }
}

/// <summary>归一化表情定义(默认值已合并,关键帧已物化,注册一次全程只读)。</summary>
public sealed class EmotionDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Desc { get; init; } = "";
    public double Transition { get; init; } = 500;
    public bool Gaze { get; init; } = true;
    public required int[] Pool { get; init; }
    public double PoolMinMs { get; init; } = 9000;
    public double PoolMaxMs { get; init; } = 16000;
    public double PoolSpeed { get; init; } = 6;
    public bool BlinkEnabled { get; init; } = true;
    public double BlinkMinMs { get; init; } = 6000;
    public double BlinkMaxMs { get; init; } = 14000;
    public double Openness { get; init; } = 1;
    public bool Antics { get; init; }
    public required PoseState Base { get; init; }
    public AnimRaw[] Anims { get; init; } = [];
    public SeqFrame[]? Frames { get; init; }
    public string? Settle { get; init; }
    public string? SettleNext { get; init; }
}

#endregion

#region 每帧姿态(body + 双眼)

/// <summary>身体姿态字段。Ribbons/Confetti/Zzz/Orbit 数据保留,粒子特效未移植。</summary>
public sealed class BodyState
{
    public double X, Y, Scale = 1, Rotate;
    public string Color = "#F3F0EA";
    public double Breathe = 0.01;
    public double Ribbons, Confetti, Zzz, Orbit;
    public double Yaw;   // 自旋偏航(每帧由引擎写入)

    public BodyState Clone() => (BodyState)MemberwiseClone();

    public void CopyFrom(BodyState o)
    {
        X = o.X; Y = o.Y; Scale = o.Scale; Rotate = o.Rotate; Color = o.Color;
        Breathe = o.Breathe; Ribbons = o.Ribbons; Confetti = o.Confetti;
        Zzz = o.Zzz; Orbit = o.Orbit; Yaw = o.Yaw;
    }

    public void Apply(BodySpec s)
    {
        if (s.X.HasValue) X = s.X.Value;
        if (s.Y.HasValue) Y = s.Y.Value;
        if (s.Scale.HasValue) Scale = s.Scale.Value;
        if (s.Rotate.HasValue) Rotate = s.Rotate.Value;
        if (s.Breathe.HasValue) Breathe = s.Breathe.Value;
        if (s.Zzz.HasValue) Zzz = s.Zzz.Value;
        if (s.Orbit.HasValue) Orbit = s.Orbit.Value;
        if (s.Ribbons.HasValue) Ribbons = s.Ribbons.Value;
        if (s.Confetti.HasValue) Confetti = s.Confetti.Value;
        if (s.Color is not null) Color = s.Color;
    }

    public void LerpFrom(BodyState a, BodyState b, double t)
    {
        X = a.X + (b.X - a.X) * t;
        Y = a.Y + (b.Y - a.Y) * t;
        Scale = a.Scale + (b.Scale - a.Scale) * t;
        Rotate = a.Rotate + (b.Rotate - a.Rotate) * t;
        Breathe = a.Breathe + (b.Breathe - a.Breathe) * t;
        Ribbons = a.Ribbons + (b.Ribbons - a.Ribbons) * t;
        Confetti = a.Confetti + (b.Confetti - a.Confetti) * t;
        Zzz = a.Zzz + (b.Zzz - a.Zzz) * t;
        Orbit = a.Orbit + (b.Orbit - a.Orbit) * t;
        Color = a.Color == b.Color ? b.Color : ColorUtil.Lerp(a.Color, b.Color, t);
    }
}

/// <summary>单眼姿态字段。</summary>
public sealed class EyeState
{
    public double X, Y, ScaleX = 1, ScaleY = 1, Rotate, Open = 1, LookX, LookY;
    public string Color = "#1A1A1A";

    public EyeState Clone() => (EyeState)MemberwiseClone();

    public void CopyFrom(EyeState o)
    {
        X = o.X; Y = o.Y; ScaleX = o.ScaleX; ScaleY = o.ScaleY;
        Rotate = o.Rotate; Open = o.Open; LookX = o.LookX; LookY = o.LookY; Color = o.Color;
    }

    public void Apply(EyeSpec s)
    {
        if (s.X.HasValue) X = s.X.Value;
        if (s.Y.HasValue) Y = s.Y.Value;
        if (s.ScaleX.HasValue) ScaleX = s.ScaleX.Value;
        if (s.ScaleY.HasValue) ScaleY = s.ScaleY.Value;
        if (s.Rotate.HasValue) Rotate = s.Rotate.Value;
        if (s.Open.HasValue) Open = s.Open.Value;
        if (s.LookX.HasValue) LookX = s.LookX.Value;
        if (s.LookY.HasValue) LookY = s.LookY.Value;
        if (s.Color is not null) Color = s.Color;
    }

    public void LerpFrom(EyeState a, EyeState b, double t)
    {
        X = a.X + (b.X - a.X) * t;
        Y = a.Y + (b.Y - a.Y) * t;
        ScaleX = a.ScaleX + (b.ScaleX - a.ScaleX) * t;
        ScaleY = a.ScaleY + (b.ScaleY - a.ScaleY) * t;
        Rotate = a.Rotate + (b.Rotate - a.Rotate) * t;
        Open = a.Open + (b.Open - a.Open) * t;
        LookX = a.LookX + (b.LookX - a.LookX) * t;
        LookY = a.LookY + (b.LookY - a.LookY) * t;
        Color = a.Color == b.Color ? b.Color : ColorUtil.Lerp(a.Color, b.Color, t);
    }
}

/// <summary>一帧完整姿态(body + 左右眼)。</summary>
public sealed class PoseState
{
    public BodyState Body = new();
    public EyeState Left = new();
    public EyeState Right = new();

    public PoseState() { }

    public PoseState Clone() => new() { Body = Body.Clone(), Left = Left.Clone(), Right = Right.Clone() };

    public void CopyFrom(PoseState o)
    {
        Body.CopyFrom(o.Body);
        Left.CopyFrom(o.Left);
        Right.CopyFrom(o.Right);
    }

    /// <summary>把 body / both / left / right 覆盖片段合并到当前姿态(原地)。</summary>
    public void ApplySpec(BodySpec? body, EyeSpec? both, EyeSpec? left, EyeSpec? right)
    {
        if (body is not null) Body.Apply(body);
        if (both is not null) { Left.Apply(both); Right.Apply(both); }
        if (left is not null) Left.Apply(left);
        if (right is not null) Right.Apply(right);
    }

    /// <summary>逐字段插值写入自身:自 = a、b 按 t 插值(对应 engine.js lerpPose)。</summary>
    public void LerpInto(PoseState a, PoseState b, double t)
    {
        Body.LerpFrom(a.Body, b.Body, t);
        Left.LerpFrom(a.Left, b.Left, t);
        Right.LerpFrom(a.Right, b.Right, t);
    }
}

#endregion

/// <summary>临界阻尼弹簧(engine.js springStep:子步 1/120 保证数值稳定,由调用方控制)。</summary>
public struct EmotionSpring
{
    public double X, V, T;

    public EmotionSpring(double v0) { X = v0; V = 0; T = v0; }

    public void Step(double w, double z, double dt)
    {
        V += (-2 * z * w * V - w * w * (X - T)) * dt;
        X += V * dt;
        if (!double.IsFinite(X) || !double.IsFinite(V)) { X = T; V = 0; }
    }
}

/// <summary>颜色插值与色阶工具(engine.js hexToRgb/rgbToHex/lerpColor,ball.js shade)。</summary>
public static class ColorUtil
{
    public static string Lerp(string a, string b, double t)
    {
        if (a == b) return b;
        var (ar, ag, ab) = Parse(a);
        var (br, bg, bb) = Parse(b);
        return ToHex(
            ar + (br - ar) * t,
            ag + (bg - ag) * t,
            ab + (bb - ab) * t);
    }

    /// <summary>amt &gt; 0 向白色靠,amt &lt; 0 向黑色靠(ball.js shade)。</summary>
    public static byte Shade(double channel, double amt)
    {
        double target = amt < 0 ? 0 : 255;
        return (byte)Math.Round(channel + (target - channel) * Math.Abs(amt));
    }

    public static (byte R, byte G, byte B) Parse(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 3) h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
        var n = int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((byte)(n >> 16 & 255), (byte)(n >> 8 & 255), (byte)(n & 255));
    }

    public static string ToHex(double r, double g, double b)
    {
        static string Part(double v) => Math.Clamp((int)Math.Round(v), 0, 255).ToString("x2", CultureInfo.InvariantCulture);
        return "#" + Part(r) + Part(g) + Part(b);
    }
}
