using System;
using System.Collections.Generic;
using System.IO;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Backup / restore (rule 23): timestamped MDB file copies, logged in
    /// BackupLogs and audited. Restore always creates a "PreRestore" safety
    /// backup first. Automatic backups follow Backup.AutoEnabled +
    /// Backup.AutoTime from System Settings (never hardcoded values — rule 36).
    /// </summary>
    public static class BackupBLL
    {
        public const string TypeManual = "Manual";
        public const string TypeAuto = "Auto";
        public const string TypePreRestore = "PreRestore";

        /// <summary>Default destination: DataFolder\Backups (created on demand).</summary>
        public static string DefaultBackupFolder()
        {
            string folder = Path.Combine(AppContext.DataFolder, "Backups");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        /// <summary>Copies the live MDB to a timestamped file and logs the result.</summary>
        public static string Backup(string targetFolder, string backupType)
        {
            if (backupType != TypeAuto)
            {
                AppContext.RequirePermission("Backup.Execute");
            }
            string databasePath = AppContext.DatabasePath;
            if (string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
            {
                throw new BusinessException("لم يتم العثور على ملف قاعدة البيانات.");
            }
            string folder = string.IsNullOrWhiteSpace(targetFolder) ? DefaultBackupFolder() : targetFolder.Trim();
            if (!Directory.Exists(folder))
            {
                throw new BusinessException("مجلد النسخ غير موجود: " + folder);
            }
            string fileName = "ModernTime_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mdb";
            string targetPath = Path.Combine(folder, fileName);
            File.Copy(databasePath, targetPath, true);
            FileInfo info = new FileInfo(targetPath);

            BackupLog backup = new BackupLog();
            backup.BackupType = backupType;
            backup.FilePath = targetPath;
            backup.FileSize = (int)Math.Min(info.Length, int.MaxValue);
            backup.CreatedAt = DateTime.Now;
            backup.CreatedBy = AppContext.CurrentUser != null ? (int?)AppContext.CurrentUser.UserId : null;
            backup.Status = "Completed";
            BackupDAL.Insert(backup);

            AuditBLL.Log("Backup.Create", "BackupLogs", backupType, null, targetPath);
            Logger.LogInfo("BackupBLL.Backup", backupType + " -> " + targetPath);
            return targetPath;
        }

        /// <summary>
        /// Restores a chosen backup: verifies the file, takes a PreRestore
        /// safety backup of the current database, copies the backup over the
        /// live file and logs the restore. Other workstations must shut down
        /// the application while restoring (single-user maintenance action).
        /// </summary>
        public static string Restore(string backupPath)
        {
            AppContext.RequirePermission("Backup.Execute");
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            {
                throw new BusinessException("ملف النسخة المحدد غير موجود.");
            }
            if (string.Equals(Path.GetFullPath(backupPath), Path.GetFullPath(AppContext.DatabasePath),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("الملف المحدد هو ملف قاعدة البيانات الحالي نفسه.");
            }
            string safetyPath = Backup(DefaultBackupFolder(), TypePreRestore);
            File.Copy(backupPath, AppContext.DatabasePath, true);

            BackupLog restore = new BackupLog();
            restore.BackupType = "Restore";
            restore.FilePath = AppContext.DatabasePath;
            restore.CreatedAt = DateTime.Now;
            restore.CreatedBy = AppContext.CurrentUser != null ? (int?)AppContext.CurrentUser.UserId : null;
            restore.Status = "Restored";
            restore.RestorePath = backupPath;
            BackupDAL.Insert(restore);

            AuditBLL.Log("Backup.Restore", "BackupLogs", "Restore", null, backupPath);
            Logger.LogInfo("BackupBLL.Restore", "restored from " + backupPath + " (safety: " + safetyPath + ")");
            return safetyPath;
        }

        /// <summary>Backup files present in one folder (newest first).</summary>
        public static List<FileInfo> ListBackupFiles(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return new List<FileInfo>();
            }
            List<FileInfo> files = new List<FileInfo>(new DirectoryInfo(folder).GetFiles("*.mdb"));
            files.AddRange(new DirectoryInfo(folder).GetFiles("*.bak"));
            files.Sort(delegate(FileInfo left, FileInfo right)
            {
                return right.LastWriteTime.CompareTo(left.LastWriteTime);
            });
            return files;
        }

        /// <summary>Recent BackupLogs rows (permission "Backup.Execute").</summary>
        public static List<BackupLog> GetLog(int maxRows)
        {
            AppContext.RequirePermission("Backup.Execute");
            return BackupDAL.Search(maxRows);
        }

        /// <summary>
        /// Runs the daily automatic backup when due (called from a timer):
        /// enabled by Backup.AutoEnabled, no later than Backup.AutoTime, once
        /// per day (last completed Auto backup must be from an earlier day).
        /// Failures are logged, never shown to block work.
        /// </summary>
        public static void RunAutoBackupIfDue()
        {
            try
            {
                bool enabled = SettingsBLL.GetBool("Backup.AutoEnabled", true);
                if (!enabled)
                {
                    return;
                }
                string[] parts = (SettingsBLL.GetString("Backup.AutoTime", "23:30") ?? "23:30").Split(':');
                int hour, minute;
                if (parts.Length >= 2 && int.TryParse(parts[0], out hour) && int.TryParse(parts[1], out minute))
                {
                    DateTime due = DateTime.Now.Date.AddHours(hour).AddMinutes(minute);
                    if (DateTime.Now < due)
                    {
                        return;
                    }
                }
                DateTime? lastAuto = BackupDAL.LastBackupTime(TypeAuto);
                if (lastAuto != null && lastAuto.Value.Date >= DateTime.Now.Date)
                {
                    return;
                }
                Backup(DefaultBackupFolder(), TypeAuto);
            }
            catch (Exception autoBackupFailure)
            {
                Logger.LogError(autoBackupFailure, "BackupBLL.RunAutoBackupIfDue failed");
            }
        }
    }
}
