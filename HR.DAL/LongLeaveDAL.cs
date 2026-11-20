using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Long-duration leaves persistence (child care, family care, going abroad,
    /// income improvement): period rows that may be open-ended (ToDate = NULL).
    /// Feeds the attendance engine through LoadApprovedForEngine with an
    /// in-transaction @@IDENTITY insert (Jet: separate connection returns 0).
    /// All statements parameterized.
    /// </summary>
    public static class LongLeaveDAL
    {
        public static List<LongLeaveType> GetTypes()
        {
            return Db.ReadList(
                "SELECT LongLeaveTypeId, TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes " +
                "FROM LongLeaveTypes WHERE IsActive <> 0 ORDER BY LongLeaveTypeId",
                MapType);
        }

        public static List<LongLeaveType> GetAllTypes()
        {
            return Db.ReadList(
                "SELECT LongLeaveTypeId, TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes " +
                "FROM LongLeaveTypes ORDER BY LongLeaveTypeId",
                MapType);
        }

        public static LongLeaveType GetTypeById(int typeId)
        {
            List<LongLeaveType> rows = Db.ReadList(
                "SELECT LongLeaveTypeId, TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes " +
                "FROM LongLeaveTypes WHERE LongLeaveTypeId = ?",
                MapType, Db.P(typeId));
            return rows.Count > 0 ? rows[0] : null;
        }

        private static LongLeaveType MapType(OleDbDataReader r)
        {
            LongLeaveType row = new LongLeaveType();
            row.LongLeaveTypeId = r.GetInt32(0);
            row.TypeCode = r.IsDBNull(1) ? null : r.GetString(1);
            row.TypeName = r.IsDBNull(2) ? null : r.GetString(2);
            row.IsLimited = r.IsDBNull(3) ? (bool?)null : r.GetBoolean(3);
            row.MaxMonths = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            row.IsActive = r.IsDBNull(5) ? (bool?)null : r.GetBoolean(5);
            row.Notes = r.IsDBNull(6) ? null : r.GetString(6);
            return row;
        }

        public static LongLeaveView GetById(int longLeaveId)
        {
            List<LongLeaveView> rows = Db.ReadList(
                "SELECT v.LongLeaveId, v.EmployeeId, e.EmployeeCode, e.FullNameAr, v.TypeId, t.TypeName, " +
                "v.FromDate, v.ToDate, v.Status, v.Reason, v.Notes, v.ApprovedBy " +
                "FROM (LongLeaves v INNER JOIN Employees e ON v.EmployeeId = e.EmployeeId) " +
                "LEFT JOIN LongLeaveTypes t ON v.TypeId = t.LongLeaveTypeId WHERE v.LongLeaveId = ?",
                MapView, Db.P(longLeaveId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static List<LongLeaveView> Search(string status, int? typeId, int? employeeId,
            DateTime? fromDate, DateTime? toDate, int maxRows)
        {
            string sql =
                "SELECT TOP " + maxRows + " v.LongLeaveId, v.EmployeeId, e.EmployeeCode, e.FullNameAr, v.TypeId, t.TypeName, " +
                "v.FromDate, v.ToDate, v.Status, v.Reason, v.Notes, v.ApprovedBy " +
                "FROM (LongLeaves v INNER JOIN Employees e ON v.EmployeeId = e.EmployeeId) " +
                "LEFT JOIN LongLeaveTypes t ON v.TypeId = t.LongLeaveTypeId WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (!string.IsNullOrEmpty(status))
            {
                sql += " AND v.Status = ?";
                parameters.Add(Db.P(status));
            }
            if (typeId != null)
            {
                sql += " AND v.TypeId = ?";
                parameters.Add(Db.P(typeId.Value));
            }
            if (employeeId != null)
            {
                sql += " AND v.EmployeeId = ?";
                parameters.Add(Db.P(employeeId.Value));
            }
            if (fromDate != null)
            {
                sql += " AND (v.ToDate IS NULL OR v.ToDate >= ?)";
                parameters.Add(Db.PDate(fromDate.Value));
            }
            if (toDate != null)
            {
                sql += " AND v.FromDate <= ?";
                parameters.Add(Db.PDate(toDate.Value));
            }
            sql += " ORDER BY v.FromDate DESC, v.LongLeaveId DESC";
            return Db.ReadList(sql, MapView, parameters.ToArray());
        }

        private static LongLeaveView MapView(OleDbDataReader r)
        {
            LongLeaveView row = new LongLeaveView();
            row.LongLeaveId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.EmployeeCode = r.IsDBNull(2) ? null : r.GetString(2);
            row.EmployeeName = r.IsDBNull(3) ? null : r.GetString(3);
            row.TypeId = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            row.TypeName = r.IsDBNull(5) ? null : r.GetString(5);
            row.FromDate = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6);
            row.ToDate = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7);
            row.Status = r.IsDBNull(8) ? null : r.GetString(8);
            row.Reason = r.IsDBNull(9) ? null : r.GetString(9);
            row.Notes = r.IsDBNull(10) ? null : r.GetString(10);
            row.ApprovedBy = r.IsDBNull(11) ? (int?)null : Convert.ToInt32(r.GetValue(11));
            return row;
        }

        public static int Insert(LongLeave leave, int? createdBy)
        {
            string sql = "INSERT INTO LongLeaves (EmployeeId, TypeId, FromDate, ToDate, Status, Reason, Notes, CreatedAt, CreatedBy) " +
                         "VALUES (?, ?, ?, ?, 'Pending', ?, ?, Now(), ?)";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(leave.EmployeeId ?? 0));
                    command.Parameters.Add(Db.P(leave.TypeId ?? 0));
                    command.Parameters.Add(Db.PDateRequired(leave.FromDate));
                    command.Parameters.Add(Db.PDate(leave.ToDate));
                    command.Parameters.Add(Db.P(leave.Reason ?? string.Empty));
                    command.Parameters.Add(Db.P(leave.Notes ?? string.Empty));
                    command.Parameters.Add(Db.PNullableId(createdBy));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Update(int longLeaveId, LongLeave leave)
        {
            Db.ExecuteNonQuery(
                "UPDATE LongLeaves SET EmployeeId = ?, TypeId = ?, FromDate = ?, ToDate = ?, Reason = ?, Notes = ? " +
                "WHERE LongLeaveId = ?",
                Db.P(leave.EmployeeId ?? 0), Db.P(leave.TypeId ?? 0),
                Db.PDateRequired(leave.FromDate), Db.PDate(leave.ToDate),
                Db.P(leave.Reason ?? string.Empty), Db.P(leave.Notes ?? string.Empty), Db.P(longLeaveId));
        }

        public static void SetStatus(int longLeaveId, string status, int? approvedBy)
        {
            Db.ExecuteNonQuery(
                "UPDATE LongLeaves SET Status = ?, ApprovedBy = ? WHERE LongLeaveId = ?",
                Db.P(status), Db.PNullableId(approvedBy), Db.P(longLeaveId));
        }

        public static void SetToDate(int longLeaveId, DateTime? toDate)
        {
            Db.ExecuteNonQuery(
                "UPDATE LongLeaves SET ToDate = ? WHERE LongLeaveId = ?",
                Db.PDate(toDate), Db.P(longLeaveId));
        }

        public static bool HasOverlapping(int employeeId, DateTime fromDate, DateTime? toDate, int? excludeId)
        {
            long count = Db.Count(
                "SELECT COUNT(*) FROM LongLeaves WHERE EmployeeId = ? AND Status IN ('Pending', 'Approved') " +
                "AND LongLeaveId <> ? AND FromDate <= ? AND (ToDate IS NULL OR ToDate >= ?)",
                Db.P(employeeId), Db.P(excludeId ?? 0),
                Db.PDate(toDate == null ? new DateTime(2099, 12, 31) : toDate.Value.Date),
                Db.PDate(fromDate.Date));
            return count > 0;
        }

        /// <summary>Approved long leaves overlapping the window, ready for the engine (open-ended normalized to 2099).</summary>
        public static List<LeaveRequest> LoadApprovedForEngine(DateTime fromDate, DateTime toDate)
        {
            DateTime searchFrom = fromDate.AddYears(-1);
            return Db.ReadList(
                "SELECT LongLeaveId, EmployeeId, FromDate, ToDate, Reason FROM LongLeaves " +
                "WHERE Status = 'Approved' AND FromDate <= ? AND (ToDate IS NULL OR ToDate >= ?)",
                MapEngineRow, Db.PDate(toDate.Date), Db.PDate(searchFrom));
        }

        private static LeaveRequest MapEngineRow(OleDbDataReader r)
        {
            LeaveRequest row = new LeaveRequest();
            row.LeaveId = -r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.StartDate = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2);
            row.EndDate = r.IsDBNull(3) ? new DateTime(2099, 12, 31) : r.GetDateTime(3);
            row.Reason = r.IsDBNull(4) ? null : r.GetString(4);
            row.Status = "Approved";
            return row;
        }
    }
}
