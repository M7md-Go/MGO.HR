using System;

namespace HR.Entities
{
    /// <summary>Read-only row: LongLeaves joined with Employees and LongLeaveTypes.</summary>
    public class LongLeaveView
    {
        /// <summary>LongLeaveId.</summary>
        public int LongLeaveId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }
        /// <summary>EmployeeCode.</summary>
        public string EmployeeCode { get; set; }
        /// <summary>FullNameAr.</summary>
        public string EmployeeName { get; set; }
        /// <summary>TypeId.</summary>
        public int? TypeId { get; set; }
        /// <summary>LongLeaveTypes.TypeName.</summary>
        public string TypeName { get; set; }
        /// <summary>FromDate.</summary>
        public DateTime? FromDate { get; set; }
        /// <summary>ToDate (null = open-ended).</summary>
        public DateTime? ToDate { get; set; }
        /// <summary>Status.</summary>
        public string Status { get; set; }
        /// <summary>Reason.</summary>
        public string Reason { get; set; }
        /// <summary>Notes.</summary>
        public string Notes { get; set; }
        /// <summary>ApprovedBy user id.</summary>
        public int? ApprovedBy { get; set; }
    }
}
