using System;

namespace HR.Entities
{
    /// <summary>Read-model of the approval state of one monthly schedule for the UI:
    /// derived status, display name and the recorded submitter/approver names.</summary>
    public class ScheduleStatusView
    {
        /// <summary>Internal status: Draft / Submitted / Approved / Rejected.</summary>
        public string Status { get; set; }

        /// <summary>Arabic display name of the status.</summary>
        public string StatusAr { get; set; }

        /// <summary>Full name of the user who submitted the schedule.</summary>
        public string SubmittedByName { get; set; }

        /// <summary>Submission timestamp.</summary>
        public DateTime? SubmittedAt { get; set; }

        /// <summary>Full name of the user who approved the schedule.</summary>
        public string ApprovedByName { get; set; }

        /// <summary>Approval timestamp.</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>Mandatory reason recorded on the last rejection.</summary>
        public string RejectReason { get; set; }

        /// <summary>True when the month is engine-locked.</summary>
        public bool IsLocked { get; set; }
    }
}
