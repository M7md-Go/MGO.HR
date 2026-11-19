using System;

namespace HR.Entities
{
    /// <summary>Maps to table [ActualJobs].</summary>
    public class ActualJob
    {
        /// <summary>Primary key (AUTOINCREMENT: ActualJobId).</summary>
        public int ActualJobId { get; set; }
        /// <summary>JobCode.</summary>
        public string JobCode { get; set; }

        /// <summary>JobName.</summary>
        public string JobName { get; set; }

        /// <summary>DepartmentId.</summary>
        public int? DepartmentId { get; set; }

        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }
    }
}
