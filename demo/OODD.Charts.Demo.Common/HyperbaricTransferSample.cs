using OODD.Charts.Medical.HyperbaricTransfer;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>神经外科高压氧疗接送样例：脑出血术后第 5 天，平车往返高压氧舱。</summary>
    public static class HyperbaricTransferSample
    {
        public static HyperbaricTransferDocument Build()
        {
            HyperbaricTransferDocument doc = new HyperbaricTransferDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "转运单号：NS-2026-0306-12",
            };

            doc.Patient.Name = "陈立军";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "58";
            doc.Patient.Department = "神经外科";
            doc.Patient.Ward = "二病区";
            doc.Patient.BedNumber = "8床";
            doc.Patient.AdmissionNumber = "2026022803";
            doc.Patient.AdmissionDate = "2026-02-28";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "高血压性脑出血术后"));
            doc.Patient.ExtraFields.Add(new HeaderField("surgery-date", "手术日期", "2026-03-01"));

            doc.SetCell(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.Date, 1, "2026-03-06");
            doc.SetCell(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.OutboundTime, 1, "08:40（病房出发）");
            doc.SetCell(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.ReturnTime, 1, "10:25（返回病房）");
            doc.SetCell(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.Mode, 1,
                "平车转运，头高位 30°，氧气袋持续吸氧 3 L/min，静脉通路通畅");
            doc.SetCell(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.Escort, 1,
                "责任护士 张倩、护工 刘伟，携带急救箱与病历");

            doc.SetCell(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital, 0, "36.8");
            doc.SetCell(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital, 1, "78");
            doc.SetCell(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital, 2, "18");
            doc.SetCell(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital, 3, "142/86");
            doc.SetCell(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital, 4, "97%");
            doc.SetCell(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital, 5,
                "GCS 12 分（E3V4M5）");

            doc.SetCell(HyperbaricTransferSections.Inbound, HyperbaricTransferRows.InboundVital, 0, "36.9");
            doc.SetCell(HyperbaricTransferSections.Inbound, HyperbaricTransferRows.InboundVital, 1, "76");
            doc.SetCell(HyperbaricTransferSections.Inbound, HyperbaricTransferRows.InboundVital, 2, "17");
            doc.SetCell(HyperbaricTransferSections.Inbound, HyperbaricTransferRows.InboundVital, 3, "136/82");
            doc.SetCell(HyperbaricTransferSections.Inbound, HyperbaricTransferRows.InboundVital, 4, "98%");
            doc.SetCell(HyperbaricTransferSections.Inbound, HyperbaricTransferRows.InboundVital, 5,
                "GCS 13 分（E4V4M5）");

            doc.SetCell(HyperbaricTransferSections.Handover, HyperbaricTransferRows.OutboundNote, 1,
                "平车头高位 30°，途中保持气道通畅，持续吸氧 3 L/min。患者神志朦胧、呼之能应，无抽搐、无呕吐，双侧瞳孔等大等圆约 3 mm、对光反射灵敏；08:55 安全入舱，与舱内护士交接生命体征及静脉通路。");
            doc.SetCell(HyperbaricTransferSections.Handover, HyperbaricTransferRows.InboundNote, 1,
                "10:10 出舱，面色红润，无头痛、恶心及呕吐，切口敷料干燥无渗血；GCS 13 分，四肢肌力同前，血压 136/82 mmHg。10:25 返回病房，与接收护士床旁核对腕带、输液通路及随身物品，交接无误。");

            doc.SetCell(HyperbaricTransferSections.Signature, HyperbaricTransferRows.Sign, 0, "张倩");
            doc.SetCell(HyperbaricTransferSections.Signature, HyperbaricTransferRows.Sign, 1, "李娜");
            doc.SetCell(HyperbaricTransferSections.Signature, HyperbaricTransferRows.Sign, 2, "2026-03-06 10:30");

            return doc;
        }
    }
}
