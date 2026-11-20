using System;

namespace HR.Entities
{
    /// <summary>Read-only row: Penalties joined with Employees.</summary>
    public class PenaltyView
    {
        /// <summary>PenaltyId.</summary>
        public int PenaltyId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }
        /// <summary>EmployeeCode.</summary>
        public string EmployeeCode { get; set; }
        /// <summary>FullNameAr.</summary>
        public string EmployeeName { get; set; }
        /// <summary>AttDate — attendance day the penalty refers to (optional).</summary>
        public DateTime? AttDate { get; set; }
        /// <summary>PenaltyDate — decision day.</summary>
        public DateTime? PenaltyDate { get; set; }
        /// <summary>TypeCode (Warning/Deduction/Penalty/Absence/Violation).</summary>
        public string TypeCode { get; set; }
        /// <summary>Amount (currency, optional).</summary>
        public decimal? Amount { get; set; }
        /// <summary>Reason.</summary>
        public string Reason { get; set; }
        /// <summary>IssuedBy user id.</summary>
        public int? IssuedBy { get; set; }
        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
