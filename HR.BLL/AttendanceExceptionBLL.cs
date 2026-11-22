using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Browsing the attendance-exception registry ([AttendanceExceptions]) —
    /// engine findings per employee-day (bad shift, absence, missing punch,
    /// extra punches, late, early leave, continuous work breaches) plus manual
    /// annotations. Recalculations regenerate engine rows (CreatedBy IS NULL),
    /// so only manual rows are deletable.
    /// </summary>
    public static class AttendanceExceptionBLL
    {
        public static List<AttendanceExceptionView> Browse(int? employeeId, DateTime? fromDate,
            DateTime? toDate, string excType)
        {
            return AttendanceExceptionDAL.Browse(employeeId, fromDate, toDate, excType, 1000);
        }

        /// <summary>Removes a manual exception row; engine rows cannot be deleted here.</summary>
        public static void DeleteManual(int exceptionId)
        {
            AppContext.RequirePermission("Attendance.Edit");
            int? createdBy = AttendanceExceptionDAL.CreatedByOf(exceptionId);
            if (createdBy == null)
            {
                throw new BusinessException("هذا الاستثناء مولَّد من محرك الحضور — أعِد حساب الفترة بدلاً من حذفه يدويًا.");
            }
            AttendanceExceptionDAL.Delete(exceptionId);
            AuditBLL.Log("Att.DeleteException", "AttendanceExceptions", exceptionId.ToString(),
                "CreatedBy=" + createdBy.Value, null);
        }

        /// <summary>Arabic display name of an engine exception type.</summary>
        public static string TypeName(string excType)
        {
            if (excType == "BadShift") { return "وردية غير محددة"; }
            if (excType == "Absent") { return "غياب"; }
            if (excType == "MissingPunch") { return "بصمة ناقصة"; }
            if (excType == "ExtraPunch") { return "بصمة زائدة"; }
            if (excType == "Late") { return "تأخير (دقائق)"; }
            if (excType == "EarlyLeave") { return "انصراف مبكر (دقائق)"; }
            if (excType == "OverContinuous") { return "تجاوز ساعات متواصلة"; }
            return excType;
        }
    }
}
