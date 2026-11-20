using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>Full snapshot of one monthly schedule grid handed to the UI.</summary>
    public class MonthData
    {
        /// <summary>Schedule header id; null when the month has no header yet.</summary>
        public int? ScheduleId { get; set; }

        /// <summary>Year of the schedule.</summary>
        public int Year { get; set; }

        /// <summary>Month of the schedule.</summary>
        public int Month { get; set; }

        /// <summary>True when the month is locked (edits: Admin only, audited).</summary>
        public bool IsLocked { get; set; }

        /// <summary>Employees included in this view (active, filtered).</summary>
        public List<Employee> Employees { get; set; }

        /// <summary>All shifts by id (templates used by the grid).</summary>
        public Dictionary<int, Shift> Shifts { get; set; }

        /// <summary>Cell map: EmployeeId → DayNumber (1-based) → ShiftId.</summary>
        public Dictionary<int, Dictionary<int, int>> Cells { get; set; }
    }

    /// <summary>
    /// Monthly schedule business rules (rules 7/8). The grid itself lives in the UI,
    /// but every write and every validation rule passes through this class —
    /// never through the form: category anti-mixing (BR-09), ≤ 24 continuous hours,
    /// weekly bounds 36–42 (configurable via Sch.* settings), lock enforcement
    /// and audit logging.
    /// </summary>
    public static class ScheduleBLL
    {
        public static MonthData LoadMonth(int year, int month, int? departmentId, int? sectionId)
        {
            ValidatePeriod(year, month);

            MonthData data = new MonthData();
            data.Year = year;
            data.Month = month;

            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.Status = EmployeeBLL.StatusActive;
            filter.DepartmentId = departmentId;
            filter.SectionId = sectionId;
            filter.PageSize = 5000;
            data.Employees = EmployeeBLL.Search(filter);

            List<Shift> shiftList = ShiftBLL.GetAll(false);
            data.Shifts = new Dictionary<int, Shift>();
            foreach (Shift shift in shiftList)
            {
                data.Shifts[shift.ShiftId] = shift;
            }

            MonthlySchedule schedule = ScheduleDAL.GetSchedule(year, month);
            data.ScheduleId = schedule == null ? (int?)null : schedule.ScheduleId;
            data.Cells = new Dictionary<int, Dictionary<int, int>>();
            if (schedule != null)
            {
                foreach (ScheduleDetail detail in ScheduleDAL.GetDetails(schedule.ScheduleId))
                {
                    if (detail.EmployeeId == null || detail.DayNumber == null || detail.ShiftId == null)
                    {
                        continue;
                    }
                    if (!data.Cells.ContainsKey(detail.EmployeeId.Value))
                    {
                        data.Cells[detail.EmployeeId.Value] = new Dictionary<int, int>();
                    }
                    data.Cells[detail.EmployeeId.Value][detail.DayNumber.Value] = detail.ShiftId.Value;
                }
            }

            MonthlyShiftLock lockRow = ScheduleDAL.GetLock(year, month);
            data.IsLocked = lockRow != null && lockRow.IsLocked == true;
            return data;
        }

        /// <summary>Whether a month is currently locked (without loading the full grid).</summary>
        public static bool IsLocked(int year, int month)
        {
            MonthlyShiftLock lockRow = ScheduleDAL.GetLock(year, month);
            return lockRow != null && lockRow.IsLocked == true;
        }

        /// <summary>
        /// Saves the supplied cells for the employees that carry them. Locked months
        /// reject non-Admin saves (rule 7); Admin saves are audit-flagged. All rule-8
        /// validations run before any DB write.
        /// </summary>
        public static void SaveMonth(int year, int month, List<Employee> employees,
            Dictionary<int, Dictionary<int, int>> cells, Dictionary<int, Shift> shifts)
        {
            AppContext.RequirePermission("Schedule.Edit");
            ValidatePeriod(year, month);
            if (cells == null || cells.Count == 0)
            {
                throw new BusinessException("لا يوجد جدول لحفظه.");
            }
            bool locked = IsLocked(year, month);
            if (locked && !AppContext.IsCurrentUserAdmin)
            {
                throw new BusinessException("الشهر " + year + "-" + month + " مقفل. لا يمكن التعديل إلا بواسطة مسؤول النظام، وتُسجَّل العملية في سجل العمليات.");
            }
            ValidateCells(year, month, employees, cells, shifts);

            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            MonthlySchedule schedule = ScheduleDAL.GetSchedule(year, month);
            int scheduleId = schedule == null ? ScheduleDAL.InsertSchedule(year, month, userId) : schedule.ScheduleId;

            Dictionary<int, Employee> byId = new Dictionary<int, Employee>();
            foreach (Employee employee in employees)
            {
                byId[employee.EmployeeId] = employee;
            }
            int savedEmployees = 0;
            foreach (KeyValuePair<int, Dictionary<int, int>> pair in cells)
            {
                if (!byId.ContainsKey(pair.Key))
                {
                    continue;
                }
                ScheduleDAL.ReplaceEmployeeDetails(scheduleId, pair.Key, pair.Value);
                savedEmployees++;
            }

            string newSummary = "Year=" + year + ", Month=" + month + ", Employees=" + savedEmployees +
                                ", Rows=" + CountCells(cells) + (locked ? ", LockedEdit=Admin" : "");
            AuditBLL.Log(locked ? "Schedule.SaveLocked" : "Schedule.Save", "ScheduleDetails",
                year + "-" + month, null, newSummary);
        }

        /// <summary>Locks a month (permission "Schedule.Approve"); audited.</summary>
        public static void LockMonth(int year, int month)
        {
            AppContext.RequirePermission("Schedule.Approve");
            ValidatePeriod(year, month);
            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            ScheduleDAL.SetLock(year, month, true, userId, null);
            AuditBLL.Log("Schedule.Lock", "MonthlyShiftLocks", year + "-" + month, null, "IsLocked=True");
        }

        /// <summary>Unlocks a month (permission "Schedule.Unlock") with a mandatory reason; audited.</summary>
        public static void UnlockMonth(int year, int month, string reason)
        {
            AppContext.RequirePermission("Schedule.Unlock");
            ValidatePeriod(year, month);
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new BusinessException("سبب فتح الشهر المقفل إلزامي (يُسجَّل في سجل العمليات).");
            }
            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            ScheduleDAL.SetLock(year, month, false, userId, reason.Trim());
            AuditBLL.Log("Schedule.Unlock", "MonthlyShiftLocks", year + "-" + month, "IsLocked=True",
                "IsLocked=False, Reason=" + reason.Trim());
        }

        // ---------------- validation (rule 8) ----------------

        /// <summary>Runs every business rule on the in-memory grid; throws Arabic BusinessException.</summary>
        public static void ValidateCells(int year, int month, List<Employee> employees,
            Dictionary<int, Dictionary<int, int>> cells, Dictionary<int, Shift> shifts)
        {
            ValidatePeriod(year, month);
            int daysInMonth = DateTime.DaysInMonth(year, month);
            Dictionary<int, Employee> byId = new Dictionary<int, Employee>();
            foreach (Employee employee in employees)
            {
                byId[employee.EmployeeId] = employee;
            }

            bool disableMixing = SettingsBLL.GetBool("Sch.DisableMixingCategories", true);
            int minWeeklyHours = SettingsBLL.GetInt("Sch.MinWeeklyHours", 36);
            int maxWeeklyHours = SettingsBLL.GetInt("Sch.MaxWeeklyHours", 42);
            int maxContinuousHours = SettingsBLL.GetInt("Sch.MaxContinuousHours", 24);

            foreach (KeyValuePair<int, Dictionary<int, int>> employeePair in cells)
            {
                Employee employee;
                byId.TryGetValue(employeePair.Key, out employee);
                string who = employee != null ? " للموظف '" + employee.EmployeeCode + " - " + employee.FullNameAr + "'" : "";
                Dictionary<int, int> days = employeePair.Value;

                foreach (KeyValuePair<int, int> dayPair in days)
                {
                    if (dayPair.Key < 1 || dayPair.Key > daysInMonth)
                    {
                        throw new BusinessException("يوم غير صالح (" + dayPair.Key + ") في جدول الشهر" + who + ".");
                    }
                    if (!shifts.ContainsKey(dayPair.Value))
                    {
                        throw new BusinessException("وردية غير موجودة (رقم " + dayPair.Value + ") في يوم " + dayPair.Key + who + ". أعد تحميل الورديات.");
                    }
                }

                if (disableMixing)
                {
                    string firstCategory = null;
                    int? firstDay = null;
                    foreach (KeyValuePair<int, int> dayPair in days)
                    {
                        Shift shift = shifts[dayPair.Value];
                        if (IsRest(shift))
                        {
                            continue;
                        }
                        if (firstCategory == null)
                        {
                            firstCategory = shift.CategoryCode;
                            firstDay = dayPair.Key;
                            continue;
                        }
                        if (!string.Equals(shift.CategoryCode, firstCategory, StringComparison.Ordinal))
                        {
                            throw new BusinessException("يوم " + dayPair.Key + who + ": يمزج أصناف ورديات مختلفة ('" +
                                (shift.CategoryCode ?? "بدون صنف") + "') مع صنف اليوم " + firstDay +
                                " — منع خلط الأصناف مفعل (BR-09).");
                        }
                    }
                }

                for (int day = 1; day < daysInMonth; day++)
                {
                    int shiftAId;
                    int shiftBId;
                    if (!days.TryGetValue(day, out shiftAId) || !days.TryGetValue(day + 1, out shiftBId))
                    {
                        continue;
                    }
                    Shift shiftA = shifts[shiftAId];
                    Shift shiftB = shifts[shiftBId];
                    if (IsRest(shiftA) || IsRest(shiftB) || shiftA.DurationHrs == null || shiftB.DurationHrs == null)
                    {
                        continue;
                    }
                    int? startAMin = TimeHelper.ParseMinutes(shiftA.StartTime);
                    int? startBMin = TimeHelper.ParseMinutes(shiftB.StartTime);
                    if (startAMin == null || startBMin == null)
                    {
                        continue;
                    }
                    int durationAMin = Convert.ToInt32(Math.Round(shiftA.DurationHrs.Value * 60f));
                    int durationBMin = Convert.ToInt32(Math.Round(shiftB.DurationHrs.Value * 60f));
                    bool crosses = startAMin.Value + durationAMin > 1440;
                    int endTOD = (startAMin.Value + durationAMin) % 1440;
                    long endAbsolute = (day + (crosses ? 1 : 0)) * 1440L + endTOD;
                    long startAbsolute = (day + 1) * 1440L + startBMin.Value;
                    if (endAbsolute > startAbsolute)
                    {
                        throw new BusinessException("تعارض تعاقب يومَي " + day + "-" + (day + 1) + who +
                            ": وردية اليوم " + day + " لا تنتهي قبل بداية وردية اليوم " + (day + 1) + ".");
                    }
                    if (endAbsolute == startAbsolute && durationAMin + durationBMin > maxContinuousHours * 60)
                    {
                        throw new BusinessException("وردية " + day + "-" + (day + 1) + who + ": عمل متواصل يتجاوز " +
                            maxContinuousHours + " ساعة — ممنوع (قاعدة 8-2).");
                    }
                }

                for (int day = 1; day <= daysInMonth; day++)
                {
                    DateTime weekStart = new DateTime(year, month, day);
                    if (weekStart.DayOfWeek != DayOfWeek.Saturday || day + 6 > daysInMonth)
                    {
                        continue;
                    }
                    int scheduledDays = 0;
                    double weekHours = 0;
                    bool weekComplete = true;
                    for (int offset = 0; offset < 7; offset++)
                    {
                        int shiftId;
                        if (!days.TryGetValue(day + offset, out shiftId) || !shifts.ContainsKey(shiftId))
                        {
                            weekComplete = false;
                            break;
                        }
                        Shift shift = shifts[shiftId];
                        if (!IsRest(shift) && shift.DurationHrs != null)
                        {
                            scheduledDays++;
                            weekHours += shift.DurationHrs.Value;
                        }
                    }
                    if (!weekComplete || scheduledDays == 0)
                    {
                        continue;
                    }
                    if (weekHours < minWeeklyHours || weekHours > maxWeeklyHours)
                    {
                        throw new BusinessException("الأسبوع " + day + " إلى " + (day + 6) + who + ": إجمالي " +
                            weekHours.ToString("0.#") + " ساعة خارج الحدود الأسبوعية (" + minWeeklyHours + "–" +
                            maxWeeklyHours + ") — قاعدتا 8-4 و8-5.");
                    }
                }
            }
        }

        private static bool IsRest(Shift shift)
        {
            return string.Equals(shift.CategoryCode, "Rest", StringComparison.Ordinal) ||
                   string.Equals(shift.ShiftCode, "FR", StringComparison.OrdinalIgnoreCase);
        }

        private static int CountCells(Dictionary<int, Dictionary<int, int>> cells)
        {
            int total = 0;
            foreach (KeyValuePair<int, Dictionary<int, int>> pair in cells)
            {
                total += pair.Value.Count;
            }
            return total;
        }

        private static void ValidatePeriod(int year, int month)
        {
            if (year < 2000 || year > 2100)
            {
                throw new BusinessException("السنة يجب أن تكون بين 2000 و 2100.");
            }
            if (month < 1 || month > 12)
            {
                throw new BusinessException("الشهر يجب أن يكون بين 1 و 12.");
            }
        }
    }
}
