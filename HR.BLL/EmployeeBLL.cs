using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Employee business logic: validation (rule 24), lifecycle (BR-02 soft delete),
    /// audit trail (rule 22) and historical trail (Phase 1 design, EmployeeHistory).
    /// </summary>
    public static class EmployeeBLL
    {
        public const string StatusActive = "Active";
        public const string StatusSuspended = "Suspended";
        public const string StatusArchived = "Archived";

        public static Employee GetById(int employeeId)
        {
            return EmployeeDAL.GetById(employeeId);
        }

        public static Employee GetByCode(string employeeCode, bool includeArchived)
        {
            return EmployeeDAL.GetByCode(employeeCode, includeArchived);
        }

        /// <summary>Paged search for the employee grid (rule 27 — pagination).</summary>
        public static List<Employee> Search(EmployeeSearchFilter filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException("filter");
            }
            if (filter.PageSize <= 0 || filter.PageSize > 10000)
            {
                filter.PageSize = 10000;
            }
            return EmployeeDAL.Search(filter);
        }

        /// <summary>Adds a new employee (permission "Employees.Add").</summary>
        public static int Add(Employee employee)
        {
            AppContext.RequirePermission("Employees.Add");
            if (employee == null)
            {
                throw new BusinessException("لا يوجد موظف لحفظه.");
            }
            Validate(employee, true);

            employee.EmployeeCode = employee.EmployeeCode.Trim();
            Employee existingByCode = EmployeeDAL.GetByCode(employee.EmployeeCode, true);
            if (existingByCode != null)
            {
                throw new BusinessException("كود الموظف '" + employee.EmployeeCode + "' مستخدم بالفعل لموظف آخر. يجب إدخال كود فريد.");
            }
            if (!string.IsNullOrWhiteSpace(employee.NationalId) && NationalIdExists(employee.NationalId, null))
            {
                throw new BusinessException("الرقم القومي '" + employee.NationalId + "' مسجل بالفعل لموظف آخر.");
            }

            employee.Status = StatusActive;
            employee.IsDeleted = false;
            employee.CreatedAt = DateTime.Now;
            employee.CreatedBy = CurrentUserId();
            employee.UpdatedAt = DateTime.Now;
            employee.UpdatedBy = CurrentUserId();

            int newId = EmployeeDAL.Insert(employee);
            AuditBLL.Log("Employees.Add", "Employees", newId.ToString(), null, employee);
            return newId;
        }

        /// <summary>Updates an existing employee (permission "Employees.Edit") with field-level history.</summary>
        public static void Update(Employee employee)
        {
            AppContext.RequirePermission("Employees.Edit");
            if (employee == null)
            {
                throw new BusinessException("لا يوجد موظف لحفظه.");
            }
            Validate(employee, false);

            Employee oldEmployee = EmployeeDAL.GetById(employee.EmployeeId);
            if (oldEmployee == null || oldEmployee.IsDeleted == true)
            {
                throw new BusinessException("الموظف غير موجود أو مؤرشف، لا يمكن تعديله.");
            }
            if (!string.Equals(oldEmployee.NationalId, employee.NationalId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(employee.NationalId) &&
                NationalIdExists(employee.NationalId, employee.EmployeeId))
            {
                throw new BusinessException("الرقم القومي '" + employee.NationalId + "' مسجل بالفعل لموظف آخر.");
            }

            employee.UpdatedAt = DateTime.Now;
            employee.UpdatedBy = CurrentUserId();
            string diffText = TextSerializer.Diff(oldEmployee, employee);

            EmployeeDAL.UpdateWithHistory(employee, oldEmployee, diffText, CurrentUserId());

            AuditBLL.Log("Employees.Edit", "Employees", employee.EmployeeId.ToString(), oldEmployee, employee);
        }

        /// <summary>Suspends an employee temporarily (rule 8 — إيقاف الموظف).</summary>
        public static void Suspend(int employeeId, string reason)
        {
            ChangeLifecycle(employeeId, StatusSuspended, "Employees.Suspend", reason);
        }

        /// <summary>Re-activates a suspended employee (rule 8 — إعادة تفعيل الموظف).</summary>
        public static void Activate(int employeeId, string reason)
        {
            ChangeLifecycle(employeeId, StatusActive, "Employees.Activate", reason);
        }

        /// <summary>Archives an employee: flag + status, history is never removed (rule 8/BR-02).</summary>
        public static void Archive(int employeeId, string reason)
        {
            AppContext.RequirePermission("Employees.Archive");
            Employee employee = EmployeeDAL.GetById(employeeId);
            if (employee == null || employee.IsDeleted == true)
            {
                throw new BusinessException("الموظف غير موجود أو تم أرشفته مسبقًا.");
            }
            EmployeeDAL.SoftDelete(employeeId, CurrentUserId());
            string auditReason = string.IsNullOrEmpty(reason) ? "" : " - " + reason;
            RecordHistory(employeeId, "Archive", "Status", employee.Status, StatusArchived, "ارشفة" + auditReason);
            AuditBLL.Log("Employees.Archive", "Employees", employeeId.ToString(), employee, null);
        }

        /// <summary>
        /// Physical delete (permission "Employees.Delete") is allowed only when the
        /// employee has zero activity links; otherwise rule BR-02 applies.
        /// </summary>
        public static void HardDelete(int employeeId)
        {
            AppContext.RequirePermission("Employees.Delete");
            Employee employee = EmployeeDAL.GetById(employeeId);
            if (employee == null)
            {
                throw new BusinessException("الموظف غير موجود.");
            }
            long activityLinks = EmployeeDAL.CountActivityLinks(employeeId);
            if (activityLinks > 0)
            {
                throw new BusinessException(
                    "لا يمكن حذف الموظف '" + employee.EmployeeCode + "' حذفًا نهائيًا لارتباطه بسجلات نظام (" +
                    activityLinks + " سجل). استخدم خيار الأرشفة بدلاً من الحذف.");
            }
            EmployeeDAL.Delete(employeeId);
            AuditBLL.Log("Employees.Delete", "Employees", employeeId.ToString(), employee, null);
        }

        /// <summary>Restores an archived employee back to Active (rule 8 — safe restore, history preserved).</summary>
        public static void Unarchive(int employeeId, string reason)
        {
            AppContext.RequirePermission("Employees.Archive");
            Employee employee = EmployeeDAL.GetById(employeeId);
            if (employee == null)
            {
                throw new BusinessException("الموظف غير موجود.");
            }
            if (employee.IsDeleted != true)
            {
                throw new BusinessException("الموظف غير مؤرشف.");
            }
            EmployeeDAL.Unarchive(employeeId, CurrentUserId());
            string auditReason = string.IsNullOrEmpty(reason) ? "" : " - " + reason;
            RecordHistory(employeeId, "Archive", "Status", StatusArchived, StatusActive, "استعادة من الأرشفة" + auditReason);
            AuditBLL.Log("Employees.Unarchive", "Employees", employeeId.ToString(), employee, null);
        }

        private static void ChangeLifecycle(int employeeId, string newStatus, string auditAction, string reason)
        {
            Employee employee = EmployeeDAL.GetById(employeeId);
            if (employee == null)
            {
                throw new BusinessException("الموظف غير موجود.");
            }
            if (employee.IsDeleted == true)
            {
                throw new BusinessException("الموظف مؤرشف، يرجى استخدام عمليات الأرشفة.");
            }
            string oldStatus = employee.Status;
            EmployeeDAL.SetStatus(employeeId, newStatus, CurrentUserId());
            RecordHistory(employeeId, "StatusChange", "Status", oldStatus, newStatus, reason);
            AuditBLL.Log(auditAction, "Employees", employeeId.ToString(), oldStatus, newStatus);
        }

        private static void RecordHistory(int employeeId, string changeType, string fieldName, string oldValue, string newValue, string extra)
        {
            EmployeeHistory history = new EmployeeHistory();
            history.EmployeeId = employeeId;
            history.ChangeDate = DateTime.Now;
            history.ChangeType = changeType;
            history.FieldName = fieldName;
            history.OldValue = oldValue;
            history.NewValue = newValue + (string.IsNullOrEmpty(extra) ? "" : " - " + extra);
            history.ChangedBy = CurrentUserId();
            EmployeeHistoryDAL.Insert(history);
        }

        /// <summary>Validation per rule 24; Arabic, user-friendly messages.</summary>
        private static void Validate(Employee employee, bool isNew)
        {
            if (string.IsNullOrWhiteSpace(employee.EmployeeCode))
            {
                throw new BusinessException("كود الموظف إلزامي.");
            }
            if (string.IsNullOrWhiteSpace(employee.FullNameAr))
            {
                throw new BusinessException("اسم الموظف بالعربية إلزامي.");
            }
            if (employee.HireDate == null)
            {
                throw new BusinessException("تاريخ التعيين إلزامي.");
            }
            if (employee.HireDate.Value.Date > DateTime.Today)
            {
                throw new BusinessException("تاريخ التعيين لا يمكن أن يكون في المستقبل.");
            }
            if (employee.BirthDate != null && employee.BirthDate.Value.Date >= DateTime.Today)
            {
                throw new BusinessException("تاريخ الميلاد غير صحيح، يجب أن يكون قبل اليوم.");
            }
            if (employee.BirthDate != null && employee.HireDate != null &&
                employee.BirthDate.Value.AddYears(15) > employee.HireDate.Value)
            {
                throw new BusinessException("عمر الموظف عند التعيين أقل من الحد المقبول (15 سنة).");
            }
            if (employee.CompanyId == null)
            {
                throw new BusinessException("الشركة إلزامية.");
            }
            if (employee.DepartmentId == null)
            {
                throw new BusinessException("القسم إلزامي.");
            }
            if (employee.JobTitleId == null)
            {
                throw new BusinessException("المسمى الوظيفي إلزامي.");
            }
            if (employee.EmployeeStatusId == null)
            {
                throw new BusinessException("حالة الموظف إلزامية.");
            }
            if (employee.ContractHours == null || employee.ContractHours < 1 || employee.ContractHours > 12)
            {
                throw new BusinessException("ساعات العقد يجب أن تكون بين 1 و 12 ساعة.");
            }
            if (!string.IsNullOrWhiteSpace(employee.DeviceEnrollNumber) &&
                !IsDigits(employee.DeviceEnrollNumber))
            {
                throw new BusinessException("رقم الموظف في جهاز البصمة يجب أن يكون أرقامًا فقط.");
            }
        }

        private static bool NationalIdExists(string nationalId, int? excludeEmployeeId)
        {
            string sql = "SELECT COUNT(*) FROM Employees WHERE NationalId = ? AND IsDeleted = 0";
            if (excludeEmployeeId != null)
            {
                sql = sql + " AND EmployeeId <> ?";
                return Db.Count(sql, Db.P(nationalId), Db.PNullableId(excludeEmployeeId)) > 0;
            }
            return Db.Count(sql, Db.P(nationalId)) > 0;
        }

        private static bool IsDigits(string text)
        {
            foreach (char digit in text)
            {
                if (digit < '0' || digit > '9')
                {
                    return false;
                }
            }
            return text.Length > 0;
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
