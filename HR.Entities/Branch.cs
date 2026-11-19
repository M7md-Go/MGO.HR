using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Branches].</summary>
    public class Branch
    {
        /// <summary>Primary key (AUTOINCREMENT: BranchId).</summary>
        public int BranchId { get; set; }
        /// <summary>BranchCode.</summary>
        public string BranchCode { get; set; }

        /// <summary>BranchName.</summary>
        public string BranchName { get; set; }

        /// <summary>CompanyId.</summary>
        public int? CompanyId { get; set; }

        /// <summary>Address.</summary>
        public string Address { get; set; }

        /// <summary>Phone.</summary>
        public string Phone { get; set; }

        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }
    }
}
