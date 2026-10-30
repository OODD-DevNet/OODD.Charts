using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.HyperbaricTransfer
{
    /// <summary>区段标识，供样例回填。</summary>
    public static class HyperbaricTransferSections
    {
        public const string Transfer = "transfer";
        public const string Outbound = "outbound";
        public const string Inbound = "inbound";
        public const string Handover = "handover";
        public const string Signature = "signature";
    }

    /// <summary>行标识，供样例按名回填。</summary>
    public static class HyperbaricTransferRows
    {
        public const string Date = "date";
        public const string OutboundTime = "outbound-time";
        public const string ReturnTime = "return-time";
        public const string Mode = "mode";
        public const string Escort = "escort";

        public const string OutboundVital = "outbound-vital";
        public const string InboundVital = "inbound-vital";

        public const string OutboundNote = "outbound-note";
        public const string InboundNote = "inbound-note";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 神经外科高压氧疗接送记录单。记录病房与高压氧舱之间的往返转运：
    /// 转运时间与方式、去程/回程生命体征、途中情况与床旁交接、双方护士签名。
    /// </summary>
    public sealed class HyperbaricTransferDocument : FormSheetDocument
    {
        public HyperbaricTransferDocument()
        {
            FormTitle = "神经外科高压氧疗接送记录单";
            FooterNote = "本单一式两份，神经外科与高压氧科各存一份；转运途中持续吸氧并保持静脉通路通畅。";

            AddSection(new FormSection("一、转运信息",
                new FormColumn("项目", 1f, CellAlign.Center, true),
                new FormColumn("内容", 2.2f, CellAlign.Near))
            {
                Key = HyperbaricTransferSections.Transfer,
                RowHeight = 30f,
            });
            AddRow(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.Date, "转运日期");
            AddRow(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.OutboundTime, "去程时间");
            AddRow(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.ReturnTime, "回程时间");
            AddRow(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.Mode, "转运方式");
            AddRow(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.Escort, "护送人员");

            AddSection(new FormSection("二、去程生命体征",
                new FormColumn("体温(℃)", 1f),
                new FormColumn("脉搏", 1f),
                new FormColumn("呼吸", 1f),
                new FormColumn("血压(mmHg)", 1.2f),
                new FormColumn("SpO2", 1f),
                new FormColumn("意识", 1.2f))
            {
                Key = HyperbaricTransferSections.Outbound,
                RowHeight = 34f,
            });
            AddRow(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital);

            AddSection(new FormSection("三、回程生命体征",
                new FormColumn("体温(℃)", 1f),
                new FormColumn("脉搏", 1f),
                new FormColumn("呼吸", 1f),
                new FormColumn("血压(mmHg)", 1.2f),
                new FormColumn("SpO2", 1f),
                new FormColumn("意识", 1.2f))
            {
                Key = HyperbaricTransferSections.Inbound,
                RowHeight = 34f,
            });
            AddRow(HyperbaricTransferSections.Inbound, HyperbaricTransferRows.InboundVital);

            AddSection(new FormSection("四、途中情况与交接",
                new FormColumn("项目", 1f, CellAlign.Center, true),
                new FormColumn("记录", 3f, CellAlign.Near))
            {
                Key = HyperbaricTransferSections.Handover,
                RowHeight = 30f,
            });
            AddRow(HyperbaricTransferSections.Handover, HyperbaricTransferRows.OutboundNote, "去程途中");
            AddRow(HyperbaricTransferSections.Handover, HyperbaricTransferRows.InboundNote, "回程途中");

            AddSection(new FormSection("五、签名",
                new FormColumn("护送护士", 1.6f, CellAlign.Center, true),
                new FormColumn("接收护士", 1.6f, CellAlign.Center, true),
                new FormColumn("交接时间", 1.6f, CellAlign.Center, true))
            {
                Key = HyperbaricTransferSections.Signature,
                RowHeight = 34f,
            });
            AddRow(HyperbaricTransferSections.Signature, HyperbaricTransferRows.Sign);
        }
    }

    /// <summary>接送记录版式：A4 纵向，五区段竖排。</summary>
    public sealed class HyperbaricTransferTemplate : FormSheetTemplate
    {
        public HyperbaricTransferTemplate()
        {
            Tokens.Width = 794f;
        }

        public override string Key => "medical.neurosurgery-hyperbaric-transfer";

        public override string DisplayName => "神经外科高压氧疗接送记录单";
    }
}
