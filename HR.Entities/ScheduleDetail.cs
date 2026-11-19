using System;

namespace HR.Entities
{
    /// <summary>Maps to table [ScheduleDetails].</summary>
    public class ScheduleDetail
    {
        /// <summary>Primary key (AUTOINCREMENT: DetailId).</summary>
        public int DetailId { get; set; }
        /// <summary>ScheduleId.</summary>
        public int? ScheduleId { get; set; }

        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>DayNumber.</summary>
        public int? DayNumber { get; set; }

        /// <summary>ShiftId.</summary>
        public int? ShiftId { get; set; }
    }
}
