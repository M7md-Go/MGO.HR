using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        public const string TypePreCleanup = "PreCleanup";
        private const string FilePrefix = "ModernTime_";

        /// <summary>
        /// Backup destination (rule 36 — configurable, not hardcoded): the
        /// "Backup.Folder" AppSettings key overrides the default
        /// DataFolder\Backups; the folder is created on demand.
        /// </summary>
        public static string DefaultBackupFolder()
        {
            string configured = (SettingsBLL.GetString("Backup.Folder", "") ?? "").Trim();
            string folder = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.DataFolder, "Backups")
                : configured;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        /// <summary>Permission-checked backup entry point.</summary>
        public static string Backup(string targetFolder, string backupType)
        {
            if (backupType != TypeAuto)
            {
                AppContext.RequirePermission("Backup.Execute");
            }
            return BackupInternal(targetFolder, backupType);
        }

        /// <summary>
        /// Safety backup for privileged maintenance tools (data cleanup):
        /// the caller enforces its own permission (Settings.Manage), so this
        /// entry point does not demand Backup.Execute as well.
        /// </summary>
        public static string BackupForMaintenance(string targetFolder, string backupType)
        {
            return BackupInternal(targetFolder, backupType);
        }

        /// <summary>Shared backup body: timestamped copy + BackupLogs row + audit + retention purge.</summary>
        private static string BackupInternal(string targetFolder, string backupType)
        {
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
            string fileName = FilePrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mdb";
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

            PurgeExpiredBackups(folder);
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
            // never restore an invalid copy: explicit Arabic refusal, current data stays untouched
            string invalidReason = VerifyBackupFile(backupPath);
            if (invalidReason != null)
            {
                throw new BusinessException("رفض استعادة نسخة غير صالحة: " + invalidReason +
                    "\nبياناتك الحالية لم تُلمس. اختر نسخة صالحة أو أنشئ نسخة جديدة أولًا.");
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

        /// <summary>Current size of the MDB database file in bytes (0 when missing).</summary>
        public static long DatabaseSizeBytes()
        {
            try
            {
                FileInfo info = new FileInfo(AppContext.DatabasePath);
                return info.Exists ? info.Length : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Compacts the Access database using the Jet JRO component (late-bound COM,
        /// no new assembly reference): compacts to a temp file, keeps a safety copy
        /// of the original beside it, then swaps. Throws Arabic BusinessException
        /// when JRO is unavailable.
        /// </summary>
        public static void CompactDatabase()
        {
            string sourcePath = AppContext.DatabasePath;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                throw new BusinessException("ملف قاعدة البيانات غير موجود.");
            }
            string tempPath = sourcePath + ".compact.tmp";
            string backupPath = sourcePath + ".before-compact.bak";
            string sourceConn = Db.BuildConnectionString(sourcePath);
            string targetConn = Db.BuildConnectionString(tempPath);
            Type jetEngineType = Type.GetTypeFromProgID("JRO.JetEngine");
            if (jetEngineType == null)
            {
                throw new BusinessException("مكوّن ضغط قاعدة Jet (JRO) غير متوفر على هذا الجهاز. " +
                    "ثبّت Microsoft Jet 4.0 Service Pack 8 أو قم بالنسخ الاحتياطي يدويًا بدلًا من الضغط.");
            }
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            object engine = null;
            try
            {
                engine = Activator.CreateInstance(jetEngineType);
                jetEngineType.InvokeMember("CompactDatabase", BindingFlags.InvokeMethod, null, engine,
                    new object[] { sourceConn, targetConn });
            }
            catch (Exception unexpected)
            {
                throw new BusinessException("فشل ضغط قاعدة البيانات: " + unexpected.Message);
            }
            finally
            {
                if (engine != null)
                {
                    try { System.Runtime.InteropServices.Marshal.ReleaseComObject(engine); }
                    catch (Exception) { }
                }
            }
            // swap: original -> safety copy, compacted -> original
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            File.Move(sourcePath, backupPath);
            try
            {
                File.Move(tempPath, sourcePath);
            }
            catch (Exception)
            {
                File.Move(backupPath, sourcePath); // roll back on any failure
                throw;
            }
            try { File.Delete(backupPath); }
            catch (Exception) { }
            Db.ResetConnectionString();
        }
    }
}
