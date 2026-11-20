using System;

namespace HR.Entities
{
    /// <summary>Maps to table [LongLeaveTypes] — long-duration leave categories (child care, family care, going abroad, income improvement).</summary>
    public class LongLeaveType
    {
        /// <summary>Primary key (AUTOINCREMENT: LongLeaveTypeId).</summary>
        public int LongLeaveTypeId { get; set; }
        /// <summary>TypeCode (ChildCare/FamilyCare/Abroad/IncomeImprove).</summary>
        public string TypeCode { get; set; }
        /// <summary>TypeName (Arabic display name).</summary>
        public string TypeName { get; set; }
        /// <summary>IsLimited (false = unlimited duration until manually ended).</summary>
        public bool? IsLimited { get; set; }
        /// <summary>MaxMonths (null when unlimited or open limit).</summary>
        public int? MaxMonths { get; set; }
        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }
        /// <summary>Notes.</summary>
        public string Notes { get; set; }
    }
}
