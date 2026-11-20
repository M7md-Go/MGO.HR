using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Raw fingerprint logs (rule 9): FingerprintLogs keeps every punch exactly
    /// as the device delivered it (enroll code, time, in/out flag, source
    /// device); EmployeeId is a resolved link that stays NULL while the enroll
    /// code has no employee, so no record is ever lost. Deduplication relies on
    /// the UQ_Log unique constraint (EmployeeId, LogTime, InOutFlag) plus a
    /// prefilter set built in the same key format (BuildKey).
    /// </summary>
    public static class FingerprintDAL
    {
        /// <summary>
        /// Dedup key identical for EmployeeId-mapped rows (UQ_Log domain) and
        /// for unmapped rows (EmployeeId NULL, matched by EmpDevCode instead).
        /// </summary>
        public static string BuildKey(int? employeeId, string enrollCode, DateTime logTime, int? inOutFlag)
        {
            return (employeeId != null ? employeeId.Value : -1)
                + "|" + (employeeId == null ? (enrollCode ?? string.Empty) : string.Empty)
                + "|" + logTime.Ticks + "|" + (inOutFlag ?? 0);
        }

        /// <summary>Existing keys inside a time window (the download batch range).</summary>
        public static HashSet<string> LoadExistingKeys(DateTime fromTime, DateTime toTime)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            using (OleDbConnection connection = Db.CreateConnection())
            using (OleDbCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT EmployeeId, EmpDevCode, LogTime, InOutFlag FROM FingerprintLogs WHERE LogTime >= ? AND LogTime <= ?";
                command.Parameters.Add(Db.PDate(fromTime));
                command.Parameters.Add(Db.PDate(toTime));
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int? employeeId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                        string enrollCode = reader.IsDBNull(1) ? null : reader.GetString(1);
                        DateTime logTime = reader.GetDateTime(2);
                        int? inOutFlag = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                        keys.Add(BuildKey(employeeId, enrollCode, logTime, inOutFlag));
                    }
                }
            }
            return keys;
        }

        /// <summary>
        /// Inserts new logs inside one transaction (bulk path). When the batch
        /// fails (typically a duplicate that raced another workstation), the
        /// transaction is rolled back and every row is retried after an exact
        /// existence check, so one bad row never loses the rest of the batch.
        /// </summary>
        public static int InsertLogs(int deviceId, List<FingerprintLog> logs)
        {
            int inserted = 0;
            using (OleDbConnection connection = Db.CreateConnection())
            using (OleDbTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    using (OleDbCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            "INSERT INTO FingerprintLogs (DeviceId, EmployeeId, EmpDevCode, LogTime, InOutFlag, SourceFile, CreatedAt) " +
                            "VALUES (?, ?, ?, ?, ?, ?, Now())";
                        OleDbParameter deviceParameter = command.Parameters.Add(Db.P(deviceId));
                        OleDbParameter employeeParameter = command.Parameters.Add(Db.PNullableId(null));
                        OleDbParameter enrollParameter = command.Parameters.Add(Db.P("x"));
                        OleDbParameter timeParameter = command.Parameters.Add(Db.PDate(DateTime.Now));
                        OleDbParameter flagParameter = command.Parameters.Add(Db.P(0));
                        OleDbParameter sourceParameter = command.Parameters.Add(Db.P("x"));
                        foreach (FingerprintLog logEntry in logs)
                        {
                            deviceParameter.Value = deviceId;
                            employeeParameter.Value = logEntry.EmployeeId != null
                                ? (object)logEntry.EmployeeId.Value
                                : DBNull.Value;
                            enrollParameter.Value = (object)(logEntry.EmpDevCode ?? string.Empty);
                            timeParameter.Value = logEntry.LogTime != null ? (object)logEntry.LogTime.Value : DateTime.Now;
                            flagParameter.Value = (object)(logEntry.InOutFlag ?? 0);
                            sourceParameter.Value = (object)(logEntry.SourceFile ?? string.Empty);
                            command.ExecuteNonQuery();
                            inserted++;
                        }
                    }
                    transaction.Commit();
                    return inserted;
                }
                catch (OleDbException)
                {
                    transaction.Rollback();
                }
            }
            foreach (FingerprintLog logEntry in logs)
            {
                if (LogExists(logEntry))
                {
                    continue;
                }
                inserted += RowInsert(deviceId, logEntry);
            }
            return inserted;
        }

        private static bool LogExists(FingerprintLog logEntry)
        {
            long count = Db.Count(
                "SELECT COUNT(*) FROM FingerprintLogs " +
                "WHERE ((EmployeeId = ? AND EmpDevCode = ?) OR (EmployeeId IS NULL AND EmpDevCode = ?)) " +
                "AND LogTime = ? AND InOutFlag = ?",
                Db.PNullableId(logEntry.EmployeeId),
                Db.P(logEntry.EmpDevCode ?? string.Empty),
                Db.P(logEntry.EmpDevCode ?? string.Empty),
                Db.PDateRequired(logEntry.LogTime),
                Db.P(logEntry.InOutFlag ?? 0));
            return count > 0;
        }

        private static int RowInsert(int deviceId, FingerprintLog logEntry)
        {
            Db.ExecuteNonQuery(
                "INSERT INTO FingerprintLogs (DeviceId, EmployeeId, EmpDevCode, LogTime, InOutFlag, SourceFile, CreatedAt) " +
                "VALUES (?, ?, ?, ?, ?, ?, Now())",
                Db.P(deviceId),
                Db.PNullableId(logEntry.EmployeeId),
                Db.P(logEntry.EmpDevCode ?? string.Empty),
                Db.PDateRequired(logEntry.LogTime),
                Db.P(logEntry.InOutFlag ?? 0),
                Db.P(logEntry.SourceFile ?? string.Empty));
            return 1;
        }

        /// <summary>Raw-log browser rows (joined with employee and device names).</summary>
        public static List<FingerprintLogView> SearchLogs(int? deviceId, int? employeeId,
            DateTime? fromTime, DateTime? toTime, int maxRows)
        {
            StringBuilder sql = new StringBuilder(
                "SELECT TOP " + maxRows + " l.LogId, l.DeviceId, d.DeviceName, l.EmployeeId, l.EmpDevCode, " +
                "e.EmployeeCode, e.FullNameAr, l.LogTime, l.InOutFlag, l.SourceFile, l.CreatedAt " +
                "FROM (FingerprintLogs l LEFT JOIN Employees e ON l.EmployeeId = e.EmployeeId) " +
                "LEFT JOIN Devices d ON l.DeviceId = d.DeviceId " +
                "WHERE 1 = 1");
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (deviceId != null)
            {
                sql.Append(" AND l.DeviceId = ?");
                parameters.Add(Db.P(deviceId.Value));
            }
            if (employeeId != null)
            {
                sql.Append(" AND l.EmployeeId = ?");
                parameters.Add(Db.P(employeeId.Value));
            }
            if (fromTime != null)
            {
                sql.Append(" AND l.LogTime >= ?");
                parameters.Add(Db.PDate(fromTime.Value));
            }
            if (toTime != null)
            {
                sql.Append(" AND l.LogTime <= ?");
                parameters.Add(Db.PDate(toTime.Value));
            }
            sql.Append(" ORDER BY l.LogTime DESC, l.LogId DESC");
            return Db.ReadList(sql.ToString(), MapLogView, parameters.ToArray());
        }

        private static FingerprintLogView MapLogView(OleDbDataReader reader)
        {
            FingerprintLogView view = new FingerprintLogView();
            view.LogId = reader.GetInt32(0);
            view.DeviceId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            view.DeviceName = reader.IsDBNull(2) ? null : reader.GetString(2);
            view.EmployeeId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
            view.EmpDevCode = reader.IsDBNull(4) ? null : reader.GetString(4);
            view.EmployeeCode = reader.IsDBNull(5) ? null : reader.GetString(5);
            view.EmployeeName = reader.IsDBNull(6) ? null : reader.GetString(6);
            view.LogTime = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7);
            view.InOutFlag = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8);
            view.SourceFile = reader.IsDBNull(9) ? null : reader.GetString(9);
            view.CreatedAt = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10);
            return view;
        }

        /// <summary>Number of log rows recorded from one device.</summary>
        public static long CountByDevice(int deviceId)
        {
            return Db.Count("SELECT COUNT(*) FROM FingerprintLogs WHERE DeviceId = ?", Db.P(deviceId));
        }
    }
}
