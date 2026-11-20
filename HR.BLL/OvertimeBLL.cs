using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Overtime read model (rule 14): the engine writes rows with Source=Auto
    /// and Status=Pending or Approved (per Sch.OTRequiresApprove); reports and
    /// the approvals screen consume them read-only. Approval writes are gated
    /// by "Attendance.Edit" and audit-logged.
    /// </summary>
    public static class OvertimeBLL
    {
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusRejected = "Rejected";

        /// <summary>Translates a status to Arabic for display.</summary>
        public static string StatusName(string status)
        {
            if (status == StatusApproved)
            {
                return "معتمد";
            }
            if (status == StatusRejected)
            {
                return "مرفوض";
            }
            if (status == StatusPending)
            {
                return "معلّق";
            }
            return status;
        }

        public static List<OvertimeView> Search(DateTime? fromDate, DateTime? toDate,
            int? employeeId, string status)
        {
            if (status != StatusPending && status != StatusApproved &&
                status != StatusRejected && !string.IsNullOrEmpty(status))
            {
                throw new BusinessException("حالة الساعات الإضافية غير معروفة: " + status);
            }
            return OvertimeDAL.Search(fromDate, toDate, employeeId,
                string.IsNullOrEmpty(status) ? null : status, 1000);
        }

        /// <summary>Approves one Pending row; screen button stays gated too.</summary>
        public static void Approve(int overtimeId)
        {
            AppContext.RequirePermission("Attendance.Edit");
            OvertimeDAL.SetStatus(overtimeId, StatusApproved,
                AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId);
            AuditBLL.Log("Overtime.Approve", "Overtime", overtimeId.ToString(), StatusPending, StatusApproved);
        }

        /// <summary>Rejects one Pending row.</summary>
        public static void Reject(int overtimeId)
        {
            AppContext.RequirePermission("Attendance.Edit");
            OvertimeDAL.SetStatus(overtimeId, StatusRejected,
                AppContext.CurrentUser == null ? (int?)null : AppContext.CurrentUser.UserId);
            AuditBLL.Log("Overtime.Reject", "Overtime", overtimeId.ToString(), StatusPending, StatusRejected);
        }

        /// <summary>Formats minutes as H:mm for reports.</summary>
        public static string FormatMinutes(int? minutes)
        {
            if (minutes == null || minutes.Value <= 0)
            {
                return "0:00";
            }
            return (minutes.Value / 60) + ":" + (minutes.Value % 60).ToString("00");
        }
    }
}
