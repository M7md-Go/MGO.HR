using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Overtime].</summary>
    public class Overtime
    {
        /// <summary>Primary key (AUTOINCREMENT: OvertimeId).</summary>
        public int OvertimeId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>OtDate.</summary>
        public DateTime? OtDate { get; set; }

        /// <summary>PlannedMinutes.</summary>
        public int? PlannedMinutes { get; set; }

        /// <summary>ActualMinutes.</summary>
        public int? ActualMinutes { get; set; }

        /// <summary>EffectiveMinutes.</summary>
        public int? EffectiveMinutes { get; set; }

        /// <summary>Rate.</summary>
        public double? Rate { get; set; }

        /// <summary>Source.</summary>
        public string Source { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>RequestedBy.</summary>
        public int? RequestedBy { get; set; }

        /// <summary>ApprovedBy.</summary>
        public int? ApprovedBy { get; set; }

        /// <summary>ApprovedAt.</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>Notes.</summary>
        public string Notes { get; set; }
    }
}
