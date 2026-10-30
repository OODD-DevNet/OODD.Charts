using OODD.Charts.Medical.TcmNursing;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>中医辨证施护样例：冠心病稳定型心绞痛住院患者，连续施护 3 天症状缓解。</summary>
    public static class TcmNursingSample
    {
        public static TcmNursingDocument Build()
        {
            TcmNursingDocument doc = new TcmNursingDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "记录单号：TCM-2026-0311-05",
            };

            doc.Patient.Name = "吴国华";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "67";
            doc.Patient.Department = "心血管内科";
            doc.Patient.Ward = "一病区";
            doc.Patient.BedNumber = "15床";
            doc.Patient.AdmissionNumber = "2026030805";
            doc.Patient.AdmissionDate = "2026-03-08";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "冠心病 稳定型心绞痛（胸痹 气虚血瘀证）"));
            doc.Patient.ExtraFields.Add(new HeaderField("pattern-date", "辨证日期", "2026-03-09"));

            doc.SetCell(TcmNursingSections.Syndrome, TcmNursingRows.Pattern, 0, "气虚血瘀证");
            doc.SetCell(TcmNursingSections.Syndrome, TcmNursingRows.Pattern, 1, "益气活血、通络止痛");
            doc.SetCell(TcmNursingSections.Syndrome, TcmNursingRows.Pattern, 2, "舌暗紫、边有齿痕，脉细涩");
            doc.SetCell(TcmNursingSections.Syndrome, TcmNursingRows.Pattern, 3, "胸痹心痛、气短乏力，劳则加重，休息后可缓解");

            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Auricular, 0, "2026-03-09 09:30");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Auricular, 2,
                "取心、神门、交感、皮质下穴，王不留行籽贴压，双耳交替，每日按压 3 次、每次 3 分钟。");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Auricular, 3, "按压后胸闷减轻，夜间入睡较前容易。");

            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Acupoint, 0, "2026-03-09 15:00");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Acupoint, 2,
                "内关、膻中、心俞穴贴敷活血通络方，每次 4 小时，贴敷处皮肤无红肿破损。");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Acupoint, 3, "胸前区憋闷感缓解，无皮肤过敏。");

            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.FootBath, 0, "2026-03-10 16:00");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.FootBath, 2,
                "红花、艾叶、川芎煎液 40℃ 泡足 20 分钟，睡前进行，家属全程陪护。");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.FootBath, 3, "足部温暖，夜间睡眠延长至 6 小时。");

            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Emotion, 0, "2026-03-11 10:00");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Emotion, 2,
                "讲解疾病转归，指导八段锦“双手托天理三焦”式，每日 2 次、每次 10 分钟。");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Emotion, 3, "情绪平稳，焦虑评分由 55 分降至 42 分。");

            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Diet, 0, "2026-03-11 16:30");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Diet, 2,
                "低盐低脂、少食多餐，山药、莲子、黑木耳煮粥，忌生冷油腻及浓茶。");
            doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Diet, 3, "食欲改善，大便通畅，无腹胀。");

            doc.Section(TcmNursingSections.Notes)!.Note =
                "3 月 9 日：患者胸闷发作 2 次，每次 3～5 分钟，含服硝酸甘油后缓解；遵医嘱予耳穴压豆及穴位贴敷，施护过程顺利，无不适。\n" +
                "3 月 10 日：夜间胸闷未再发作，睡眠 6 小时，晨起精神可；足浴后肢端温暖，舌暗紫较前转淡。\n" +
                "3 月 11 日：胸痛未发作，气短减轻，可床边活动 200 米；已向患者及家属宣教避寒保暖、规律服药、随身携带硝酸甘油，胸痛持续 15 分钟不缓解立即就医。";

            doc.SetCell(TcmNursingSections.Signature, TcmNursingRows.Sign, 0, "赵晓梅");
            doc.SetCell(TcmNursingSections.Signature, TcmNursingRows.Sign, 1, "孙丽");
            doc.SetCell(TcmNursingSections.Signature, TcmNursingRows.Sign, 2, "2026-03-11 17:00");

            return doc;
        }
    }
}
