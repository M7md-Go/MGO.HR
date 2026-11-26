using System;

namespace HR.Entities
{
    /// <summary>One row of the schedule approval ledger (RosterCore lesson):
    /// stage transitions of a monthly schedule — Submitted / Approved / Rejected —
    /// with the acting user, a mandatory reason on rejection and a timestamp.</summary>
    public class ScheduleApproval
    {
        /// <summary>Primary key (AUTOINCREMENT: ApprovalId).</summary>
        public int ApprovalId { get; set; }

        /// <summary>ScheduleId of the monthly schedule.</summary>
        public int ScheduleId { get; set; }

        /// <summary>Stage: Submitted / Approved / Rejected.</summary>
        public string Stage { get; set; }

        /// <summary>User performing the transition.</summary>
        public int? UserId { get; set; }

        /// <summary>Mandatory rejection reason; null on other stages.</summary>
        public string Reason { get; set; }

        /// <summary>Transition timestamp.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
