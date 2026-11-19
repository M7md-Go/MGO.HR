using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Missions].</summary>
    public class Mission
    {
        /// <summary>Primary key (AUTOINCREMENT: MissionId).</summary>
        public int MissionId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>StartDate.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>StartTime.</summary>
        public string StartTime { get; set; }

        /// <summary>EndDate.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>EndTime.</summary>
        public string EndTime { get; set; }

        /// <summary>Location.</summary>
        public string Location { get; set; }

        /// <summary>Purpose.</summary>
        public string Purpose { get; set; }

        /// <summary>IsExemptAtt.</summary>
        public bool? IsExemptAtt { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>ApprovedBy.</summary>
        public int? ApprovedBy { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
