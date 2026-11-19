using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Shifts].</summary>
    public class Shift
    {
        /// <summary>Primary key (AUTOINCREMENT: ShiftId).</summary>
        public int ShiftId { get; set; }
        /// <summary>ShiftCode.</summary>
        public string ShiftCode { get; set; }

        /// <summary>ShiftName.</summary>
        public string ShiftName { get; set; }

        /// <summary>StartTime.</summary>
        public string StartTime { get; set; }

        /// <summary>EndTime.</summary>
        public string EndTime { get; set; }

        /// <summary>CrossDay.</summary>
        public bool? CrossDay { get; set; }

        /// <summary>DurationHrs.</summary>
        public float? DurationHrs { get; set; }

        /// <summary>NormalHours.</summary>
        public float? NormalHours { get; set; }

        /// <summary>BreakMinutes.</summary>
        public int? BreakMinutes { get; set; }

        /// <summary>CategoryCode.</summary>
        public string CategoryCode { get; set; }

        /// <summary>RequireCheck.</summary>
        public bool? RequireCheck { get; set; }

        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }
    }
}
