using System;

namespace HR.Entities
{
    /// <summary>Maps to table [LeaveRequests].</summary>
    public class LeaveRequest
    {
        /// <summary>Primary key (AUTOINCREMENT: LeaveId).</summary>
        public int LeaveId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>LeaveTypeId.</summary>
        public int? LeaveTypeId { get; set; }

        /// <summary>StartDate.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>EndDate.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>DaysCount.</summary>
        public int? DaysCount { get; set; }

        /// <summary>Reason.</summary>
        public string Reason { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>ApprovedBy.</summary>
        public int? ApprovedBy { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>CreatedBy.</summary>
        public int? CreatedBy { get; set; }
    }
}
