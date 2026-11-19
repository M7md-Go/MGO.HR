using System;

namespace HR.Entities
{
    /// <summary>Maps to table [PublicHolidays].</summary>
    public class PublicHoliday
    {
        /// <summary>Primary key (AUTOINCREMENT: HolidayId).</summary>
        public int HolidayId { get; set; }
        /// <summary>HolidayDate.</summary>
        public DateTime? HolidayDate { get; set; }

        /// <summary>HolidayName.</summary>
        public string HolidayName { get; set; }

        /// <summary>IsRecurring.</summary>
        public bool? IsRecurring { get; set; }

        /// <summary>ShiftId.</summary>
        public int? ShiftId { get; set; }

        /// <summary>AppliesTo.</summary>
        public int? AppliesTo { get; set; }
    }
}
