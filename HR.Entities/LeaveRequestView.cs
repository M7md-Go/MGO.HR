using System;

namespace HR.Entities
{
    /// <summary>Read-only row: LeaveRequests joined with Employees and LeaveTypes.</summary>
    public class LeaveRequestView
    {
        /// <summary>LeaveId.</summary>
        public int LeaveId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }
        /// <summary>EmployeeCode.</summary>
        public string EmployeeCode { get; set; }
        /// <summary>FullNameAr.</summary>
        public string EmployeeName { get; set; }
        /// <summary>LeaveTypeId.</summary>
        public int? LeaveTypeId { get; set; }
        /// <summary>TypeName.</summary>
        public string TypeName { get; set; }
        /// <summary>StartDate.</summary>
        public DateTime? StartDate { get; set; }
        /// <summary>EndDate.</summary>
        public DateTime? EndDate { get; set; }
        /// <summary>DaysCount.</summary>
        public decimal? DaysCount { get; set; }
        /// <summary>Reason.</summary>
        public string Reason { get; set; }
        /// <summary>RejectReason (filled when Status = Rejected).</summary>
        public string RejectReason { get; set; }
        /// <summary>Status (Pending/Approved/Rejected).</summary>
        public string Status { get; set; }
        /// <summary>ApprovedBy user id.</summary>
        public int? ApprovedBy { get; set; }
        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
