using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Sections].</summary>
    public class Section
    {
        /// <summary>Primary key (AUTOINCREMENT: SectionId).</summary>
        public int SectionId { get; set; }
        /// <summary>SectionCode.</summary>
        public string SectionCode { get; set; }

        /// <summary>SectionName.</summary>
        public string SectionName { get; set; }

        /// <summary>DepartmentId.</summary>
        public int? DepartmentId { get; set; }

        /// <summary>ManagerEmpId.</summary>
        public int? ManagerEmpId { get; set; }

        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }
    }
}
