using System;

namespace HR.Entities
{
    /// <summary>Read-only row for the overtime browser: Overtime joined with Employees.</summary>
    public class OvertimeView
    {
        /// <summary>OvertimeId.</summary>
        public int OvertimeId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }
        /// <summary>EmployeeCode.</summary>
        public string EmployeeCode { get; set; }
        /// <summary>FullNameAr.</summary>
        public string EmployeeName { get; set; }
        /// <summary>OtDate.</summary>
        public DateTime? OtDate { get; set; }
        /// <summary>PlannedMinutes.</summary>
        public int? PlannedMinutes { get; set; }
        /// <summary>ActualMinutes.</summary>
        public int? ActualMinutes { get; set; }
        /// <summary>EffectiveMinutes.</summary>
        public int? EffectiveMinutes { get; set; }
        /// <summary>Rate (1.0 / 1.5 / 2.0 ...).</summary>
        public double? Rate { get; set; }
        /// <summary>Source (Auto / Manual).</summary>
        public string Source { get; set; }
        /// <summary>Status (Pending / Approved / Rejected).</summary>
        public string Status { get; set; }
    }
}
