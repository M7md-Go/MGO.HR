using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Manual attendance-correction requests over [AttendanceAdjustments]: a
    /// request changes the punch times or the final status of one employee-day;
    /// approval applies the correction to [Attendance] flagged IsManuallyChg
    /// so the recalculation engine never overwrites it.
    /// </summary>
    public static class AttendanceAdjustmentDAL
    {
        public static List<AttendanceAdjustmentView> Browse(string status, int? employeeId,
            DateTime? fromDate, DateTime? toDate, int maxRows)
        {
            string sql =
                "SELECT TOP " + maxRows + " a.AdjustmentId, a.EmployeeId, e.EmployeeCode, e.FullNameAr, a.AttDate, " +
                "a.AdjustmentType, a.NewInTime, a.NewOutTime, a.NewStatus, a.Reason, a.Status, a.CreatedAt " +
                "FROM (AttendanceAdjustments a INNER JOIN Employees e ON a.EmployeeId = e.EmployeeId) WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (!string.IsNullOrEmpty(status))
            {
                sql += " AND a.Status = ?";
                parameters.Add(Db.P(status));
            }
            if (employeeId != null)
            {
                sql += " AND a.EmployeeId = ?";
                parameters.Add(Db.P(employeeId.Value));
            }
            if (fromDate != null)
            {
                sql += " AND a.AttDate >= ?";
                parameters.Add(Db.PDate(fromDate.Value.Date));
            }
            if (toDate != null)
            {
                sql += " AND a.AttDate <= ?";
                parameters.Add(Db.PDate(toDate.Value.Date));
            }
            sql += " ORDER BY a.CreatedAt DESC, a.AdjustmentId DESC";
            return Db.ReadList(sql, MapView, parameters.ToArray());
        }

        public static AttendanceAdjustment GetById(int adjustmentId)
        {
            List<AttendanceAdjustment> rows = Db.ReadList(
                "SELECT AdjustmentId, EmployeeId, AttDate, AdjustmentType, NewInTime, NewOutTime, NewStatus, " +
                "Reason, Status, ApprovedBy, CreatedAt FROM AttendanceAdjustments WHERE AdjustmentId = ?",
                MapRow, Db.P(adjustmentId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static int Insert(AttendanceAdjustment adjustment, int? createdBy)
        {
            string sql = "INSERT INTO AttendanceAdjustments (EmployeeId, AttDate, AdjustmentType, NewInTime, " +
                "NewOutTime, NewStatus, Reason, Status, CreatedBy, CreatedAt) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, 'Pending', ?, Now())";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(adjustment.EmployeeId));
                    command.Parameters.Add(Db.PDateRequired(adjustment.AttDate));
                    command.Parameters.Add(Db.P(adjustment.AdjustmentType));
                    command.Parameters.Add(Db.P(adjustment.NewInTime));
                    command.Parameters.Add(Db.P(adjustment.NewOutTime));
                    command.Parameters.Add(Db.P(adjustment.NewStatus));
                    command.Parameters.Add(Db.P(adjustment.Reason));
                    command.Parameters.Add(Db.PNullableId(createdBy));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Update(AttendanceAdjustment adjustment)
        {
            Db.ExecuteNonQuery(
                "UPDATE AttendanceAdjustments SET EmployeeId = ?, AttDate = ?, AdjustmentType = ?, NewInTime = ?, " +
                "NewOutTime = ?, NewStatus = ?, Reason = ? WHERE AdjustmentId = ?",
                Db.P(adjustment.EmployeeId), Db.PDateRequired(adjustment.AttDate), Db.P(adjustment.AdjustmentType),
                Db.P(adjustment.NewInTime), Db.P(adjustment.NewOutTime), Db.P(adjustment.NewStatus),
                Db.P(adjustment.Reason), Db.P(adjustment.AdjustmentId));
        }

        public static void SetStatus(int adjustmentId, string status, int? approvedBy)
        {
            Db.ExecuteNonQuery(
                "UPDATE AttendanceAdjustments SET Status = ?, ApprovedBy = ?, ApprovedAt = Now() WHERE AdjustmentId = ?",
                Db.P(status), Db.PNullableId(approvedBy), Db.P(adjustmentId));
        }

        /// <summary>
        /// Applies an approved correction to [Attendance]: updates the
        /// employee-day row when it exists, otherwise inserts a manual one.
        /// Always flags IsManuallyChg = True so the engine keeps its hands off.
        /// </summary>
        /// <param name="startTime">Absolute entrance moment (nullable).</param>
        /// <param name="endTime">Absolute exit moment (nullable).</param>
        public static int ApplyToAttendance(int employeeId, DateTime attDate, DateTime? startTime,
            DateTime? endTime, string newStatus, int? approvedBy)
        {
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                int updated;
                using (OleDbCommand find = new OleDbCommand(
                    "SELECT AttendanceId FROM Attendance WHERE EmployeeId = ? AND AttDate = ?",
                    connection, transaction))
                {
                    find.Parameters.Add(Db.P(employeeId));
                    find.Parameters.Add(Db.PDate(attDate.Date));
                    object found = find.ExecuteScalar();
                    if (found != null && found != DBNull.Value)
                    {
                        using (OleDbCommand update = new OleDbCommand(
                            "UPDATE Attendance SET StartTime = ?, EndTime = ?, Status = ?, IsManuallyChg = True, " +
                            "ApprovedBy = ?, ApprovedAt = Now() WHERE EmployeeId = ? AND AttDate = ?",
                            connection, transaction))
                        {
                            update.Parameters.Add(startTime == null ? Db.P(null) : Db.PDate(startTime));
                            update.Parameters.Add(endTime == null ? Db.P(null) : Db.PDate(endTime));
                            update.Parameters.Add(Db.P(newStatus));
                            update.Parameters.Add(Db.PNullableId(approvedBy));
                            update.Parameters.Add(Db.P(employeeId));
                            update.Parameters.Add(Db.PDate(attDate.Date));
                            updated = update.ExecuteNonQuery();
                        }
                        return updated;
                    }
                }
                using (OleDbCommand insert = new OleDbCommand(
                    "INSERT INTO Attendance (StartTime, EndTime, Status, IsManuallyChg, ApprovedBy, ApprovedAt, EmployeeId, AttDate, CreatedAt) " +
                    "VALUES (?, ?, ?, True, ?, Now(), ?, ?, Now())",
                    connection, transaction))
                {
                    insert.Parameters.Add(startTime == null ? Db.P(null) : Db.PDate(startTime));
                    insert.Parameters.Add(endTime == null ? Db.P(null) : Db.PDate(endTime));
                    insert.Parameters.Add(Db.P(newStatus));
                    insert.Parameters.Add(Db.PNullableId(approvedBy));
                    insert.Parameters.Add(Db.P(employeeId));
                    insert.Parameters.Add(Db.PDate(attDate.Date));
                    insert.ExecuteNonQuery();
                }
                return 1;
            });
        }

        private static AttendanceAdjustmentView MapView(OleDbDataReader r)
        {
            AttendanceAdjustmentView view = new AttendanceAdjustmentView();
            view.AdjustmentId = r.GetInt32(0);
            view.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            view.EmployeeCode = r.IsDBNull(2) ? null : r.GetString(2);
            view.EmployeeName = r.IsDBNull(3) ? null : r.GetString(3);
            view.AttDate = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            view.AdjustmentType = r.IsDBNull(5) ? null : r.GetString(5);
            view.NewInTime = r.IsDBNull(6) ? null : r.GetString(6);
            view.NewOutTime = r.IsDBNull(7) ? null : r.GetString(7);
            view.NewStatus = r.IsDBNull(8) ? null : r.GetString(8);
            view.Reason = r.IsDBNull(9) ? null : r.GetString(9);
            view.Status = r.IsDBNull(10) ? null : r.GetString(10);
            view.CreatedAt = r.IsDBNull(11) ? (DateTime?)null : r.GetDateTime(11);
            return view;
        }

        private static AttendanceAdjustment MapRow(OleDbDataReader r)
        {
            AttendanceAdjustment row = new AttendanceAdjustment();
            row.AdjustmentId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.AttDate = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2);
            row.AdjustmentType = r.IsDBNull(3) ? null : r.GetString(3);
            row.NewInTime = r.IsDBNull(4) ? null : r.GetString(4);
            row.NewOutTime = r.IsDBNull(5) ? null : r.GetString(5);
            row.NewStatus = r.IsDBNull(6) ? null : r.GetString(6);
            row.Reason = r.IsDBNull(7) ? null : r.GetString(7);
            row.Status = r.IsDBNull(8) ? null : r.GetString(8);
            row.ApprovedBy = r.IsDBNull(9) ? (int?)null : Convert.ToInt32(r.GetValue(9));
            row.CreatedAt = r.IsDBNull(10) ? (DateTime?)null : r.GetDateTime(10);
            return row;
        }
    }
}
