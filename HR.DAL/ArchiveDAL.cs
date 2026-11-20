using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>ArchiveLogs access plus raw read/cut of archive-eligible tables (rule 28).</summary>
    public static class ArchiveDAL
    {
        /// <summary>Number of rows in the table older than the cutoff date.</summary>
        public static int CountRows(string tableName, string dateColumn, DateTime toDate)
        {
            object result = Db.ExecuteScalar(
                "SELECT COUNT(*) FROM [" + tableName + "] WHERE [" + dateColumn + "] < ?",
                Db.PDate(toDate));
            return result == null ? 0 : Convert.ToInt32(result);
        }

        /// <summary>Raw rows older than the cutoff: header once, then string cells per row.</summary>
        public static ArchiveBatch LoadForExport(string tableName, string dateColumn, DateTime toDate)
        {
            ArchiveBatch batch = new ArchiveBatch();
            string sql = "SELECT * FROM [" + tableName + "] WHERE [" + dateColumn + "] < ?";
            batch.Rows = Db.ReadList(sql, delegate(OleDbDataReader reader)
            {
                List<string> cells = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (batch.Header.Count < reader.FieldCount)
                    {
                        batch.Header.Add(reader.GetName(i));
                    }
                    cells.Add(reader.IsDBNull(i) ? string.Empty :
                        Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture));
                }
                return cells;
            }, Db.PDate(toDate));
            return batch;
        }

        /// <summary>Deletes the archived range and writes the ArchiveLogs row in one transaction.</summary>
        public static int ArchiveRange(string tableName, string dateColumn, DateTime toDate,
            int rowsCount, string archiveFile, int? archivedBy)
        {
            int deleted = Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                int affected = Db.ExecuteNonQueryOn(connection, transaction,
                    "DELETE FROM [" + tableName + "] WHERE [" + dateColumn + "] < ?",
                    Db.PDate(toDate));
                Db.ExecuteNonQueryOn(connection, transaction,
                    "INSERT INTO ArchiveLogs (TableName, FromDate, ToDate, RowsCount, ArchiveFile, ArchivedBy, CreatedAt) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?)",
                    Db.P(tableName),
                    Db.PDate((DateTime?)null),
                    Db.PDate(toDate),
                    Db.P(rowsCount),
                    Db.P(archiveFile),
                    Db.PNullableId(archivedBy),
                    Db.PDate(DateTime.Now));
                return affected;
            });
            return deleted;
        }

        /// <summary>Recent archive operations for the maintenance screen.</summary>
        public static List<ArchiveLog> Search(int maxRows)
        {
            return Db.ReadList(
                "SELECT TOP " + maxRows + " ArchiveId, TableName, FromDate, ToDate, RowsCount, ArchiveFile, ArchivedBy, CreatedAt " +
                "FROM ArchiveLogs ORDER BY CreatedAt DESC, ArchiveId DESC",
                delegate(OleDbDataReader reader)
                {
                    ArchiveLog archive = new ArchiveLog();
                    archive.ArchiveId = reader.GetInt32(0);
                    archive.TableName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    archive.FromDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                    archive.ToDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                    archive.RowsCount = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                    archive.ArchiveFile = reader.IsDBNull(5) ? null : reader.GetString(5);
                    archive.ArchivedBy = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
                    archive.CreatedAt = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7);
                    return archive;
                });
        }
    }
}
