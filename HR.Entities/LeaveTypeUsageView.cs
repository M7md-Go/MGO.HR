using System;

namespace HR.Entities
{
    /// <summary>
    /// One row of the special-leaves usage grid (sick, maternity, childcare…):
    /// leaves that are tracked by actual approved usage rather than consumed
    /// from a fixed annual quota.
    /// </summary>
    public class LeaveTypeUsageView
    {
        public int LeaveTypeId { get; set; }
        public string TypeCode { get; set; }
        public string TypeName { get; set; }

        /// <summary>Approved requests of the selected year.</summary>
        public int ApprovedRequests { get; set; }

        /// <summary>Approved used days of the selected year.</summary>
        public decimal UsedDays { get; set; }

        /// <summary>Approved used days over all years.</summary>
        public decimal LifetimeDays { get; set; }

        /// <summary>Indicative ceiling (type AnnualDays) or null when unlimited.</summary>
        public int? AnnualDays { get; set; }

        /// <summary>Whether a supporting document is required.</summary>
        public bool RequiresDoc { get { return RequiresDocValue == true; } }
        public bool? RequiresDocValue { get; set; }
    }
}
