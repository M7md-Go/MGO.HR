using System;
using System.Collections.Generic;
using HR.Entities;

namespace HR.Services
{
    /// <summary>
    /// Attendance Calculation Engine (rule 10) — a PURE calculator with no
    /// database access: it receives employees, dates, schedule cells, shifts,
    /// punches, approved leaves, approved missions, holidays and a rules
    /// snapshot, and produces write-ready Attendance rows, AttendanceException
    /// rows and auto Overtime rows. Day-precedence (rule 36, safest and most
    /// scalable order): exempt Mission &gt; approved Leave &gt; public Holiday
    /// &gt; weekly rest (FR shift, or any Sch.RestDays day without a cell) &gt; workday. Punch
    /// pairing is sequential per employee — every punch is consumed at most
    /// once, so a night-shift exit punch (cross-day shift, rule 13) lands on
    /// the day whose shift it closes and can never be re-paired as a stray
    /// arrival by the next day. Duplicate taps collapse within
    /// DuplicatePunchWindowMin, resolving the near-duplicates produced when an
    /// enroll code is mapped after a first download. All policy values come
    /// from EngineRules (System Settings); the only fixed internal limits are
    /// the pairing search bounds below, which are engine mechanics and
    /// documented in PHASE7_STATUS.md.
    /// </summary>
    public static class AttendanceEngine
    {
        /// <summary>Earliest arrival paired with a shift start (punches further ahead are not arrivals).</summary>
        private const int PreInWindowHours = 2;

        /// <summary>How long after the planned exit an exit punch is still searched (night-shift closes, morning exits).</summary>
        private const int PostOutWindowHours = 12;

        /// <summary>Runs one pass over the input window and returns untouched write-ready rows.</summary>
        public static EngineCalculationOutput Calculate(EngineCalculationInput input)
        {
            EngineCalculationOutput output = new EngineCalculationOutput();
            if (input == null || input.Employees == null || input.Employees.Count == 0)
            {
                return output;
            }

            Dictionary<int, List<FingerprintLog>> punchesByEmployee = IndexPunches(input.Punches);
            Dictionary<int, List<LeaveRequest>> leavesByEmployee = IndexByEmployee(input.ApprovedLeaves,
                delegate(LeaveRequest row) { return row.EmployeeId; });
            Dictionary<int, List<Mission>> missionsByEmployee = IndexByEmployee(input.ApprovedMissions,
                delegate(Mission row) { return row.EmployeeId; });
            Dictionary<string, PublicHoliday> holidayByDate = new Dictionary<string, PublicHoliday>();
            Dictionary<string, PublicHoliday> holidayByMonthDay = new Dictionary<string, PublicHoliday>();
            if (input.Holidays != null)
            {
                foreach (PublicHoliday holiday in input.Holidays)
                {
                    if (holiday.HolidayDate == null)
                    {
                        continue;
                    }
                    if (holiday.IsRecurring == true)
                    {
                        holidayByMonthDay[RecurringKey(holiday.HolidayDate.Value)] = holiday;
                    }
                    else
                    {
                        holidayByDate[holiday.HolidayDate.Value.Date.Ticks.ToString()] = holiday;
                    }
                }
            }

            foreach (Employee employee in input.Employees)
            {
                Dictionary<int, Dictionary<long, int>> monthCells;
                if (input.ScheduleCells == null || !input.ScheduleCells.TryGetValue(employee.EmployeeId, out monthCells))
                {
                    continue;
                }
                List<FingerprintLog> punches = punchesByEmployee.ContainsKey(employee.EmployeeId) ?
                    punchesByEmployee[employee.EmployeeId] : new List<FingerprintLog>();
                List<LeaveRequest> leaves = leavesByEmployee.ContainsKey(employee.EmployeeId) ?
                    leavesByEmployee[employee.EmployeeId] : new List<LeaveRequest>();
                List<Mission> missions = missionsByEmployee.ContainsKey(employee.EmployeeId) ?
                    missionsByEmployee[employee.EmployeeId] : new List<Mission>();

                int punchCursor = 0;
                for (DateTime date = input.FromDate.Date; date <= input.ToDate.Date; date = date.AddDays(1))
                {
                    if (input.SkipKeys != null && input.SkipKeys.Contains(employee.EmployeeId + "|" + date.Ticks))
                    {
                        continue;
                    }
                    punchCursor = CalculateDay(input, output, employee.EmployeeId, date, monthCells,
                        punches, punchCursor, leaves, missions, holidayByDate, holidayByMonthDay);
                }
            }
            return output;
        }

        private static int CalculateDay(EngineCalculationInput input, EngineCalculationOutput output,
            int employeeId, DateTime date, Dictionary<long, int> cells, List<FingerprintLog> punches, int punchCursor,
            List<LeaveRequest> leaves, List<Mission> missions,
            Dictionary<string, PublicHoliday> holidayByDate, Dictionary<string, PublicHoliday> holidayByMonthDay)
        {
            int shiftId;
            Shift shift = null;
            // MODERNTIME 2.0 P0 fix: cells are keyed by full date (ticks), not
            // day-of-month — multi-month windows must resolve each date to its
            // own month's shift.
            if (cells.TryGetValue(date.Ticks, out shiftId))
            {
                if (input.ShiftIndex == null || !input.ShiftIndex.TryGetValue(shiftId, out shift))
                {
                    return punchCursor;
                }
            }
            if (shift == null)
            {
                Mission freeMission = FindMission(missions, date);
                if (freeMission != null && freeMission.IsExemptAtt == true)
                {
                    AddAttendance(output, employeeId, date, null, null, null, "Mission", null, 0, 0, 0);
                    return punchCursor;
                }
                if (FindLeave(leaves, date))
                {
                    AddAttendance(output, employeeId, date, null, null, null, "Leave", null, 0, 0, 0);
                    return punchCursor;
                }
                if (FindHoliday(date, holidayByDate, holidayByMonthDay))
                {
                    CalculateRestDayWork(input, output, employeeId, date, null, "Holiday", punches, ref punchCursor);
                    return punchCursor;
                }
                if (IsWeeklyRest(input.Rules, date))
                {
                    CalculateRestDayWork(input, output, employeeId, date, null, "WeeklyRest", punches, ref punchCursor);
                    return punchCursor;
                }
                return punchCursor;
            }
            if (string.IsNullOrEmpty(shift.StartTime) || string.IsNullOrEmpty(shift.EndTime))
            {
                AddException(output, employeeId, date, "BadShift", null);
                return punchCursor;
            }

            bool restShift = IsRestShift(shift);
            if (restShift && punches.Count <= punchCursor)
            {
                AddAttendance(output, employeeId, date, shift.ShiftId, null, null, "WeeklyRest", null, 0, 0, 0);
                return punchCursor;
            }

            bool isHoliday = FindHoliday(date, holidayByDate, holidayByMonthDay);
            bool isWeeklyRest = restShift;

            Mission mission = FindMission(missions, date);
            if (mission != null && mission.IsExemptAtt == true)
            {
                AddAttendance(output, employeeId, date, shift.ShiftId, null, null, "Mission", null, 0, 0, 0);
                return punchCursor;
            }
            if (FindLeave(leaves, date))
            {
                AddAttendance(output, employeeId, date, shift.ShiftId, null, null, "Leave", null, 0, 0, 0);
                return punchCursor;
            }
            if (isHoliday)
            {
                CalculateRestDayWork(input, output, employeeId, date, shift.ShiftId, "Holiday", punches, ref punchCursor);
                return punchCursor;
            }
            if (isWeeklyRest)
            {
                CalculateRestDayWork(input, output, employeeId, date, shift.ShiftId, "WeeklyRest", punches, ref punchCursor);
                return punchCursor;
            }

            return CalculateWorkDay(input, output, employeeId, date, shift, punches, punchCursor);
        }

        private static int CalculateWorkDay(EngineCalculationInput input, EngineCalculationOutput output,
            int employeeId, DateTime date, Shift shift, List<FingerprintLog> punches, int punchCursor)
        {
            bool requireCheck = shift.RequireCheck != false;
            TimeSpan startSpan = ParseHm(shift.StartTime);
            TimeSpan endSpan = ParseHm(shift.EndTime);
            DateTime expectedIn = date.Date + startSpan;
            DateTime expectedOut = expectedIn + WorkSpan(startSpan, endSpan, shift.CrossDay == true);
            DateTime inWindowStart = expectedIn.AddHours(-PreInWindowHours);
            DateTime inWindowEnd = expectedOut;

            int inIndex = FindFirstPunch(punches, punchCursor, inWindowStart, inWindowEnd);
            if (inIndex < 0)
            {
                if (requireCheck)
                {
                    AddAttendance(output, employeeId, date, shift.ShiftId, null, null, "Absent", null, 0, 0, 0);
                    AddException(output, employeeId, date, "Absent", null);
                }
                return punchCursor;
            }

            DateTime punchIn = punches[inIndex].LogTime ?? date.Date;
            int outIndex = FindLastPunch(punches, inIndex + 1, punchIn.AddMinutes(1),
                expectedOut.AddHours(PostOutWindowHours));
            if (outIndex < 0)
            {
                if (requireCheck)
                {
                    AddAttendance(output, employeeId, date, shift.ShiftId, punchIn, null, "MissingPunch", null, 0,
                        punchIn > expectedIn.AddMinutes(input.Rules.GraceMinutesLate) ?
                        (int)(punchIn - expectedIn).TotalMinutes : 0, 0);
                    AddException(output, employeeId, date, "MissingPunch", null);
                }
                return inIndex + 1;
            }

            DateTime punchOut = punches[outIndex].LogTime ?? expectedOut;
            int nextCursor = outIndex + 1;
            if (requireCheck && CountGaps(punches, inIndex, outIndex, input.Rules.DuplicatePunchWindowMin) > 0)
            {
                AddException(output, employeeId, date, "ExtraPunch", null);
            }

            int workedMinutes = (int)(punchOut - punchIn).TotalMinutes;
            int breakMinutes = shift.BreakMinutes ?? 0;
            if (workedMinutes > breakMinutes)
            {
                workedMinutes -= breakMinutes;
                if (workedMinutes < 0)
                {
                    workedMinutes = 0;
                }
            }

            int lateMinutes = 0;
            if (punchIn > expectedIn.AddMinutes(input.Rules.GraceMinutesLate))
            {
                lateMinutes = (int)(punchIn - expectedIn).TotalMinutes;
                AddException(output, employeeId, date, "Late", lateMinutes);
            }
            int earlyMinutes = 0;
            if (punchOut < expectedOut.AddMinutes(-input.Rules.GraceMinutesEarlyLeave))
            {
                earlyMinutes = (int)(expectedOut - punchOut).TotalMinutes;
                AddException(output, employeeId, date, "EarlyLeave", earlyMinutes);
            }
            if (input.Rules.MaxContinuousWorkHours > 0 &&
                (int)(punchOut - punchIn).TotalMinutes > input.Rules.MaxContinuousWorkHours * 60)
            {
                AddException(output, employeeId, date, "OverContinuous",
                    (int)(punchOut - punchIn).TotalMinutes);
            }

            int overtimeMinutes = 0;
            if (punchOut > expectedOut.AddMinutes(input.Rules.OvertimeThresholdMin))
            {
                overtimeMinutes = (int)(punchOut - expectedOut).TotalMinutes;
                AddOvertimeRow(output, employeeId, date, overtimeMinutes, input.Rules.OvertimeRate);
            }

            string status = lateMinutes > 0 ? "Late" : (earlyMinutes > 0 ? "EarlyLeave" : "Present");
            AddAttendance(output, employeeId, date, shift.ShiftId, punchIn, punchOut, status,
                workedMinutes, overtimeMinutes, lateMinutes, earlyMinutes);
            return nextCursor;
        }

        private static void CalculateRestDayWork(EngineCalculationInput input, EngineCalculationOutput output,
            int employeeId, DateTime date, int? shiftId, string status, List<FingerprintLog> punches, ref int punchCursor)
        {
            DateTime dayEnd = date.Date.AddDays(1);
            int first = FindFirstPunch(punches, punchCursor, date.Date, dayEnd);
            if (first < 0)
            {
                AddAttendance(output, employeeId, date, shiftId, null, null, status, null, 0, 0, 0);
                return;
            }
            int last = FindLastPunch(punches, first, (punches[first].LogTime ?? date.Date).AddMinutes(1), dayEnd);
            DateTime inTime = punches[first].LogTime ?? date.Date;
            DateTime outTime = last < 0 ? inTime : (punches[last].LogTime ?? inTime);
            int workedMinutes = (int)(outTime - inTime).TotalMinutes;
            punchCursor = (last < 0 ? first : last) + 1;
            if (workedMinutes <= 0)
            {
                AddAttendance(output, employeeId, date, shiftId, inTime, outTime, status, null, 0, 0, 0);
                return;
            }
            if (input.Rules.MaxContinuousWorkHours > 0 && workedMinutes > input.Rules.MaxContinuousWorkHours * 60)
            {
                AddException(output, employeeId, date, "OverContinuous", workedMinutes);
            }
            AddOvertimeRow(output, employeeId, date, workedMinutes, input.Rules.HolidayOvertimeRate);
            AddAttendance(output, employeeId, date, shiftId, inTime, outTime, status, workedMinutes, workedMinutes, 0, 0);
        }

        private static void AddAttendance(EngineCalculationOutput output, int employeeId, DateTime date,
            int? shiftId, DateTime? inTime, DateTime? outTime, string status, int? minutesWorked, int overtimeMinutes,
            int lateMinutes, int earlyMinutes)
        {
            Attendance row = new Attendance();
            row.EmployeeId = employeeId;
            row.AttDate = date;
            row.ShiftId = shiftId;
            row.StartTime = inTime;
            row.EndTime = outTime;
            row.Status = status;
            row.IsManuallyChg = false;
            row.MinutesWorked = minutesWorked;
            row.OvertimeMin = overtimeMinutes;
            row.LateMinutes = lateMinutes;
            row.EarlyLeaveMinutes = earlyMinutes;
            output.AttendanceRows.Add(row);
        }

        private static void AddException(EngineCalculationOutput output, int employeeId, DateTime date,
            string excType, int? excValue)
        {
            AttendanceException row = new AttendanceException();
            row.EmployeeId = employeeId;
            row.AttDate = date;
            row.ExcType = excType;
            row.ExcValue = excValue;
            output.Exceptions.Add(row);
        }

        private static void AddOvertimeRow(EngineCalculationOutput output, int employeeId, DateTime date,
            int actualMinutes, double rate)
        {
            Overtime row = new Overtime();
            row.EmployeeId = employeeId;
            row.OtDate = date;
            row.ActualMinutes = actualMinutes;
            row.EffectiveMinutes = actualMinutes;
            row.Rate = rate;
            row.Source = "Auto";
            row.Status = "Pending";
            output.OvertimeRows.Add(row);
        }

        private static Dictionary<int, List<FingerprintLog>> IndexPunches(List<FingerprintLog> punches)
        {
            Dictionary<int, List<FingerprintLog>> index = new Dictionary<int, List<FingerprintLog>>();
            if (punches == null)
            {
                return index;
            }
            foreach (FingerprintLog punch in punches)
            {
                if (punch.EmployeeId == null || punch.LogTime == null)
                {
                    continue;
                }
                List<FingerprintLog> bucket;
                if (!index.TryGetValue(punch.EmployeeId.Value, out bucket))
                {
                    bucket = new List<FingerprintLog>();
                    index[punch.EmployeeId.Value] = bucket;
                }
                bucket.Add(punch);
            }
            foreach (List<FingerprintLog> bucket in index.Values)
            {
                bucket.Sort(delegate(FingerprintLog a, FingerprintLog b)
                {
                    return (a.LogTime ?? DateTime.MinValue).CompareTo(b.LogTime ?? DateTime.MinValue);
                });
            }
            return index;
        }

        private static Dictionary<int, List<T>> IndexByEmployee<T>(List<T> rows, Converter<T, int?> keySelector)
        {
            Dictionary<int, List<T>> index = new Dictionary<int, List<T>>();
            if (rows == null)
            {
                return index;
            }
            foreach (T row in rows)
            {
                int? employeeId = keySelector(row);
                if (employeeId == null)
                {
                    continue;
                }
                List<T> bucket;
                if (!index.TryGetValue(employeeId.Value, out bucket))
                {
                    bucket = new List<T>();
                    index[employeeId.Value] = bucket;
                }
                bucket.Add(row);
            }
            return index;
        }

        private static bool FindLeave(List<LeaveRequest> leaves, DateTime date)
        {
            foreach (LeaveRequest request in leaves)
            {
                if (request.StartDate != null && request.EndDate != null &&
                    date.Date >= request.StartDate.Value.Date && date.Date <= request.EndDate.Value.Date)
                {
                    return true;
                }
            }
            return false;
        }

        private static Mission FindMission(List<Mission> missions, DateTime date)
        {
            foreach (Mission mission in missions)
            {
                if (mission.StartDate != null && mission.EndDate != null &&
                    date.Date >= mission.StartDate.Value.Date && date.Date <= mission.EndDate.Value.Date)
                {
                    return mission;
                }
            }
            return null;
        }

        private static bool FindHoliday(DateTime date, Dictionary<string, PublicHoliday> holidayByDate,
            Dictionary<string, PublicHoliday> holidayByMonthDay)
        {
            if (holidayByDate.ContainsKey(date.Date.Ticks.ToString()))
            {
                return true;
            }
            return holidayByMonthDay.ContainsKey(RecurringKey(date));
        }

        private static string RecurringKey(DateTime date)
        {
            return date.Month + "-" + date.Day;
        }

        private static int FindFirstPunch(List<FingerprintLog> punches, int startIndex, DateTime from, DateTime to)
        {
            for (int i = startIndex; i < punches.Count; i++)
            {
                DateTime time = punches[i].LogTime ?? DateTime.MinValue;
                if (time >= from && time <= to)
                {
                    return i;
                }
                if (time > to)
                {
                    return -1;
                }
            }
            return -1;
        }

        private static int FindLastPunch(List<FingerprintLog> punches, int startIndex, DateTime from, DateTime to)
        {
            int found = -1;
            for (int i = startIndex; i < punches.Count; i++)
            {
                DateTime time = punches[i].LogTime ?? DateTime.MinValue;
                if (time > to)
                {
                    break;
                }
                if (time >= from)
                {
                    found = i;
                }
            }
            return found;
        }

        /// <summary>Counts punch clusters beyond the first inside the [inIndex, outIndex] span (duplicate taps).</summary>
        private static int CountGaps(List<FingerprintLog> punches, int inIndex, int outIndex, int dupWindowMinutes)
        {
            int clusters = 0;
            DateTime previous = DateTime.MinValue;
            for (int i = inIndex; i <= outIndex; i++)
            {
                DateTime time = punches[i].LogTime ?? DateTime.MinValue;
                if (i == inIndex || (time - previous).TotalMinutes > dupWindowMinutes)
                {
                    clusters++;
                }
                previous = time;
            }
            return clusters - 1;
        }

        private static bool IsRestShift(Shift shift)
        {
            return string.Equals(shift.ShiftCode, "FR", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>BR-03 rest day: from Sch.RestDays via EngineRules (fallback Friday-only).</summary>
        private static bool IsWeeklyRest(EngineRules rules, DateTime date)
        {
            List<DayOfWeek> restDays = rules.RestDays;
            if (restDays == null || restDays.Count == 0)
            {
                restDays = WeekRestDays.Parse(null);
            }
            return restDays.Contains(date.DayOfWeek);
        }

        private static TimeSpan WorkSpan(TimeSpan start, TimeSpan end, bool crossDay)
        {
            if (crossDay)
            {
                return end + TimeSpan.FromDays(1) - start;
            }
            return end - start;
        }

        private static TimeSpan ParseHm(string value)
        {
            string[] parts = (value ?? "00:00").Split(':');
            int hours;
            int minutes;
            int.TryParse(parts.Length > 0 ? parts[0] : "0", out hours);
            int.TryParse(parts.Length > 1 ? parts[1] : "0", out minutes);
            return new TimeSpan(Math.Min(Math.Max(hours, 0), 23), Math.Min(Math.Max(minutes, 0), 59), 0);
        }
    }
}
