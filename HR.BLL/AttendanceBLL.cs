using System;
using System.Collections.Generic;
using System.Diagnostics;
using HR.DAL;
using HR.Entities;
using HR.Helpers;
using HR.Services;

namespace HR.BLL
{
    /// <summary>Counters of one recalculation run, shown to the operator and audit-logged.</summary>
    public class RecalcSummary
    {
        /// <summary>Window start (date).</summary>
        public DateTime FromDate { get; set; }
        /// <summary>Window end (date).</summary>
        public DateTime ToDate { get; set; }
        /// <summary>Employee-days the engine processed (schedule cells inside the window, minus manual rows).</summary>
        public int DaysProcessed { get; set; }
        /// <summary>Days skipped because a manual correction owns them.</summary>
        public int DaysSkippedManual { get; set; }
        /// <summary>Attendance rows written (inserts or engine updates).</summary>
        public int AttendanceRows { get; set; }
        /// <summary>Exception rows raised.</summary>
        public int Exceptions { get; set; }
        /// <summary>Auto overtime rows written.</summary>
        public int OvertimeRows { get; set; }
        /// <summary>Total wall time in milliseconds.</summary>
        public int DurationMs { get; set; }
        /// <summary>E-01: readable "employee @ date" list of manual days preserved untouched (audit + operator report).</summary>
        public List<string> PreservedManualDays { get; set; }
    }

    /// <summary>
    /// Orchestrates the attendance engine (rule 10): loads schedule cells,
    /// shifts, punches, approved leaves/missions and holidays, runs the pure
    /// HR.Services/AttendanceEngine over the window and persists everything in
    /// ONE transaction. Running a recalculation modifies attendance, so it is
    /// gated by the existing seed permission "Attendance.Edit"; browsing is
    /// read-only. Manual corrections (IsManuallyChg = true) are never touched.
    /// </summary>
    public static class AttendanceBLL
    {
        /// <summary>Recalculation window cap: one full quarter, keeps Jet transactions short.</summary>
        public const int MaxRecalcDays = 93;

        public static RecalcSummary Recalculate(DateTime fromDate, DateTime toDate)
        {
            AppContext.RequireAnyPermission(new string[] { "Attendance.Engine", "Attendance.Edit" });
            fromDate = fromDate.Date;
            toDate = toDate.Date;
            if (toDate < fromDate)
            {
                throw new BusinessException("تاريخ النهاية قبل تاريخ البداية.");
            }
            if ((toDate - fromDate).TotalDays + 1 > MaxRecalcDays)
            {
                throw new BusinessException("الفترة أطول من " + MaxRecalcDays +
                    " يوماً — قسّمها على مرات حتى تظل المعاملة قصيرة.");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            // MODERNTIME 2.0 P0 fix: cell maps of DIFFERENT months are merged
            // here, so the inner key must be the full date (ticks) — day-of-month
            // keys collided across months and silently reused the later month's
            // shift for every earlier month in the window.
            Dictionary<int, Dictionary<long, int>> cells = new Dictionary<int, Dictionary<long, int>>();
            Dictionary<int, Shift> shiftIndex = new Dictionary<int, Shift>();
            List<string> processedKeys = new List<string>();

            DateTime monthCursor = new DateTime(fromDate.Year, fromDate.Month, 1);
            while (monthCursor <= toDate)
            {
                MonthData month = ScheduleBLL.LoadMonth(monthCursor.Year, monthCursor.Month, null, null);
                foreach (KeyValuePair<int, Shift> shiftPair in month.Shifts)
                {
                    shiftIndex[shiftPair.Key] = shiftPair.Value;
                }
                foreach (KeyValuePair<int, Dictionary<int, int>> employeePair in month.Cells)
                {
                    Dictionary<long, int> employeeCells;
                    if (!cells.TryGetValue(employeePair.Key, out employeeCells))
                    {
                        employeeCells = new Dictionary<long, int>();
                        cells[employeePair.Key] = employeeCells;
                    }
                    foreach (KeyValuePair<int, int> dayPair in employeePair.Value)
                    {
                        DateTime cellDate = new DateTime(monthCursor.Year, monthCursor.Month, dayPair.Key);
                        if (cellDate < fromDate || cellDate > toDate)
                        {
                            continue;
                        }
                        employeeCells[cellDate.Ticks] = dayPair.Value;
                        processedKeys.Add(employeePair.Key + "|" + cellDate.Ticks);
                    }
                }
                monthCursor = monthCursor.AddMonths(1);
            }

            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.IncludeArchived = true;
            filter.PageSize = 10000;
            List<Employee> employees = EmployeeBLL.Search(filter);
            if (employees.Count == 0)
            {
                throw new BusinessException("لا يوجد موظفون لحساب الحضور.");
            }
            if (processedKeys.Count == 0)
            {
                throw new BusinessException("لا يوجد جدول شهري معتمد داخل الفترة المحددة — أنشئ الجدول أولاً.");
            }

            DateTime punchFrom = fromDate;
            DateTime punchTo = toDate.AddDays(2);
            RecalcSummary summary = new RecalcSummary();
            summary.FromDate = fromDate;
            summary.ToDate = toDate;
            summary.DaysProcessed = processedKeys.Count;

            EngineCalculationInput input = new EngineCalculationInput();
            input.FromDate = fromDate;
            input.ToDate = toDate;
            input.Employees = employees;
            input.ScheduleCells = cells;
            input.ShiftIndex = shiftIndex;
            input.Punches = FingerprintDAL.LoadMappedLogs(punchFrom, punchTo);
            input.ApprovedLeaves = AttendanceDAL.LoadApprovedLeaves(fromDate, toDate);

            foreach (LeaveRequest openEnded in LongLeaveDAL.LoadApprovedForEngine(fromDate, toDate))
            {
                input.ApprovedLeaves.Add(openEnded);
            }
            input.ApprovedMissions = AttendanceDAL.LoadApprovedMissions(fromDate, toDate);
            input.Holidays = AttendanceDAL.LoadHolidays(fromDate, toDate);
            input.SkipKeys = AttendanceDAL.LoadManualKeys(fromDate, toDate);
            input.Rules = LoadRules();
            summary.DaysSkippedManual = input.SkipKeys.Count;

            // E-01: manual days are preserved untouched — declare exactly which
            // employee-days were protected so the operator and the audit trail
            // both state the modifications that were kept.
            Dictionary<int, string> codeById = new Dictionary<int, string>();
            foreach (Employee employee in employees)
            {
                codeById[employee.EmployeeId] = employee.EmployeeCode;
            }
            List<string> preserved = new List<string>();
            foreach (string skipKey in input.SkipKeys)
            {
                int skipSeparator = skipKey.IndexOf('|');
                int skipEmployeeId = Convert.ToInt32(skipKey.Substring(0, skipSeparator));
                DateTime skipDate = new DateTime(long.Parse(skipKey.Substring(skipSeparator + 1)));
                preserved.Add((codeById.ContainsKey(skipEmployeeId) ? codeById[skipEmployeeId] :
                    skipEmployeeId.ToString()) + " @ " + skipDate.ToString("yyyy-MM-dd"));
            }
            summary.PreservedManualDays = preserved;

            EngineCalculationOutput output = AttendanceEngine.Calculate(input);
            if (!input.Rules.OvertimeRequiresApprove)
            {
                foreach (Overtime row in output.OvertimeRows)
                {
                    row.Status = "Approved";
                    row.ApprovedBy = AppContext.CurrentUser == null ?
                        (int?)null : AppContext.CurrentUser.UserId;
                    row.ApprovedAt = DateTime.Now;
                }
            }
            AttendanceDAL.WriteEngineResults(output.AttendanceRows, output.Exceptions,
                output.OvertimeRows, processedKeys, input.SkipKeys);

            summary.AttendanceRows = output.AttendanceRows.Count;
            summary.Exceptions = output.Exceptions.Count;
            summary.OvertimeRows = output.OvertimeRows.Count;
            summary.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            string preservedSample = preserved.Count <= 15 ?
                string.Join("; ", preserved.ToArray()) :
                string.Join("; ", preserved.GetRange(0, 15).ToArray()) + " … (" + preserved.Count + " إجمالًا)";
            AuditBLL.Log("Attendance.Recalculate", "Attendance", null, null,
                "From=" + fromDate.ToString("yyyy-MM-dd") + ", To=" + toDate.ToString("yyyy-MM-dd") +
                ", Days=" + summary.DaysProcessed + ", SkippedManual=" + summary.DaysSkippedManual +
                ", Preserved=[" + preservedSample + "]" +
                ", Attendance=" + summary.AttendanceRows + ", Exceptions=" + summary.Exceptions +
                ", Overtime=" + summary.OvertimeRows + ", Ms=" + summary.DurationMs);
            return summary;
        }

        /// <summary>Read-only browser rows for the attendance screen and later reports.</summary>
        public static List<AttendanceView> Browse(DateTime fromDate, DateTime toDate, int? employeeId,
            int? departmentId)
        {
            return AttendanceDAL.Browse(fromDate.Date, toDate.Date, employeeId, departmentId, 2000);
        }

        /// <summary>Rules snapshot — every value lives in System Settings (rule 11/36).</summary>
        private static EngineRules LoadRules()
        {
            EngineRules rules = new EngineRules();
            rules.GraceMinutesLate = SettingsBLL.GetInt("Att.GraceMinutesLate", 30);
            rules.GraceMinutesEarlyLeave = SettingsBLL.GetInt("Att.GraceMinutesEarlyLeave", 15);
            rules.DuplicatePunchWindowMin = SettingsBLL.GetInt("Att.DuplicatePunchWindowMin", 1);
            rules.OvertimeThresholdMin = SettingsBLL.GetInt("Sch.OTMinThresholdMinutes", 30);
            rules.OvertimeRequiresApprove = SettingsBLL.GetBool("Sch.OTRequiresApprove", true);
            rules.OvertimeRate = SettingsBLL.GetDouble("Att.OvertimeRate", 1.5);
            rules.HolidayOvertimeRate = SettingsBLL.GetDouble("Att.HolidayOTRate", 2.0);
            rules.MaxContinuousWorkHours = SettingsBLL.GetDouble("Att.MaxContinuousWorkHours", 24);
            rules.RestDays = WeekRestDays.Parse(SettingsBLL.GetString(WeekRestDays.SettingKey, WeekRestDays.DefaultDays));
            return rules;
        }
    }
}
