using System;

namespace HR.Entities
{
    /// <summary>Maps to table [AttendanceExceptions].</summary>
    public class AttendanceException
    {
        /// <summary>Primary key (AUTOINCREMENT: ExceptionId).</summary>
        public int ExceptionId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>AttDate.</summary>
        public DateTime? AttDate { get; set; }

        /// <summary>ExcType.</summary>
        public string ExcType { get; set; }

        /// <summary>ExcValue.</summary>
        public int? ExcValue { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>AppliedTo.</summary>
        public int? AppliedTo { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>CreatedBy.</summary>
        public int? CreatedBy { get; set; }

        /// <summary>UpdatedAt.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>UpdatedBy.</summary>
        public int? UpdatedBy { get; set; }
    }
}
