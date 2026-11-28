using System;
using System.Collections.Generic;

namespace HR.Entities
{
    /// <summary>
    /// Attendance rules snapshot (rule 11/12/14): every threshold comes from
    /// System Settings — the engine itself contains no policy constants.
    /// </summary>
    public class EngineRules
    {
        /// <summary>Att.GraceMinutesLate — arrival later than shift start + grace counts as late.</summary>
        public int GraceMinutesLate { get; set; }
        /// <summary>Att.GraceMinutesEarlyLeave — leaving earlier than shift end - grace counts as early leave.</summary>
        public int GraceMinutesEarlyLeave { get; set; }
        /// <summary>Att.DuplicatePunchWindowMin — consecutive punches within the window collapse into one.</summary>
        public int DuplicatePunchWindowMin { get; set; }
        /// <summary>Sch.OTMinThresholdMinutes — minimum overtime span counted beyond the shift end.</summary>
        public int OvertimeThresholdMin { get; set; }
        /// <summary>Sch.OTRequiresApprove — engine overtime rows stay Pending until approved.</summary>
        public bool OvertimeRequiresApprove { get; set; }
        /// <summary>Att.OvertimeRate — overtime factor on ordinary workdays.</summary>
        public double OvertimeRate { get; set; }
        /// <summary>Att.HolidayOTRate — overtime factor on holidays and weekly rest.</summary>
        public double HolidayOvertimeRate { get; set; }
        /// <summary>Att.MaxContinuousWorkHours — a worked span above this raises an exception row.</summary>
        public double MaxContinuousWorkHours { get; set; }
        /// <summary>Sch.RestDays — configured weekly rest days (BR-03); never empty.</summary>
        public List<DayOfWeek> RestDays { get; set; }
    }

    /// <summary>Everything the engine needs for one recalculation pass (rule 10).</summary>
    public class EngineCalculationInput
    {
        /// <summary>First calculated date (inclusive).</summary>
        public DateTime FromDate { get; set; }
        /// <summary>Last calculated date (inclusive).</summary>
        public DateTime ToDate { get; set; }
        /// <summary>Employees participating (including archived so history stays complete).</summary>
        public List<Employee> Employees { get; set; }
        /// <summary>Schedule cells: EmployeeId → (full-date ticks → ShiftId).
        /// MODERNTIME 2.0 P0 fix: keyed by the FULL DATE (DateTime.Ticks), never by
        /// day-of-month — a recalculation spanning several months must not let
        /// month N+1's day numbers overwrite month N's shifts (employee + full
        /// date identity, per the 2.0 audit).</summary>
        public Dictionary<int, Dictionary<long, int>> ScheduleCells { get; set; }
        /// <summary>Shift index by ShiftId (only active shifts referenced by the schedule).</summary>
        public Dictionary<int, Shift> ShiftIndex { get; set; }
        /// <summary>Mapped fingerprint punches in an extended window, ordered by EmployeeId then LogTime.</summary>
        public List<FingerprintLog> Punches { get; set; }
        /// <summary>Approved leave requests overlapping the window.</summary>
        public List<LeaveRequest> ApprovedLeaves { get; set; }
        /// <summary>Approved missions overlapping the window.</summary>
        public List<Mission> ApprovedMissions { get; set; }
        /// <summary>All non-recurring holidays in the window plus every recurring holiday definition.</summary>
        public List<PublicHoliday> Holidays { get; set; }
        /// <summary>"EmployeeId|dateTicks" keys of manually edited attendance rows the engine must not touch.</summary>
        public HashSet<string> SkipKeys { get; set; }
        /// <summary>Rules snapshot built from System Settings.</summary>
        public EngineRules Rules { get; set; }
    }

    /// <summary>Everything the engine produced in one pass, ready for a single transactional write.</summary>
    public class EngineCalculationOutput
    {
        /// <summary>Upsert rows for [Attendance].</summary>
        public List<Attendance> AttendanceRows { get { return _attendanceRows; } }
        /// <summary>Engine-generated rows for [AttendanceExceptions] (CreatedBy stays NULL).</summary>
        public List<AttendanceException> Exceptions { get { return _exceptions; } }
        /// <summary>Auto overtime rows for [Overtime].</summary>
        public List<Overtime> OvertimeRows { get { return _overtimeRows; } }

        private readonly List<Attendance> _attendanceRows = new List<Attendance>();
        private readonly List<AttendanceException> _exceptions = new List<AttendanceException>();
        private readonly List<Overtime> _overtimeRows = new List<Overtime>();
    }
}
