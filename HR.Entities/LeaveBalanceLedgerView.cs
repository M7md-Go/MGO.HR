using System;

namespace HR.Entities
{
    /// <summary>Read-only row: LeaveBalances joined with Employees and LeaveTypes.</summary>
    public class LeaveBalanceLedgerView
    {
        /// <summary>BalanceId.</summary>
        public int BalanceId { get; set; }

        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>EmployeeCode.</summary>
        public string EmployeeCode { get; set; }

        /// <summary>FullNameAr.</summary>
        public string EmployeeName { get; set; }

        /// <summary>YearVal (balance year).</summary>
        public int? YearVal { get; set; }

        /// <summary>LeaveTypeId.</summary>
        public int? LeaveTypeId { get; set; }

        /// <summary>LeaveTypeCode.</summary>
        public string TypeCode { get; set; }

        /// <summary>Type name (from LeaveTypes).</summary>
        public string TypeName { get; set; }

        /// <summary>QuotaDays.</summary>
        public decimal? QuotaDays { get; set; }

        /// <summary>UsedDays.</summary>
        public decimal? UsedDays { get; set; }

        /// <summary>Remaining.</summary>
        public decimal? Remaining { get; set; }
    }
}
