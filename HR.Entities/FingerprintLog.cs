using System;

namespace HR.Entities
{
    /// <summary>Maps to table [FingerprintLogs].</summary>
    public class FingerprintLog
    {
        /// <summary>Primary key (AUTOINCREMENT: LogId).</summary>
        public int LogId { get; set; }
        /// <summary>DeviceId.</summary>
        public int? DeviceId { get; set; }

        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>EmpDevCode.</summary>
        public string EmpDevCode { get; set; }

        /// <summary>LogTime.</summary>
        public DateTime? LogTime { get; set; }

        /// <summary>InOutFlag.</summary>
        public int? InOutFlag { get; set; }

        /// <summary>SourceFile.</summary>
        public string SourceFile { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
