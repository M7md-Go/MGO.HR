using System;

namespace HR.Entities
{
    /// <summary>Maps to table [LeaveBalances].</summary>
    public class LeaveBalance
    {
        /// <summary>Primary key (AUTOINCREMENT: BalanceId).</summary>
        public int BalanceId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>YearVal.</summary>
        public int? YearVal { get; set; }

        /// <summary>LeaveTypeId.</summary>
        public int? LeaveTypeId { get; set; }

        /// <summary>QuotaDays.</summary>
        public int? QuotaDays { get; set; }

        /// <summary>UsedDays.</summary>
        public int? UsedDays { get; set; }

        /// <summary>Remaining.</summary>
        public int? Remaining { get; set; }
    }
}
