using System;
using System.Collections.Generic;

namespace HR.Entities
{
    /// <summary>Header of a grouped shift-change request after schedule approval
    /// (RosterCore): several day/shift items in one request with the hour-balance
    /// rule (total new hours = total old hours across the group).</summary>
    public class ScheduleChangeRequest
    {
        /// <summary>Primary key (AUTOINCREMENT: RequestId).</summary>
        public int RequestId { get; set; }

        /// <summary>Schedule year.</summary>
        public int YearVal { get; set; }

        /// <summary>Schedule month.</summary>
        public int MonthVal { get; set; }

        /// <summary>Pending / Approved / Executed / Rejected.</summary>
        public string Status { get; set; }

        /// <summary>Business reason for the change (mandatory).</summary>
        public string Reason { get; set; }

        /// <summary>Requesting user.</summary>
        public int? RequestedBy { get; set; }

        /// <summary>Approving/rejecting user.</summary>
        public int? DecidedBy { get; set; }

        /// <summary>Approval/rejection reason (mandatory on rejection).</summary>
        public string DecideReason { get; set; }

        /// <summary>User who executed the approved request.</summary>
        public int? ExecutedBy { get; set; }

        /// <summary>Creation timestamp.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>Decision timestamp.</summary>
        public DateTime? DecidedAt { get; set; }

        /// <summary>Execution timestamp.</summary>
        public DateTime? ExecutedAt { get; set; }
    }
}
