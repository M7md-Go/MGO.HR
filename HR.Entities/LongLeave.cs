using System;

namespace HR.Entities
{
    /// <summary>Maps to table [LongLeaves] — one long-duration leave period of an employee.</summary>
    public class LongLeave
    {
        /// <summary>Primary key (AUTOINCREMENT: LongLeaveId).</summary>
        public int LongLeaveId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }
        /// <summary>TypeId (LongLeaveTypes.LongLeaveTypeId).</summary>
        public int? TypeId { get; set; }
        /// <summary>FromDate (required).</summary>
        public DateTime? FromDate { get; set; }
        /// <summary>ToDate (null = open-ended/unlimited).</summary>
        public DateTime? ToDate { get; set; }
        /// <summary>Status (Pending/Approved/Rejected/Ended).</summary>
        public string Status { get; set; }
        /// <summary>Reason.</summary>
        public string Reason { get; set; }
        /// <summary>Notes.</summary>
        public string Notes { get; set; }
        /// <summary>ApprovedBy user id.</summary>
        public int? ApprovedBy { get; set; }
        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }
        /// <summary>CreatedBy user id.</summary>
        public int? CreatedBy { get; set; }
    }
}
