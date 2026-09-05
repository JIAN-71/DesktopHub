/* ============================================================
 * EmotionBallControl.cs —— 表情球 WPF 渲染控件,移植自 aora-bot emotion-ball(js/ball.js)
 *
 * 渲染范围:身体(径向渐变 blob 轮廓)+ 双眼(48 点轮廓眼环,球面投影贴合身体),
 * 对应原版 applyPose/setEye;彩带/撒花/zzz 等粒子特效未移植。
 *
 * 实现要点:
 *   - 单 Visual 自绘(OnRender),CompositionTarget.Rendering 驱动引擎 Tick;
 *   - 逐帧只改 Transform 属性与 Brush 引用(零分配),眼环几何仅在形变帧重建;
 *   - 坐标系与原版一致:viewBox -15 -15 259 259(世界变换里补正 15px 边距),
 *     身体中心 HeadC,眼睛按身体轮廓局部半宽做经度换算 + 余弦压缩,自旋到背面自动隐藏。
 *   - 与原版的唯一偏离:双眼对水平正面化(FaceFrontX)。原版眼环把注视方向烘焙在位置里
 *     (待机环 0 看右上 / 环 8 看左下),大尺寸网页下读作"张望",44px 胶囊里会读成"侧脸背对",
 *     故投影前把双眼对中点拉回面部中线;环的形状/俯仰/大小差异与 look/自旋动画全保留。
 * 上游: https://github.com/sam70361/aora-bot (社区许可,非商业使用需注明出处)
 * ============================================================ */

using System.Windows;
using System.Windows.Media;

namespace DesktopHub.App.EmotionBall;

public sealed class EmotionBallControl : FrameworkElement
{
    private const double View = 259;  // 与原版 viewBox 一致(-15 -15 259 259)
    private const double Pad = 15;

    private readonly EmotionEngine _engine;

    // ---------- 静态几何缓存 ----------

    private static readonly StreamGeometry BodyGeometry = BuildClosed(EmotionBallData.BodyRing, 0, EmotionBallData.BodyPoints);
    private static readonly StreamGeometry[][] EyeGeometries = BuildEyeGeometries();

    // 身体轮廓逐行半宽采样表(ball.js silRows),供眼睛贴合任意高度的轮廓宽度
    private const double SilStep = 2;
    private static readonly double SilMinY, SilMaxY;
    private static readonly double[] SilRows;

    static EmotionBallControl()
    {
        var ring = EmotionBallData.BodyRing;
        double minY = double.MaxValue, maxY = double.MinValue;
        for (var i = 0; i < EmotionBallData.BodyPoints; i++)
        {
            var y = ring[i * 2 + 1];
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        SilMinY = minY;
        SilMaxY = maxY;

        var rows = (int)Math.Ceiling((maxY - minY) / SilStep) + 1;
        var data = new double[rows * 2];
        for (var r = 0; r < rows; r++)
        {
            var y = minY + r * SilStep;
            double lo = double.MaxValue, hi = double.MinValue;
            for (var e = 0; e < EmotionBallData.BodyPoints; e++)
            {
                var a = e * 2;
                var b = (e + 1) % EmotionBallData.BodyPoints * 2;
                var y0 = ring[a + 1];
                var y1 = ring[b + 1];
                if (y0 <= y && y1 >= y || y1 <= y && y0 >= y)
                {
                    var t = y1 == y0 ? 0 : (y - y0) / (y1 - y0);
                    var x = ring[a] + (ring[b] - ring[a]) * t;
                    if (x < lo) lo = x;
                    if (x > hi) hi = x;
                }
            }
            if (lo > hi) { lo = EmotionBallData.HeadC - 4; hi = EmotionBallData.HeadC + 4; }
            data[r * 2] = lo;
            data[r * 2 + 1] = hi;
        }
        SilRows = data;
    }

    private static (double Lo, double Hi) SilAt(double y)
    {
        var r = (int)Math.Round((Math.Clamp(y, SilMinY, SilMaxY) - SilMinY) / SilStep);
        r = Math.Clamp(r, 0, SilRows.Length / 2 - 1);
        return (SilRows[r * 2], SilRows[r * 2 + 1]);
    }

    private static StreamGeometry BuildClosed(double[] flat, int pointOffset, int count)
    {
        var geo = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var ctx = geo.Open())
        {
            var o = pointOffset * 2;
            ctx.BeginFigure(new Point(flat[o], flat[o + 1]), true, true);
            for (var i = 1; i < count; i++)
            {
                var j = o + i * 2;
                ctx.LineTo(new Point(flat[j], flat[j + 1]), true, false);
            }
        }
        geo.Freeze();
        return geo;
    }

    private static StreamGeometry[][] BuildEyeGeometries()
    {
        var all = new StreamGeometry[EmotionBallData.RingCount][];
        for (var i = 0; i < EmotionBallData.RingCount; i++)
        {
            all[i] =
            [
                BuildClosed(EmotionBallData.Rings, (i * 2) * EmotionBallData.EyePoints, EmotionBallData.EyePoints),
                BuildClosed(EmotionBallData.Rings, (i * 2 + 1) * EmotionBallData.EyePoints, EmotionBallData.EyePoints),
            ];
        }
        return all;
    }

    // ---------- 变换(逐帧只改属性值,零分配) ----------

    private readonly TransformGroup _world = new();
    private readonly TranslateTransform _worldOffset = new();
    private readonly ScaleTransform _worldScale = new();

    private readonly TransformGroup _bodyT = new();
    private readonly TranslateTransform _bodyPos = new(EmotionBallData.HeadC, EmotionBallData.HeadC);
    private readonly RotateTransform _bodyRot = new();
    private readonly ScaleTransform _bodyScale = new();
    private readonly TranslateTransform _bodyOrigin = new(-EmotionBallData.HeadC, -EmotionBallData.HeadC);

    private readonly TransformGroup _eyeLT = new();
    private readonly TransformGroup _eyeRT = new();
    private readonly TranslateTransform[] _eyePos = { new(), new() };
    private readonly RotateTransform[] _eyeRot = { new(), new() };
    private readonly ScaleTransform[] _eyeScaleT = { new(), new() };
    private readonly TranslateTransform[] _eyeBase = { new(), new() };

    // ---------- 笔刷(按颜色缓存引用,变化时才重建) ----------

    private Brush _bodyBrush = BuildBodyBrush("#F3F0EA");
    private string _bodyColor = "#F3F0EA";
    private readonly Brush?[] _eyeBrush = new Brush?[2];
    private readonly string?[] _eyeColor = new string?[2];

    private StreamGeometry _eyeLGeo;
    private StreamGeometry _eyeRGeo;

    public EmotionBallControl()
    {
        _engine = new EmotionEngine("01"); // 开场「唤醒」→ 序列播完自动落到「待机放空」
        _eyeLGeo = EyeGeometries[_engine.ExprIndex][0];
        _eyeRGeo = EyeGeometries[_engine.ExprIndex][1];

        // 顺序即应用顺序(先排先应用):先补 viewBox -15 边距(未缩放坐标),再缩放,最后居中。
        // ⚠️ 顺序写反会让 15px 平移作用在缩放后的最终坐标上,球体整体偏右下 15px。
        _world.Children.Add(new TranslateTransform(Pad, Pad));
        _world.Children.Add(_worldScale);
        _world.Children.Add(_worldOffset);

        _bodyT.Children.Add(_bodyPos);
        _bodyT.Children.Add(_bodyRot);
        _bodyT.Children.Add(_bodyScale);
        _bodyT.Children.Add(_bodyOrigin);

        foreach (var eye in new[] { 0, 1 })
        {
            var tg = eye == 0 ? _eyeLT : _eyeRT;
            tg.Children.Add(_eyePos[eye]);
            tg.Children.Add(_eyeRot[eye]);
            tg.Children.Add(_eyeScaleT[eye]);
            tg.Children.Add(_eyeBase[eye]);
        }

        Loaded += (_, _) => { UpdateFit(); CompositionTarget.Rendering += OnRendering; };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnRendering;
        SizeChanged += (_, _) => UpdateFit();
    }

    // ---------- 对外 API ----------

    /// <summary>切换表情(如 "10" 开心、"21" 生气;未知 ID 自动回退待机)。</summary>
    public void SetEmotion(string id) => _engine.SetEmotion(id);

    /// <summary>点击彩蛋:自旋一圈。</summary>
    public void Spin(int turns = 1) => _engine.Spin(turns);

    /// <summary>弹跳一下。</summary>
    public void Bounce() => _engine.Bounce();

    /// <summary>当前表情 ID。</summary>
    public string? EmotionId => _engine.EmotionId;

    /// <summary>小尺寸下放大眼睛占比,保证 32~48px 仍可读(默认 1.15)。</summary>
    public double EyeScale { get => _engine.EyeScale; set => _engine.EyeScale = value; }

    // ---------- 渲染循环 ----------

    private void OnRendering(object? sender, EventArgs e)
    {
        try
        {
            if (!IsVisible || ActualWidth <= 0 || ActualHeight <= 0) return;
            _engine.Tick();
            if (_engine.RingDirty) RefreshEyeGeometries();
            InvalidateVisual();
        }
        catch (Exception)
        {
            // 渲染循环兜底:单帧异常不中断动画(此前观察到的异常均来自调试探针,已移除)
        }
    }

    private void RefreshEyeGeometries()
    {
        if (_engine.MorphActive)
        {
            _eyeLGeo = BuildMorphGeometry(0);
            _eyeRGeo = BuildMorphGeometry(1);
        }
        else
        {
            _eyeLGeo = EyeGeometries[_engine.ExprIndex][0];
            _eyeRGeo = EyeGeometries[_engine.ExprIndex][1];
        }
    }

    /// <summary>形变中的临时几何:从引擎的插值数组重建(每帧一次,仅形变期间)。</summary>
    private StreamGeometry BuildMorphGeometry(int eye)
    {
        var flat = _engine.RingCur;
        var geo = new StreamGeometry { FillRule = FillRule.Nonzero };
        var o = eye * EmotionBallData.EyePoints * 2;
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(flat[o], flat[o + 1]), true, true);
            for (var i = 1; i < EmotionBallData.EyePoints; i++)
            {
                var j = o + i * 2;
                ctx.LineTo(new Point(flat[j], flat[j + 1]), true, false);
            }
        }
        geo.Freeze();
        return geo;
    }

    private void UpdateFit()
    {
        var s = Math.Min(ActualWidth, ActualHeight) / View;
        if (s <= 0 || !double.IsFinite(s)) return;
        _worldScale.ScaleX = _worldScale.ScaleY = s;
        _worldOffset.X = (ActualWidth - View * s) / 2;
        _worldOffset.Y = (ActualHeight - View * s) / 2;
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(_world);
        var pose = _engine.Pose;

        // 身体:translate(HeadC+x, HeadC+y) · rotate · scale · translate(-HeadC,-HeadC)
        _bodyPos.X = EmotionBallData.HeadC + pose.Body.X;
        _bodyPos.Y = EmotionBallData.HeadC + pose.Body.Y;
        _bodyRot.Angle = pose.Body.Rotate;
        _bodyScale.ScaleX = _bodyScale.ScaleY = pose.Body.Scale;
        dc.PushTransform(_bodyT);
        dc.DrawGeometry(BodyBrush(pose.Body.Color), null, BodyGeometry);
        dc.Pop();

        // 双眼:球面投影定位,自旋到背面时隐藏
        var yaw = pose.Body.Yaw;
        var (bxL, byL) = Centroid(0);
        var (bxR, byR) = Centroid(1);
        // 双眼对公共水平偏移:眼环数据把注视方向烘焙在位置里(如待机环偏向一侧),
        // 小胶囊尺寸下整对眼睛挤向一边 + 余弦压缩会被读成"侧脸/背对"。
        // FaceFrontX=1 把双眼对中点拉回面部中线(只动位置,保留环的形状/大小/俯仰),
        // 是相对原版的有意偏离;lookX/自旋 yaw 在其后叠加,不受影响。
        var pairDx = ((bxL + bxR) / 2 - EmotionBallData.HeadC) * FaceFrontX;
        if (UpdateEyeTransform(pose.Left, 0, yaw, bxL, byL, pairDx))
        {
            dc.PushTransform(_eyeLT);
            dc.DrawGeometry(EyeBrush(pose.Left.Color, 0), null, _eyeLGeo);
            dc.Pop();
        }
        if (UpdateEyeTransform(pose.Right, 1, yaw, bxR, byR, pairDx))
        {
            dc.PushTransform(_eyeRT);
            dc.DrawGeometry(EyeBrush(pose.Right.Color, 1), null, _eyeRGeo);
            dc.Pop();
        }

        dc.Pop();
    }

    /// <summary>双眼对水平正面化强度(1=完全回正,0=原版烘焙注视位)。</summary>
    private const double FaceFrontX = 1.0;

    /// <summary>眼睛定位(ball.js setEye):轮廓贴合 + 经度换算 + 余弦压缩。</summary>
    private bool UpdateEyeTransform(EyeState e, int eye, double yaw, double bx, double by, double pairDx)
    {
        var open = Math.Clamp(e.Open, 0.02, 2.4);
        var sy = Math.Clamp(e.ScaleY * open * EmotionBallData.FaceEye, 0.02, 2.4);
        var sxBase = e.ScaleX * EmotionBallData.FaceEye;

        // 纵向:脸部拟合映射 + 轮廓钳制
        var halfH = EmotionBallData.EyeHalf * sy + 2;
        var ey0 = EmotionBallData.HeadC + EmotionBallData.FaceY
                + (by - EmotionBallData.HeadC) * EmotionBallData.FaceSy + e.Y + e.LookY;
        ey0 = Math.Clamp(ey0, SilMinY + halfH, SilMaxY - halfH);

        var (lo, hi) = SilAt(ey0);
        var cx0 = (lo + hi) / 2;
        var hw = Math.Max((hi - lo) / 2, 12);

        // 横向:经度换算 + 双眼对正面化 + 自旋偏航 + 余弦压缩
        var ox = EmotionBallData.FaceX + (bx - EmotionBallData.HeadC - pairDx) * EmotionBallData.FaceSx
               + e.X + e.LookX;
        var theta = Math.Clamp(ox / hw, -1.15, 1.15);
        var total = theta + yaw;
        var cn = Math.Cos(total);
        if (cn <= 0.02) return false; // 绕到背面,隐藏
        var ex = cx0 + hw * Math.Sin(total) * 0.985;
        var dyN = (ey0 - EmotionBallData.HeadC) / 130;
        var fy = Math.Sqrt(1 - dyN * dyN * 0.22);

        _eyePos[eye].X = ex;
        _eyePos[eye].Y = ey0;
        _eyeRot[eye].Angle = e.Rotate;
        _eyeScaleT[eye].ScaleX = sxBase * cn;
        _eyeScaleT[eye].ScaleY = sy * fy;
        _eyeBase[eye].X = -bx;
        _eyeBase[eye].Y = -by;
        return true;
    }

    private (double X, double Y) Centroid(int eye)
    {
        var flat = _engine.RingCur;
        double x = 0, y = 0;
        var o = eye * EmotionBallData.EyePoints * 2;
        for (var i = 0; i < EmotionBallData.EyePoints; i++)
        {
            x += flat[o + i * 2];
            y += flat[o + i * 2 + 1];
        }
        return (x / EmotionBallData.EyePoints, y / EmotionBallData.EyePoints);
    }

    // ---------- 笔刷 ----------

    private Brush BodyBrush(string color)
    {
        if (color != _bodyColor)
        {
            _bodyColor = color;
            _bodyBrush = BuildBodyBrush(color);
        }
        return _bodyBrush;
    }

    private Brush EyeBrush(string color, int eye)
    {
        if (color != _eyeColor[eye])
        {
            _eyeColor[eye] = color;
            var (r, g, b) = ColorUtil.Parse(color);
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            _eyeBrush[eye] = brush;
        }
        return _eyeBrush[eye]!;
    }

    /// <summary>身体径向渐变:球面高光(0% 提亮 22% / 62% 本色 / 100% 压暗 12%),对应原版 radialGradient。</summary>
    private static Brush BuildBodyBrush(string color)
    {
        var (r, g, b) = ColorUtil.Parse(color);
        var stops = new GradientStopCollection
        {
            new(Color.FromRgb(ColorUtil.Shade(r, 0.22), ColorUtil.Shade(g, 0.22), ColorUtil.Shade(b, 0.22)), 0),
            new(Color.FromRgb(r, g, b), 0.62),
            new(Color.FromRgb(ColorUtil.Shade(r, -0.12), ColorUtil.Shade(g, -0.12), ColorUtil.Shade(b, -0.12)), 1),
        };
        var brush = new RadialGradientBrush(stops)
        {
            Center = new Point(0.38, 0.32),
            GradientOrigin = new Point(0.38, 0.32),
            RadiusX = 0.75,
            RadiusY = 0.75,
        };
        brush.Freeze();
        return brush;
    }
}
