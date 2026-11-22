using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Administration of the leave-type catalogue: codes, names, annual quotas,
    /// paid flag and whether documents are required. Reads are open to any
    /// authenticated user (the request screens need the list); writes are gated
    /// by the seeded permission "Org.Manage". Deleting is refused while any
    /// request or balance references the type.
    /// </summary>
    public static class LeaveTypeBLL
    {
        public static List<LeaveType> List()
        {
            return LeaveTypeDAL.List();
        }

        public static LeaveType GetById(int leaveTypeId)
        {
            return LeaveTypeDAL.GetById(leaveTypeId);
        }

        public static int Create(string code, string name, int? annualDays, bool? isPaid, bool? requiresDoc)
        {
            AppContext.RequirePermission("Org.Manage");
            Validate(code, name, annualDays);
            if (LeaveTypeDAL.GetByCode(code) != null)
            {
                throw new BusinessException("كود نوع الإجازة '" + code + "' مستخدم بالفعل.");
            }
            LeaveType type = new LeaveType();
            type.LeaveTypeCode = code;
            type.TypeName = name;
            type.AnnualDays = annualDays;
            type.IsPaid = isPaid == true;
            type.RequiresDoc = requiresDoc == true;
            int newId = LeaveTypeDAL.Insert(type);
            AuditBLL.Log("Leaves.AddType", "LeaveTypes", newId.ToString(), null, code + " - " + name);
            return newId;
        }

        public static void Update(int leaveTypeId, string code, string name, int? annualDays, bool? isPaid, bool? requiresDoc)
        {
            AppContext.RequirePermission("Org.Manage");
            Validate(code, name, annualDays);
            LeaveType stored = LeaveTypeDAL.GetById(leaveTypeId);
            if (stored == null)
            {
                throw new BusinessException("نوع الإجازة المطلوب تعديله غير موجود.");
            }
            LeaveType duplicate = LeaveTypeDAL.GetByCode(code);
            if (duplicate != null && duplicate.LeaveTypeId != leaveTypeId)
            {
                throw new BusinessException("كود نوع الإجازة '" + code + "' مستخدم بالفعل.");
            }
            stored.LeaveTypeCode = code;
            stored.TypeName = name;
            stored.AnnualDays = annualDays;
            stored.IsPaid = isPaid == true;
            stored.RequiresDoc = requiresDoc == true;
            LeaveTypeDAL.Update(stored);
            AuditBLL.Log("Leaves.UpdateType", "LeaveTypes", leaveTypeId.ToString(),
                stored.LeaveTypeCode + " - " + stored.TypeName, code + " - " + name);
        }

        /// <summary>Deletes a type only when no request or balance row references it.</summary>
        public static void Delete(int leaveTypeId)
        {
            AppContext.RequirePermission("Org.Manage");
            LeaveType stored = LeaveTypeDAL.GetById(leaveTypeId);
            if (stored == null)
            {
                throw new BusinessException("نوع الإجازة المطلوب حذفه غير موجود.");
            }
            long usage = LeaveTypeDAL.CountUsages(leaveTypeId);
            if (usage > 0)
            {
                throw new BusinessException("لا يمكن حذف نوع الإجازة لارتباطه بـ " + usage +
                    " سجل طلب/رصيد — عطّله أو احذف السجلات التابعة أولاً.");
            }
            LeaveTypeDAL.Delete(leaveTypeId);
            AuditBLL.Log("Leaves.DeleteType", "LeaveTypes", leaveTypeId.ToString(),
                stored.LeaveTypeCode + " - " + stored.TypeName, null);
        }

        private static void Validate(string code, string name, int? annualDays)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new BusinessException("أدخل كود نوع الإجازة.");
            }
            if (code.Trim().Length > 10)
            {
                throw new BusinessException("كود نوع الإجازة لا يتجاوز 10 حروف.");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessException("أدخل مسمى نوع الإجازة.");
            }
            if (name.Trim().Length > 50)
            {
                throw new BusinessException("مسمى نوع الإجازة لا يتجاوز 50 حرفًا.");
            }
            if (annualDays != null && annualDays.Value < 0)
            {
                throw new BusinessException("أيام الرصيد السنوي لا يمكن أن تكون سالبة.");
            }
        }
    }
}
