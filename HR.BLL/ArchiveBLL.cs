using System;
using System.Collections.Generic;
using System.IO;
using HR.DAL;
using HR.Entities;
using HR.Helpers;
using HR.Services;

namespace HR.BLL
{
    /// <summary>
    /// Data archiving (rule 28): exports old rows to an Excel file, then removes
    /// them from the live MDB in one transaction. Only whitelisted high-volume
    /// tables are archived; historical attendance data is never deleted directly
    /// without a preceding export. The active period (default 3 years) comes
    /// from the "Arch.ActivePeriodDays" setting, never hardcoded.
    /// </summary>
    public static class ArchiveBLL
    {
        private const string TableAttendance = "Attendance";
        private const string TableFingerprints = "FingerprintLogs";

        /// <summary>Returns archivable tables as display-name / table-name pairs.</summary>
        public static List<KeyValuePair<string, string>> GetArchiveTables()
        {
            List<KeyValuePair<string, string>> tables = new List<KeyValuePair<string, string>>();
            tables.Add(new KeyValuePair<string, string>("سجلات الحضور", TableAttendance));
            tables.Add(new KeyValuePair<string, string>("سجلات البصمة الخام", TableFingerprints));
            return tables;
        }

        /// <summary>Cutoff date: today minus the configured active period (default 1095 days = 3 years).</summary>
        public static DateTime GetCutoff()
        {
            int activeDays = SettingsBLL.GetInt("Arch.ActivePeriodDays", 1095);
            if (activeDays <= 0)
            {
                activeDays = 1095;
            }
            return DateTime.Today.AddDays(-activeDays);
        }

        /// <summary>Count of rows older than the cutoff for one archivable table — permission "Archive.Execute".</summary>
        public static int PreviewCount(string tableName)
        {
            AppContext.RequirePermission("Archive.Execute");
            return ArchiveDAL.CountRows(tableName, ResolveDateColumn(tableName), GetCutoff());
        }

        /// <summary>
        /// Archives one table: cuts rows older than the cutoff into an Excel file,
        /// deletes them inside one transaction and records the operation —
        /// permission "Archive.Execute".
        /// </summary>
        public static string Execute(string tableName)
        {
            AppContext.RequirePermission("Archive.Execute");
            string dateColumn = ResolveDateColumn(tableName);
            DateTime cutoff = GetCutoff();

            int rowsCount = ArchiveDAL.CountRows(tableName, dateColumn, cutoff);
            if (rowsCount == 0)
            {
                throw new BusinessException("لا توجد بيانات أقدم من " + cutoff.ToString("yyyy-MM-dd") + " في هذا الجدول.");
            }

            ArchiveBatch batch = ArchiveDAL.LoadForExport(tableName, dateColumn, cutoff);
            string folder = Path.Combine(AppContext.DataFolder, "Archive");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            string filePath = Path.Combine(folder,
                tableName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml");

            ReportData report = new ReportData();
            report.Title = "أرشفة " + DisplayArabicName(tableName);
            report.PeriodText = "البيانات الأقدم من " + cutoff.ToString("yyyy-MM-dd") + " — عدد السجلات: " + rowsCount;
            foreach (string header in batch.Header)
            {
                report.AddColumn(header, header, 110);
            }
            foreach (List<string> cells in batch.Rows)
            {
                report.Rows.Add(cells);
            }
            ExcelWriter.Export(report, filePath);

            int? archivedBy = AppContext.CurrentUser != null ? (int?)AppContext.CurrentUser.UserId : null;
            ArchiveDAL.ArchiveRange(tableName, dateColumn, cutoff, rowsCount, filePath, archivedBy);
            AuditBLL.Log("Archive.Execute", tableName, null, rowsCount + " rows", filePath);
            return "تمت أرشفة " + batch.Rows.Count + " سجلًا — الملف: " + filePath;
        }

        /// <summary>Recent archive operations — permission "Archive.Execute".</summary>
        public static List<ArchiveLog> GetLog(int maxRows)
        {
            AppContext.RequirePermission("Archive.Execute");
            return ArchiveDAL.Search(maxRows);
        }

        private static string ResolveDateColumn(string tableName)
        {
            if (string.Equals(tableName, TableAttendance, StringComparison.OrdinalIgnoreCase))
            {
                return "AttDate";
            }
            if (string.Equals(tableName, TableFingerprints, StringComparison.OrdinalIgnoreCase))
            {
                return "LogTime";
            }
            throw new BusinessException("جدول غير قابل للأرشفة: " + tableName);
        }

        private static string DisplayArabicName(string tableName)
        {
            if (string.Equals(tableName, TableAttendance, StringComparison.OrdinalIgnoreCase))
            {
                return "سجلات الحضور";
            }
            if (string.Equals(tableName, TableFingerprints, StringComparison.OrdinalIgnoreCase))
            {
                return "سجلات البصمة الخام";
            }
            return tableName;
        }
    }
}
