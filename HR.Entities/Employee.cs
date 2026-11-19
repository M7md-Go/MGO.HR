using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Employees].</summary>
    public class Employee
    {
        /// <summary>Primary key (AUTOINCREMENT: EmployeeId).</summary>
        public int EmployeeId { get; set; }
        /// <summary>EmployeeCode.</summary>
        public string EmployeeCode { get; set; }

        /// <summary>NationalId.</summary>
        public string NationalId { get; set; }

        /// <summary>FullNameAr.</summary>
        public string FullNameAr { get; set; }

        /// <summary>FullNameEn.</summary>
        public string FullNameEn { get; set; }

        /// <summary>Gender.</summary>
        public string Gender { get; set; }

        /// <summary>BirthDate.</summary>
        public DateTime? BirthDate { get; set; }

        /// <summary>HireDate.</summary>
        public DateTime? HireDate { get; set; }

        /// <summary>EndDate.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>CompanyId.</summary>
        public int? CompanyId { get; set; }

        /// <summary>BranchId.</summary>
        public int? BranchId { get; set; }

        /// <summary>DepartmentId.</summary>
        public int? DepartmentId { get; set; }

        /// <summary>SectionId.</summary>
        public int? SectionId { get; set; }

        /// <summary>JobTitleId.</summary>
        public int? JobTitleId { get; set; }

        /// <summary>ActualJobId.</summary>
        public int? ActualJobId { get; set; }

        /// <summary>EmployeeStatusId.</summary>
        public int? EmployeeStatusId { get; set; }

        /// <summary>QualificationId.</summary>
        public int? QualificationId { get; set; }

        /// <summary>EmploymentType.</summary>
        public string EmploymentType { get; set; }

        /// <summary>Phone.</summary>
        public string Phone { get; set; }

        /// <summary>Email.</summary>
        public string Email { get; set; }

        /// <summary>Address.</summary>
        public string Address { get; set; }

        /// <summary>PhotoPath.</summary>
        public string PhotoPath { get; set; }

        /// <summary>DeviceEnrollNumber.</summary>
        public string DeviceEnrollNumber { get; set; }

        /// <summary>ShiftGroupCode.</summary>
        public string ShiftGroupCode { get; set; }

        /// <summary>ContractHours.</summary>
        public float? ContractHours { get; set; }

        /// <summary>Salary.</summary>
        public decimal? Salary { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>IsDeleted.</summary>
        public bool? IsDeleted { get; set; }

        /// <summary>Notes.</summary>
        public string Notes { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>CreatedBy.</summary>
        public int? CreatedBy { get; set; }

        /// <summary>UpdatedAt.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>UpdatedBy.</summary>
        public int? UpdatedBy { get; set; }
    }
}
