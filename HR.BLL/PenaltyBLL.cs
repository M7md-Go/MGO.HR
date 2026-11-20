using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Penalty management (rule 17): Warning / Deduction / Penalty / Absence /
    /// Violation with date, amount, reason and issuing user. Every write is
    /// gated by "Penalties.Add" and audit-logged. The attendance engine reads
    /// absence penalties through its normal day calculation, and reports read
    /// them directly; Penalties has no FKs, so Delete is a hard delete and the
    /// row is preserved in the Audit Log.
    /// </summary>
    public static class PenaltyBLL
    {
        public const string TypeWarning = "Warning";
        public const string TypeDeduction = "Deduction";
        public const string TypePenalty = "Penalty";
        public const string TypeAbsence = "Absence";
        public const string TypeViolation = "Violation";

        public static List<PenaltyView> Search(int? employeeId, DateTime? fromDate,
            DateTime? toDate, string typeCode)
        {
            return PenaltyDAL.Search(employeeId, fromDate, toDate, typeCode, 1000);
        }

        public static Penalty GetById(int penaltyId)
        {
            return PenaltyDAL.GetById(penaltyId);
        }

        /// <summary>Arabic label for a type code (rule 17 wording).</summary>
        public static string TypeName(string typeCode)
        {
            switch (typeCode)
            {
                case TypeWarning: return "إنذار";
                case TypeDeduction: return "خصم";
                case TypePenalty: return "جزاء";
                case TypeAbsence: return "غياب";
                case TypeViolation: return "مخالفة";
                default: return typeCode ?? "";
            }
        }

        public static void Create(Penalty penalty)
        {
            AppContext.RequirePermission("Penalties.Add");
            ValidateContent(penalty);
            penalty.IssuedBy = CurrentUserId();
            int penaltyId = PenaltyDAL.Insert(penalty, penalty.IssuedBy);
            AuditBLL.Log("Penalty.Create", "Penalties", penaltyId.ToString(), null,
                "Employee=" + penalty.EmployeeId + ", Type=" + penalty.TypeCode +
                ", Amount=" + (penalty.Amount.HasValue ? penalty.Amount.Value.ToString() : "-") +
                ", Date=" + penalty.PenaltyDate.Value.ToString("yyyy-MM-dd"));
        }

        public static void Update(int penaltyId, Penalty penalty)
        {
            AppContext.RequirePermission("Penalties.Add");
            Penalty stored = PenaltyDAL.GetById(penaltyId);
            if (stored == null)
            {
                throw new BusinessException("الجزاء غير موجود.");
            }
            ValidateContent(penalty);
            penalty.IssuedBy = CurrentUserId();
            PenaltyDAL.Update(penaltyId, penalty);
            AuditBLL.Log("Penalty.Update", "Penalties", penaltyId.ToString(),
                stored.TypeCode + "/" + (stored.Amount.HasValue ? stored.Amount.Value.ToString() : "-"),
                penalty.TypeCode + "/" + (penalty.Amount.HasValue ? penalty.Amount.Value.ToString() : "-"));
        }

        public static void Delete(int penaltyId)
        {
            AppContext.RequirePermission("Penalties.Add");
            Penalty stored = PenaltyDAL.GetById(penaltyId);
            if (stored == null)
            {
                throw new BusinessException("الجزاء غير موجود.");
            }
            PenaltyDAL.Delete(penaltyId);
            AuditBLL.Log("Penalty.Delete", "Penalties", penaltyId.ToString(),
                "Type=" + stored.TypeCode + ", Employee=" + stored.EmployeeId, null);
        }

        private static void ValidateContent(Penalty penalty)
        {
            if (penalty == null || penalty.EmployeeId == null || penalty.EmployeeId.Value <= 0)
            {
                throw new BusinessException("اختر موظفًا أولاً (الرقم غير صالح).");
            }
            if (penalty.PenaltyDate == null)
            {
                throw new BusinessException("حدد تاريخ الجزاء.");
            }
            if (string.IsNullOrEmpty(penalty.TypeCode) ||
                (penalty.TypeCode != TypeWarning && penalty.TypeCode != TypeDeduction &&
                 penalty.TypeCode != TypePenalty && penalty.TypeCode != TypeAbsence &&
                 penalty.TypeCode != TypeViolation))
            {
                throw new BusinessException("نوع الجزاء يجب أن يكون: إنذار، خصم، جزاء، غياب، أو مخالفة.");
            }
            if (penalty.Amount.HasValue && penalty.Amount.Value < 0)
            {
                throw new BusinessException("قيمة الجزاء لا يمكن أن تكون سالبة.");
            }
        }

        private static int CurrentUserId()
        {
            return AppContext.CurrentUser == null ? 0 : AppContext.CurrentUser.UserId;
        }
    }
}
