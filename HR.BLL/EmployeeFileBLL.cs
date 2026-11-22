using System;
using System.Collections.Generic;
using System.IO;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Per-employee document attachments: add (with a stored relative path so the
    /// installation stays portable), list, delete. Physical file copy is done by
    /// the UI layer; this BLL validates and persists.
    /// </summary>
    public static class EmployeeFileBLL
    {
        /// <summary>Lists the attachments of one employee.</summary>
        public static List<EmployeeFile> List(int employeeId)
        {
            if (employeeId <= 0)
            {
                throw new BusinessException("الموظف غير محدد.");
            }
            return EmployeeFileDAL.ListByEmployee(employeeId);
        }

        /// <summary>Registers a copied attachment (relative app path) for the employee.</summary>
        public static void Add(int employeeId, string relativePath, string category, string notes)
        {
            if (employeeId <= 0)
            {
                throw new BusinessException("الموظف غير محدد.");
            }
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new BusinessException("مسار الملف مطلوب.");
            }
            if (relativePath.Length > 255)
            {
                throw new BusinessException("مسار الملف أطول من الحد المسموح (255 حرفًا).");
            }
            if (!string.IsNullOrEmpty(category) && category.Length > 50)
            {
                throw new BusinessException("التصنيف أطول من الحد المسموح (50 حرفًا).");
            }
            if (!string.IsNullOrEmpty(notes) && notes.Length > 500)
            {
                throw new BusinessException("الملاحظات أطول من الحد المسموح (500 حرف).");
            }
            EmployeeFile file = new EmployeeFile();
            file.EmployeeId = employeeId;
            file.FilePath = relativePath.Trim();
            file.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            file.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            EmployeeFileDAL.Insert(file, CurrentUserId());
            AuditBLL.Log("EmployeeFile.Add", "EmployeeFiles", employeeId.ToString(), null, file.FilePath);
        }

        private static int CurrentUserId()
        {
            return AppContext.CurrentUser == null ? 0 : AppContext.CurrentUser.UserId;
        }

        /// <summary>Removes an attachment record (the physical file stays on disk).</summary>
        public static void Delete(int fileId, int employeeId)
        {
            List<EmployeeFile> files = EmployeeFileDAL.ListByEmployee(employeeId);
            EmployeeFile stored = null;
            foreach (EmployeeFile file in files)
            {
                if (file.FileId == fileId)
                {
                    stored = file;
                    break;
                }
            }
            if (stored == null)
            {
                throw new BusinessException("المرفق غير موجود لهذا الموظف.");
            }
            EmployeeFileDAL.Delete(fileId);
            AuditBLL.Log("EmployeeFile.Delete", "EmployeeFiles", employeeId.ToString(), stored.FilePath, null);
        }

        /// <summary>Resolves a stored relative path against the application folder.</summary>
        public static string ResolvePath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return null;
            }
            string candidate = storedPath.Trim();
            if (Path.IsPathRooted(candidate))
            {
                return File.Exists(candidate) ? candidate : null;
            }
            string combined = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, candidate);
            return File.Exists(combined) ? combined : null;
        }

        /// <summary>Target folder under the application directory (relative "Attachments").</summary>
        public static string AttachmentsFolder()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
        }
    }
}
