/* ============================================================
 * EmotionEngine.cs —— 表情球驱动引擎,移植自 aora-bot emotion-ball(js/engine.js)
 *
 * 职责(与原版一致):
 *   1. 表情注册表:EmotionSeed 归一化(合并默认值、物化 sequence 每帧完整姿态)
 *   2. 每帧合成:基础姿态 → sequence 关键帧采样 → 内置呼吸 → 动画原语 →
 *      表情池轮换 → 眨眼调度 → 弹簧整步(眼环形变/开合度/自旋)→ 弹跳 → 注视漂移 → 过渡插值
 *   3. 对外:SetEmotion / Spin / Bounce / Tick
 *
 * 未移植(粒子特效层,胶囊 44px 下不可读,后续需要再补):
 *   彩带拖尾、撒花、常驻环带、zzz 睡眠字母、线稿模式、鼠标注视、待机休眠切换。
 * 上游: https://github.com/sam70361/aora-bot (社区许可,非商业使用需注明出处)
 * ============================================================ */

using System.Diagnostics;

namespace DesktopHub.App.EmotionBall;

public sealed class EmotionEngine
{
    private const double Tau = Math.PI * 2;
    private const string FallbackId = "02";

    // 弹跳:4 段递减抛物线(高度 / 时长秒),对应 engine.js BOUNCE_SEGS
    private static readonly (double H, double D)[] BounceSegs = { (48, 0.5), (28, 0.382), (14, 0.27), (6, 0.177) };
    private static readonly double BounceTotal = BounceSegs.Sum(s => s.D);

    private readonly Random _rand = new();
    private readonly double _seed = Random.Shared.NextDouble() * 100;

    // ---------- 注册表(进程内共享,种子数据只读) ----------
    private static Dictionary<string, EmotionDef>? _registry;
    private static Dictionary<string, EmotionDef> Registry => _registry ??= BuildRegistry();

    private static Dictionary<string, EmotionDef> BuildRegistry()
    {
        var map = new Dictionary<string, EmotionDef>(StringComparer.Ordinal);
        foreach (var raw in EmotionSeed.Create())
        {
            var def = Normalize(raw);
            map[def.Id] = def;
        }
        return map;
    }

    private static EmotionDef Normalize(EmotionRaw raw)
    {
        var basePose = new PoseState();
        basePose.ApplySpec(raw.Body, raw.Both, raw.Left, raw.Right);

        var pool = (raw.Pool ?? new[] { 0, 8 }).Where(i => i >= 0 && i < EmotionBallData.RingCount).ToArray();
        if (pool.Length == 0) pool = [0];

        var blinkEnabled = !raw.BlinkOff;
        var blinkMin = 6000.0;
        var blinkMax = 14000.0;
        if (raw.BlinkMs is { Length: 2 } bm) { blinkMin = bm[0]; blinkMax = bm[1]; }
        else if (raw.BlinkOff) { blinkMin = blinkMax = 0; }

        SeqFrame[]? frames = null;
        if (raw.Frames is { Length: > 0 })
        {
            frames = raw.Frames
                .Select(f =>
                {
                    var p = basePose.Clone();
                    p.ApplySpec(f.Body, f.Both, f.Left, f.Right);
                    return new SeqFrame { At = f.At, Pose = p };
                })
                .OrderBy(f => f.At)
                .ToArray();
        }

        var poolMs = raw.PoolMs ?? new[] { 9000.0, 16000.0 };
        return new EmotionDef
        {
            Id = raw.Id,
            Name = raw.Name,
            Desc = raw.Desc,
            Transition = raw.Transition >= 0 ? raw.Transition : 500,
            Gaze = raw.Gaze,
            Pool = pool,
            PoolMinMs = poolMs[0],
            PoolMaxMs = poolMs.Length > 1 ? poolMs[1] : poolMs[0],
            PoolSpeed = raw.PoolSpeed ?? 6,
            BlinkEnabled = blinkEnabled,
            BlinkMinMs = blinkMin,
            BlinkMaxMs = blinkMax,
            Openness = raw.Openness ?? 1,
            Antics = raw.Antics,
            Base = basePose,
            Anims = raw.Anims ?? [],
            Frames = frames,
            Settle = raw.Settle,
            SettleNext = raw.SettleNext,
        };
    }

    // ---------- 运行时状态 ----------

    private EmotionDef _def = null!;
    private PoseState? _prevPose;                 // 表情切换的过渡起点
    private bool _hasTicked;
    private double _emoStart, _transStart, _transDur;
    private double _dt = 1 / 60.0, _lastTick;

    // sequence 关键帧状态
    private SeqFrame[]? _seqFrames;
    private string? _seqSettle, _seqSettleNext;
    private bool _seqDone;

    // 眼环池轮换 + 形变弹簧
    private int _exprIdx = -1;
    private int _poolPos;
    private double _poolNext;
    private EmotionSpring _ringSpring = new(1);
    private double _ringSpeed = 7;
    private bool _settleSyncNeeded = true;
    // 扁平 48 点 × 2 眼:[0..95] 左眼,[96..191] 右眼
    private readonly double[] _ringSrc = new double[192];
    private readonly double[] _ringDst = new double[192];
    private readonly double[] _ringCur = new double[192];

    // 眨眼(开合度弹簧 + 关键帧队列)
    private EmotionSpring _open = new(1);
    private readonly List<(double At, double V)> _blinkQ = [];
    private double _blinkNext = double.PositiveInfinity;

    // 待机小动作(自旋 / 弹跳)
    private double _anticNext;
    private double _bounceAt = -1;
    private EmotionSpring _spinSpring;
    private bool _spinning;

    // 小尺寸实例放大眼睛占比(胶囊 44px 下保证可读)
    private double _eyeScale = 1.15;

    /// <summary>当前帧姿态(Tick 后有效,渲染层直接读取)。</summary>
    public PoseState Pose { get; } = new();

    /// <summary>当前眼环插值结果(扁平数组,Tick 后有效,渲染层直接读取)。</summary>
    public double[] RingCur => _ringCur;

    /// <summary>本帧眼环数据是否变化(渲染层据此重建几何)。</summary>
    public bool RingDirty { get; private set; }

    /// <summary>本帧眼环是否处于形变中(true=需逐帧重建几何;false=可用缓存几何)。</summary>
    public bool MorphActive { get; private set; }

    /// <summary>当前眼环索引(静止态渲染层用它取缓存几何)。</summary>
    public int ExprIndex => _exprIdx;

    /// <summary>当前表情 ID。</summary>
    public string? EmotionId => _def?.Id;

    public double EyeScale { get => _eyeScale; set => _eyeScale = value; }

    public EmotionEngine(string initialEmotion = "01")
    {
        SetEmotion(initialEmotion);
    }

    private static double NowMs() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    private double Rand(double a, double b) => a + _rand.NextDouble() * (b - a);

    // ---------- 对外交互 ----------

    /// <summary>切换表情(未知 ID 回退待机)。</summary>
    public bool SetEmotion(string id)
    {
        if (!Registry.TryGetValue(id, out var def))
            def = Registry[FallbackId];

        var now = NowMs();
        var prevId = _def?.Id;
        // 过渡起点 = 上一帧姿态(首次切换前未渲染过,直接硬切)
        _prevPose = _hasTicked ? Pose.Clone() : null;
        _def = def;
        _emoStart = now;
        _transStart = now;
        _transDur = _prevPose is not null ? def.Transition : 0;
        _seqFrames = def.Frames;
        _seqSettle = def.Settle;
        _seqSettleNext = def.SettleNext;
        _seqDone = false;

        _poolPos = 0;
        SetExpr(def.Pool[0], def.PoolSpeed >= 10 ? 10 : 8);
        _poolNext = now + Rand(def.PoolMinMs, def.PoolMaxMs);
        if (prevId is not null && prevId != def.Id && def.BlinkEnabled) BlinkNow(now);
        _blinkNext = def.BlinkEnabled ? now + Rand(def.BlinkMinMs, def.BlinkMaxMs) : double.PositiveInfinity;
        _anticNext = now + Rand(2500, 5000);
        return true;
    }

    /// <summary>自旋(弹簧追整数圈;进行中不可打断)。</summary>
    public void Spin(int turns = 1)
    {
        if (_spinning) return;
        var d = _rand.NextDouble() < 0.5 ? -1 : 1;
        _spinSpring = new EmotionSpring(0) { T = Math.Max(1, turns) * Tau * d };
        _spinning = true;
    }

    /// <summary>弹跳(4 段递减抛物线)。</summary>
    public void Bounce()
    {
        if (_bounceAt < 0) _bounceAt = NowMs();
    }

    // ---------- 每帧推进 ----------

    /// <summary>由渲染循环每帧调用(内部用 Stopwatch 计时,与原版 performance.now 对应)。</summary>
    public void Tick()
    {
        var now = NowMs();
        _dt = _lastTick > 0 ? Math.Clamp((now - _lastTick) / 1000.0, 0.001, 0.05) : 1 / 60.0;
        _lastTick = now;
        RingDirty = false;   // 脏标记每帧重置,由 Compose 按需置位,Tick 返回后供渲染层读取
        Compose(now, 0);
        _hasTicked = true;
    }

    /// <summary>合成当前帧:base → sequence → 呼吸 → anims → 池轮换/眨眼/小动作 → 弹簧 → 过渡。</summary>
    private void Compose(double now, int depth)
    {
        var def = _def;
        var t = now - _emoStart;
        var pose = Pose;

        if (_seqFrames is not null)
        {
            var (switched, hasPose) = SampleSequence(t, now);
            if (switched)
            {
                // sequence 播完且 settle.next:已切到新表情,重入合成
                if (depth < 4) { Compose(now, depth + 1); return; }
                pose.CopyFrom(def.Base);
            }
            else if (hasPose) pose.CopyFrom(_seqScratch);
            else pose.CopyFrom(def.Base);
        }
        else pose.CopyFrom(def.Base);

        // 内置呼吸(相位用绝对时间,切换表情不跳变)
        var br = pose.Body.Breathe;
        if (br != 0)
        {
            var ph = Tau * now / 3600;
            pose.Body.Scale += br * Math.Sin(ph);
            pose.Body.Y += br * 55 * Math.Sin(ph + 0.6);
        }

        foreach (var a in def.Anims)
            ApplyAnim(pose, a, t);

        var dt = _dt;

        // 表情池轮换:poolMs 间隔内随机跳到池内另一个眼环
        if (now >= _poolNext)
        {
            if (def.Pool.Length > 1)
            {
                _poolPos = (_poolPos + 1 + _rand.Next(def.Pool.Length - 1)) % def.Pool.Length;
                SetExpr(def.Pool[_poolPos], def.PoolSpeed);
            }
            _poolNext = now + Rand(def.PoolMinMs, def.PoolMaxMs);
        }

        // 眨眼调度:间隔到点入队关键帧,队列驱动开合度弹簧目标
        if (def.BlinkEnabled && now >= _blinkNext)
        {
            BlinkNow(now);
            _blinkNext = now + Rand(def.BlinkMinMs, def.BlinkMaxMs);
        }
        double? openKey = null;
        while (_blinkQ.Count > 0 && now >= _blinkQ[0].At)
        {
            openKey = _blinkQ[0].V;
            _blinkQ.RemoveAt(0);
        }
        _open.T = openKey ?? (_blinkQ.Count > 0 ? _open.T : def.Openness);

        // 待机小动作:9~18s 随机自旋 / 弹跳 / 眨眼
        if (def.Antics && now >= _anticNext)
        {
            if (!_spinning && _bounceAt < 0)
            {
                var pick = _rand.NextDouble();
                if (pick < 0.45) Spin(1);
                else if (pick < 0.8) Bounce();
                else BlinkNow(now);
            }
            _anticNext = now + Rand(9000, 18000);
        }

        // 弹簧整步(子步 1/120 保证数值稳定):形变 / 开合 / 自旋
        var steps = Math.Max(1, (int)Math.Ceiling(dt / (1.0 / 120)));
        var j = dt / steps;
        for (var si = 0; si < steps; si++)
        {
            _ringSpring.Step(_ringSpeed, 1, j);
            _open.Step(26, 1, j);
            if (_spinning)
            {
                _spinSpring.Step(6.2, 1, j);
                if (Math.Abs(_spinSpring.T - _spinSpring.X) < 0.01 && Math.Abs(_spinSpring.V) < 0.05)
                    _spinning = false;
            }
        }
        pose.Body.Yaw = _spinning ? _spinSpring.X : 0;

        // 弹跳位移:-4·h·n(1-n) 抛物线
        if (_bounceAt >= 0)
        {
            var be = (now - _bounceAt) / 1000.0;
            if (be >= BounceTotal) _bounceAt = -1;
            else
            {
                double acc = 0;
                var bi = 0;
                while (bi < BounceSegs.Length && be >= acc + BounceSegs[bi].D) { acc += BounceSegs[bi].D; bi++; }
                var seg = BounceSegs[Math.Min(bi, BounceSegs.Length - 1)];
                var bn = (be - acc) / seg.D;
                pose.Body.Y += -4 * seg.H * bn * (1 - bn);
            }
        }

        // 眼环插值:形变中逐点插值;静止后同步一次目标环(渲染层切回缓存几何)
        MorphActive = _ringSpring.X < 0.999 || Math.Abs(_ringSpring.V) > 0.001;
        if (MorphActive)
        {
            var rs = Math.Clamp(_ringSpring.X, 0, 1.35);
            for (var i = 0; i < _ringCur.Length; i++)
                _ringCur[i] = _ringSrc[i] + (_ringDst[i] - _ringSrc[i]) * rs;
            RingDirty = true;
            _settleSyncNeeded = true;
        }
        else if (_settleSyncNeeded)
        {
            Array.Copy(_ringDst, _ringCur, _ringCur.Length);
            _settleSyncNeeded = false;
            RingDirty = true;
        }

        // 常驻眼神微漂移:每只眼相位错开,永不完全静止(鼠标注视未移植,目标恒 0)
        if (def.Gaze)
        {
            var w = now / 1000;
            pose.Left.LookX += 1.4 * Math.Sin(0.42 * w) + 0.5 * Math.Sin(1.0 * w);
            pose.Right.LookX += 1.4 * Math.Sin(0.42 * w + 1) + 0.5 * Math.Sin(1.0 * w + 2);
            pose.Left.LookY += 0.9 * Math.Sin(0.58 * w);
            pose.Right.LookY += 0.9 * Math.Sin(0.58 * w + 1);
        }

        // 小尺寸实例放大眼睛占比
        if (_eyeScale != 1)
        {
            pose.Left.ScaleX *= _eyeScale;
            pose.Left.ScaleY *= _eyeScale;
            pose.Right.ScaleX *= _eyeScale;
            pose.Right.ScaleY *= _eyeScale;
        }

        // 开合度 = 配置基础值 × 眨眼弹簧(弹簧可过冲到 1.08)
        var openS = Math.Clamp(_open.X, 0.02, 1.5);
        pose.Left.Open = Math.Clamp(pose.Left.Open, 0, 1.3) * openS;
        pose.Right.Open = Math.Clamp(pose.Right.Open, 0, 1.3) * openS;
        pose.Left.ScaleX = Math.Max(pose.Left.ScaleX, 0.05);
        pose.Left.ScaleY = Math.Max(pose.Left.ScaleY, 0.05);
        pose.Right.ScaleX = Math.Max(pose.Right.ScaleX, 0.05);
        pose.Right.ScaleY = Math.Max(pose.Right.ScaleY, 0.05);

        // 表情切换过渡插值
        var tt = now - _transStart;
        if (_transDur > 0 && tt < _transDur && _prevPose is not null)
            pose.LerpInto(_prevPose, pose, EaseInOutCubic(tt / _transDur));
    }

    /// <summary>sequence 采样;播完按 settle 处理(base 回落 / hold 定格 / next 切换)。</summary>
    private (bool Switched, bool HasPose) SampleSequence(double t, double now)
    {
        var frames = _seqFrames!;
        var last = frames[^1];

        if (t >= last.At)
        {
            if (!_seqDone)
            {
                _seqDone = true;
                if (_seqSettle == "base")
                {
                    // 从序列末帧平滑回落到基础姿态
                    _prevPose = _hasTicked ? Pose.Clone() : last.Pose.Clone();
                    _transStart = now;
                    _transDur = _def.Transition;
                    _seqFrames = null;
                    return (false, false);
                }
                if (_seqSettleNext is not null)
                {
                    SetEmotion(_seqSettleNext);
                    return (true, false);
                }
                // hold:定格在末帧
            }
            _seqScratch.CopyFrom(last.Pose);
            return (false, true);
        }

        if (t <= frames[0].At)
        {
            _seqScratch.CopyFrom(frames[0].Pose);
            return (false, true);
        }
        for (var i = 0; i < frames.Length - 1; i++)
        {
            var a = frames[i];
            var b = frames[i + 1];
            if (t >= a.At && t < b.At)
            {
                var k = EaseInOutCubic((t - a.At) / (b.At - a.At));
                _seqScratch.LerpInto(a.Pose, b.Pose, k);
                return (false, true);
            }
        }
        _seqScratch.CopyFrom(last.Pose);
        return (false, true);
    }

    private readonly PoseState _seqScratch = new();

    // ---------- 动画原语(engine.js ANIM_TYPES / applyAnim) ----------

    private static double EaseInOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    private void BlinkNow(double t)
    {
        // 合上 → 停 70ms → 睁到 1.08 过冲 → 300ms 落回 1,14% 概率追加连眨
        _blinkQ.Add((t, 0.05));
        _blinkQ.Add((t + 70, 0.05));
        _blinkQ.Add((t + 150, 1.08));
        _blinkQ.Add((t + 300, 1));
        if (_rand.NextDouble() < 0.14)
        {
            _blinkQ.Add((t + 370, 0.05));
            _blinkQ.Add((t + 480, 1));
        }
    }

    /// <summary>切换眼环目标:当前插值冻结为新起点,弹簧从 0 重新弹向 1。</summary>
    private void SetExpr(int idx, double speed)
    {
        if (idx == _exprIdx && _ringSpring.X >= 0.999) return;
        var s = Math.Clamp(_ringSpring.X, 0, 1);
        for (var i = 0; i < _ringSrc.Length; i++)
            _ringSrc[i] += (_ringDst[i] - _ringSrc[i]) * s;
        CopyRing(idx, _ringDst);
        _ringSpring.X = 0;
        _ringSpring.V = 0;
        _ringSpring.T = 1;
        _ringSpeed = speed != 0 ? speed : 7;
        _exprIdx = idx;
        _settleSyncNeeded = true;
    }

    private static void CopyRing(int idx, double[] dst)
    {
        var src = EmotionBallData.Rings;
        var offset = idx * 192;
        for (var i = 0; i < 192; i++) dst[i] = src[offset + i];
    }

    private void ApplyAnim(PoseState pose, AnimRaw a, double t)
    {
        var v = EvalAnim(a, t);
        switch (a.Target)
        {
            case "eyes":
                ApplyEyeProp(pose.Left, a.Prop, v);
                ApplyEyeProp(pose.Right, a.Prop, v);
                break;
            case "body":
                ApplyBodyProp(pose.Body, a.Prop, v);
                break;
            case "left":
                ApplyEyeProp(pose.Left, a.Prop, v);
                break;
            case "right":
                ApplyEyeProp(pose.Right, a.Prop, v);
                break;
        }
    }

    private static void ApplyEyeProp(EyeState e, string prop, double v)
    {
        switch (prop)
        {
            case "scale": e.ScaleX += v; e.ScaleY += v; break;
            case "scaleX": e.ScaleX += v; break;
            case "scaleY": e.ScaleY += v; break;
            case "x": e.X += v; break;
            case "y": e.Y += v; break;
            case "rotate": e.Rotate += v; break;
            case "open": e.Open += v; break;
            case "lookX": e.LookX += v; break;
            case "lookY": e.LookY += v; break;
        }
    }

    private static void ApplyBodyProp(BodyState b, string prop, double v)
    {
        switch (prop)
        {
            case "scale": b.Scale += v; break;
            case "x": b.X += v; break;
            case "y": b.Y += v; break;
            case "rotate": b.Rotate += v; break;
            case "breathe": b.Breathe += v; break;
        }
    }

    /// <summary>动画原语求值(engine.js ANIM_TYPES,t 单位毫秒,与原版一致)。</summary>
    private double EvalAnim(AnimRaw a, double t)
    {
        switch (a.Type)
        {
            case "sine":
                return (a.Amp ?? 0) * Math.Sin(Tau * t / (a.Period ?? 2000) + (a.Phase ?? 0));
            case "pulse":
                // 节奏缩放:0 → amp 平滑往复
                return (a.Amp ?? 0) * 0.5 * (1 - Math.Cos(Tau * t / (a.Period ?? 1000) + (a.Phase ?? 0)));
            case "jitter":
            {
                // 随机小抖动(多正弦伪噪声),decay 毫秒内衰减到 0
                var s = t / 1000 * (a.Speed ?? 8);
                var v = (Math.Sin(s * 3.1 + _seed) +
                         Math.Sin(s * 5.7 + _seed * 2.3) +
                         Math.Sin(s * 9.3 + _seed * 4.1)) / 3 * (a.Amp ?? 0);
                if (a.Decay is { } dec) v *= Math.Clamp(1 - t / dec, 0, 1);
                return v;
            }
            case "scan":
            {
                // 三角波快速来回扫动
                var per = a.Period ?? 800;
                var p = (t + (a.PhaseMs ?? 0)) % per / per;
                var tri = p < 0.5 ? p * 4 - 1 : 3 - p * 4;
                return (a.Amp ?? 0) * tri;
            }
            case "glance":
            {
                // 张望:平滑方波,在 ±amp 两端各停留片刻再换边
                var per = a.Period ?? 3600;
                var ph = Tau * ((t + (a.PhaseMs ?? 0)) % per / per) + (a.Phase ?? 0);
                return (a.Amp ?? 0) * Math.Tanh(2.8 * Math.Sin(ph));
            }
            case "blink":
            {
                // 周期眨眼:interval 周期内前 dur 毫秒闭合再睁开(负值叠加到 open)
                var interval = a.Interval ?? 3800;
                var dur = a.Dur ?? 200;
                var p = (t + (a.PhaseMs ?? 0) + _seed * 97) % interval;
                if (p >= dur) return 0;
                return -(a.Depth ?? 1) * Math.Sin(Math.PI * (p / dur));
            }
            default:
                return 0;
        }
    }
}
