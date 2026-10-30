using System;
using SkiaSharp;

namespace OODD.Charts.Geoscience.Tlnp
{
    /// <summary>
    /// T-lnP 表单令牌。坐标纸的全部常数（气压范围、温度范围、倾斜量、线族成员、格距）
    /// 都在这里，模板与线族代码不出现字面量坐标。
    /// <para>
    /// <b>未校准</b>：倾斜量、幅面与线族密度是按“等压线等距、等温线斜直、交点可解析”这三条
    /// 几何纪律定的演示值，没有与 QX/T 123-2011 或任何业务底图逐项对齐；
    /// 独立标准代号亦未核实（docs/05 §5、docs/06 §4.9）。
    /// </para>
    /// </summary>
    public sealed class TlnpStyle
    {
        public GeoscienceStyle Base = new GeoscienceStyle();

        public GeosciencePalette Palette = new GeosciencePalette();

        /// <summary>绘图区左侧留给气压刻度的宽度（px）。</summary>
        public float PlotLeftGutter = 52f;

        /// <summary>绘图区右侧留白（px）。</summary>
        public float PlotRightInset = 8f;

        /// <summary>标题带与绘图区之间的留白（px）。</summary>
        public float PlotTopInset = 8f;

        /// <summary>绘图区下方温度刻度带的高度（px）。</summary>
        public float XAxisHeight = 26f;

        /// <summary>气压轴顶端（hPa，图上方）。</summary>
        public double PressureMinHpa = 100d;

        /// <summary>气压轴底端（hPa，图下方）。</summary>
        public double PressureMaxHpa = 1050d;

        /// <summary>横轴（1000 hPa 处温度读数）下限（℃）。</summary>
        public double TemperatureMinC = -80d;

        /// <summary>横轴上限（℃）。</summary>
        public double TemperatureMaxC = 60d;

        /// <summary>等温线间隔（℃）。</summary>
        public double IsothermStepC = 10d;

        /// <summary>
        /// 倾斜量：每降一个数量级气压，等温线向右平移多少摄氏度。
        /// 等温线在 (x, log p) 域里是严格直线，x = T + 倾斜量·log10(P₀/p)。
        /// </summary>
        public double SkewDegreesPerDecade = 110d;

        /// <summary>等压线（hPa）。等距由对数纵轴保证，不靠这里等间隔。</summary>
        public double[] Isobars = { 1050d, 1000d, 850d, 700d, 500d, 400d, 300d, 250d, 200d, 150d, 100d };

        /// <summary>干绝热线的位温（℃）。</summary>
        public double[] DryAdiabatsC = { -40d, -20d, 0d, 20d, 40d, 60d, 80d, 100d, 140d, 180d };

        /// <summary>湿绝热线的湿球位温（℃），积分起点取 1000 hPa。</summary>
        public double[] MoistAdiabatsC = { -20d, -10d, 0d, 10d, 20d, 30d };

        /// <summary>等饱和比湿线（g/kg）。</summary>
        public double[] MixingRatiosGPerKg = { 0.4d, 1d, 2d, 4d, 8d, 12d, 16d, 20d };

        /// <summary>曲线族每条线的采样点数（对数气压等分）。</summary>
        public int CurveSamples = 96;

        public float IsobarStroke = 0.75f;
        public float IsothermStroke = 0.55f;
        public float DryAdiabatStroke = 0.45f;
        public float MoistAdiabatStroke = 0.45f;
        public float MixingRatioStroke = 0.4f;
        public float TemperatureCurveStroke = 2.1f;
        public float DewPointCurveStroke = 1.5f;

        public SKColor IsobarColor = new SKColor(45, 45, 45);
        public SKColor IsothermColor = new SKColor(125, 125, 125);
        public SKColor DryAdiabatColor = new SKColor(166, 120, 60);
        public SKColor MoistAdiabatColor = new SKColor(60, 140, 90);
        public SKColor MixingRatioColor = new SKColor(140, 100, 160);

        /// <summary>曲线上的层次点半径（px）。0 表示不点。</summary>
        public float LevelMarkerRadius = 1.6f;

        public float AxisLabelSize = 7f;

        /// <summary>刻度文字框高 / 字号。</summary>
        public float AxisLabelHeightFactor = 1.6f;

        /// <summary>刻度文字与刻线之间的内边距（px）。</summary>
        public float AxisLabelPadding = 2f;

        /// <summary>温度刻度文字框的半宽（px）。</summary>
        public float TemperatureLabelHalfWidth = 18f;

        /// <summary>横轴标题框宽（px）与标题距刻度行的间隙（px）。</summary>
        public float TemperatureAxisTitleWidth = 160f;

        public float AxisTitleGap = 1f;

        public float AxisTitleSize = 8.5f;

        /// <summary>标题带内左右两侧站名/时次的贴边距离（px）。</summary>
        public float TitleSidePadding = 4f;

        public float CharacteristicLabelSize = 7.5f;

        /// <summary>标签框高 / 字号。</summary>
        public float CharacteristicBoxHeightFactor = 1.45f;

        /// <summary>特征层标签框与曲线之间的最小间隙（px）。</summary>
        public float CharacteristicGap = 4f;

        /// <summary>特征层标签框内边距（px）。</summary>
        public float CharacteristicPadding = 2f;

        /// <summary>标签避让时每次纵向试探的步长（× 框高）。</summary>
        public float CharacteristicNudgeFactor = 1.25f;

        /// <summary>纵向试探的层数（上下各试探这么多次）。</summary>
        public int CharacteristicNudgeSteps = 4;

        public float CharacteristicLeaderStroke = 0.5f;

        /// <summary>特征层标签框线粗（px）与锚点圆半径（px）。</summary>
        public float CharacteristicBoxStroke = 0.6f;

        public float CharacteristicAnchorRadius = 1.3f;

        public float GridTickLength = 4f;

        /// <summary>层次填图的斜线间距（mm）与线粗（mm）。</summary>
        public float HatchSpacingMm = 1.8f;

        public float HatchStrokeMm = 0.3f;

        public string PressureFormat = "0";

        public string TemperatureFormat = "0";

        /// <summary>页脚说明：取证状态写在图上，避免被当作国标 1:1 底图。</summary>
        public string FooterNote = "示例底图：倾斜量与线族密度为可调令牌，未按 QX/T 123 底图校准";

        public TlnpStyle Clone()
        {
            TlnpStyle copy = (TlnpStyle)MemberwiseClone();
            copy.Base = Base.Clone();
            copy.Palette = Palette.Clone();
            copy.Isobars = (double[])Isobars.Clone();
            copy.DryAdiabatsC = (double[])DryAdiabatsC.Clone();
            copy.MoistAdiabatsC = (double[])MoistAdiabatsC.Clone();
            copy.MixingRatiosGPerKg = (double[])MixingRatiosGPerKg.Clone();
            return copy;
        }

        /// <summary>按 <see cref="TemperatureMinC"/> / <see cref="TemperatureMaxC"/> / 间隔生成等温线成员。</summary>
        public double[] IsothermMembers()
        {
            if (!(IsothermStepC > 0d))
            {
                throw new InvalidOperationException("等温线间隔必须为正，实到 " + IsothermStepC);
            }

            int count = (int)Math.Round((TemperatureMaxC - TemperatureMinC) / IsothermStepC);
            double[] values = new double[count + 1];
            for (int i = 0; i <= count; i++)
            {
                values[i] = TemperatureMinC + i * IsothermStepC;
            }
            return values;
        }
    }
}
