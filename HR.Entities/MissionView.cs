using System;

namespace HR.Entities
{
    /// <summary>Read-only row: Missions joined with Employees.</summary>
    public class MissionView
    {
        /// <summary>MissionId.</summary>
        public int MissionId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }
        /// <summary>EmployeeCode.</summary>
        public string EmployeeCode { get; set; }
        /// <summary>FullNameAr.</summary>
        public string EmployeeName { get; set; }
        /// <summary>StartDate.</summary>
        public DateTime? StartDate { get; set; }
        /// <summary>StartTime (HH:mm, optional).</summary>
        public string StartTime { get; set; }
        /// <summary>EndDate.</summary>
        public DateTime? EndDate { get; set; }
        /// <summary>EndTime (HH:mm, optional).</summary>
        public string EndTime { get; set; }
        /// <summary>Location.</summary>
        public string Location { get; set; }
        /// <summary>Purpose.</summary>
        public string Purpose { get; set; }
        /// <summary>IsExemptAtt — exempt missions stop attendance calculation for their days.</summary>
        public bool? IsExemptAtt { get; set; }
        /// <summary>Status (Pending/Approved/Rejected).</summary>
        public string Status { get; set; }
        /// <summary>ApprovedBy user id.</summary>
        public int? ApprovedBy { get; set; }
    }
}
