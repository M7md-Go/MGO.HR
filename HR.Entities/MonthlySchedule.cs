using System;

namespace HR.Entities
{
    /// <summary>Maps to table [MonthlySchedules].</summary>
    public class MonthlySchedule
    {
        /// <summary>Primary key (AUTOINCREMENT: ScheduleId).</summary>
        public int ScheduleId { get; set; }
        /// <summary>YearVal.</summary>
        public int? YearVal { get; set; }

        /// <summary>MonthVal.</summary>
        public int? MonthVal { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>CreatedBy.</summary>
        public int? CreatedBy { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>ApprovedBy.</summary>
        public int? ApprovedBy { get; set; }

        /// <summary>ApprovedAt.</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>LockedAt.</summary>
        public DateTime? LockedAt { get; set; }

        /// <summary>User who submitted the schedule for approval.</summary>
        public int? SubmittedBy { get; set; }

        /// <summary>Submission timestamp.</summary>
        public DateTime? SubmittedAt { get; set; }

        /// <summary>Mandatory reason recorded on the last rejection.</summary>
        public string RejectReason { get; set; }
    }
}
