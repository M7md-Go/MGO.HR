using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Database administration and monitoring for the dedicated DB tab
    /// (permission "Settings.Manage" — administrators only). Read-only
    /// monitoring plus one guarded maintenance action (old-log cleanup) that
    /// always takes a PreCleanup safety backup first, confirms through the
    /// caller, writes the audit trail and reports per-table deleted counts.
    /// </summary>
    public static class DatabaseBLL
    {
        /// <summary>BLL-side view of a table column (keeps HR.UI free of any HR.DAL reference).</summary>
        public class ColumnView
        {
            public string Name { get; set; }
            public int Ordinal { get; set; }
            public string TypeName { get; set; }
            public int? MaxChars { get; set; }
            public bool Nullable { get; set; }
            public bool PrimaryKey { get; set; }
        }

        /// <summary>BLL-side view of a table index.</summary>
        public class IndexView
        {
            public string IndexName { get; set; }
            public string ColumnName { get; set; }
            public bool Unique { get; set; }
            public bool Primary { get; set; }
        }

        /// <summary>BLL-side view of one slow statement recorded by the DAL layer.</summary>
        public class SlowQueryView
        {
            public string Sql { get; set; }
            public long ElapsedMs { get; set; }
            public DateTime At { get; set; }
        }

        /// <summary>Whitelisted maintenance tables with their date column (nothing else is ever mass-deleted).</summary>
        private static readonly Dictionary<string, string> CleanupTables =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static DatabaseBLL()
        {
            CleanupTables["FingerprintLogs"] = "LogTime";
            CleanupTables["AuditLogs"] = "ActionTime";
            CleanupTables["LoginHistory"] = "AttemptTime";
        }

        public class DatabaseInfo
        {
            public string Path { get; set; }
            public string BackupFolder { get; set; }
            public long SizeBytes { get; set; }
            public int TableCount { get; set; }
            public string ConnectionError { get; set; }
            public DateTime? LastManualBackup { get; set; }
            public DateTime? LastAutoBackup { get; set; }
            public int RetentionDays { get; set; }
            public int SlowQueryThresholdMs { get; set; }
        }

        private static void RequireAdmin()
        {
            AppContext.RequirePermission("Settings.Manage");
        }

        /// <summary>Snapshot of database facts for the monitoring screen (never throws for a closed DB).</summary>
        public static DatabaseInfo GetInfo()
        {
            RequireAdmin();
            DatabaseInfo info = new DatabaseInfo();
            info.Path = AppContext.DatabasePath ?? "";
            info.BackupFolder = BackupBLL.DefaultBackupFolder();
            info.SizeBytes = BackupBLL.DatabaseSizeBytes();
            info.ConnectionError = DbSchema.TestConnection();
            try
            {
                info.TableCount = DbSchema.ListTables().Count;
            }
            catch (Exception)
            {
                info.TableCount = 0;
            }
            try
            {
                info.LastManualBackup = BackupDAL.LastBackupTime(BackupBLL.TypeManual);
                info.LastAutoBackup = BackupDAL.LastBackupTime(BackupBLL.TypeAuto);
            }
            catch (Exception)
            {
                // log tables are secondary facts; the rest of the screen stays useful
            }
            info.RetentionDays = SettingsBLL.GetInt("Backup.RetentionDays", 30);
            info.SlowQueryThresholdMs = QueryMonitor.ThresholdMs;
            return info;
        }

        public static List<string> ListTables()
        {
            RequireAdmin();
            return DbSchema.ListTables();
        }

        public static long CountRows(string tableName)
        {
            RequireAdmin();
            return DbSchema.CountRows(tableName);
        }

        public static List<ColumnView> GetColumns(string tableName)
        {
            RequireAdmin();
            List<ColumnView> result = new List<ColumnView>();
            foreach (DbSchema.ColumnInfo column in DbSchema.GetColumns(tableName))
            {
                ColumnView view = new ColumnView();
                view.Name = column.Name;
                view.Ordinal = column.Ordinal;
                view.TypeName = column.TypeName;
                view.MaxChars = column.MaxChars;
                view.Nullable = column.Nullable;
                view.PrimaryKey = column.PrimaryKey;
                result.Add(view);
            }
            return result;
        }

        public static List<IndexView> GetIndexes(string tableName)
        {
            RequireAdmin();
            List<IndexView> result = new List<IndexView>();
            foreach (DbSchema.IndexInfo index in DbSchema.GetIndexes(tableName))
            {
                IndexView view = new IndexView();
                view.IndexName = index.IndexName;
                view.ColumnName = index.ColumnName;
                view.Unique = index.Unique;
                view.Primary = index.Primary;
                result.Add(view);
            }
            return result;
        }

        public static List<string> Relationships()
        {
            RequireAdmin();
            return DbSchema.TryRelationships();
        }

        public static System.Data.DataTable ReadRows(string tableName, int maxRows)
        {
            RequireAdmin();
            return DbSchema.ReadRows(tableName, maxRows);
        }

        public static string TestConnection()
        {
            RequireAdmin();
            return DbSchema.TestConnection();
        }

        public static List<SlowQueryView> SlowQueries()
        {
            RequireAdmin();
            List<SlowQueryView> result = new List<SlowQueryView>();
            foreach (QueryMonitor.SlowQuery query in QueryMonitor.Snapshot())
            {
                SlowQueryView view = new SlowQueryView();
                view.Sql = query.Sql;
                view.ElapsedMs = query.ElapsedMs;
                view.At = query.At;
                result.Add(view);
            }
            return result;
        }

        public static void ClearSlowLog()
        {
            RequireAdmin();
            QueryMonitor.Clear();
        }

        /// <summary>Names + row counts of the whitelisted maintenance tables (live facts for the dialog).</summary>
        public static List<KeyValuePair<string, long>> CleanupCandidates()
        {
            RequireAdmin();
            List<KeyValuePair<string, long>> rows = new List<KeyValuePair<string, long>>();
            foreach (string table in CleanupTables.Keys)
            {
                rows.Add(new KeyValuePair<string, long>(table, DbSchema.CountRows(table)));
            }
            return rows;
        }

        /// <summary>
        /// Deletes old rows from whitelisted log tables (older than beforeDate).
        /// Order of operations is strict: permission → validation → safety
        /// PreCleanup backup → transactional deletes → audit. Data before the
        /// operation always remains available in the safety backup copy.
        /// </summary>
        public static Dictionary<string, int> CleanupOlderThan(List<string> tableNames, DateTime beforeDate)
        {
            RequireAdmin();
            if (tableNames == null || tableNames.Count == 0)
            {
                throw new BusinessException("اختر جدولاً واحداً على الأقل للتنظيف.");
            }
            if (beforeDate.Date >= DateTime.Now.Date)
            {
                throw new BusinessException("حد التنظيف يجب أن يكون تاريخاً سابقاً (احتفاظ اليوم وبياناته).");
            }
            string dateColumn;
            List<string> valid = new List<string>();
            foreach (string tableName in tableNames)
            {
                if (!CleanupTables.TryGetValue(tableName ?? "", out dateColumn))
                {
                    throw new BusinessException("الجدول «" + tableName + "» ليس ضمن جداول التنظيف المسموحة.");
                }
                valid.Add(tableName.Trim());
            }
            // safety first: the cleanup point of no return gets a full backup
            BackupBLL.BackupForMaintenance(BackupBLL.DefaultBackupFolder(), BackupBLL.TypePreCleanup);

            Dictionary<string, int> deleted = new Dictionary<string, int>();
            foreach (string tableName in valid)
            {
                CleanupTables.TryGetValue(tableName, out dateColumn);
                deleted[tableName] = Db.ExecuteNonQuery("DELETE FROM [" + tableName + "] WHERE [" +
                    dateColumn + "] < ?", Db.PDate(beforeDate.Date));
            }
            AuditBLL.Log("Db.Cleanup", "Log tables", string.Join(", ", valid.ToArray()), null,
                "Before=" + beforeDate.ToString("yyyy-MM-dd") + " Deleted=" + Sum(deleted));
            return deleted;
        }

        private static int Sum(Dictionary<string, int> values)
        {
            int total = 0;
            foreach (int value in values.Values)
            {
                total += value;
            }
            return total;
        }
    }
}
