using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.Forms.IcuNursing
{
    /// <summary>ICU 护理记录单的列键（与表头树叶子一一对应）。</summary>
    public static class IcuNursingColumns
    {
        public const string HeartRate = "hr";
        public const string Respiration = "rr";
        public const string Temperature = "temp";
        public const string Systolic = "sbp";
        public const string Diastolic = "dbp";
        public const string CentralVenousPressure = "cvp";
        public const string Intake = "intake";
        public const string Output = "output";
        public const string Urine = "urine";

        public static readonly IReadOnlyList<string> All = new List<string>
        {
            HeartRate, Respiration, Temperature, Systolic, Diastolic, CentralVenousPressure,
            Intake, Output, Urine,
        }.AsReadOnly();

        public static string LabelOf(string key)
        {
            switch (key)
            {
                case HeartRate: return "心率";
                case Respiration: return "呼吸";
                case Temperature: return "体温";
                case Systolic: return "收缩压";
                case Diastolic: return "舒张压";
                case CentralVenousPressure: return "中心静脉压";
                case Intake: return "入量";
                case Output: return "出量";
                case Urine: return "尿量";
                default: return key;
            }
        }
    }

    /// <summary>一个时点的护理记录。</summary>
    public sealed class IcuRecord
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        public IcuRecord(string timeLabel)
        {
            TimeLabel = timeLabel ?? string.Empty;
        }

        public string TimeLabel { get; }

        public void Set(string columnKey, string value)
        {
            _values[columnKey] = value ?? string.Empty;
        }

        public string Get(string columnKey)
        {
            string? value;
            return _values.TryGetValue(columnKey, out value) ? value : string.Empty;
        }
    }

    /// <summary>
    /// ICU 护理记录单文档。主表数据经 <see cref="Records"/> 给出；
    /// 右侧交班小结是行距独立的旁挂子表，内容经 <see cref="HandoverNotes"/> 给出。
    /// </summary>
    public sealed class IcuNursingDocument : ChartDocument
    {
        public IList<IcuRecord> Records { get; } = new List<IcuRecord>();

        /// <summary>交班小结四行内容（与模板侧表行标签一一对应）。</summary>
        public IList<string> HandoverNotes { get; } = new List<string>();
    }
}
