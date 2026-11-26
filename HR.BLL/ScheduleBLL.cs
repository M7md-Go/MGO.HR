using System;
using System.Collections.Generic;
using System.Globalization;
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
            SaveMonth(year, month, employees, cells, shifts, false);
        }

        /// <summary>
        /// Full save with the pre-approved continuous-overrun door (RosterCore
        /// lesson): when the grid contains a continuous-work stretch beyond
        /// Sch.MaxContinuousHours, the UI may re-save with allowContinuousOverrun
        /// only for holders of "Schedule.Unlock"; the bypass is audit-flagged.
        /// </summary>
        public static void SaveMonth(int year, int month, List<Employee> employees,
            Dictionary<int, Dictionary<int, int>> cells, Dictionary<int, Shift> shifts,
            bool allowContinuousOverrun)
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
            ValidateCells(year, month, employees, cells, shifts, allowContinuousOverrun);

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
                                ", Rows=" + CountCells(cells) + (locked ? ", LockedEdit=Admin" : "") +
                                (allowContinuousOverrun ? ", ContinuousOverrun=PreApproved" : "");
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
            ValidateCells(year, month, employees, cells, shifts, false);
        }

        /// <summary>Full validation with the optional pre-approved continuous-overrun bypass.</summary>
        public static void ValidateCells(int year, int month, List<Employee> employees,
            Dictionary<int, Dictionary<int, int>> cells, Dictionary<int, Shift> shifts,
            bool allowContinuousOverrun)
        {
            ValidatePeriod(year, month);
            int daysInMonth = DateTime.DaysInMonth(year, month);
            Dictionary<int, Employee> byId = new Dictionary<int, Employee>();
            foreach (Employee employee in employees)
            {
                byId[employee.EmployeeId] = employee;
            }

            string mixPolicy = SettingsBLL.GetString("Sch.MixPolicy", "Both");
            if (string.IsNullOrWhiteSpace(mixPolicy))
            {
                mixPolicy = "Both";
            }
            List<DayOfWeek> restDays = WeekRestDays.Parse(SettingsBLL.GetString(WeekRestDays.SettingKey, WeekRestDays.DefaultDays));
            DayOfWeek weekAnchor = WeekRestDays.WeekStartAfterRest(restDays);
            int minWeeklyHours = SettingsBLL.GetInt("Sch.MinWeeklyHours", 36);
            int maxWeeklyHours = SettingsBLL.GetInt("Sch.MaxWeeklyHours", 42);
            int maxContinuousHours = SettingsBLL.GetInt("Sch.MaxContinuousHours", 24);
            int maxConsecutiveDays = SettingsBLL.GetInt("Sch.MaxConsecutiveWorkDays", 6);
            int targetMonthlyHours = SettingsBLL.GetInt("Sch.TargetMonthlyHours", 0);

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

                if (!string.Equals(mixPolicy, "Off", StringComparison.OrdinalIgnoreCase))
                {
                    Shift firstShift = null;
                    int? firstDay = null;
                    foreach (KeyValuePair<int, int> dayPair in days)
                    {
                        Shift shift = shifts[dayPair.Value];
                        if (IsRest(shift))
                        {
                            continue;
                        }
                        if (firstShift == null)
                        {
                            firstShift = shift;
                            firstDay = dayPair.Key;
                            continue;
                        }
                        if (!MixAllowed(firstShift, shift, mixPolicy))
                        {
                            throw new BusinessException("يوم " + dayPair.Key + who + ": الوردية '" + DescribeShift(shift) +
                                "' لا يجوز خلطها مع الوردية '" + DescribeShift(firstShift) + "' المختارة في اليوم " + firstDay +
                                " — منع خلط ورديات العمل المختلفة المدة أو الصنف مفعل (BR-09). يمكن تغيير السياسة من الإعدادات: Sch.MixPolicy.");
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
                    if (endAbsolute > startAbsolute && !allowContinuousOverrun)
                    {
                        throw new BusinessException("تعارض تعاقب يومَي " + day + "-" + (day + 1) + who +
                            ": وردية اليوم " + day + " لا تنتهي قبل بداية وردية اليوم " + (day + 1) + ".");
                    }
                    if (endAbsolute == startAbsolute && durationAMin + durationBMin > maxContinuousHours * 60
                        && !allowContinuousOverrun)
                    {
                        throw new BusinessException("وردية " + day + "-" + (day + 1) + who + ": عمل متواصل يتجاوز " +
                            maxContinuousHours + " ساعة — ممنوع (قاعدة 8-2).");
                    }
                }

                for (int day = 1; day <= daysInMonth; day++)
                {
                    DateTime weekStart = new DateTime(year, month, day);
                    if (weekStart.DayOfWeek != weekAnchor || day + 6 > daysInMonth)
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

                // Rule (RosterCore): no more than Sch.MaxConsecutiveWorkDays
                // consecutive scheduled work days inside the month.
                if (maxConsecutiveDays > 0)
                {
                    int consecutive = 0;
                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        int dayShiftId;
                        Shift dayShift;
                        if (days.TryGetValue(day, out dayShiftId) && shifts.TryGetValue(dayShiftId, out dayShift)
                            && !IsRest(dayShift))
                        {
                            consecutive++;
                            if (consecutive > maxConsecutiveDays)
                            {
                                throw new BusinessException("موظف" + who + ": " + maxConsecutiveDays
                                    + " أيام عمل متتابعة تنتهي في يوم " + day
                                    + " — الحد المسموح لأقصى " + maxConsecutiveDays
                                    + " أيام عمل متتابعة (Sch.MaxConsecutiveWorkDays).");
                            }
                        }
                        else
                        {
                            consecutive = 0;
                        }
                    }
                }

                // Rule (RosterCore): monthly target hours (0 = disabled via Sch.TargetMonthlyHours).
                if (targetMonthlyHours > 0)
                {
                    double monthHours = 0;
                    foreach (KeyValuePair<int, int> monthPair in days)
                    {
                        Shift monthShift;
                        if (shifts.TryGetValue(monthPair.Value, out monthShift) && !IsRest(monthShift)
                            && monthShift.DurationHrs != null)
                        {
                            monthHours += monthShift.DurationHrs.Value;
                        }
                    }
                    if (monthHours < targetMonthlyHours)
                    {
                        throw new BusinessException("ساعات الشهر" + who + ": " + monthHours.ToString("0.#")
                            + " ساعة أقل من الهدف الشهري (" + targetMonthlyHours
                            + " ساعة) — لا يمكن الحفظ (قاعدة الهدف الشهري).");
                    }
                }
            }
        }

        private static bool IsRest(Shift shift)
        {
            return string.Equals(shift.CategoryCode, "Rest", StringComparison.Ordinal) ||
                   string.Equals(shift.ShiftCode, "FR", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>BR-09: decides whether two work shifts may coexist in one employee's
        /// month under the configured policy (Sch.MixPolicy = Off / Category / Duration / Both).</summary>
        private static bool MixAllowed(Shift first, Shift other, string policy)
        {
            bool sameCategory = string.Equals(first.CategoryCode, other.CategoryCode, StringComparison.Ordinal);
            bool sameDuration = string.Equals(DurationBucket(first), DurationBucket(other), StringComparison.Ordinal);
            if (string.Equals(policy, "Category", StringComparison.OrdinalIgnoreCase))
            {
                return sameCategory;
            }
            if (string.Equals(policy, "Duration", StringComparison.OrdinalIgnoreCase))
            {
                return sameDuration;
            }
            return sameCategory && sameDuration;
        }

        /// <summary>Effective shift duration bucket rounded to the nearest half hour —
        /// makes the 7-hour shift never interchangeable with 6/9/12/24-hour shifts.</summary>
        private static string DurationBucket(Shift shift)
        {
            if (shift.DurationHrs == null)
            {
                return "?";
            }
            double bucket = Math.Round(shift.DurationHrs.Value * 2.0, MidpointRounding.AwayFromZero) / 2.0;
            return bucket.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>Arabic shift description (code, name, category, duration) used in BR-09 messages.</summary>
        private static string DescribeShift(Shift shift)
        {
            return (shift.ShiftCode ?? "?") + " " + (shift.ShiftName ?? "?") +
                   " — صنف '" + (shift.CategoryCode ?? "بدون صنف") + "' — مدة " + DurationBucket(shift) + " ساعة";
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

        // ---------------- approval chain (RosterCore lesson) ----------------

        /// <summary>Submits the month's schedule for approval ("Schedule.Edit").
        /// Runs the FULL validation grid first — invalid schedules cannot be submitted.</summary>
        public static void SubmitForApproval(int year, int month)
        {
            AppContext.RequirePermission("Schedule.Edit");
            ValidatePeriod(year, month);
            MonthlySchedule schedule = ScheduleDAL.GetSchedule(year, month);
            if (schedule == null)
            {
                throw new BusinessException("لا يوجد جدول لهذا الشهر — احفظه أولاً ثم أرسله للاعتماد.");
            }
            string status = schedule.Status == null ? "Draft" : schedule.Status;
            if (status == "Submitted")
            {
                throw new BusinessException("الجدول مُرسل بالفعل للاعتماد — بانتظار قرار المُعتمِد.");
            }
            if (status == "Approved")
            {
                throw new BusinessException("الجدول معتمد بالفعل — استخدم طلب تعديل الورديات بعد الاعتماد.");
            }
            MonthData monthData = LoadMonth(year, month, null, null);
            if (CountCells(monthData.Cells) == 0)
            {
                throw new BusinessException("الجدول فارغ — لا يمكن إرسال جدول بلا ورديات للاعتماد.");
            }
            ValidateCells(year, month, monthData.Employees, monthData.Cells, monthData.Shifts, false);
            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            ScheduleDAL.SetScheduleSubmitted(schedule.ScheduleId, userId);
            ScheduleDAL.InsertApproval(schedule.ScheduleId, "Submitted", userId, null);
            AuditBLL.Log("Schedule.Submit", "MonthlySchedules", year + "-" + month, status, "Submitted");
        }

        /// <summary>Final approval ("Schedule.Approve"); locks the month; audited.</summary>
        public static void ApproveMonth(int year, int month)
        {
            AppContext.RequirePermission("Schedule.Approve");
            ValidatePeriod(year, month);
            MonthlySchedule schedule = ScheduleDAL.GetSchedule(year, month);
            if (schedule == null)
            {
                throw new BusinessException("لا يوجد جدول لهذا الشهر — لا يمكن اعتماد جدول غير محفوظ.");
            }
            string status = schedule.Status == null ? "Draft" : schedule.Status;
            if (status != "Submitted")
            {
                throw new BusinessException("لا يمكن الاعتماد إلا لجدول مُرسل للاعتماد (الحالة الحالية: " + StatusAr(status) + ").");
            }
            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            ScheduleDAL.SetScheduleApproved(schedule.ScheduleId, userId);
            ScheduleDAL.InsertApproval(schedule.ScheduleId, "Approved", userId, null);
            ScheduleDAL.SetLock(year, month, true, userId, null);
            AuditBLL.Log("Schedule.Approve", "MonthlySchedules", year + "-" + month, status, "Approved+Locked");
        }

        /// <summary>Rejection ("Schedule.Approve") returns the schedule to Draft with a
        /// MANDATORY reason, unlocks the month and is fully audited.</summary>
        public static void RejectMonth(int year, int month, string reason)
        {
            AppContext.RequirePermission("Schedule.Approve");
            ValidatePeriod(year, month);
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new BusinessException("سبب الرفض إلزامي (يُعرض على مُعدّ الجدول ويُسجَّل في سجل العمليات).");
            }
            MonthlySchedule schedule = ScheduleDAL.GetSchedule(year, month);
            if (schedule == null)
            {
                throw new BusinessException("لا يوجد جدول لهذا الشهر.");
            }
            string status = schedule.Status == null ? "Draft" : schedule.Status;
            if (status != "Submitted")
            {
                throw new BusinessException("لا يمكن الرفض إلا لجدول مُرسل للاعتماد (الحالة الحالية: " + StatusAr(status) + ").");
            }
            reason = reason.Trim();
            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            ScheduleDAL.SetScheduleRejected(schedule.ScheduleId, userId, reason);
            ScheduleDAL.InsertApproval(schedule.ScheduleId, "Rejected", userId, reason);
            ScheduleDAL.SetLock(year, month, false, userId, reason);
            AuditBLL.Log("Schedule.Reject", "MonthlySchedules", year + "-" + month, status, "Rejected, Reason=" + reason);
        }

        /// <summary>Reads the approval state of a month for UI display (no permission gate).</summary>
        public static ScheduleStatusView ScheduleStatus(int year, int month)
        {
            ValidatePeriod(year, month);
            ScheduleStatusView view = new ScheduleStatusView();
            MonthlySchedule schedule = ScheduleDAL.GetSchedule(year, month);
            MonthlyShiftLock lockRow = ScheduleDAL.GetLock(year, month);
            view.IsLocked = lockRow != null && lockRow.IsLocked.GetValueOrDefault();
            view.Status = schedule == null ? "Draft" : (schedule.Status == null ? "Draft" : schedule.Status);
            if (view.Status == "Submitted") { view.StatusAr = "مُرسل للاعتماد"; }
            else if (view.Status == "Approved") { view.StatusAr = "معتمد"; }
            else if (view.Status == "Rejected") { view.StatusAr = "مرفوض — بانتظار التعديل"; }
            else { view.StatusAr = "مسودة"; }
            view.SubmittedAt = schedule == null ? null : schedule.SubmittedAt;
            view.SubmittedByName = UserName(schedule == null ? null : schedule.SubmittedBy);
            view.ApprovedAt = schedule == null ? null : schedule.ApprovedAt;
            view.ApprovedByName = UserName(schedule == null ? null : schedule.ApprovedBy);
            view.RejectReason = schedule == null ? null : schedule.RejectReason;
            return view;
        }

        /// <summary>Resolves a user id to a display name (falls back to the raw id text).</summary>
        private static string UserName(int? userId)
        {
            if (userId == null) { return null; }
            List<UserView> users = UserBLL.ListUsers();
            foreach (UserView user in users)
            {
                if (user.UserId == userId.Value)
                {
                    return string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
                }
            }
            return "مستخدم #" + userId.Value;
        }

        /// <summary>Arabic label for a raw schedule status value.</summary>
        private static string StatusAr(string status)
        {
            if (status == "Submitted") { return "مُرسل للاعتماد"; }
            if (status == "Approved") { return "معتمد"; }
            if (status == "Rejected") { return "مرفوض"; }
            return "مسودة";
        }

        // ---------------- rotation templates (RosterCore lesson) ----------------

        /// <summary>Names of the rotation templates stored in Sch.RotationTemplates
        /// (format: name=CODE:count,CODE:count;name2=... ; DEF=default fill shift, REST=rest).</summary>
        public static List<string> RotationTemplateNames()
        {
            List<string> names = new List<string>();
            string raw = SettingsBLL.GetString("Sch.RotationTemplates", "");
            string[] templates = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string template in templates)
            {
                string trimmed = template.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    names.Add(trimmed.Substring(0, eq).Trim());
                }
            }
            return names;
        }

        /// <summary>Parses one rotation template into its (shiftCode, dayCount) pattern.</summary>
        public static List<KeyValuePair<string, int>> RotationTemplateSpec(string templateName)
        {
            string raw = SettingsBLL.GetString("Sch.RotationTemplates", "");
            string[] templates = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string template in templates)
            {
                string trimmed = template.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                if (!string.Equals(trimmed.Substring(0, eq).Trim(), templateName, StringComparison.Ordinal))
                {
                    continue;
                }
                List<KeyValuePair<string, int>> pattern = new List<KeyValuePair<string, int>>();
                string[] parts = trimmed.Substring(eq + 1).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    string item = part.Trim();
                    int colon = item.IndexOf(':');
                    if (colon <= 0)
                    {
                        continue;
                    }
                    string code = item.Substring(0, colon).Trim().ToUpperInvariant();
                    int count;
                    if (!int.TryParse(item.Substring(colon + 1).Trim(), out count) || count <= 0)
                    {
                        throw new BusinessException("قالب دوري غير صالح: العدد بعد النقطتين يجب أن يكون رقماً أكبر من صفر (" + item + ").");
                    }
                    pattern.Add(new KeyValuePair<string, int>(code, count));
                }
                if (pattern.Count == 0)
                {
                    throw new BusinessException("قالب دوري فارغ أو غير صالح: " + templateName);
                }
                return pattern;
            }
            throw new BusinessException("لا يوجد قالب دوري بهذا الاسم: " + templateName);
        }

        /// <summary>Builds a one-employee full-month cell map by cycling a rotation
        /// pattern from day 1. Work slots fall back to the rest shift on public
        /// holidays; DEF = default fill shift, REST = rest shift, other codes
        /// resolve through the passed shift dictionary by ShiftCode.</summary>
        public static Dictionary<int, Dictionary<int, int>> ApplyRotation(int year, int month,
            List<KeyValuePair<string, int>> pattern, Dictionary<int, Shift> shifts,
            int? defaultShiftId, int? restShiftId, HashSet<int> holidayDays)
        {
            if (pattern == null || pattern.Count == 0)
            {
                throw new BusinessException("نمط الدوران فارغ.");
            }
            if (defaultShiftId == null)
            {
                throw new BusinessException("لا يمكن تطبيق النمط: لا توجد وردية ملء افتراضية — حدد وردية العمل أولاً.");
            }
            if (restShiftId == null)
            {
                throw new BusinessException("لا يمكن تطبيق النمط: لا توجد وردية راحة (كود FR) — أضفها من شاشة الورديات.");
            }
            List<int> patternIds = new List<int>();
            foreach (KeyValuePair<string, int> pair in pattern)
            {
                if (pair.Key == "DEF")
                {
                    patternIds.Add(defaultShiftId.Value);
                    continue;
                }
                if (pair.Key == "REST")
                {
                    patternIds.Add(restShiftId.Value);
                    continue;
                }
                bool found = false;
                foreach (Shift shift in shifts.Values)
                {
                    if (string.Equals(shift.ShiftCode, pair.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        patternIds.Add(shift.ShiftId);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    throw new BusinessException("القالب يشير إلى كود وردية غير موجود: " + pair.Key);
                }
            }
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int cursor = 0;
            Dictionary<int, Dictionary<int, int>> cells = new Dictionary<int, Dictionary<int, int>>();
            Dictionary<int, int> row = new Dictionary<int, int>();
            for (int day = 1; day <= daysInMonth; day++)
            {
                int shiftId = patternIds[cursor % patternIds.Count];
                cursor++;
                if (holidayDays != null && holidayDays.Contains(day))
                {
                    Shift chosen;
                    if (shifts.TryGetValue(shiftId, out chosen) && !IsRest(chosen))
                    {
                        shiftId = restShiftId.Value;
                    }
                }
                row[day] = shiftId;
            }
            cells[1] = row;
            return cells;
        }

        /// <summary>Pre-scan for the UI: lists adjacent-day conflicts (overlap or
        /// continuous work beyond Sch.MaxContinuousHours) WITHOUT throwing, so the
        /// caller can ask for a pre-approval confirmation before saving (RosterCore lesson).</summary>
        public static List<string> ContinuousOverruns(int year, int month, List<Employee> employees,
            Dictionary<int, Dictionary<int, int>> cells, Dictionary<int, Shift> shifts)
        {
            List<string> overruns = new List<string>();
            if (cells == null || cells.Count == 0 || shifts == null || shifts.Count == 0)
            {
                return overruns;
            }
            int maxContinuousHours = SettingsBLL.GetInt("Sch.MaxContinuousHours", 24);
            int daysInMonth = DateTime.DaysInMonth(year, month);
            foreach (Employee employee in employees)
            {
                if (!cells.ContainsKey(employee.EmployeeId))
                {
                    continue;
                }
                Dictionary<int, int> days = cells[employee.EmployeeId];
                string who = " للموظف '" + (employee.EmployeeCode ?? employee.EmployeeId.ToString()) + "'";
                for (int day = 1; day < daysInMonth; day++)
                {
                    int shiftAId;
                    int shiftBId;
                    if (!days.TryGetValue(day, out shiftAId) || !days.TryGetValue(day + 1, out shiftBId))
                    {
                        continue;
                    }
                    Shift shiftA;
                    Shift shiftB;
                    if (!shifts.TryGetValue(shiftAId, out shiftA) || !shifts.TryGetValue(shiftBId, out shiftB))
                    {
                        continue;
                    }
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
                        overruns.Add("تعارض تعاقب يومَي " + day + "-" + (day + 1) + who);
                    }
                    else if (endAbsolute == startAbsolute && durationAMin + durationBMin > maxContinuousHours * 60)
                    {
                        overruns.Add("عمل متواصل يتجاوز " + maxContinuousHours + " ساعة في يومَي " + day + "-" + (day + 1) + who);
                    }
                }
            }
            return overruns;
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
