using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Companies].</summary>
    public class Company
    {
        /// <summary>Primary key (AUTOINCREMENT: CompanyId).</summary>
        public int CompanyId { get; set; }
        /// <summary>CompanyCode.</summary>
        public string CompanyCode { get; set; }

        /// <summary>CompanyNameAr.</summary>
        public string CompanyNameAr { get; set; }

        /// <summary>CompanyNameEn.</summary>
        public string CompanyNameEn { get; set; }

        /// <summary>TaxNumber.</summary>
        public string TaxNumber { get; set; }

        /// <summary>Phone.</summary>
        public string Phone { get; set; }

        /// <summary>Email.</summary>
        public string Email { get; set; }

        /// <summary>Address.</summary>
        public string Address { get; set; }

        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }
    }
}
