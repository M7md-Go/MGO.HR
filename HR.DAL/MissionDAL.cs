using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>Missions CRUD (rule 16): parameterized; searches join employee names; the engine reads approved rows via AttendanceDAL.</summary>
    public static class MissionDAL
    {
        public static MissionView GetById(int missionId)
        {
            List<MissionView> rows = Db.ReadList(
                "SELECT m.MissionId, m.EmployeeId, e.EmployeeCode, e.FullNameAr, m.StartDate, m.StartTime, m.EndDate, m.EndTime, " +
                "m.Location, m.Purpose, m.IsExemptAtt, m.Status, m.ApprovedBy " +
                "FROM (Missions m INNER JOIN Employees e ON m.EmployeeId = e.EmployeeId) WHERE m.MissionId = ?",
                MapView, Db.P(missionId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static List<MissionView> Search(string status, int? employeeId, DateTime? fromDate,
            DateTime? toDate, int maxRows)
        {
            string sql =
                "SELECT TOP " + maxRows + " m.MissionId, m.EmployeeId, e.EmployeeCode, e.FullNameAr, m.StartDate, m.StartTime, " +
                "m.EndDate, m.EndTime, m.Location, m.Purpose, m.IsExemptAtt, m.Status, m.ApprovedBy " +
                "FROM (Missions m INNER JOIN Employees e ON m.EmployeeId = e.EmployeeId) WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (!string.IsNullOrEmpty(status))
            {
                sql += " AND m.Status = ?";
                parameters.Add(Db.P(status));
            }
            if (employeeId != null)
            {
                sql += " AND m.EmployeeId = ?";
                parameters.Add(Db.P(employeeId.Value));
            }
            if (fromDate != null)
            {
                sql += " AND m.EndDate >= ?";
                parameters.Add(Db.PDate(fromDate.Value));
            }
            if (toDate != null)
            {
                sql += " AND m.StartDate <= ?";
                parameters.Add(Db.PDate(toDate.Value));
            }
            sql += " ORDER BY m.StartDate DESC, m.MissionId DESC";
            return Db.ReadList(sql, MapView, parameters.ToArray());
        }

        private static MissionView MapView(OleDbDataReader r)
        {
            MissionView row = new MissionView();
            row.MissionId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.EmployeeCode = r.IsDBNull(2) ? null : r.GetString(2);
            row.EmployeeName = r.IsDBNull(3) ? null : r.GetString(3);
            row.StartDate = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            row.StartTime = r.IsDBNull(5) ? null : r.GetString(5);
            row.EndDate = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6);
            row.EndTime = r.IsDBNull(7) ? null : r.GetString(7);
            row.Location = r.IsDBNull(8) ? null : r.GetString(8);
            row.Purpose = r.IsDBNull(9) ? null : r.GetString(9);
            row.IsExemptAtt = r.IsDBNull(10) ? (bool?)null : r.GetBoolean(10);
            row.Status = r.IsDBNull(11) ? null : r.GetString(11);
            row.ApprovedBy = r.IsDBNull(12) ? (int?)null : Convert.ToInt32(r.GetValue(12));
            return row;
        }

        public static int Insert(Mission mission, int? createdBy)
        {
            string sql =
                "INSERT INTO Missions (EmployeeId, StartDate, StartTime, EndDate, EndTime, Location, Purpose, IsExemptAtt, Status, CreatedAt, CreatedBy) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Pending', Now(), ?)";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(mission.EmployeeId ?? 0));
                    command.Parameters.Add(Db.PDateRequired(mission.StartDate));
                    command.Parameters.Add(Db.P(mission.StartTime ?? string.Empty));
                    command.Parameters.Add(Db.PDateRequired(mission.EndDate));
                    command.Parameters.Add(Db.P(mission.EndTime ?? string.Empty));
                    command.Parameters.Add(Db.P(mission.Location ?? string.Empty));
                    command.Parameters.Add(Db.P(mission.Purpose ?? string.Empty));
                    command.Parameters.Add(Db.PBool(mission.IsExemptAtt));
                    command.Parameters.Add(Db.PNullableId(createdBy));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Update(int missionId, Mission mission)
        {
            Db.ExecuteNonQuery(
                "UPDATE Missions SET StartDate = ?, StartTime = ?, EndDate = ?, EndTime = ?, Location = ?, Purpose = ?, IsExemptAtt = ? " +
                "WHERE MissionId = ?",
                Db.PDate(mission.StartDate), Db.P(mission.StartTime ?? string.Empty),
                Db.PDate(mission.EndDate), Db.P(mission.EndTime ?? string.Empty),
                Db.P(mission.Location ?? string.Empty), Db.P(mission.Purpose ?? string.Empty),
                Db.PBool(mission.IsExemptAtt), Db.P(missionId));
        }

        public static void SetStatus(int missionId, string status, int? approvedBy)
        {
            Db.ExecuteNonQuery(
                "UPDATE Missions SET Status = ?, ApprovedBy = ? WHERE MissionId = ?",
                Db.P(status), Db.PNullableId(approvedBy), Db.P(missionId));
        }
    }
}
