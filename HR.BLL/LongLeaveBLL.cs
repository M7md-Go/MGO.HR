using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Long-duration leaves business logic (child care, family care, going
    /// abroad, income improvement): full-day (month/year scale) periods that
    /// may be open-ended (ToDate = NULL) for unlimited types. Limited types
    /// enforce a maximum duration in months. Creating a request needs any
    /// authenticated user; approving, rejecting or ending needs "Leaves.Approve";
    /// every state change is audit-logged. Approved open-ended periods feed
    /// the attendance engine automatically (ToDate normalized to 2099-12-31).
    /// </summary>
    public static class LongLeaveBLL
    {
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusRejected = "Rejected";
        public const string StatusEnded = "Ended";

        public static List<LongLeaveType> Types()
        {
            return LongLeaveDAL.GetTypes();
        }

        public static List<LongLeaveView> Search(string status, int? typeId, int? employeeId,
            DateTime? fromDate, DateTime? toDate)
        {
            return LongLeaveDAL.Search(RequireSearchStatus(status), typeId, employeeId, fromDate, toDate, 1000);
        }

        private static string RequireSearchStatus(string status)
        {
            if (status == StatusPending || status == StatusApproved || status == StatusRejected ||
                status == StatusEnded || string.IsNullOrEmpty(status))
            {
                return string.IsNullOrEmpty(status) ? null : status;
            }
            throw new BusinessException("حالة الإجازة طويلة المدة غير معروفة: " + status);
        }

        public static LongLeaveView GetById(int longLeaveId)
        {
            return LongLeaveDAL.GetById(longLeaveId);
        }

        public static int Create(LongLeave leave)
        {
            ValidateContent(leave, 0);
            int longLeaveId = LongLeaveDAL.Insert(leave, CurrentUserId());
            AuditBLL.Log("LongLeave.Create", "LongLeaves", longLeaveId.ToString(), null,
                "Employee=" + leave.EmployeeId +
                ", Type=" + (leave.TypeId ?? 0) +
                ", From=" + (leave.FromDate ?? DateTime.MinValue).ToString("yyyy-MM-dd") +
                ", To=" + (leave.ToDate == null ? "OpenEnded" : leave.ToDate.Value.ToString("yyyy-MM-dd")));
            return longLeaveId;
        }

        public static void Update(int longLeaveId, LongLeave leave)
        {
            ValidateContent(leave, longLeaveId);
            LongLeaveView stored = LongLeaveDAL.GetById(longLeaveId);
            if (stored == null)
            {
                throw new BusinessException("طلب الإجازة طويلة المدة غير موجود.");
            }
            if (stored.Status == StatusApproved || stored.Status == StatusEnded)
            {
                throw new BusinessException("لا يمكن تعديل إجازة معتمدة — أنهِ الإجازة أو أنشئ طلبًا جديدًا.");
            }
            LongLeaveDAL.Update(longLeaveId, leave);
            AuditBLL.Log("LongLeave.Update", "LongLeaves", longLeaveId.ToString(), stored.Status, stored.Status);
        }

        public static void Approve(int longLeaveId)
        {
            AppContext.RequirePermission("Leaves.Approve");
            LongLeaveView stored = LongLeaveDAL.GetById(longLeaveId);
            if (stored == null)
            {
                throw new BusinessException("طلب الإجازة طويلة المدة غير موجود.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("الاعتماد متاح للطلبات المعلّقة فقط — الحالة الحالية: " + stored.Status + ".");
            }
            LongLeaveDAL.SetStatus(longLeaveId, StatusApproved, CurrentUserId());
            AuditBLL.Log("LongLeave.Approve", "LongLeaves", longLeaveId.ToString(), StatusPending, StatusApproved);
        }

        public static void Reject(int longLeaveId)
        {
            AppContext.RequirePermission("Leaves.Approve");
            LongLeaveView stored = LongLeaveDAL.GetById(longLeaveId);
            if (stored == null)
            {
                throw new BusinessException("طلب الإجازة طويلة المدة غير موجود.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("الرفض متاح للطلبات المعلّقة فقط — الحالة الحالية: " + stored.Status + ".");
            }
            LongLeaveDAL.SetStatus(longLeaveId, StatusRejected, CurrentUserId());
            AuditBLL.Log("LongLeave.Reject", "LongLeaves", longLeaveId.ToString(), StatusPending, StatusRejected);
        }

        /// <summary>Ends an approved period at an explicit date (also closes open-ended periods).</summary>
        public static void SetEndDate(int longLeaveId, DateTime endDate)
        {
            AppContext.RequirePermission("Leaves.Approve");
            LongLeaveView stored = LongLeaveDAL.GetById(longLeaveId);
            if (stored == null)
            {
                throw new BusinessException("طلب الإجازة طويلة المدة غير موجود.");
            }
            if (stored.Status != StatusApproved && stored.Status != StatusPending)
            {
                throw new BusinessException("إنهاء الإجازة متاح للطلبات المعتمدة أو المعلّقة فقط — الحالة الحالية: " + stored.Status + ".");
            }
            if (endDate < (stored.FromDate ?? DateTime.MinValue))
            {
                throw new BusinessException("تاريخ النهاية قبل تاريخ البداية.");
            }
            LongLeaveDAL.SetToDate(longLeaveId, endDate.Date);
            if (stored.Status == StatusApproved)
            {
                LongLeaveDAL.SetStatus(longLeaveId, StatusEnded, stored.ApprovedBy);
            }
            AuditBLL.Log("LongLeave.SetEndDate", "LongLeaves", longLeaveId.ToString(),
                stored.ToDate == null ? "OpenEnded" : stored.ToDate.Value.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"));
        }

        private static void ValidateContent(LongLeave leave, int excludeId)
        {
            if (leave == null || leave.EmployeeId == null || leave.EmployeeId.Value <= 0)
            {
                throw new BusinessException("اختر موظفًا أولاً (الرقم غير صالح).");
            }
            if (leave.TypeId == null || leave.TypeId.Value <= 0)
            {
                throw new BusinessException("اختر نوع الإجازة طويلة المدة.");
            }
            if (leave.FromDate == null)
            {
                throw new BusinessException("حدد تاريخ بداية الإجازة.");
            }
            LongLeaveType type = LongLeaveDAL.GetTypeById(leave.TypeId.Value);
            if (type == null)
            {
                throw new BusinessException("نوع الإجازة طويلة المدة غير موجود.");
            }
            if (leave.ToDate != null)
            {
                if (leave.ToDate.Value.Date < leave.FromDate.Value.Date)
                {
                    throw new BusinessException("تاريخ النهاية قبل تاريخ البداية.");
                }
                if (type.IsLimited == true && type.MaxMonths != null && type.MaxMonths.Value > 0)
                {
                    int months = DurationMonths(leave.FromDate.Value.Date, leave.ToDate.Value.Date);
                    if (months > type.MaxMonths.Value)
                    {
                        throw new BusinessException("المدة " + months + " شهرًا تتجاوز الحد الأقصى لهذا النوع: " +
                            type.MaxMonths.Value + " شهرًا (" + type.TypeName + ").");
                    }
                }
            }
            else if (type.IsLimited == true)
            {
                throw new BusinessException("نوع الإجازة «" + type.TypeName + "» محدد المدة — يجب اختيار تاريخ نهاية.");
            }
            if (!string.IsNullOrEmpty(leave.Reason) && leave.Reason.Length > 500)
            {
                throw new BusinessException("السبب يتجاوز 500 حرفًا.");
            }
            if (LongLeaveDAL.HasOverlapping(leave.EmployeeId ?? 0,
                leave.FromDate.Value.Date, leave.ToDate, excludeId))
            {
                throw new BusinessException("يوجد طلب إجازة طويلة المدة متداخل مع نفس الفترة لهذا الموظف (معلّق أو معتمد أو منتهٍ).");
            }
        }

        /// <summary>Whole months spanned by the period (used against MaxMonths).</summary>
        private static int DurationMonths(DateTime fromDate, DateTime toDate)
        {
            int months = (toDate.Year - fromDate.Year) * 12 + (toDate.Month - fromDate.Month);
            if (toDate.Day > fromDate.Day)
            {
                months = months + 1;
            }
            return Math.Max(months, 0);
        }

        private static int CurrentUserId()
        {
            return AppContext.CurrentUser == null ? 0 : AppContext.CurrentUser.UserId;
        }
    }
}
