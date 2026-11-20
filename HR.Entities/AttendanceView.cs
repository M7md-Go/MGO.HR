using System;

namespace HR.Entities
{
    /// <summary>Read-only row for the attendance browser: Attendance joined with Employees and Shifts.</summary>
    public class AttendanceView
    {
        /// <summary>AttendanceId.</summary>
        public int AttendanceId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }
        /// <summary>EmployeeCode.</summary>
        public string EmployeeCode { get; set; }
        /// <summary>FullNameAr.</summary>
        public string EmployeeName { get; set; }
        /// <summary>AttDate.</summary>
        public DateTime? AttDate { get; set; }
        /// <summary>ShiftId.</summary>
        public int? ShiftId { get; set; }
        /// <summary>ShiftCode.</summary>
        public string ShiftCode { get; set; }
        /// <summary>StartTime written by the engine (absolute datetime).</summary>
        public DateTime? StartTime { get; set; }
        /// <summary>EndTime written by the engine (absolute datetime, crosses days for night shifts).</summary>
        public DateTime? EndTime { get; set; }
        /// <summary>Status (Present/Late/EarlyLeave/MissingPunch/Absent/Leave/Mission/Holiday/WeeklyRest).</summary>
        public string Status { get; set; }
        /// <summary>IsManuallyChg — manually corrected rows are never overwritten by the engine.</summary>
        public bool? IsManuallyChg { get; set; }
        /// <summary>MinutesWorked.</summary>
        public int? MinutesWorked { get; set; }
        /// <summary>OvertimeMin.</summary>
        public int? OvertimeMin { get; set; }

        /// <summary>LateMinutes from Attendance.</summary>
        public int? LateMinutes { get; set; }

        /// <summary>EarlyLeaveMinutes from Attendance.</summary>
        public int? EarlyLeaveMinutes { get; set; }

        /// <summary>DepartmentId of the employee (report grouping).</summary>
        public int? DepartmentId { get; set; }

        /// <summary>Department name of the employee (report display).</summary>
        public string DepartmentName { get; set; }
    }
}
