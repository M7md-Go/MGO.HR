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
            ValidateContent(request);
            int leaveTypeId = request.LeaveTypeId ?? 0;
            LeaveType type = LeaveDAL.GetTypeById(leaveTypeId);
            if (type == null)
            {
                throw new BusinessException("نوع الإجازة غير موجود.");
            }
            int userId = CurrentUserId();
            request.DaysCount = DayCount(request.StartDate ?? DateTime.MinValue, request.EndDate ?? DateTime.MinValue);
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
            LeaveType type = LeaveDAL.GetTypeById(request.LeaveTypeId ?? 0);
            if (type == null)
            {
                throw new BusinessException("نوع الإجازة غير موجود.");
            }
            request.DaysCount = DayCount(request.StartDate ?? DateTime.MinValue, request.EndDate ?? DateTime.MinValue);
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

        public static void Reject(int leaveId)
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
            LeaveDAL.SetRequestStatus(leaveId, StatusRejected, CurrentUserId());
            AuditBLL.Log("Leave.Reject", "LeaveRequests", leaveId.ToString(), StatusPending, StatusRejected);
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
        public static void SaveEmployeeQuota(int employeeId, int year, int leaveTypeId, int quotaDays)
        {
            AppContext.RequirePermission("Leaves.Approve");
            LeaveType type = LeaveDAL.GetTypeById(leaveTypeId);
            if (type == null)
            {
                throw new BusinessException("نوع الإجازة غير موجود.");
            }
            if (quotaDays < 0 || quotaDays > 365)
            {
                throw new BusinessException("الرصيد يجب أن يكون بين 0 و 365 يومًا.");
            }
            LeaveBalance balance = MaterializeBalance(employeeId, year, leaveTypeId);
            int oldQuota = balance.QuotaDays ?? 0;
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

        private static int DayCount(DateTime startDate, DateTime endDate)
        {
            return (endDate.Date - startDate.Date).Days + 1;
        }

        private static void CheckBalance(LeaveRequest request, LeaveType type, int? excludeLeaveId)
        {
            if (string.Equals(type.LeaveTypeCode, "Hajj", StringComparison.Ordinal))
            {
                int lifetime = LeaveDAL.UsedDaysAllYears(request.EmployeeId ?? 0, type.LeaveTypeId);
                if (lifetime > 0)
                {
                    throw new BusinessException("إجازة الحج تُمنح مرة واحدة في العمر — المُستهلَك سابقاً: " + lifetime + " يوماً.");
                }
            }
            int year = (request.StartDate ?? DateTime.Now).Year;
            LeaveBalance balance = MaterializeBalance(request.EmployeeId ?? 0, year, type.LeaveTypeId);
            if ((request.DaysCount ?? 0) > (balance.Remaining ?? 0))
            {
                throw new BusinessException("الرصيد لا يكفي — المطلوب: " + request.DaysCount +
                    " يوم، المتبقي: " + (balance.Remaining ?? 0) + " يوم (" + type.TypeName + ").");
            }
        }

        private static void CheckBalanceForUpdate(LeaveRequestView stored, LeaveRequest request, LeaveType type)
        {
            if (stored.Status != StatusApproved)
            {
                CheckBalance(request, type, stored.LeaveId);
                return;
            }
            int year = (request.StartDate ?? DateTime.Now).Year;
            int previouslyUsed = LeaveDAL.UsedDays(stored.EmployeeId ?? 0, year, type.LeaveTypeId);
            int newUsed = previouslyUsed - (stored.DaysCount ?? 0) + (request.DaysCount ?? 0);
            LeaveBalance balance = MaterializeBalance(stored.EmployeeId ?? 0, year, type.LeaveTypeId);
            if (newUsed > (balance.QuotaDays ?? 0))
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
            int quota = existing != null && existing.QuotaDays != null ?
                existing.QuotaDays.Value : (type != null && type.AnnualDays != null ? type.AnnualDays.Value : 0);
            int used = LeaveDAL.UsedDays(employeeId, year, leaveTypeId);
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
