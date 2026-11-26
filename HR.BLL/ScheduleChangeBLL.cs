using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Post-approval shift-change requests (RosterCore lesson): after a month is
    /// approved and locked, editors may request a BALANCED group of day/shift
    /// changes (sum of new hours == sum of old hours across the whole group).
    /// Path: create ("Schedule.Edit") → decide ("Schedule.Approve") →
    /// execute ("Schedule.Unlock"). Every step is audited.
    /// </summary>
    public static class ScheduleChangeBLL
    {
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusExecuted = "Executed";
        public const string StatusRejected = "Rejected";

        public static List<ScheduleChangeRequest> List(string status)
        {
            return ScheduleDAL.GetChangeRequests(status);
        }

        /// <summary>Items of one request (UI-facing so forms never touch the DAL).</summary>
        public static List<ScheduleChangeItem> Items(int requestId)
        {
            return ScheduleDAL.GetChangeItems(requestId);
        }

        /// <summary>Creates a pending change request with its items. The month must
        /// be locked or approved; every item's day/shift is validated against the
        /// live month; the hour-balance rule is enforced across the whole group.</summary>
        public static int Create(int year, int month, string reason, List<ScheduleChangeItem> items)
        {
            AppContext.RequirePermission("Schedule.Edit");
            if (year < 2000 || year > 2100 || month < 1 || month > 12)
            {
                throw new BusinessException("السنة أو الشهر غير صالح.");
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new BusinessException("سبب الطلب إلزامي.");
            }
            if (items == null || items.Count == 0)
            {
                throw new BusinessException("لا يمكن إنشاء طلب بدون بنود — أضف موظفاً ويوماً ووردية جديدة على الأقل.");
            }
            bool locked = ScheduleBLL.IsLocked(year, month);
            MonthlySchedule sched = ScheduleDAL.GetSchedule(year, month);
            bool approved = sched != null && string.Equals(sched.Status, StatusApproved, StringComparison.Ordinal);
            if (!locked && !approved)
            {
                throw new BusinessException("طلبات تعديل الورديات تكون بعد اعتماد الجدول وقفله فقط — أرسل الجدول للاعتماد واعتمده أولاً.");
            }
            MonthData md = ScheduleBLL.LoadMonth(year, month, null, null);
            if (md.ScheduleId == null)
            {
                throw new BusinessException("لا يوجد جدول محفوظ لهذا الشهر.");
            }
            int daysInMonth = DateTime.DaysInMonth(year, month);

            HashSet<int> employeeIds = new HashSet<int>();
            foreach (Employee employee in md.Employees)
            {
                employeeIds.Add(employee.EmployeeId);
            }
            HashSet<string> seenPairs = new HashSet<string>();
            double oldHours = 0;
            double newHours = 0;
            foreach (ScheduleChangeItem item in items)
            {
                if (item.EmployeeId <= 0 || !employeeIds.Contains(item.EmployeeId))
                {
                    throw new BusinessException("بند يشير إلى موظف غير موجود في جدول هذا الشهر.");
                }
                if (item.DayNumber < 1 || item.DayNumber > daysInMonth)
                {
                    throw new BusinessException("اليوم يجب أن يكون بين 1 و " + daysInMonth + ".");
                }
                string pairKey = item.EmployeeId + "-" + item.DayNumber;
                if (seenPairs.Contains(pairKey))
                {
                    throw new BusinessException("بند مكرر لنفس الموظف ونفس اليوم (" + pairKey + ") — اجمعها في بند واحد.");
                }
                seenPairs.Add(pairKey);
                if (item.NewShiftId == null || !md.Shifts.ContainsKey(item.NewShiftId.Value))
                {
                    throw new BusinessException("الوردية الجديدة غير موجودة — أعد تحميل الشهر.");
                }
                Shift newShift = md.Shifts[item.NewShiftId.Value];
                newHours += ShiftHours(newShift);
                int oldShiftId;
                Shift oldShift = null;
                if (md.Cells.ContainsKey(item.EmployeeId) && md.Cells[item.EmployeeId].TryGetValue(item.DayNumber, out oldShiftId))
                {
                    md.Shifts.TryGetValue(oldShiftId, out oldShift);
                }
                item.OldShiftId = oldShift == null ? (int?)null : oldShift.ShiftId;
                if (oldShift != null)
                {
                    oldHours += ShiftHours(oldShift);
                }
            }
            if (Math.Abs(newHours - oldHours) > 0.01)
            {
                throw new BusinessException("مجموع ساعات البنود الجديدة يجب أن يساوي مجموع القديم عبر المجموعة (شرط التوازن): القديم " +
                    oldHours.ToString("0.#") + " ساعة مقابل الجديد " + newHours.ToString("0.#") +
                    " ساعة — عدّل البنود أو عوّض النقص ببند موازٍ.");
            }
            reason = reason.Trim();
            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            int requestId = ScheduleDAL.InsertChangeRequest(year, month, reason, userId);
            foreach (ScheduleChangeItem item in items)
            {
                ScheduleDAL.InsertChangeItem(requestId, item.EmployeeId, item.DayNumber,
                    item.OldShiftId, item.NewShiftId);
            }
            AuditBLL.Log("ScheduleChange.Create", "ScheduleChangeRequests", requestId.ToString(), null,
                "Year=" + year + ", Month=" + month + ", Items=" + items.Count +
                ", OldHours=" + oldHours.ToString("0.#") + ", NewHours=" + newHours.ToString("0.#"));
            return requestId;
        }

        /// <summary>Decides a pending request ("Schedule.Approve"); rejection needs a mandatory reason.</summary>
        public static void Decide(int requestId, bool approve, string reason)
        {
            AppContext.RequirePermission("Schedule.Approve");
            ScheduleChangeRequest request = ScheduleDAL.GetChangeRequest(requestId);
            if (request == null)
            {
                throw new BusinessException("الطلب غير موجود.");
            }
            if (!string.Equals(request.Status, StatusPending, StringComparison.Ordinal))
            {
                throw new BusinessException("لا يمكن البت إلا في طلب معلق (الحالة الحالية: " + StatusAr(request.Status) + ").");
            }
            if (!approve && string.IsNullOrWhiteSpace(reason))
            {
                throw new BusinessException("سبب الرفض إلزامي.");
            }
            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            ScheduleDAL.SetChangeRequestDecided(requestId, approve, userId, approve ? null : (reason == null ? null : reason.Trim()));
            AuditBLL.Log("ScheduleChange.Decide", "ScheduleChangeRequests", requestId.ToString(),
                StatusPending, (approve ? StatusApproved : StatusRejected) +
                (approve ? "" : ", Reason=" + reason.Trim()));
        }

        /// <summary>Executes an approved request ("Schedule.Unlock"): applies every item
        /// to the live schedule grid via ReplaceEmployeeDetails, then marks it executed.</summary>
        public static void Execute(int requestId)
        {
            AppContext.RequirePermission("Schedule.Unlock");
            ScheduleChangeRequest request = ScheduleDAL.GetChangeRequest(requestId);
            if (request == null)
            {
                throw new BusinessException("الطلب غير موجود.");
            }
            if (!string.Equals(request.Status, StatusApproved, StringComparison.Ordinal))
            {
                throw new BusinessException("لا يمكن التنفيذ إلا لطلب معتمد (الحالة الحالية: " + StatusAr(request.Status) + ").");
            }
            MonthData md = ScheduleBLL.LoadMonth(request.YearVal, request.MonthVal, null, null);
            if (md.ScheduleId == null)
            {
                throw new BusinessException("الجدول لم يُحفظ لهذا الشهر — لا يمكن التنفيذ.");
            }
            List<ScheduleChangeItem> items = ScheduleDAL.GetChangeItems(requestId);
            Dictionary<int, Dictionary<int, int>> updates = new Dictionary<int, Dictionary<int, int>>();
            foreach (ScheduleChangeItem item in items)
            {
                Dictionary<int, int> row;
                if (!updates.TryGetValue(item.EmployeeId, out row))
                {
                    row = new Dictionary<int, int>();
                    Dictionary<int, int> existing;
                    if (md.Cells.TryGetValue(item.EmployeeId, out existing))
                    {
                        foreach (KeyValuePair<int, int> pair in existing)
                        {
                            row[pair.Key] = pair.Value;
                        }
                    }
                    updates[item.EmployeeId] = row;
                }
                if (item.NewShiftId == null)
                {
                    row.Remove(item.DayNumber);
                }
                else
                {
                    row[item.DayNumber] = item.NewShiftId.Value;
                }
            }
            foreach (KeyValuePair<int, Dictionary<int, int>> pair in updates)
            {
                ScheduleDAL.ReplaceEmployeeDetails(md.ScheduleId.Value, pair.Key, pair.Value);
            }
            int? userId = AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId;
            ScheduleDAL.SetChangeRequestExecuted(requestId, userId);
            AuditBLL.Log("ScheduleChange.Execute", "ScheduleChangeRequests", requestId.ToString(),
                StatusApproved, StatusExecuted + ", Items=" + items.Count +
                ", Year=" + request.YearVal + ", Month=" + request.MonthVal);
        }

        /// <summary>Arabic label for a change-request status.</summary>
        public static string StatusAr(string status)
        {
            if (string.Equals(status, StatusPending, StringComparison.Ordinal)) { return "معلقة"; }
            if (string.Equals(status, StatusApproved, StringComparison.Ordinal)) { return "معتمدة"; }
            if (string.Equals(status, StatusExecuted, StringComparison.Ordinal)) { return "منفذة"; }
            if (string.Equals(status, StatusRejected, StringComparison.Ordinal)) { return "مرفوضة"; }
            return status == null ? "" : status;
        }

        private static double ShiftHours(Shift shift)
        {
            float hours = shift.DurationHrs ?? shift.NormalHours ?? 8f;
            return hours <= 0f ? 0.0 : (double)hours;
        }
    }
}
