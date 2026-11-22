using System;

namespace HR.Entities
{
    /// <summary>Browse row for [AttendanceExceptions] (engine + manual exception registry).</summary>
    public class AttendanceExceptionView
    {
        public int ExceptionId { get; set; }
        public int? EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public DateTime? AttDate { get; set; }
        public string ExcType { get; set; }
        public int? ExcValue { get; set; }
        /// <summary>NULL when the engine generated the row during a recalculation.</summary>
        public int? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        /// <summary>True only for rows created by a user (they survive recalculations).</summary>
        public bool IsManual { get { return CreatedBy != null; } }
    }
}
