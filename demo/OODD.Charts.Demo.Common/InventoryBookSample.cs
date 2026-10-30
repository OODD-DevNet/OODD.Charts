using OODD.Charts.Medical.InventoryBook;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>点物本样例：2026-03-20 急诊科抢救区急救车周清点，账物相符并封车。</summary>
    public static class InventoryBookSample
    {
        public static InventoryBookDocument Build()
        {
            InventoryBookDocument doc = new InventoryBookDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "台账编号：DW-2026-012",
            };

            doc.Patient.Name = "李娜";
            doc.Patient.Sex = "女";
            doc.Patient.Department = "急诊科";
            doc.Patient.Ward = "抢救区";
            doc.Patient.ExtraFields.Add(new HeaderField("cart", "抢救车编号", "急救-03"));
            doc.Patient.ExtraFields.Add(new HeaderField("cycle", "清点周期", "每周一、四"));

            AddItem(doc, InventoryBookRows.Epinephrine, "5", "5", "5", "0", "有效期至 2026-11");
            AddItem(doc, InventoryBookRows.Atropine, "5", "5", "5", "0", "有效期至 2027-02");
            AddItem(doc, InventoryBookRows.Diazepam, "3", "3", "3", "0", "专柜加锁，效期 2026-12");
            AddItem(doc, InventoryBookRows.Saline, "4", "4", "4", "0", "有效期至 2027-05");
            AddItem(doc, InventoryBookRows.InfusionSet, "10", "10", "10", "0", "独立包装完好");
            AddItem(doc, InventoryBookRows.Syringe, "10", "10", "8", "-2", "22:10 已从库房补足 2 支");
            AddItem(doc, InventoryBookRows.SuctionTube, "8", "8", "8", "0", "无菌包装，效期 2028-01");
            AddItem(doc, InventoryBookRows.OxygenBottle, "2", "2", "2", "0", "灭菌水在效期内");
            AddItem(doc, InventoryBookRows.DefibrillatorPad, "2", "2", "2", "0", "效期 2027-08，粘性良好");
            AddItem(doc, InventoryBookRows.Seal, "2", "2", "2", "0", "封条编号 2026032001");

            AddShift(doc, InventoryBookRows.DayShift, "李娜", "陈静", "2026-03-20 08:10");
            AddShift(doc, InventoryBookRows.NightShift, "王芳", "陈静", "2026-03-20 20:05");
            AddShift(doc, InventoryBookRows.HeadNurseCheck, "周敏", "王芳", "2026-03-20 22:15");

            doc.Section(InventoryBookSections.Notes)!.Note =
                "夜班清点发现注射器 20ml 少 2 支，已于 22:10 从库房补足；抢救车封条完好，无过期药品。";

            return doc;
        }

        private static void AddItem(InventoryBookDocument doc, string rowKey, string baseCount,
            string dayCount, string nightCount, string difference, string remark)
        {
            doc.SetCell(InventoryBookSections.Items, rowKey, 1, baseCount);
            doc.SetCell(InventoryBookSections.Items, rowKey, 2, dayCount);
            doc.SetCell(InventoryBookSections.Items, rowKey, 3, nightCount);
            doc.SetCell(InventoryBookSections.Items, rowKey, 4, difference);
            doc.SetCell(InventoryBookSections.Items, rowKey, 5, remark);
        }

        private static void AddShift(InventoryBookDocument doc, string rowKey, string counter,
            string checker, string time)
        {
            doc.SetCell(InventoryBookSections.Shifts, rowKey, 1, counter);
            doc.SetCell(InventoryBookSections.Shifts, rowKey, 2, checker);
            doc.SetCell(InventoryBookSections.Shifts, rowKey, 3, time);
        }
    }
}
