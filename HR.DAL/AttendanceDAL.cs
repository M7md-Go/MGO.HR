using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Attendance persistence (rule 10): every engine recalculation writes
    /// [Attendance] upserts, engine-owned [AttendanceExceptions] and auto
    /// [Overtime] rows inside ONE transaction, so a polluted month is never
    /// half-written. Manually edited rows (IsManuallyChg = true) and manual /
    /// approved Overtime rows are never touched. Exception rows are identified
    /// as engine-owned by CreatedBy IS NULL — manual exception rows survive.
    /// </summary>
    public static class AttendanceDAL
    {
        private static string KeyOf(int employeeId, DateTime date)
        {
            return employeeId + "|" + date.Ticks;
        }

        /// <summary>Keys ("EmployeeId|dateTicks") of manually corrected rows inside a window.</summary>
        public static HashSet<string> LoadManualKeys(DateTime fromDate, DateTime toDate)
        {
            HashSet<string> keys = new HashSet<string>();
            List<Attendance> rows = Db.ReadList(
                "SELECT AttendanceId, EmployeeId, AttDate, ShiftId, StartTime, EndTime, Status, IsManuallyChg, MinutesWorked, OvertimeMin, ApprovedBy, ApprovedAt, CreatedAt FROM Attendance WHERE IsManuallyChg = True AND AttDate >= ? AND AttDate <= ?",
                MapAttendance, Db.PDate(fromDate.Date), Db.PDate(toDate.Date));
            foreach (Attendance row in rows)
            {
                if (row.EmployeeId != null && row.AttDate != null)
                {
                    keys.Add(KeyOf(row.EmployeeId.Value, row.AttDate.Value));
                }
            }
            return keys;
        }

        private static Attendance MapAttendance(OleDbDataReader r)
        {
            Attendance row = new Attendance();
            row.AttendanceId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.AttDate = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2);
            row.ShiftId = r.IsDBNull(3) ? (int?)null : Convert.ToInt32(r.GetValue(3));
            row.StartTime = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            row.EndTime = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5);
            row.Status = r.IsDBNull(6) ? null : r.GetString(6);
            row.IsManuallyChg = r.IsDBNull(7) ? (bool?)null : r.GetBoolean(7);
            row.MinutesWorked = r.IsDBNull(8) ? (int?)null : Convert.ToInt32(r.GetValue(8));
            row.OvertimeMin = r.IsDBNull(9) ? (int?)null : Convert.ToInt32(r.GetValue(9));
            row.ApprovedBy = r.IsDBNull(10) ? (int?)null : Convert.ToInt32(r.GetValue(10));
            row.ApprovedAt = r.IsDBNull(11) ? (DateTime?)null : r.GetDateTime(11);
            row.CreatedAt = r.IsDBNull(12) ? (DateTime?)null : r.GetDateTime(12);
            return row;
        }

        /// <summary>Browser rows joined with employee and shift codes.</summary>
        public static List<AttendanceView> Browse(DateTime fromDate, DateTime toDate, int? employeeId,
            int? departmentId, int maxRows)
        {
            StringBuilder sql = new StringBuilder(
                "SELECT TOP " + maxRows + " a.AttendanceId, a.EmployeeId, e.EmployeeCode, e.FullNameAr, a.AttDate, " +
                "a.ShiftId, s.ShiftCode, a.StartTime, a.EndTime, a.Status, a.IsManuallyChg, a.MinutesWorked, a.OvertimeMin, " +
                "a.LateMinutes, a.EarlyLeaveMinutes, e.DepartmentId, dep.DepartmentName " +
                "FROM ((Attendance a INNER JOIN Employees e ON a.EmployeeId = e.EmployeeId) " +
                "LEFT JOIN Shifts s ON a.ShiftId = s.ShiftId) " +
                "LEFT JOIN Departments dep ON e.DepartmentId = dep.DepartmentId " +
                "WHERE a.AttDate >= ? AND a.AttDate <= ?");
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            parameters.Add(Db.PDate(fromDate));
            parameters.Add(Db.PDate(toDate));
            if (employeeId != null)
            {
                sql.Append(" AND a.EmployeeId = ?");
                parameters.Add(Db.P(employeeId.Value));
            }
            if (departmentId != null)
            {
                sql.Append(" AND e.DepartmentId = ?");
                parameters.Add(Db.P(departmentId.Value));
            }
            sql.Append(" ORDER BY a.AttDate DESC, e.EmployeeCode");
            return Db.ReadList(sql.ToString(), MapView, parameters.ToArray());
        }

        private static AttendanceView MapView(OleDbDataReader r)
        {
            AttendanceView view = new AttendanceView();
            view.AttendanceId = r.GetInt32(0);
            view.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            view.EmployeeCode = r.IsDBNull(2) ? null : r.GetString(2);
            view.EmployeeName = r.IsDBNull(3) ? null : r.GetString(3);
            view.AttDate = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            view.ShiftId = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5));
            view.ShiftCode = r.IsDBNull(6) ? null : r.GetString(6);
            view.StartTime = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7);
            view.EndTime = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8);
            view.Status = r.IsDBNull(9) ? null : r.GetString(9);
            view.IsManuallyChg = r.IsDBNull(10) ? (bool?)null : r.GetBoolean(10);
            view.MinutesWorked = r.IsDBNull(11) ? (int?)null : Convert.ToInt32(r.GetValue(11));
            view.OvertimeMin = r.IsDBNull(12) ? (int?)null : Convert.ToInt32(r.GetValue(12));
            view.LateMinutes = r.IsDBNull(13) ? (int?)null : Convert.ToInt32(r.GetValue(13));
            view.EarlyLeaveMinutes = r.IsDBNull(14) ? (int?)null : Convert.ToInt32(r.GetValue(14));
            view.DepartmentId = r.IsDBNull(15) ? (int?)null : Convert.ToInt32(r.GetValue(15));
            view.DepartmentName = r.IsDBNull(16) ? null : r.GetString(16);
            return view;
        }

        /// <summary>Approved leave requests overlapping the window (feeds the engine).</summary>
        public static List<LeaveRequest> LoadApprovedLeaves(DateTime fromDate, DateTime toDate)
        {
            return Db.ReadList(
                "SELECT LeaveId, EmployeeId, LeaveTypeId, StartDate, EndDate, DaysCount, Reason, Status, ApprovedBy, CreatedAt, CreatedBy " +
                "FROM LeaveRequests WHERE Status = 'Approved' AND StartDate <= ? AND EndDate >= ?",
                MapLeave, Db.PDate(toDate.Date), Db.PDate(fromDate.Date));
        }

        private static LeaveRequest MapLeave(OleDbDataReader r)
        {
            LeaveRequest row = new LeaveRequest();
            row.LeaveId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.LeaveTypeId = r.IsDBNull(2) ? (int?)null : Convert.ToInt32(r.GetValue(2));
            row.StartDate = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3);
            row.EndDate = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            row.DaysCount = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5));
            row.Reason = r.IsDBNull(6) ? null : r.GetString(6);
            row.Status = r.IsDBNull(7) ? null : r.GetString(7);
            row.ApprovedBy = r.IsDBNull(8) ? (int?)null : Convert.ToInt32(r.GetValue(8));
            row.CreatedAt = r.IsDBNull(9) ? (DateTime?)null : r.GetDateTime(9);
            row.CreatedBy = r.IsDBNull(10) ? (int?)null : Convert.ToInt32(r.GetValue(10));
            return row;
        }

        /// <summary>Approved missions overlapping the window (feeds the engine).</summary>
        public static List<Mission> LoadApprovedMissions(DateTime fromDate, DateTime toDate)
        {
            return Db.ReadList(
                "SELECT MissionId, EmployeeId, StartDate, StartTime, EndDate, EndTime, Location, Purpose, IsExemptAtt, Status, ApprovedBy, CreatedAt " +
                "FROM Missions WHERE Status = 'Approved' AND StartDate <= ? AND EndDate >= ?",
                MapMission, Db.PDate(toDate.Date), Db.PDate(fromDate.Date));
        }

        private static Mission MapMission(OleDbDataReader r)
        {
            Mission row = new Mission();
            row.MissionId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.StartDate = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2);
            row.StartTime = r.IsDBNull(3) ? null : r.GetString(3);
            row.EndDate = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            row.EndTime = r.IsDBNull(5) ? null : r.GetString(5);
            row.Location = r.IsDBNull(6) ? null : r.GetString(6);
            row.Purpose = r.IsDBNull(7) ? null : r.GetString(7);
            row.IsExemptAtt = r.IsDBNull(8) ? (bool?)null : r.GetBoolean(8);
            row.Status = r.IsDBNull(9) ? null : r.GetString(9);
            row.ApprovedBy = r.IsDBNull(10) ? (int?)null : Convert.ToInt32(r.GetValue(10));
            row.CreatedAt = r.IsDBNull(11) ? (DateTime?)null : r.GetDateTime(11);
            return row;
        }

        /// <summary>Non-recurring holidays inside the window plus every recurring definition.</summary>
        public static List<PublicHoliday> LoadHolidays(DateTime fromDate, DateTime toDate)
        {
            return Db.ReadList(
                "SELECT HolidayId, HolidayDate, HolidayName, IsRecurring, ShiftId, AppliesTo " +
                "FROM PublicHolidays WHERE (IsRecurring = False AND HolidayDate >= ? AND HolidayDate <= ?) OR IsRecurring = True",
                MapHoliday, Db.PDate(fromDate.Date), Db.PDate(toDate.Date));
        }

        private static PublicHoliday MapHoliday(OleDbDataReader r)
        {
            PublicHoliday row = new PublicHoliday();
            row.HolidayId = r.GetInt32(0);
            row.HolidayDate = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1);
            row.HolidayName = r.IsDBNull(2) ? null : r.GetString(2);
            row.IsRecurring = r.IsDBNull(3) ? (bool?)null : r.GetBoolean(3);
            row.ShiftId = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            row.AppliesTo = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5));
            return row;
        }

        /// <summary>
        /// Single-transaction write of one recalculation pass: clears engine-owned
        /// exceptions of every processed employee-day, re-inserts the fresh ones,
        /// upserts Attendance and auto Overtime rows.
        /// </summary>
        /// <param name="processedKeys">"EmployeeId|dateTicks" of every employee-day the engine just recalculated.</param>
        public static void WriteEngineResults(List<Attendance> attendanceRows,
            List<AttendanceException> exceptions, List<Overtime> overtimeRows, List<string> processedKeys)
        {
            using (OleDbConnection connection = Db.CreateConnection())
            using (OleDbTransaction transaction = connection.BeginTransaction())
            using (OleDbCommand clearCommand = connection.CreateCommand())
            using (OleDbCommand insertExceptionCommand = connection.CreateCommand())
            using (OleDbCommand findCommand = connection.CreateCommand())
            using (OleDbCommand upsertCommand = connection.CreateCommand())
            {
                clearCommand.Transaction = transaction;
                clearCommand.CommandText =
                    "DELETE FROM AttendanceExceptions WHERE EmployeeId = ? AND AttDate = ? AND CreatedBy IS NULL";
                clearCommand.Parameters.Add(Db.P(1));
                clearCommand.Parameters.Add(Db.PDate(DateTime.MinValue));

                insertExceptionCommand.Transaction = transaction;
                insertExceptionCommand.CommandText =
                    "INSERT INTO AttendanceExceptions (EmployeeId, AttDate, ExcType, ExcValue, Status, CreatedAt) " +
                    "VALUES (?, ?, ?, ?, 'Open', Now())";
                insertExceptionCommand.Parameters.Add(Db.P(1));
                OleDbParameter excDateParameter = insertExceptionCommand.Parameters.Add(Db.PDate(DateTime.MinValue));
                OleDbParameter excTypeParameter = insertExceptionCommand.Parameters.Add(Db.P(string.Empty));
                OleDbParameter excValueParameter = insertExceptionCommand.Parameters.Add(Db.P(1));

                findCommand.Transaction = transaction;
                findCommand.CommandText =
                    "SELECT AttendanceId FROM Attendance WHERE EmployeeId = ? AND AttDate = ?";
                findCommand.Parameters.Add(Db.P(1));
                findCommand.Parameters.Add(Db.PDate(DateTime.MinValue));

                upsertCommand.Transaction = transaction;

                foreach (string key in processedKeys)
                {
                    int separatorIndex = key.IndexOf('|');
                    clearCommand.Parameters[0].Value = Convert.ToInt32(key.Substring(0, separatorIndex));
                    clearCommand.Parameters[1].Value = new DateTime(long.Parse(key.Substring(separatorIndex + 1)));
                    clearCommand.ExecuteNonQuery();
                }

                foreach (AttendanceException exception in exceptions)
                {
                    insertExceptionCommand.Parameters[0].Value = exception.EmployeeId ?? (object)DBNull.Value;
                    excDateParameter.Value = exception.AttDate == null ? (object)DBNull.Value : exception.AttDate.Value;
                    excTypeParameter.Value = exception.ExcType ?? string.Empty;
                    excValueParameter.Value = exception.ExcValue ?? (object)DBNull.Value;
                    insertExceptionCommand.ExecuteNonQuery();
                }

                foreach (Attendance row in attendanceRows)
                {
                    findCommand.Parameters[0].Value = row.EmployeeId ?? (object)DBNull.Value;
                    findCommand.Parameters[1].Value = row.AttDate == null ? (object)DBNull.Value : row.AttDate.Value.Date;
                    object found = findCommand.ExecuteScalar();
                    if (found == null || found == DBNull.Value)
                    {
                        upsertCommand.CommandText =
                            "INSERT INTO Attendance (ShiftId, StartTime, EndTime, Status, IsManuallyChg, MinutesWorked, OvertimeMin, LateMinutes, EarlyLeaveMinutes, ApprovedBy, ApprovedAt, EmployeeId, AttDate) " +
                            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
                        PrepareAttendanceParameters(upsertCommand, row);
                        upsertCommand.Parameters.Add(Db.PNullableId(row.EmployeeId));
                        upsertCommand.Parameters.Add(Db.PDate(row.AttDate));
                        upsertCommand.ExecuteNonQuery();
                    }
                    else
                    {
                        upsertCommand.CommandText =
                            "UPDATE Attendance SET ShiftId = ?, StartTime = ?, EndTime = ?, Status = ?, " +
                            "IsManuallyChg = ?, MinutesWorked = ?, OvertimeMin = ?, LateMinutes = ?, EarlyLeaveMinutes = ?, " +
                            "ApprovedBy = ?, ApprovedAt = ? WHERE AttendanceId = ?";
                        PrepareAttendanceParameters(upsertCommand, row);
                        upsertCommand.Parameters.Add(Db.P(Convert.ToInt32(found)));
                        upsertCommand.ExecuteNonQuery();
                    }
                }

                foreach (Overtime row in overtimeRows)
                {
                    object found = Db.ExecuteScalarOn(connection, transaction,
                        "SELECT OvertimeId FROM Overtime WHERE EmployeeId = ? AND OtDate = ?",
                        Db.P(row.EmployeeId), Db.PDate(row.OtDate));
                    if (found == null || found == DBNull.Value)
                    {
                        Db.ExecuteNonQueryOn(connection, transaction,
                            "INSERT INTO Overtime (EmployeeId, OtDate, PlannedMinutes, ActualMinutes, EffectiveMinutes, Rate, Source, Status, RequestedBy) " +
                            "VALUES (?, ?, ?, ?, ?, ?, 'Auto', ?, ?)",
                            Db.P(row.EmployeeId), Db.PDate(row.OtDate), Db.P(row.PlannedMinutes ?? 0),
                            Db.P(row.ActualMinutes ?? 0), Db.P(row.EffectiveMinutes ?? 0),
                            Db.P(row.Rate), Db.P(row.Status), Db.P(row.RequestedBy));
                    }
                    else
                    {
                        Db.ExecuteNonQueryOn(connection, transaction,
                            "UPDATE Overtime SET ActualMinutes = ?, EffectiveMinutes = ?, Rate = ?, Status = ? " +
                            "WHERE OvertimeId = ? AND Source = 'Auto' AND Status = 'Pending'",
                            Db.P(row.ActualMinutes ?? 0), Db.P(row.EffectiveMinutes ?? 0),
                            Db.P(row.Rate), Db.P(row.Status), Db.P(Convert.ToInt32(found)));
                    }
                }

                transaction.Commit();
            }
        }

        /// <summary>The nine engine-owned columns, in the exact order of both SQL statements; callers append the key parameters.</summary>
        private static void PrepareAttendanceParameters(OleDbCommand command, Attendance row)
        {
            command.Parameters.Clear();
            command.Parameters.Add(Db.PNullableId(row.ShiftId));
            command.Parameters.Add(Db.PDate(row.StartTime));
            command.Parameters.Add(Db.PDate(row.EndTime));
            command.Parameters.Add(Db.P(row.Status));
            command.Parameters.Add(Db.PBool(false));
            command.Parameters.Add(Db.P(row.MinutesWorked ?? 0));
            command.Parameters.Add(Db.P(row.OvertimeMin ?? 0));
            command.Parameters.Add(Db.P(row.LateMinutes ?? 0));
            command.Parameters.Add(Db.P(row.EarlyLeaveMinutes ?? 0));
            command.Parameters.Add(Db.PNullableId(row.ApprovedBy));
            command.Parameters.Add(Db.PDate(row.ApprovedAt));
        }
    }
}
