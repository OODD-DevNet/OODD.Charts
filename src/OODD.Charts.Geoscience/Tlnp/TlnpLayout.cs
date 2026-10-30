using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using SkiaSharp;

namespace OODD.Charts.Geoscience.Tlnp
{
    /// <summary>一个已避让好的特征层标注：文本、标签框与曲线上的引出锚点。</summary>
    public sealed class TlnpCharacteristicLabel
    {
        internal TlnpCharacteristicLabel(SoundingCharacteristicLevel level, SKRect box, SKPoint anchor)
        {
            Level = level;
            Box = box;
            Anchor = anchor;
        }

        public SoundingCharacteristicLevel Level { get; }

        public string Text => Level.Label;

        /// <summary>最终标签框（已保证不与 T/td 曲线段、其他标签框相交）。</summary>
        public SKRect Box { get; }

        /// <summary>曲线上的引出锚点（该气压处温度曲线的像素点）。</summary>
        public SKPoint Anchor { get; }
    }

    /// <summary>
    /// T-lnP 的版面结果：两条像素轴、五族线、T/td 曲线、特征层标签与填图层多边形。
    /// 绘制与测试都从这里取落点，避免“画一套、测另一套”。
    /// </summary>
    public sealed class TlnpLayout
    {
        private readonly List<SKPoint> _temperatureCurve;
        private readonly List<SKPoint> _dewPointCurve;
        private readonly List<TlnpCharacteristicLabel> _labels;
        private readonly List<SKPoint> _hatchPolygon;

        internal TlnpLayout(
            SKRect bounds,
            SKRect titleRect,
            SKRect footerRect,
            SKRect plotRect,
            SKRect xAxisRect,
            SKRect pressureAxisTitleRect,
            NumericAxis temperatureAxis,
            NumericAxis pressureAxis,
            LineFamilyGenerator families,
            double skewDegreesPerDecade,
            double referencePressureHpa,
            List<SKPoint> temperatureCurve,
            List<SKPoint> dewPointCurve,
            List<TlnpCharacteristicLabel> labels,
            List<SKPoint> hatchPolygon)
        {
            Bounds = bounds;
            TitleRect = titleRect;
            FooterRect = footerRect;
            PlotRect = plotRect;
            XAxisRect = xAxisRect;
            PressureAxisTitleRect = pressureAxisTitleRect;
            TemperatureAxis = temperatureAxis;
            PressureAxis = pressureAxis;
            Families = families;
            SkewDegreesPerDecade = skewDegreesPerDecade;
            ReferencePressureHpa = referencePressureHpa;
            _temperatureCurve = temperatureCurve;
            _dewPointCurve = dewPointCurve;
            _labels = labels;
            _hatchPolygon = hatchPolygon;
        }

        public SKRect Bounds { get; }
        public SKRect TitleRect { get; }
        public SKRect FooterRect { get; }
        public SKRect PlotRect { get; }
        public SKRect XAxisRect { get; }

        /// <summary>
        /// 气压轴标题「气压/hPa」的独占带：在顶端刻度（100 hPa）之上。
        /// 该带与刻度带必须分开，否则标题的下半截会压住 100。
        /// </summary>
        public SKRect PressureAxisTitleRect { get; }

        /// <summary>横轴：域的 x 坐标（1000 hPa 处的温度读数）→ 像素。</summary>
        public NumericAxis TemperatureAxis { get; }

        /// <summary>纵轴：对数气压 → 像素。</summary>
        public NumericAxis PressureAxis { get; }

        public LineFamilyGenerator Families { get; }

        public double SkewDegreesPerDecade { get; }

        public double ReferencePressureHpa { get; }

        public IReadOnlyList<SKPoint> TemperatureCurve => _temperatureCurve;

        public IReadOnlyList<SKPoint> DewPointCurve => _dewPointCurve;

        public IReadOnlyList<TlnpCharacteristicLabel> CharacteristicLabels => _labels;

        /// <summary>层次填图的多边形；未启用时为空集合。</summary>
        public IReadOnlyList<SKPoint> HatchPolygon => _hatchPolygon;

        /// <summary>
        /// 温度—气压 → 域横坐标：x = T + 倾斜量·log10(P₀/p)。
        /// 等温线在该坐标下是严格直线，等压线是水平线。
        /// </summary>
        public double TemperatureCoordinate(double temperatureC, double pressureHpa)
        {
            if (!(pressureHpa > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(pressureHpa), "气压必须为正，实到 " + pressureHpa);
            }
            return temperatureC + SkewDegreesPerDecade * Math.Log10(ReferencePressureHpa / pressureHpa);
        }

        /// <summary>温度—气压 → 像素（夹紧到轴内，适合落点/锚点）。</summary>
        public SKPoint PointOf(double temperatureC, double pressureHpa)
        {
            return new SKPoint(
                TemperatureAxis.PositionOf(TemperatureCoordinate(temperatureC, pressureHpa)),
                PressureAxis.PositionOf(pressureHpa));
        }

        /// <summary>温度—气压 → 像素（不夹紧，适合画曲线后由裁剪矩形切边）。</summary>
        public SKPoint ExtendedPointOf(double temperatureC, double pressureHpa)
        {
            return new SKPoint(
                TemperatureAxis.PositionOfExtended(TemperatureCoordinate(temperatureC, pressureHpa)),
                PressureAxis.PositionOfExtended(pressureHpa));
        }

        /// <summary>域横坐标 → 像素。</summary>
        public float XOfCoordinate(double coordinate)
        {
            return TemperatureAxis.PositionOf(coordinate);
        }

        /// <summary>气压 → 像素 y。</summary>
        public float YOf(double pressureHpa)
        {
            return PressureAxis.PositionOf(pressureHpa);
        }
    }
}
