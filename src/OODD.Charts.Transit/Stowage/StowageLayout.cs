using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace OODD.Charts.Transit.Stowage
{
    /// <summary>
    /// 积载图版面：左 = 纵向剖面（bay × tier），右 = 横剖面（row × tier）。
    /// <para>
    /// 两个视图共用同一批 <see cref="StowageContainer"/> 与同一个 tier 轴；
    /// "同一箱的可见性判定"由 <see cref="ProfileVisible"/> / <see cref="SectionVisible"/> 给出，
    /// 绘制层只认这两个函数，测试也断言这两个函数——判定与落笔同源。
    /// </para>
    /// </summary>
    public sealed class StowageLayout
    {
        private readonly List<string> _ports;
        private readonly Dictionary<string, int> _portIndex;

        private StowageLayout(StowageDocument document, StowageStyle tokens, SKRect bounds,
                              SKRect titleRect, SKRect noteRect, SKRect profilePanel, SKRect sectionPanel,
                              SKRect footerRect, BayRowTierAxis axis, List<string> ports,
                              Dictionary<string, int> portIndex)
        {
            Document = document;
            Tokens = tokens;
            Bounds = bounds;
            TitleRect = titleRect;
            NoteRect = noteRect;
            ProfilePanel = profilePanel;
            SectionPanel = sectionPanel;
            FooterRect = footerRect;
            Axis = axis;
            _ports = ports;
            _portIndex = portIndex;
        }

        public StowageDocument Document { get; }
        public StowageStyle Tokens { get; }

        public SKRect Bounds { get; }
        public SKRect TitleRect { get; }
        public SKRect NoteRect { get; }
        public SKRect ProfilePanel { get; }
        public SKRect SectionPanel { get; }
        public SKRect FooterRect { get; }

        public BayRowTierAxis Axis { get; }

        /// <summary>目的港首次出现顺序（分色与图例都按它）。</summary>
        public IReadOnlyList<string> Ports => _ports;

        public static StowageLayout Compute(StowageDocument document, StowageStyle tokens)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            if (document.Bays.Count == 0)
            {
                throw new InvalidOperationException("积载图至少要有一个 bay。");
            }
            if (document.BayOf(document.CrossSectionBay) == null)
            {
                throw new InvalidOperationException("横剖面 bay " + document.CrossSectionBay + " 不在文档中。");
            }

            // 箱位合法性当场校验，不把坏数据留到绘制期。
            for (int i = 0; i < document.Containers.Count; i++)
            {
                StowageContainer c = document.Containers[i];
                if (document.BayOf(c.Bay) == null)
                {
                    throw new InvalidOperationException("第 " + (i + 1) + " 只箱的 bay " + c.Bay + " 不在文档中。");
                }
                if (!document.RowExists(c.Bay, c.Row))
                {
                    throw new InvalidOperationException(
                        "第 " + (i + 1) + " 只箱的 row " + c.Row + " 超出 bay " + c.Bay + " 的排位。");
                }
                if (!document.TierExists(c.Bay, c.Tier))
                {
                    throw new InvalidOperationException(
                        "第 " + (i + 1) + " 只箱的 tier " + c.Tier + " 超出 bay " + c.Bay + " 的层数。");
                }
                if (c.Size == ContainerSize.Forty && NextBayOf(document, c.Bay) == null)
                {
                    throw new InvalidOperationException(
                        "40 尺箱（bay " + c.Bay + "）右端需要相邻的下一个 bay 位，但文档里没有。");
                }
            }

            TransitStyle frame = tokens.Frame;
            float left = frame.Margin + frame.BindingMargin;
            float right = frame.Width - frame.Margin;
            SKRect bounds = new SKRect(0f, 0f, frame.Width, frame.Height);
            SKRect titleRect = new SKRect(left, frame.Margin, right, frame.Margin + frame.TitleHeight);
            SKRect noteRect = new SKRect(left, titleRect.Bottom, right, titleRect.Bottom + frame.SubtitleSize * 2.4f);
            SKRect footerRect = new SKRect(left, frame.Height - frame.Margin - frame.FooterHeight,
                right, frame.Height - frame.Margin);

            float contentTop = noteRect.Bottom + 6f;
            float contentBottom = footerRect.Top - 8f;
            float panelWidth = (right - left - tokens.PanelGap) * 0.5f;
            SKRect profilePanel = new SKRect(left, contentTop, left + panelWidth, contentBottom);
            SKRect sectionPanel = new SKRect(profilePanel.Right + tokens.PanelGap, contentTop, right, contentBottom);

            float titleBand = tokens.PanelTitleSize * 2f;
            float labelBand = tokens.LabelSize * 2f;
            SKRect profilePlot = new SKRect(profilePanel.Left + labelBand + 6f, profilePanel.Top + titleBand,
                profilePanel.Right - 4f, profilePanel.Bottom - labelBand);
            SKRect sectionPlot = new SKRect(sectionPanel.Left + labelBand + 6f, sectionPanel.Top + titleBand,
                sectionPanel.Right - 4f, sectionPanel.Bottom - labelBand);

            int tierCount = 0;
            List<int> bayNumbers = new List<int>(document.Bays.Count);
            int maxRow = 0;
            for (int i = 0; i < document.Bays.Count; i++)
            {
                StowageBay bay = document.Bays[i];
                bayNumbers.Add(bay.Number);
                if (bay.Tiers > tierCount)
                {
                    tierCount = bay.Tiers;
                }
                if (bay.PortRows * 2 > maxRow)
                {
                    maxRow = bay.PortRows * 2 - 1;
                }
                if (bay.StarboardRows * 2 > maxRow)
                {
                    maxRow = bay.StarboardRows * 2;
                }
            }

            if (tierCount * tokens.TierHeight > profilePlot.Height)
            {
                throw new InvalidOperationException(
                    "tier 总高 " + (tierCount * tokens.TierHeight).ToString("0.#", CultureInfo.InvariantCulture)
                    + " 超过剖面绘图区高度 " + profilePlot.Height.ToString("0.#", CultureInfo.InvariantCulture));
            }

            BayRowTierAxis axis = new BayRowTierAxis(bayNumbers, tierCount, tokens, profilePlot, sectionPlot);
            axis.FitRows(maxRow);

            List<string> ports = new List<string>();
            Dictionary<string, int> portIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < document.Containers.Count; i++)
            {
                string port = document.Containers[i].PortCode;
                if (!portIndex.ContainsKey(port))
                {
                    portIndex.Add(port, ports.Count);
                    ports.Add(port);
                }
            }

            return new StowageLayout(document, tokens, bounds, titleRect, noteRect, profilePanel, sectionPanel,
                footerRect, axis, ports, portIndex);
        }

        private static int? NextBayOf(StowageDocument document, int bay)
        {
            int next = int.MaxValue;
            bool found = false;
            for (int i = 0; i < document.Bays.Count; i++)
            {
                int number = document.Bays[i].Number;
                if (number > bay && number < next)
                {
                    next = number;
                    found = true;
                }
            }
            return found ? next : (int?)null;
        }

        /// <summary>目的港分色（循环取用 <see cref="StowageStyle.PortColors"/>）。</summary>
        public SKColor PortColorOf(string portCode)
        {
            if (string.IsNullOrEmpty(portCode) || _ports.Count == 0)
            {
                return Tokens.Palette.PanelFill;
            }
            int index = 0;
            _portIndex.TryGetValue(portCode, out index);
            IList<SKColor> colors = Tokens.PortColors;
            if (colors.Count == 0)
            {
                return Tokens.Palette.PanelFill;
            }
            return colors[index % colors.Count];
        }

        /// <summary>纵向剖面里该箱的箱位矩形（40 尺箱左对齐跨两个 bay 位）。</summary>
        public SKRect ProfileRectOf(StowageContainer container)
        {
            float left = Axis.BayBand(container.Bay).Left + Tokens.ContainerInset;
            int last = container.Bay;
            for (int i = 1; i < container.BaySlots; i++)
            {
                int? next = Axis.NextBay(last);
                if (next == null)
                {
                    throw new InvalidOperationException("40 尺箱缺少右邻 bay 位。");
                }
                last = next.Value;
            }
            float right = Axis.BayBand(last).Right - Tokens.ContainerInset;
            SKRect tier = Axis.TierBand(container.Tier, left, right);
            return new SKRect(tier.Left, tier.Top + Tokens.ContainerInset, tier.Right, tier.Bottom - Tokens.ContainerInset);
        }

        /// <summary>横剖面里该箱的箱位矩形（只看 row × tier）。</summary>
        public SKRect SectionRectOf(StowageContainer container)
        {
            float half = Tokens.RowPitch * Tokens.RowBandFactor * Axis.RowScale * 0.5f;
            float center = Axis.RowCenter(container.Row);
            SKRect tier = Axis.TierBand(container.Tier, center - half, center + half);
            return new SKRect(tier.Left, tier.Top + Tokens.ContainerInset, tier.Right, tier.Bottom - Tokens.ContainerInset);
        }

        /// <summary>纵向剖面可见性：合法箱位一律可见（该视图覆盖全部 bay）。</summary>
        public bool ProfileVisible(StowageContainer container)
        {
            return container != null && Axis.BayIndex(container.Bay) >= 0;
        }

        /// <summary>横剖面可见性：箱位跨到当前剖面 bay 即可见（40 尺箱跨两个 bay）。</summary>
        public bool SectionVisible(StowageContainer container)
        {
            if (container == null)
            {
                return false;
            }
            int section = Document.CrossSectionBay;
            if (container.Bay == section)
            {
                return true;
            }
            if (container.Size == ContainerSize.Forty)
            {
                int? next = Axis.NextBay(container.Bay);
                return next != null && next.Value == section;
            }
            return false;
        }

        /// <summary>箱块文字（字段顺序走令牌；最多 45 字符，超出截断）。</summary>
        public string TextOf(StowageContainer container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            List<string> parts = new List<string>();
            IList<string> order = Tokens.ContainerFieldOrder;
            for (int i = 0; i < order.Count; i++)
            {
                string part = FieldText(container, order[i]);
                if (!string.IsNullOrEmpty(part))
                {
                    parts.Add(part);
                }
            }

            string text = string.Join(Tokens.FieldSeparator, parts.ToArray());
            if (text.Length > Tokens.MaxContainerTextChars)
            {
                text = text.Substring(0, Tokens.MaxContainerTextChars);
            }
            return text;
        }

        private static string FieldText(StowageContainer container, string field)
        {
            if (string.Equals(field, "size", StringComparison.Ordinal))
            {
                return ((int)container.Size).ToString(CultureInfo.InvariantCulture);
            }
            if (string.Equals(field, "weight", StringComparison.Ordinal))
            {
                return (container.WeightKg / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "t";
            }
            if (string.Equals(field, "port", StringComparison.Ordinal))
            {
                return container.PortCode;
            }
            if (string.Equals(field, "dg", StringComparison.Ordinal))
            {
                return string.IsNullOrEmpty(container.DangerousClass) ? string.Empty : "DG" + container.DangerousClass;
            }
            if (string.Equals(field, "status", StringComparison.Ordinal))
            {
                return container.IsEmpty ? "空" : string.Empty;
            }
            return string.Empty;
        }
    }
}
