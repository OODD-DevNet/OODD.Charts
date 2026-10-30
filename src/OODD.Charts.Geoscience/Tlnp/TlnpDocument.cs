using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Geoscience.Tlnp
{
    /// <summary>
    /// 热力学常数。全部由文档给出，模板与线族只读它，不在代码里写死物理常数。
    /// <para>
    /// 这些是物理定义常数（干空气气体常数 / 定压比热 / 汽化潜热 / 摩尔比 / Magnus 系数），
    /// 不是从某份标准里抄来的数值表；底图的坐标常数仍走 <see cref="TlnpStyle"/> 令牌（docs/06 D21）。
    /// </para>
    /// </summary>
    public sealed class SoundingConstants
    {
        /// <summary>位温与饱和比湿的参考气压（hPa）。T-lnP 横轴读数按该气压处的温度标定。</summary>
        public double ReferencePressureHpa = 1000d;

        /// <summary>干空气气体常数（J·kg⁻¹·K⁻¹）。</summary>
        public double GasConstantDry = 287.05d;

        /// <summary>干空气定压比热（J·kg⁻¹·K⁻¹）。</summary>
        public double SpecificHeatDry = 1004.7d;

        /// <summary>汽化潜热（J·kg⁻¹）。</summary>
        public double LatentHeatVaporization = 2.501e6d;

        /// <summary>水汽与干空气的摩尔质量比 ε。</summary>
        public double Epsilon = 0.622d;

        /// <summary>Magnus 公式系数 a（无量纲）。</summary>
        public double MagnusA = 17.67d;

        /// <summary>Magnus 公式系数 b（℃）。</summary>
        public double MagnusB = 243.5d;

        /// <summary>0 ℃ 时的饱和水汽压（hPa）。</summary>
        public double SaturationPressureAtZeroC = 6.112d;

        /// <summary>干绝热指数 κ = R<sub>d</sub>/c<sub>p</sub>。</summary>
        public double Kappa => GasConstantDry / SpecificHeatDry;

        public SoundingConstants Clone() => (SoundingConstants)MemberwiseClone();
    }

    /// <summary>探空的一个层次。气压、温度、露点均为文档数据，库不内置任何观测值。</summary>
    public sealed class SoundingLevel
    {
        public SoundingLevel(double pressureHpa, double temperatureC, double dewPointC)
        {
            PressureHpa = pressureHpa;
            TemperatureC = temperatureC;
            DewPointC = dewPointC;
        }

        /// <summary>气压（hPa），必须为正。</summary>
        public double PressureHpa { get; set; }

        /// <summary>温度（℃）。</summary>
        public double TemperatureC { get; set; }

        /// <summary>露点（℃）。</summary>
        public double DewPointC { get; set; }

        /// <summary>该层的可选注记（如“逆温层顶”）。</summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// 特征层标注：在指定气压处把短标签落在 T 曲线旁边。
    /// 标签框的最终位置由模板做避让后写进版面，绘制与断言共用同一个框。
    /// </summary>
    public sealed class SoundingCharacteristicLevel
    {
        public SoundingCharacteristicLevel(double pressureHpa, string label)
        {
            PressureHpa = pressureHpa;
            Label = label;
        }

        public double PressureHpa { get; }

        public string Label { get; }

        /// <summary>优先放在曲线左侧（false 时优先右侧）。避让失败会换边，不保证最终仍在首选侧。</summary>
        public bool PreferLeft { get; set; } = true;
    }

    /// <summary>
    /// 探空文档。T261（T-lnP 底图）与 T262（层结曲线 / 特征层 / 层次填图）是<b>同一模板</b>的
    /// 不同字段，不新增模板键：关掉 <see cref="ShowDewPoint"/> 与特征层就是纯底图，
    /// 打开就是探空图（docs/07 T261/T262）。
    /// </summary>
    public sealed class SoundingDocument : ChartDocument
    {
        public SoundingDocument()
        {
            FormTitle = "T-lnP 温度对数压力图";
        }

        public SoundingConstants Constants { get; set; } = new SoundingConstants();

        public IList<SoundingLevel> Levels { get; } = new List<SoundingLevel>();

        public IList<SoundingCharacteristicLevel> CharacteristicLevels { get; } =
            new List<SoundingCharacteristicLevel>();

        /// <summary>要填斜线的层次下标（与按气压降序排好的 <see cref="OrderedLevels"/> 对应）；
        /// 负值表示不填。取 i 与 i+1 两层之间的 T—td 四边形。</summary>
        public int HatchedLevelIndex { get; set; } = -1;

        /// <summary>是否画露点曲线。false 时只剩温度曲线（纯底图形态）。</summary>
        public bool ShowDewPoint { get; set; } = true;

        /// <summary>是否画特征层标注。false 时不产生标注框，也不落文字。</summary>
        public bool ShowCharacteristicLabels { get; set; } = true;

        /// <summary>站名/站号，画在标题下方。</summary>
        public string? StationName { get; set; }

        /// <summary>时次说明，画在标题右侧。</summary>
        public string? ValidTimeText { get; set; }

        /// <summary>页脚说明；为空时用模板令牌里的默认说明。</summary>
        public string? FooterNote { get; set; }

        /// <summary>按气压从大到小（地面→高空）排好的层次副本。纯函数，不改动原列表。</summary>
        public IReadOnlyList<SoundingLevel> OrderedLevels()
        {
            List<SoundingLevel> copy = new List<SoundingLevel>(Levels);
            copy.Sort((a, b) => b.PressureHpa.CompareTo(a.PressureHpa));
            return copy;
        }
    }
}
