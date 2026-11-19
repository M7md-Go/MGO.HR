using System;

namespace HR.Entities
{
    /// <summary>Maps to table [MonthlyShiftLocks].</summary>
    public class MonthlyShiftLock
    {
        /// <summary>Primary key (AUTOINCREMENT: LockId).</summary>
        public int LockId { get; set; }
        /// <summary>YearVal.</summary>
        public int? YearVal { get; set; }

        /// <summary>MonthVal.</summary>
        public int? MonthVal { get; set; }

        /// <summary>IsLocked.</summary>
        public bool? IsLocked { get; set; }

        /// <summary>LockedBy.</summary>
        public int? LockedBy { get; set; }

        /// <summary>LockedAt.</summary>
        public DateTime? LockedAt { get; set; }

        /// <summary>UnlockedBy.</summary>
        public int? UnlockedBy { get; set; }

        /// <summary>UnlockedAt.</summary>
        public DateTime? UnlockedAt { get; set; }

        /// <summary>UnlockReason.</summary>
        public string UnlockReason { get; set; }
    }
}
