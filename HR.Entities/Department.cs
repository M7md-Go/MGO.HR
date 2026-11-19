using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Departments].</summary>
    public class Department
    {
        /// <summary>Primary key (AUTOINCREMENT: DepartmentId).</summary>
        public int DepartmentId { get; set; }
        /// <summary>DepartmentCode.</summary>
        public string DepartmentCode { get; set; }

        /// <summary>DepartmentName.</summary>
        public string DepartmentName { get; set; }

        /// <summary>BranchId.</summary>
        public int? BranchId { get; set; }

        /// <summary>ManagerEmpId.</summary>
        public int? ManagerEmpId { get; set; }

        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }
    }
}
