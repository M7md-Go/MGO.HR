namespace HR.Entities
{
    /// <summary>One day/shift item of a schedule-change request: the employee,
    /// the day, the currently scheduled shift and the requested new shift.</summary>
    public class ScheduleChangeItem
    {
        /// <summary>Primary key (AUTOINCREMENT: ItemId).</summary>
        public int ItemId { get; set; }

        /// <summary>Owner request id.</summary>
        public int RequestId { get; set; }

        /// <summary>Employee whose day is changed.</summary>
        public int EmployeeId { get; set; }

        /// <summary>Day number (1-based within the month).</summary>
        public int DayNumber { get; set; }

        /// <summary>Shift currently scheduled on that day (null when unscheduled).</summary>
        public int? OldShiftId { get; set; }

        /// <summary>Requested new shift.</summary>
        public int? NewShiftId { get; set; }
    }
}
