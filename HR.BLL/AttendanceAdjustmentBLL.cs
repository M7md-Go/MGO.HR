using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Manual attendance corrections ([AttendanceAdjustments]): a request locks
    /// the punches or the final status of one employee-day with a reason.
    /// Creating/editing is gated by "Attendance.Edit"; approving/rejecting by
    /// "Attendance.ApproveAdjust". Approval APPLIES the correction to
    /// [Attendance] flagged IsManuallyChg = True so the recalculation engine
    /// never overwrites the corrected day.
    /// </summary>
    public static class AttendanceAdjustmentBLL
    {
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusRejected = "Rejected";
        public const string TypeTime = "Time";
        public const string TypeStatus = "Status";

        private static readonly string[] EngineStatuses =
            new string[] { "Present", "Late", "EarlyLeave", "Mission", "Absent", "Leave", "MissingPunch", "Holiday" };

        public static List<AttendanceAdjustmentView> Browse(string status, int? employeeId,
            DateTime? fromDate, DateTime? toDate)
        {
            return AttendanceAdjustmentDAL.Browse(status, employeeId, fromDate, toDate, 1000);
        }

        /// <summary>Stored adjustment by id (edit/view dialog needs the raw row).</summary>
        public static AttendanceAdjustment GetById(int adjustmentId)
        {
            return AttendanceAdjustmentDAL.GetById(adjustmentId);
        }

        public static int Create(int employeeId, DateTime attDate, string adjustmentType,
            string newInTime, string newOutTime, string newStatus, string reason)
        {
            AppContext.RequirePermission("Attendance.Edit");
            Validate(employeeId, attDate, adjustmentType, newInTime, newOutTime, newStatus, reason);
            AttendanceAdjustment adjustment = new AttendanceAdjustment();
            adjustment.EmployeeId = employeeId;
            adjustment.AttDate = attDate.Date;
            adjustment.AdjustmentType = adjustmentType;
            adjustment.NewInTime = newInTime;
            adjustment.NewOutTime = newOutTime;
            adjustment.NewStatus = newStatus;
            adjustment.Reason = reason;
            int newId = AttendanceAdjustmentDAL.Insert(adjustment, CurrentUserId());
            AuditBLL.Log("Att.AddAdjust", "AttendanceAdjustments", newId.ToString(), null,
                employeeId + "@" + attDate.ToString("yyyy-MM-dd") + " - " + adjustmentType);
            return newId;
        }

        public static void Update(int adjustmentId, int employeeId, DateTime attDate, string adjustmentType,
            string newInTime, string newOutTime, string newStatus, string reason)
        {
            AppContext.RequirePermission("Attendance.Edit");
            AttendanceAdjustment stored = AttendanceAdjustmentDAL.GetById(adjustmentId);
            if (stored == null)
            {
                throw new BusinessException("طلب التصحيح غير موجود.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("لا يمكن تعديل طلب مصحّح معتمد أو مرفوض.");
            }
            Validate(employeeId, attDate, adjustmentType, newInTime, newOutTime, newStatus, reason);
            stored.EmployeeId = employeeId;
            stored.AttDate = attDate.Date;
            stored.AdjustmentType = adjustmentType;
            stored.NewInTime = newInTime;
            stored.NewOutTime = newOutTime;
            stored.NewStatus = newStatus;
            stored.Reason = reason;
            AttendanceAdjustmentDAL.Update(stored);
            AuditBLL.Log("Att.UpdateAdjust", "AttendanceAdjustments", adjustmentId.ToString(), null,
                employeeId + "@" + attDate.ToString("yyyy-MM-dd") + " - " + adjustmentType);
        }

        /// <summary>Approves the request and applies the correction to the attendance day.</summary>
        public static void Approve(int adjustmentId)
        {
            AppContext.RequirePermission("Attendance.ApproveAdjust");
            AttendanceAdjustment stored = AttendanceAdjustmentDAL.GetById(adjustmentId);
            if (stored == null)
            {
                throw new BusinessException("طلب التصحيح غير موجود.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("الطلب معتمد أو مرفوض بالفعل.");
            }
            int? approverId = CurrentUserId();
            DateTime? startTime = null;
            DateTime? endTime = null;
            if (stored.AdjustmentType == TypeTime)
            {
                if (stored.AttDate == null)
                {
                    throw new BusinessException("تاريخ اليوم ناقص في الطلب.");
                }
                startTime = ParseTimeOfDay(stored.NewInTime, stored.AttDate.Value);
                endTime = ParseTimeOfDay(stored.NewOutTime, stored.AttDate.Value);
                // cross-day night shift: a small end time following a late start rolls to the next day
                if (startTime != null && endTime != null && endTime.Value < startTime.Value)
                {
                    endTime = endTime.Value.AddDays(1);
                }
            }
            AttendanceAdjustmentDAL.ApplyToAttendance(stored.EmployeeId ?? 0,
                stored.AttDate ?? DateTime.MinValue, startTime, endTime, stored.NewStatus, approverId);
            AttendanceAdjustmentDAL.SetStatus(adjustmentId, StatusApproved, approverId);
            AuditBLL.Log("Att.ApproveAdjust", "AttendanceAdjustments", adjustmentId.ToString(),
                StatusPending, StatusApproved + " @" + (stored.AttDate == null ? "" : stored.AttDate.Value.ToString("yyyy-MM-dd")));
        }

        public static void Reject(int adjustmentId)
        {
            AppContext.RequirePermission("Attendance.ApproveAdjust");
            AttendanceAdjustment stored = AttendanceAdjustmentDAL.GetById(adjustmentId);
            if (stored == null)
            {
                throw new BusinessException("طلب التصحيح غير موجود.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("الطلب معتمد أو مرفوض بالفعل.");
            }
            AttendanceAdjustmentDAL.SetStatus(adjustmentId, StatusRejected, CurrentUserId());
            AuditBLL.Log("Att.RejectAdjust", "AttendanceAdjustments", adjustmentId.ToString(),
                StatusPending, StatusRejected);
        }

        /// <summary>Arabic display name of an adjustment type code.</summary>
        public static string TypeName(string adjustmentType)
        {
            if (adjustmentType == TypeTime)
            {
                return "تعديل أوقات البصمات";
            }
            if (adjustmentType == TypeStatus)
            {
                return "تغيير الحالة النهائية";
            }
            return adjustmentType;
        }

        /// <summary>Arabic display name of an attendance engine status code.</summary>
        public static string StatusName(string status)
        {
            if (status == "Present") { return "حاضر"; }
            if (status == "Late") { return "متأخر"; }
            if (status == "EarlyLeave") { return "انصراف مبكر"; }
            if (status == "Mission") { return "مأمورية"; }
            if (status == "Absent") { return "غائب"; }
            if (status == "Leave") { return "إجازة"; }
            if (status == "MissingPunch") { return "بصمة ناقصة"; }
            if (status == "Holiday") { return "عطلة رسمية"; }
            return status;
        }

        /// <summary>True when the code is one of the engine's attendance statuses.</summary>
        public static bool IsKnownStatus(string status)
        {
            return Array.IndexOf(EngineStatuses, status) >= 0;
        }

        /// <summary>Parses "HH:mm" text ( Eastern-Arabic digits accepted) into an absolute moment on the given date.</summary>
        public static DateTime? ParseTimeOfDay(string rawText, DateTime date)
        {
            string trimmed = TextNormalizer.NormalizeTimeText(rawText) ?? string.Empty;
            if (trimmed.Length == 0)
            {
                return (DateTime?)null;
            }
            string[] parts = trimmed.Split(new char[] { ':' });
            int hours;
            int minutes;
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out hours) || !int.TryParse(parts[1], out minutes) ||
                hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
            {
                throw new BusinessException("صيغة الوقت غير صحيحة: '" + trimmed + "' — استخدم HH:mm مثل 08:30.");
            }
            return date.Date.AddHours(hours).AddMinutes(minutes);
        }

        private static void Validate(int employeeId, DateTime attDate, string adjustmentType,
            string newInTime, string newOutTime, string newStatus, string reason)
        {
            if (employeeId <= 0)
            {
                throw new BusinessException("اختر الموظف أولاً.");
            }
            if (adjustmentType != TypeTime && adjustmentType != TypeStatus)
            {
                throw new BusinessException("نوع التصحيح غير معروف.");
            }
            if (adjustmentType == TypeTime && string.IsNullOrWhiteSpace(newInTime) &&
                string.IsNullOrWhiteSpace(newOutTime))
            {
                throw new BusinessException("أدخل وقت الدخول أو وقت الخروج على الأقل.");
            }
            if (adjustmentType == TypeTime)
            {
                if (!string.IsNullOrWhiteSpace(newInTime))
                {
                    ParseTimeOfDay(newInTime, attDate);
                }
                if (!string.IsNullOrWhiteSpace(newOutTime))
                {
                    ParseTimeOfDay(newOutTime, attDate);
                }
            }
            if (adjustmentType == TypeStatus && !IsKnownStatus(newStatus))
            {
                throw new BusinessException("اختر الحالة النهائية الصحيحة ليوم الحضور.");
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new BusinessException("أدخل سبب التصحيح — يُحفظ في سجل العمليات.");
            }
            if (reason.Trim().Length > 500)
            {
                throw new BusinessException("سبب التصحيح لا يتجاوز 500 حرف.");
            }
            if (attDate.Date > DateTime.Now.Date.AddDays(1))
            {
                throw new BusinessException("لا يتم تصحيح يوم مستقبلي.");
            }
        }

        private static int CurrentUserId()
        {
            return AppContext.CurrentUser == null ? 0 : AppContext.CurrentUser.UserId;
        }
    }
}
