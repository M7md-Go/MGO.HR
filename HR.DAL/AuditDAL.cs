using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>AuditLogs table access (rule 22 — Audit Log).</summary>
    public static class AuditDAL
    {
        public static void Insert(AuditLog audit)
        {
            string sql = "INSERT INTO AuditLogs (UserId, ActionName, ActionTime, TableName, RecordId, OldValue, NewValue, MachineName, IpAddress) " +
                         "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";
            Db.ExecuteNonQuery(sql,
                Db.PNullableId(audit.UserId),
                Db.P(audit.ActionName),
                Db.PDateRequired(audit.ActionTime ?? DateTime.Now),
                Db.P(audit.TableName),
                Db.P(audit.RecordId),
                Db.P(audit.OldValue),
                Db.P(audit.NewValue),
                Db.P(audit.MachineName),
                Db.P(audit.IpAddress));
        }

        private static AuditLog MapRow(OleDbDataReader reader)
        {
            AuditLog audit = new AuditLog();
            audit.AuditId = reader.GetInt32(0);
            audit.UserId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            audit.ActionName = reader.IsDBNull(2) ? null : reader.GetString(2);
            audit.ActionTime = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
            audit.TableName = reader.IsDBNull(4) ? null : reader.GetString(4);
            audit.RecordId = reader.IsDBNull(5) ? null : reader.GetString(5);
            audit.OldValue = reader.IsDBNull(6) ? null : reader.GetString(6);
            audit.NewValue = reader.IsDBNull(7) ? null : reader.GetString(7);
            audit.MachineName = reader.IsDBNull(8) ? null : reader.GetString(8);
            audit.IpAddress = reader.IsDBNull(9) ? null : reader.GetString(9);
            audit.Username = reader.IsDBNull(10) ? null : reader.GetString(10);
            return audit;
        }

        private const string SelectColumns =
            "a.AuditId, a.UserId, a.ActionName, a.ActionTime, a.TableName, a.RecordId, a.OldValue, a.NewValue, a.MachineName, a.IpAddress, u.Username";

        /// <summary>Recent audit trail with optional filters (action/table/date range), joined to Users for the username.</summary>
        public static List<AuditLog> Search(string actionName, string tableName, DateTime? fromDate, DateTime? toDate, int maxRows)
        {
            StringBuilder sql = new StringBuilder(
                "SELECT TOP " + maxRows + " " + SelectColumns +
                " FROM (AuditLogs a LEFT JOIN Users u ON a.UserId = u.UserId) WHERE 1 = 1");
            List<OleDbParameter> parameters = new List<OleDbParameter>();

            if (!string.IsNullOrWhiteSpace(actionName))
            {
                sql.Append(" AND ActionName = ?");
                parameters.Add(Db.P(actionName));
            }
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                sql.Append(" AND TableName = ?");
                parameters.Add(Db.P(tableName));
            }
            if (fromDate != null)
            {
                sql.Append(" AND ActionTime >= ?");
                parameters.Add(Db.PDateRequired(fromDate));
            }
            if (toDate != null)
            {
                sql.Append(" AND ActionTime < ?");
                parameters.Add(Db.PDateRequired(toDate.Value.AddDays(1)));
            }
            sql.Append(" ORDER BY ActionTime DESC, AuditId DESC");
            return Db.ReadList(sql.ToString(), MapRow, parameters.ToArray());
        }
    }
}
