using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Leave management (rule 15): requests with full-day validation, quotas
    /// editable per type and per employee/year, and balances that are always
    /// recomputed from approved requests (no drift). Creating a request needs
    /// any authenticated user; approving or rejecting needs "Leaves.Approve";
    /// every state change is audit-logged. Approved/period data feeds the
    /// attendance engine automatically through AttendanceDAL.LoadApprovedLeaves.
    /// </summary>
    public static class LeaveBLL
    {
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusRejected = "Rejected";

        public static List<LeaveType> Types()
        {
            return LeaveDAL.GetTypes();
        }

        public static List<LeaveRequestView> Search(string status, int? employeeId,
            DateTime? fromDate, DateTime? toDate)
        {
            return LeaveDAL.SearchRequests(RequireSearchStatus(status), employeeId, fromDate, toDate, 1000);
        }

        private static string RequireSearchStatus(string status)
        {
            if (status == StatusPending || status == StatusApproved ||
                status == StatusRejected || string.IsNullOrEmpty(status))
            {
                return string.IsNullOrEmpty(status) ? null : status;
            }
            throw new BusinessException("حالة الإجازة غير معروفة: " + status);
        }

        public static LeaveRequestView GetById(int leaveId)
        {
            return LeaveDAL.GetRequestView(leaveId);
        }

        public static int Create(LeaveRequest request)
        {
            AppContext.RequireAnyPermission(new string[] { "Leave.Create", "Leaves.Approve" });
            ValidateContent(request);
            int leaveTypeId = request.LeaveTypeId ?? 0;
            LeaveType type = LeaveDAL.GetTypeById(leaveTypeId);
            if (type == null)
            {
                throw new BusinessException("نوع الإجازة غير موجود.");
            }
            int userId = CurrentUserId();
            request.DaysCount = SumDeduction(request.EmployeeId ?? 0,
                request.StartDate ?? DateTime.MinValue, request.EndDate ?? DateTime.MinValue);
            if (LeaveDAL.HasOverlappingRequests(request.EmployeeId ?? 0,
                request.StartDate ?? DateTime.MinValue, request.EndDate ?? DateTime.MinValue, null))
            {
                throw new BusinessException("يوجد طلب إجازة متداخل مع نفس الفترة لهذا الموظف (معلّق أو معتمد).");
            }
            CheckBalance(request, type, null);
            int leaveId = LeaveDAL.InsertRequest(request);
            LeaveDAL.UpsertBalance(MaterializeBalance(request.EmployeeId ?? 0,
                (request.StartDate ?? DateTime.Now).Year, leaveTypeId));
            AuditBLL.Log("Leave.Create", "LeaveRequests", leaveId.ToString(), null,
                "Employee=" + request.EmployeeId + ", Type=" + type.LeaveTypeCode +
                ", From=" + request.StartDate.Value.ToString("yyyy-MM-dd") +
                ", To=" + request.EndDate.Value.ToString("yyyy-MM-dd") +
                ", Days=" + request.DaysCount);
            return leaveId;
        }

        public static void Update(int leaveId, LeaveRequest request)
        {
            AppContext.RequireAnyPermission(new string[] { "Leaves.Edit", "Leaves.Approve" });
            ValidateContent(request);
            LeaveRequestView stored = LeaveDAL.GetRequestView(leaveId);
            if (stored == null)
            {
                throw new BusinessException("طلب الإجازة غير موجود.");
            }
            if (stored.Status == StatusApproved)
            {
                throw new BusinessException("لا يمكن تعديل طلب معتمد — ألغه عبر المسؤول الأعلى.");
            }

            if (stored.Status == StatusRejected)
            {
                throw new BusinessException("لا يمكن تعديل طلب مرفوض — يمكن تحديث سبب الرفض فقط من شاشة الإجازات.");
            }
            LeaveType type = LeaveDAL.GetTypeById(request.LeaveTypeId ?? 0);
            if (type == null)
            {
                throw new BusinessException("نوع الإجازة غير موجود.");
            }
            request.DaysCount = SumDeduction(request.EmployeeId ?? 0,
                request.StartDate ?? DateTime.MinValue, request.EndDate ?? DateTime.MinValue);
            if (LeaveDAL.HasOverlappingRequests(stored.EmployeeId ?? 0,
                request.StartDate ?? DateTime.MinValue, request.EndDate ?? DateTime.MinValue, leaveId))
            {
                throw new BusinessException("يوجد طلب إجازة متداخل مع نفس الفترة لهذا الموظف.");
            }
            CheckBalanceForUpdate(stored, request, type);
            LeaveDAL.UpdateRequest(leaveId, request.LeaveTypeId ?? 0,
                request.StartDate ?? DateTime.MinValue, request.EndDate ?? DateTime.MinValue,
                request.DaysCount ?? 0, request.Reason);
            LeaveDAL.UpsertBalance(MaterializeBalance(stored.EmployeeId ?? 0,
                (request.StartDate ?? DateTime.Now).Year, request.LeaveTypeId ?? 0));
            AuditBLL.Log("Leave.Update", "LeaveRequests", leaveId.ToString(),
                stored.DaysCount + " يوم (" + stored.StartDate.Value.ToString("yyyy-MM-dd") + ")",
                request.DaysCount + " يوم (" + request.StartDate.Value.ToString("yyyy-MM-dd") + ")");
        }

        public static void Approve(int leaveId)
        {
            AppContext.RequirePermission("Leaves.Approve");
            LeaveRequestView stored = LeaveDAL.GetRequestView(leaveId);
            if (stored == null)
            {
                throw new BusinessException("طلب الإجازة غير موجود.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("الاعتماد متاح للطلبات المعلّقة فقط — حالة الطلب الحالية: " + stored.Status + ".");
            }
            LeaveType type = LeaveDAL.GetTypeById(stored.LeaveTypeId ?? 0);
            LeaveRequest request = new LeaveRequest();
            request.EmployeeId = stored.EmployeeId;
            request.LeaveTypeId = stored.LeaveTypeId;
            request.StartDate = stored.StartDate;
            request.EndDate = stored.EndDate;
            CheckBalance(request, type ?? new LeaveType(), leaveId);
            LeaveDAL.SetRequestStatus(leaveId, StatusApproved, CurrentUserId());
            LeaveDAL.UpsertBalance(MaterializeBalance(stored.EmployeeId ?? 0,
                (stored.StartDate ?? DateTime.Now).Year, stored.LeaveTypeId ?? 0));
            AuditBLL.Log("Leave.Approve", "LeaveRequests", leaveId.ToString(), StatusPending, StatusApproved);
        }

        public static void Reject(int leaveId, string rejectReason)
        {
            AppContext.RequirePermission("Leaves.Approve");
            LeaveRequestView stored = LeaveDAL.GetRequestView(leaveId);
            if (stored == null)
            {
                throw new BusinessException("طلب الإجازة غير موجود.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("الرفض متاح للطلبات المعلّقة فقط — حالة الطلب الحالية: " + stored.Status + ".");
            }
            LeaveDAL.SetRequestStatus(leaveId, StatusRejected, CurrentUserId(), rejectReason);
            AuditBLL.Log("Leave.Reject", "LeaveRequests", leaveId.ToString(), StatusPending,
                StatusRejected + (string.IsNullOrEmpty(rejectReason) ? string.Empty : " — السبب: " + rejectReason));
        }

        public static void SetRejectReason(int leaveId, string rejectReason)
        {
            AppContext.RequirePermission("Leaves.Approve");
            LeaveRequestView stored = LeaveDAL.GetRequestView(leaveId);
            if (stored == null)
            {
                throw new BusinessException("طلب الإجازة غير موجود.");
            }
            if (stored.Status != StatusRejected)
            {
                throw new BusinessException("تحديث سبب الرفض متاح للطلبات المرفوضة فقط.");
            }
            LeaveDAL.SetRejectReason(leaveId, rejectReason);
            AuditBLL.Log("Leave.RejectReason", "LeaveRequests", leaveId.ToString(),
                stored.RejectReason, rejectReason);
        }

        public static LeaveBalance GetOrInitBalance(int employeeId, int year, int leaveTypeId)
        {
            return MaterializeBalance(employeeId, year, leaveTypeId);
        }

        public static List<LeaveBalance> Balances(int employeeId, int year)
        {
            List<LeaveBalance> balances = new List<LeaveBalance>();
            foreach (LeaveType type in LeaveDAL.GetTypes())
            {
                balances.Add(MaterializeBalance(employeeId, year, type.LeaveTypeId));
            }
            return balances;
        }

        /// <summary>Setting key listing the quota-managed leave type codes.</summary>
        public const string BalanceTypesKey = "Leave.BalanceTypes";
        public const string CarryOverKey = "Leave.AllowCarryOver";

        /// <summary>
        /// Fiscal-year start (MM-dd). The leave-balance ledger is a FISCAL
        /// year, not a calendar one: the balance year label is the year the
        /// fiscal year STARTS in (default "07-01" → fiscal 2025 = 2025-07-01
        /// → 2026-06-30), per the owner's definition 1/7 → 30/6.
        /// </summary>
        public const string FiscalYearKey = "Leave.FiscalYearStart";

        private static int[] ParseFiscalStart()
        {
            string configured = SettingsBLL.GetString(FiscalYearKey, "07-01");
            string[] parts = (configured ?? "").Trim().Split('-');
            int month = 7;
            int day = 1;
            if (parts.Length == 2)
            {
                int parsedMonth;
                int parsedDay;
                if (int.TryParse(parts[0], out parsedMonth) && parsedMonth >= 1 && parsedMonth <= 12)
                {
                    month = parsedMonth;
                }
                if (int.TryParse(parts[1], out parsedDay) && parsedDay >= 1 && parsedDay <= 28)
                {
                    day = parsedDay;
                }
            }
            return new int[] { month, day };
        }

        /// <summary>First DAY of the fiscal year whose balance label is <paramref name="balanceYear"/>.</summary>
        public static DateTime FiscalStart(int balanceYear)
        {
            int[] monthDay = ParseFiscalStart();
            return new DateTime(balanceYear, monthDay[0], monthDay[1]);
        }

        /// <summary>
        /// Balance label of the fiscal year a given date belongs to
        /// (dates before 1/7 belong to the fiscal year that started the previous year).
        /// </summary>
        public static int BalanceYearFor(DateTime date)
        {
            return date.Date >= FiscalStart(date.Year).Date ? date.Year : date.Year - 1;
        }

        /// <summary>Codes of types managed through a yearly quota (default: Annual, Casual).</summary>
        public static List<string> BalanceTypeCodes()
        {
            string configured = SettingsBLL.GetString(BalanceTypesKey, "Annual,Casual");
            List<string> codes = new List<string>();
            foreach (string part in configured.Split(','))
            {
                string token = (part ?? string.Empty).Trim();
                if (token.Length > 0)
                {
                    codes.Add(token);
                }
            }
            return codes;
        }

        private static bool IsBalanceType(LeaveType type)
        {
            if (type == null || string.IsNullOrEmpty(type.LeaveTypeCode))
            {
                return false;
            }
            foreach (string code in BalanceTypeCodes())
            {
                if (string.Equals(code, type.LeaveTypeCode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Annual balances grid: one row per quota type with the previous-years
        /// carry-over pool (configurable through "Leave.AllowCarryOver").
        /// </summary>
        public static List<LeaveBalanceView> BalanceGrid(int employeeId, int year)
        {
            List<LeaveBalanceView> rows = new List<LeaveBalanceView>();
            bool carryOverEnabled = SettingsBLL.GetBool(CarryOverKey, true);
            foreach (LeaveType type in LeaveDAL.GetTypes())
            {
                if (!IsBalanceType(type))
                {
                    continue;
                }
                LeaveBalance balance = MaterializeBalance(employeeId, year, type.LeaveTypeId);
                LeaveBalanceView view = new LeaveBalanceView();
                view.LeaveTypeId = type.LeaveTypeId;
                view.TypeCode = type.LeaveTypeCode;
                view.TypeName = type.TypeName;
                view.QuotaDays = balance.QuotaDays ?? 0;
                view.UsedDays = balance.UsedDays ?? 0;
                view.YearRemaining = Math.Max(0m, view.QuotaDays - view.UsedDays);
                view.CarryOverDays = carryOverEnabled
                    ? LeaveDAL.CarryOver(employeeId, year, type.LeaveTypeId)
                    : 0m;
                view.TotalAvailable = view.YearRemaining + view.CarryOverDays;
                rows.Add(view);
            }
            return rows;
        }

        /// <summary>
        /// Special leaves grid: all non-quota types (sick, maternity, childcare…)
        /// tracked by their actual approved usage in the year and over all years.
        /// </summary>
        public static List<LeaveTypeUsageView> SpecialUsage(int employeeId, int year)
        {
            List<LeaveTypeUsageView> rows = new List<LeaveTypeUsageView>();
            foreach (LeaveType type in LeaveDAL.GetTypes())
            {
                if (IsBalanceType(type))
                {
                    continue;
                }
                LeaveTypeUsageView view = new LeaveTypeUsageView();
                view.LeaveTypeId = type.LeaveTypeId;
                view.TypeCode = type.LeaveTypeCode;
                view.TypeName = type.TypeName;
                view.AnnualDays = type.AnnualDays;
                view.RequiresDocValue = type.RequiresDoc;
                decimal[] stats = LeaveDAL.ApprovedRequestStatsInWindow(employeeId, type.LeaveTypeId,
                    FiscalStart(year).Date, FiscalStart(year + 1).Date);
                view.ApprovedRequests = (int)stats[0];
                view.UsedDays = stats[1];
                view.LifetimeDays = LeaveDAL.UsedDaysAllYears(employeeId, type.LeaveTypeId);
                rows.Add(view);
            }
            return rows;
        }

        /// <summary>Carry-over pool currently usable for one employee/type/year (0 when disabled).</summary>
        public static decimal UsableCarryOver(int employeeId, int year, LeaveType type)
        {
            if (type == null || !SettingsBLL.GetBool(CarryOverKey, true))
            {
                return 0m;
            }
            return LeaveDAL.CarryOver(employeeId, year, type.LeaveTypeId);
        }

        /// <summary>Edits a type quota (rule 15: values adjustable); gated by "Leaves.Approve".</summary>
        public static void SaveTypeQuota(int leaveTypeId, int? annualDays, bool? isPaid, bool? requiresDoc)
        {
            AppContext.RequirePermission("Leaves.Approve");
            LeaveType type = LeaveDAL.GetTypeById(leaveTypeId);
            if (type == null)
            {
                throw new BusinessException("نوع الإجازة غير موجود.");
            }
            if (annualDays == null || annualDays.Value < 0 || annualDays.Value > 365)
            {
                throw new BusinessException("الرصيد السنوي يجب أن يكون بين 0 و 365 يوماً.");
            }
            LeaveDAL.UpdateTypeQuota(leaveTypeId, annualDays, isPaid, requiresDoc);
            AuditBLL.Log("Leave.TypeUpdate", "LeaveTypes", leaveTypeId.ToString(),
                type.AnnualDays.ToString(), annualDays.ToString());
        }

        /// <summary>
        /// Edits one employee/year quota (rule 15: per-employee adjustment).
        /// UsedDays stays recomputed from approved requests; only the quota
        /// changes. Gated by "Leaves.Approve" and audit-logged.
        /// </summary>
        public static void SaveEmployeeQuota(int employeeId, int year, int leaveTypeId, decimal quotaDays)
        {
            AppContext.RequireAnyPermission(new string[] { "Leaves.Approve", "Leaves.Balances" });
            LeaveType type = LeaveDAL.GetTypeById(leaveTypeId);
            if (type == null)
            {
                throw new BusinessException("نوع الإجازة غير موجود.");
            }
            if (quotaDays < 0m || quotaDays > 365m)
            {
                throw new BusinessException("الرصيد يجب أن يكون بين 0 و 365 يومًا.");
            }
            LeaveBalance balance = MaterializeBalance(employeeId, year, leaveTypeId);
            decimal oldQuota = balance.QuotaDays ?? 0m;
            balance.QuotaDays = quotaDays;
            balance.Remaining = quotaDays - (balance.UsedDays ?? 0);
            LeaveDAL.UpsertBalance(balance);
            AuditBLL.Log("Leave.EmployeeQuota", "LeaveBalances",
                employeeId + "/" + year + "/" + leaveTypeId,
                oldQuota.ToString(), quotaDays.ToString());
        }

        private static void ValidateContent(LeaveRequest request)
        {
            if (request == null || request.EmployeeId == null || request.EmployeeId.Value <= 0)
            {
                throw new BusinessException("اختر موظفاً أولاً (الرقم غير صالح).");
            }
            if (request.LeaveTypeId == null || request.LeaveTypeId.Value <= 0)
            {
                throw new BusinessException("اختر نوع الإجازة.");
            }
            if (request.StartDate == null || request.EndDate == null)
            {
                throw new BusinessException("حدد تاريخ البداية والنهاية.");
            }
            if (request.EndDate.Value.Date < request.StartDate.Value.Date)
            {
                throw new BusinessException("تاريخ النهاية قبل تاريخ البداية.");
            }
        }

        /// <summary>
        /// Leave deduction follows the shift of every leave day (order aa2c7e45):
        /// a 6h or 7h shift counts as 1.0 day, 9h as 1.5 days and 12h as 2.0 days.
        /// The shift is read from the employee's monthly schedule; days without a
        /// scheduled cell (unassigned monthly schedule) default to 1.0 day, and
        /// weekly-rest days (shift code "FR") deduct nothing.
        /// </summary>
        private static decimal SumDeduction(int employeeId, DateTime startDate, DateTime endDate)
        {
            decimal total = 0m;
            Dictionary<string, MonthData> monthCache = new Dictionary<string, MonthData>();
            DateTime cursor = startDate.Date;
            while (cursor <= endDate.Date)
            {
                MonthData month = LoadMonthCached(monthCache, cursor.Year, cursor.Month);
                int shiftId;
                if (month != null && month.Cells.ContainsKey(employeeId) &&
                    month.Cells[employeeId].TryGetValue(cursor.Day, out shiftId))
                {
                    Shift shift;
                    month.Shifts.TryGetValue(shiftId, out shift);
                    total += DeductionForShift(shift);
                }
                else
                {
                    total += 1m;
                }
                cursor = cursor.AddDays(1);
            }
            return total;
        }

        /// <summary>Half-day deductible for one shift: 6-7h = 1.0, 9h = 1.5, 12h = 2.0.</summary>
        private static decimal DeductionForShift(Shift shift)
        {
            if (shift == null)
            {
                return 1m;
            }
            if (string.Equals(shift.ShiftCode ?? string.Empty, "FR", StringComparison.OrdinalIgnoreCase))
            {
                return 0m;
            }
            float hours = shift.DurationHrs != null ? shift.DurationHrs.Value :
                (shift.NormalHours != null ? shift.NormalHours.Value : 8f);
            if (hours <= 0f)
            {
                return 1m;
            }
            decimal deduction = Math.Floor(((decimal)hours / 6m) * 2m) * 0.5m;
            return deduction < 0.5m ? 0.5m : deduction;
        }

        private static MonthData LoadMonthCached(Dictionary<string, MonthData> cache, int year, int month)
        {
            string key = year + "-" + month;
            if (cache.ContainsKey(key))
            {
                return cache[key];
            }
            MonthData month = ScheduleBLL.LoadMonth(year, month, null, null);
            cache[key] = month;
            return month;
        }

        private static void CheckBalance(LeaveRequest request, LeaveType type, int? excludeLeaveId)
        {
            if (string.Equals(type.LeaveTypeCode, "Hajj", StringComparison.Ordinal))
            {
                decimal lifetime = LeaveDAL.UsedDaysAllYears(request.EmployeeId ?? 0, type.LeaveTypeId);
                if (lifetime > 0)
                {
                    throw new BusinessException("إجازة الحج تُمنح مرة واحدة في العمر — المُستهلَك سابقاً: " + lifetime + " يوماً.");
                }
            }
            int year = BalanceYearFor(request.StartDate ?? DateTime.Now);
            LeaveBalance balance = MaterializeBalance(request.EmployeeId ?? 0, year, type.LeaveTypeId);
            // quota types may draw from the previous-years carry-over pool;
            // special types keep the plain per-year quota check.
            decimal available = (balance.Remaining ?? 0m) +
                (IsBalanceType(type) ? UsableCarryOver(request.EmployeeId ?? 0, year, type) : 0m);
            if ((request.DaysCount ?? 0) > available)
            {
                throw new BusinessException("الرصيد لا يكفي — المطلوب: " + request.DaysCount +
                    " يوم، المتاح: " + available + " يوم (" + type.TypeName + ").");
            }
        }

        private static void CheckBalanceForUpdate(LeaveRequestView stored, LeaveRequest request, LeaveType type)
        {
            if (stored.Status != StatusApproved)
            {
                CheckBalance(request, type, stored.LeaveId);
                return;
            }
            int year = BalanceYearFor(request.StartDate ?? DateTime.Now);
            decimal previouslyUsed = LeaveDAL.UsedDaysInWindow(stored.EmployeeId ?? 0, type.LeaveTypeId,
                FiscalStart(year).Date, FiscalStart(year + 1).Date);
            decimal newUsed = previouslyUsed - (stored.DaysCount ?? 0m) + (request.DaysCount ?? 0m);
            LeaveBalance balance = MaterializeBalance(stored.EmployeeId ?? 0, year, type.LeaveTypeId);
            decimal updateAvailable = (balance.QuotaDays ?? 0m) +
                (IsBalanceType(type) ? UsableCarryOver(stored.EmployeeId ?? 0, year, type) : 0m);
            if (newUsed > updateAvailable)
            {
                throw new BusinessException("الرصيد بعد التعديل يتجاوز الحصة — الحصة: " + (balance.QuotaDays ?? 0) +
                    " يوماً، المطلوب: " + newUsed + " يوماً.");
            }
        }

        /// <summary>Rebuilds the balance row from the approved-request SUM (W3 pipeline, no drift).</summary>
        private static LeaveBalance MaterializeBalance(int employeeId, int year, int leaveTypeId)
        {
            LeaveType type = LeaveDAL.GetTypeById(leaveTypeId);
            LeaveBalance existing = LeaveDAL.GetBalance(employeeId, year, leaveTypeId);
            decimal quota = existing != null && existing.QuotaDays != null ?
                existing.QuotaDays.Value : (type != null && type.AnnualDays != null ? (decimal)type.AnnualDays.Value : 0m);
            decimal used = LeaveDAL.UsedDaysInWindow(employeeId, leaveTypeId,
                FiscalStart(year).Date, FiscalStart(year + 1).Date);
            LeaveBalance balance = new LeaveBalance();
            balance.EmployeeId = employeeId;
            balance.YearVal = year;
            balance.LeaveTypeId = leaveTypeId;
            balance.QuotaDays = quota;
            balance.UsedDays = used;
            balance.Remaining = quota - used;
            LeaveDAL.UpsertBalance(balance);
            return balance;
        }

        private static int CurrentUserId()
        {
            return AppContext.CurrentUser == null ? 0 : AppContext.CurrentUser.UserId;
        }
    }
}
