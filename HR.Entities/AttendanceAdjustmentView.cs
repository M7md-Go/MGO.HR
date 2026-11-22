using System;

namespace HR.Entities
{
    /// <summary>Browse row for [AttendanceAdjustments] (manual correction requests).</summary>
    public class AttendanceAdjustmentView
    {
        public int AdjustmentId { get; set; }
        public int? EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public DateTime? AttDate { get; set; }
        public string AdjustmentType { get; set; }
        public string NewInTime { get; set; }
        public string NewOutTime { get; set; }
        public string NewStatus { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
