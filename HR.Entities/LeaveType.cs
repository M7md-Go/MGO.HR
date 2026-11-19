using System;

namespace HR.Entities
{
    /// <summary>Maps to table [LeaveTypes].</summary>
    public class LeaveType
    {
        /// <summary>Primary key (AUTOINCREMENT: LeaveTypeId).</summary>
        public int LeaveTypeId { get; set; }
        /// <summary>LeaveTypeCode.</summary>
        public string LeaveTypeCode { get; set; }

        /// <summary>TypeName.</summary>
        public string TypeName { get; set; }

        /// <summary>AnnualDays.</summary>
        public int? AnnualDays { get; set; }

        /// <summary>IsPaid.</summary>
        public bool? IsPaid { get; set; }

        /// <summary>RequiresDoc.</summary>
        public bool? RequiresDoc { get; set; }
    }
}
