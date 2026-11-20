using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>BackupLogs table access (rule 23 — backup history).</summary>
    public static class BackupDAL
    {
        public static void Insert(BackupLog backup)
        {
            string sql = "INSERT INTO BackupLogs (BackupType, FilePath, FileSize, CreatedAt, CreatedBy, Status, RestorePath) " +
                         "VALUES (?, ?, ?, ?, ?, ?, ?)";
            Db.ExecuteNonQuery(sql,
                Db.P(backup.BackupType),
                Db.P(backup.FilePath),
                Db.P(backup.FileSize),
                Db.PDateRequired(backup.CreatedAt ?? DateTime.Now),
                Db.PNullableId(backup.CreatedBy),
                Db.P(backup.Status),
                Db.P(backup.RestorePath));
        }

        /// <summary>Sets the restore target path on the row created ahead of a restore.</summary>
        public static void MarkRestored(int backupId, string restoredFromPath)
        {
            Db.ExecuteNonQuery("UPDATE BackupLogs SET RestorePath = ? WHERE BackupId = ?",
                Db.P(restoredFromPath), Db.P(backupId));
        }

        /// <summary>Latest backup time for one type (Auto / Manual / PreRestore), or null.</summary>
        public static DateTime? LastBackupTime(string backupType)
        {
            object result = Db.ExecuteScalar(
                "SELECT MAX(CreatedAt) FROM BackupLogs WHERE BackupType = ? AND Status = 'Completed'",
                Db.P(backupType));
            return result is DateTime ? (DateTime)result : (DateTime?)null;
        }

        /// <summary>Recent backup history for the maintenance screen.</summary>
        public static List<BackupLog> Search(int maxRows)
        {
            return Db.ReadList(
                "SELECT TOP " + maxRows + " BackupId, BackupType, FilePath, FileSize, CreatedAt, CreatedBy, Status, RestorePath " +
                "FROM BackupLogs ORDER BY CreatedAt DESC, BackupId DESC",
                delegate(OleDbDataReader reader)
                {
                    BackupLog backup = new BackupLog();
                    backup.BackupId = reader.GetInt32(0);
                    backup.BackupType = reader.IsDBNull(1) ? null : reader.GetString(1);
                    backup.FilePath = reader.IsDBNull(2) ? null : reader.GetString(2);
                    backup.FileSize = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                    backup.CreatedAt = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
                    backup.CreatedBy = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                    backup.Status = reader.IsDBNull(6) ? null : reader.GetString(6);
                    backup.RestorePath = reader.IsDBNull(7) ? null : reader.GetString(7);
                    return backup;
                });
        }
    }
}
