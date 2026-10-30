using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.InventoryBook
{
    /// <summary>点物本区段标识。</summary>
    public static class InventoryBookSections
    {
        public const string Items = "items";
        public const string Shifts = "shifts";
        public const string Notes = "notes";
    }

    /// <summary>点物本行标识，供样例按名回填。</summary>
    public static class InventoryBookRows
    {
        public const string Epinephrine = "epinephrine";
        public const string Atropine = "atropine";
        public const string Diazepam = "diazepam";
        public const string Saline = "saline";
        public const string InfusionSet = "infusion-set";
        public const string Syringe = "syringe";
        public const string SuctionTube = "suction-tube";
        public const string OxygenBottle = "oxygen-bottle";
        public const string DefibrillatorPad = "defibrillator-pad";
        public const string Seal = "seal";

        public const string DayShift = "day-shift";
        public const string NightShift = "night-shift";
        public const string HeadNurseCheck = "head-nurse-check";
    }

    /// <summary>
    /// 病区急救车点物本：逐项核对基数、白班与夜班清点数量及差异，
    /// 下方记录班次清点人与复核人，末段用整段文字说明差异处置。
    /// </summary>
    public sealed class InventoryBookDocument : FormSheetDocument
    {
        public InventoryBookDocument()
        {
            FormTitle = "点物本";
            FooterNote = "每班清点、双人复核，账物相符后贴封条；本表保存一年。";

            AddSection(new FormSection("一、急救车物资清点",
                new FormColumn("物品名称", 1.8f, CellAlign.Near),
                new FormColumn("基数", 0.8f),
                new FormColumn("白班", 0.7f),
                new FormColumn("夜班", 0.7f),
                new FormColumn("差异", 0.7f),
                new FormColumn("备注", 1.6f, CellAlign.Near))
            {
                Key = InventoryBookSections.Items,
                RowHeight = 28f,
            });
            AddRow(InventoryBookSections.Items, InventoryBookRows.Epinephrine, "肾上腺素注射液 1mg");
            AddRow(InventoryBookSections.Items, InventoryBookRows.Atropine, "阿托品 0.5mg");
            AddRow(InventoryBookSections.Items, InventoryBookRows.Diazepam, "地西泮 10mg");
            AddRow(InventoryBookSections.Items, InventoryBookRows.Saline, "生理盐水 500ml");
            AddRow(InventoryBookSections.Items, InventoryBookRows.InfusionSet, "输液器");
            AddRow(InventoryBookSections.Items, InventoryBookRows.Syringe, "注射器 20ml");
            AddRow(InventoryBookSections.Items, InventoryBookRows.SuctionTube, "吸痰管");
            AddRow(InventoryBookSections.Items, InventoryBookRows.OxygenBottle, "氧气湿化瓶");
            AddRow(InventoryBookSections.Items, InventoryBookRows.DefibrillatorPad, "除颤电极片");
            AddRow(InventoryBookSections.Items, InventoryBookRows.Seal, "抢救车封条");

            AddSection(new FormSection("二、班次清点",
                new FormColumn("班次", 1f),
                new FormColumn("清点人", 1f),
                new FormColumn("复核人", 1f),
                new FormColumn("时间", 1.4f))
            {
                Key = InventoryBookSections.Shifts,
                RowHeight = 28f,
            });
            AddRow(InventoryBookSections.Shifts, InventoryBookRows.DayShift, "白班");
            AddRow(InventoryBookSections.Shifts, InventoryBookRows.NightShift, "夜班");
            AddRow(InventoryBookSections.Shifts, InventoryBookRows.HeadNurseCheck, "护士长抽查");

            AddSection(new FormSection("三、差异说明",
                new FormColumn("说明内容", 1f, CellAlign.Near))
            {
                Key = InventoryBookSections.Notes,
            });
        }
    }

    /// <summary>点物本版式：横向台账，六个物资列 + 班次清点 + 差异说明。</summary>
    public sealed class InventoryBookTemplate : FormSheetTemplate
    {
        public InventoryBookTemplate()
        {
            Tokens.Width = 1040f;
        }

        public override string Key => "medical.inventory-book";

        public override string DisplayName => "点物本";
    }
}
