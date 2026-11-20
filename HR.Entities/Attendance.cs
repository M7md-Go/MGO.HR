using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Attendance].</summary>
    public class Attendance
    {
        /// <summary>Primary key (AUTOINCREMENT: AttendanceId).</summary>
        public int AttendanceId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>AttDate.</summary>
        public DateTime? AttDate { get; set; }

        /// <summary>ShiftId.</summary>
        public int? ShiftId { get; set; }

        /// <summary>StartTime.</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>EndTime.</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>IsManuallyChg.</summary>
        public bool? IsManuallyChg { get; set; }

        /// <summary>MinutesWorked.</summary>
        public int? MinutesWorked { get; set; }

        /// <summary>OvertimeMin.</summary>
        public int? OvertimeMin { get; set; }

        /// <summary>LateMinutes — minutes past shift start + grace, written by the engine.</summary>
        public int? LateMinutes { get; set; }

        /// <summary>EarlyLeaveMinutes — minutes before shift end - grace, written by the engine.</summary>
        public int? EarlyLeaveMinutes { get; set; }

        /// <summary>ApprovedBy.</summary>
        public int? ApprovedBy { get; set; }

        /// <summary>ApprovedAt.</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
