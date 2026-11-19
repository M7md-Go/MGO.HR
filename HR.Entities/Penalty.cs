using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Penalties].</summary>
    public class Penalty
    {
        /// <summary>Primary key (AUTOINCREMENT: PenaltyId).</summary>
        public int PenaltyId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>AttDate.</summary>
        public DateTime? AttDate { get; set; }

        /// <summary>PenaltyDate.</summary>
        public DateTime? PenaltyDate { get; set; }

        /// <summary>TypeCode.</summary>
        public string TypeCode { get; set; }

        /// <summary>Amount.</summary>
        public decimal? Amount { get; set; }

        /// <summary>Reason.</summary>
        public string Reason { get; set; }

        /// <summary>IssuedBy.</summary>
        public int? IssuedBy { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
