using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Administration of the long-leave-type catalogue: kinds with unlimited or
    /// month-capped durations (Maternity, Study, Without salary, ...). Reads are
    /// open to any authenticated user; writes are gated by "Org.Manage".
    /// Deleting is refused while any long-leave request references the type.
    /// </summary>
    public static class LongLeaveTypeBLL
    {
        public static List<LongLeaveType> List()
        {
            return LongLeaveTypeDAL.List();
        }

        public static LongLeaveType GetById(int longLeaveTypeId)
        {
            return LongLeaveTypeDAL.GetById(longLeaveTypeId);
        }

        public static int Create(string code, string name, bool isLimited, int? maxMonths, string notes)
        {
            AppContext.RequirePermission("Org.Manage");
            Validate(code, name, isLimited, maxMonths, notes);
            if (LongLeaveTypeDAL.GetByCode(code) != null)
            {
                throw new BusinessException("كود نوع الإجازة الطويلة '" + code + "' مستخدم بالفعل.");
            }
            LongLeaveType type = new LongLeaveType();
            type.TypeCode = code;
            type.TypeName = name;
            type.IsLimited = isLimited;
            type.MaxMonths = isLimited ? maxMonths : (int?)null;
            type.IsActive = true;
            type.Notes = notes;
            int newId = LongLeaveTypeDAL.Insert(type);
            AuditBLL.Log("Leaves.AddLongType", "LongLeaveTypes", newId.ToString(), null, code + " - " + name);
            return newId;
        }

        public static void Update(int longLeaveTypeId, string code, string name, bool isLimited,
            int? maxMonths, bool isActive, string notes)
        {
            AppContext.RequirePermission("Org.Manage");
            Validate(code, name, isLimited, maxMonths, notes);
            LongLeaveType stored = LongLeaveTypeDAL.GetById(longLeaveTypeId);
            if (stored == null)
            {
                throw new BusinessException("نوع الإجازة الطويلة المطلوب تعديله غير موجود.");
            }
            LongLeaveType duplicate = LongLeaveTypeDAL.GetByCode(code);
            if (duplicate != null && duplicate.LongLeaveTypeId != longLeaveTypeId)
            {
                throw new BusinessException("كود نوع الإجازة الطويلة '" + code + "' مستخدم بالفعل.");
            }
            stored.TypeCode = code;
            stored.TypeName = name;
            stored.IsLimited = isLimited;
            stored.MaxMonths = isLimited ? maxMonths : (int?)null;
            stored.IsActive = isActive;
            stored.Notes = notes;
            LongLeaveTypeDAL.Update(stored);
            AuditBLL.Log("Leaves.UpdateLongType", "LongLeaveTypes", longLeaveTypeId.ToString(),
                stored.TypeCode + " - " + stored.TypeName, code + " - " + name);
        }

        /// <summary>Deletes a type only when no long-leave request references it.</summary>
        public static void Delete(int longLeaveTypeId)
        {
            AppContext.RequirePermission("Org.Manage");
            LongLeaveType stored = LongLeaveTypeDAL.GetById(longLeaveTypeId);
            if (stored == null)
            {
                throw new BusinessException("نوع الإجازة الطويلة المطلوب حذفه غير موجود.");
            }
            long usage = LongLeaveTypeDAL.CountUsages(longLeaveTypeId);
            if (usage > 0)
            {
                throw new BusinessException("لا يمكن حذف النوع لارتباطه بـ " + usage +
                    " طلب إجازة طويلة — عطّله بدلاً من حذفه.");
            }
            LongLeaveTypeDAL.Delete(longLeaveTypeId);
            AuditBLL.Log("Leaves.DeleteLongType", "LongLeaveTypes", longLeaveTypeId.ToString(),
                stored.TypeCode + " - " + stored.TypeName, null);
        }

        private static void Validate(string code, string name, bool isLimited, int? maxMonths, string notes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new BusinessException("أدخل كود النوع.");
            }
            if (code.Trim().Length > 20)
            {
                throw new BusinessException("كود النوع لا يتجاوز 20 حرفًا.");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessException("أدخل مسمى النوع.");
            }
            if (name.Trim().Length > 80)
            {
                throw new BusinessException("مسمى النوع لا يتجاوز 80 حرفًا.");
            }
            if (isLimited && (maxMonths == null || maxMonths.Value <= 0))
            {
                throw new BusinessException("النوع المحدد يتطلب عدد أشهر موجب.");
            }
            if (notes != null && notes.Length > 500)
            {
                throw new BusinessException("الملاحظات لا تتجاوز 500 حرف.");
            }
        }
    }
}
