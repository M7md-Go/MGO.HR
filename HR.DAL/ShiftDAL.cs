using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>Shift definitions persistence (Phase 1 design, rule 6 — shifts with actual data).</summary>
    public static class ShiftDAL
    {
        private const string SelectColumns =
            "ShiftId, ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive";

        public static List<Shift> GetAll(bool activeOnly)
        {
            string sql = "SELECT " + SelectColumns + " FROM Shifts";
            if (activeOnly)
            {
                sql = sql + " WHERE IsActive = 1";
            }
            sql = sql + " ORDER BY ShiftCode";
            return Db.ReadList(sql, MapRow);
        }

        public static Shift GetById(int shiftId)
        {
            return Db.ReadOne("SELECT " + SelectColumns + " FROM Shifts WHERE ShiftId = ?", MapRow, Db.P(shiftId));
        }

        public static Shift GetByCode(string shiftCode)
        {
            return Db.ReadOne("SELECT " + SelectColumns + " FROM Shifts WHERE ShiftCode = ?", MapRow, Db.P(shiftCode));
        }

        private static Shift MapRow(OleDbDataReader reader)
        {
            Shift shift = new Shift();
            shift.ShiftId = reader.GetInt32(0);
            shift.ShiftCode = reader.IsDBNull(1) ? null : reader.GetString(1);
            shift.ShiftName = reader.IsDBNull(2) ? null : reader.GetString(2);
            shift.StartTime = reader.IsDBNull(3) ? null : reader.GetString(3);
            shift.EndTime = reader.IsDBNull(4) ? null : reader.GetString(4);
            shift.CrossDay = reader.IsDBNull(5) ? (bool?)null : reader.GetBoolean(5);
            shift.DurationHrs = reader.IsDBNull(6) ? (float?)null : Convert.ToSingle(reader.GetDouble(6));
            shift.NormalHours = reader.IsDBNull(7) ? (float?)null : Convert.ToSingle(reader.GetDouble(7));
            shift.BreakMinutes = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8);
            shift.CategoryCode = reader.IsDBNull(9) ? null : reader.GetString(9);
            shift.RequireCheck = reader.IsDBNull(10) ? (bool?)null : reader.GetBoolean(10);
            shift.IsActive = reader.IsDBNull(11) ? (bool?)null : reader.GetBoolean(11);
            return shift;
        }

        public static int Insert(Shift shift)
        {
            string sql = "INSERT INTO Shifts (ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive) " +
                         "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(shift.ShiftCode));
                    command.Parameters.Add(Db.P(shift.ShiftName));
                    command.Parameters.Add(Db.P(shift.StartTime));
                    command.Parameters.Add(Db.P(shift.EndTime));
                    command.Parameters.Add(Db.PBool(shift.CrossDay));
                    command.Parameters.Add(Db.PSingle(shift.DurationHrs));
                    command.Parameters.Add(Db.PSingle(shift.NormalHours));
                    command.Parameters.Add(Db.P(shift.BreakMinutes));
                    command.Parameters.Add(Db.P(shift.CategoryCode));
                    command.Parameters.Add(Db.PBool(shift.RequireCheck));
                    command.Parameters.Add(Db.PBool(shift.IsActive));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Update(Shift shift)
        {
            string sql = "UPDATE Shifts SET ShiftCode = ?, ShiftName = ?, StartTime = ?, EndTime = ?, CrossDay = ?, DurationHrs = ?, NormalHours = ?, BreakMinutes = ?, CategoryCode = ?, RequireCheck = ?, IsActive = ? " +
                         "WHERE ShiftId = ?";
            Db.ExecuteNonQuery(sql,
                Db.P(shift.ShiftCode),
                Db.P(shift.ShiftName),
                Db.P(shift.StartTime),
                Db.P(shift.EndTime),
                Db.PBool(shift.CrossDay),
                Db.PSingle(shift.DurationHrs),
                Db.PSingle(shift.NormalHours),
                Db.P(shift.BreakMinutes),
                Db.P(shift.CategoryCode),
                Db.PBool(shift.RequireCheck),
                Db.PBool(shift.IsActive),
                Db.P(shift.ShiftId));
        }

        /// <summary>True when another shift (different id) already uses the code.</summary>
        public static bool CodeExists(string shiftCode, int? excludeShiftId)
        {
            string sql = "SELECT COUNT(*) FROM Shifts WHERE ShiftCode = ?";
            if (excludeShiftId != null)
            {
                sql = sql + " AND ShiftId <> ?";
                return Db.Count(sql, Db.P(shiftCode), Db.PNullableId(excludeShiftId)) > 0;
            }
            return Db.Count(sql, Db.P(shiftCode)) > 0;
        }

        /// <summary>Usage in schedules and attendance — a used shift cannot be deleted (BR-02 style guard).</summary>
        public static long CountUsage(int shiftId)
        {
            long links = Db.Count("SELECT COUNT(*) FROM ScheduleDetails WHERE ShiftId = ?", Db.P(shiftId));
            links = links + Db.Count("SELECT COUNT(*) FROM Attendance WHERE ShiftId = ?", Db.P(shiftId));
            return links;
        }

        public static void Delete(int shiftId)
        {
            Db.ExecuteNonQuery("DELETE FROM Shifts WHERE ShiftId = ?", Db.P(shiftId));
        }
    }
}
