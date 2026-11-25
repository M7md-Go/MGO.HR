using System;

namespace HR.Entities
{
    /// <summary>
    /// One row of the annual-balance grid: quota types (Annual/Casual by
    /// default, configurable via the "Leave.BalanceTypes" setting) with the
    /// previous-years carry-over figure that the balance ledger covers.
    /// </summary>
    public class LeaveBalanceView
    {
        public int LeaveTypeId { get; set; }
        public string TypeCode { get; set; }
        public string TypeName { get; set; }

        /// <summary>Quota of the selected year (already materialized).</summary>
        public int QuotaDays { get; set; }

        /// <summary>Approved used days of the selected year.</summary>
        public int UsedDays { get; set; }

        /// <summary>Remaining of the selected year (quota - used); never negative.</summary>
        public int YearRemaining { get; set; }

        /// <summary>Sum of remainders of previous years (carry-over pool).</summary>
        public int CarryOverDays { get; set; }

        /// <summary>Total currently available to provide.</summary>
        public int TotalAvailable { get; set; }
    }
}
