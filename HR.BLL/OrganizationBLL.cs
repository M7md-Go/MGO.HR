using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Organization lookups: companies, branches, departments, sections, job titles,
    /// actual jobs, qualifications, employee statuses. Permission "Org.Manage".
    /// </summary>
    public static class OrganizationBLL
    {
        // ---------------- reading (available to any authenticated user) ----------------

        public static List<Company> Companies(bool activeOnly) { return LookupDAL.GetCompanies(activeOnly); }
        public static List<Branch> Branches(bool activeOnly) { return LookupDAL.GetBranches(activeOnly); }
        public static List<Department> Departments(bool activeOnly) { return LookupDAL.GetDepartments(activeOnly); }
        public static List<Section> Sections(int? departmentId, bool activeOnly) { return LookupDAL.GetSections(departmentId, activeOnly); }
        public static List<JobTitle> JobTitles(bool activeOnly) { return LookupDAL.GetJobTitles(activeOnly); }
        public static List<ActualJob> ActualJobs(int? departmentId, bool activeOnly) { return LookupDAL.GetActualJobs(departmentId, activeOnly); }
        public static List<Qualification> Qualifications() { return LookupDAL.GetQualifications(); }
        public static List<EmployeeStatus> EmployeeStatuses() { return LookupDAL.GetEmployeeStatuses(); }

        /// <summary>Loads generic rows of one lookup table for the management screen.</summary>
        public static List<LookupRow> Rows(LookupTable table, bool includeInactive)
        {
            return LookupDAL.GetLookupRows(table, includeInactive);
        }

        // ---------------- writing ----------------

        public static void Create(LookupTable table, string code, string name, int? parentId, bool isActive)
        {
            AppContext.RequirePermission("Org.Manage");
            Validate(table, code, name);
            if (LookupDAL.CodeExists(table, code, null))
            {
                throw new BusinessException("الكود '" + code + "' مستخدم بالفعل. يجب إدخال كود فريد.");
            }
            int newId = LookupDAL.InsertLookup(table, code, name, parentId, isActive, CurrentUserId());
            AuditBLL.Log("Org.Add", LookupDAL.TableNameForAudit(table), newId.ToString(), null, code + " - " + name);
        }

        public static void Update(LookupTable table, int id, string code, string name, int? parentId, bool isActive)
        {
            AppContext.RequirePermission("Org.Manage");
            Validate(table, code, name);
            LookupRow oldRow = LookupDAL.GetLookup(table, id);
            if (oldRow == null)
            {
                throw new BusinessException("السجل المطلوب تعديله غير موجود.");
            }
            if (LookupDAL.CodeExists(table, code, id))
            {
                throw new BusinessException("الكود '" + code + "' مستخدم بالفعل. يجب إدخال كود فريد.");
            }
            LookupDAL.UpdateLookup(table, id, code, name, parentId, isActive, CurrentUserId());
            AuditBLL.Log("Org.Update", LookupDAL.TableNameForAudit(table), id.ToString(),
                oldRow.Code + " - " + oldRow.Name, code + " - " + name);
        }

        /// <summary>Deletes a lookup row only when nothing depends on it.</summary>
        public static void Delete(LookupTable table, int id)
        {
            AppContext.RequirePermission("Org.Manage");
            LookupRow oldRow = LookupDAL.GetLookup(table, id);
            if (oldRow == null)
            {
                throw new BusinessException("السجل المطلوب حذفه غير موجود.");
            }
            long usage = LookupDAL.CountUsage(table, id);
            if (usage > 0)
            {
                throw new BusinessException("لا يمكن حذف هذا السجل لارتباطه بسجلات مستخدمة (" + usage + " ارتباط). قم بتعطيله بدلاً من حذفه.");
            }
            LookupDAL.DeleteLookup(table, id);
            AuditBLL.Log("Org.Delete", LookupDAL.TableNameForAudit(table), id.ToString(), oldRow.Code + " - " + oldRow.Name, null);
        }

        /// <summary>Enables or disables a lookup row instead of deleting it.</summary>
        public static void SetActive(LookupTable table, int id, bool isActive)
        {
            AppContext.RequirePermission("Org.Manage");
            LookupRow oldRow = LookupDAL.GetLookup(table, id);
            if (oldRow == null)
            {
                throw new BusinessException("السجل غير موجود.");
            }
            string oldState = isActive ? "Active=False" : "Active=True";
            string newState = isActive ? "Active=True" : "Active=False";
            LookupDAL.UpdateLookup(table, id, oldRow.Code, oldRow.Name, oldRow.ParentId, isActive, CurrentUserId());
            AuditBLL.Log("Org.SetActive", LookupDAL.TableNameForAudit(table), id.ToString(), oldState, newState);
        }

        private static void Validate(LookupTable table, string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new BusinessException("كود السجل إلزامي.");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessException("اسم السجل إلزامي.");
            }
            if (code.Trim().Length > 20)
            {
                throw new BusinessException("الكود طويل؛ الحد الأقصى 20 حرفًا.");
            }
            if (name.Trim().Length > 150)
            {
                throw new BusinessException("الاسم طويل؛ الحد الأقصى 150 حرفًا.");
            }
        }

        private static int? CurrentUserId()
        {
            if (AppContext.CurrentUser == null)
            {
                return null;
            }
            return AppContext.CurrentUser.UserId;
        }
    }
}
