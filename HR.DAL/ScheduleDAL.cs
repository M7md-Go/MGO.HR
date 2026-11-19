using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Monthly schedules: MonthlySchedules (one row per year/month), ScheduleDetails
    /// (one row per employee/day) and MonthlyShiftLocks (lock state per month).
    /// All statements parameterized; multi-row replacement runs inside one
    /// transaction so a failed save never leaves a partially written employee row.
    /// </summary>
    public static class ScheduleDAL
    {
        private static MonthlySchedule MapSchedule(OleDbDataReader r)
        {
            MonthlySchedule entity = new MonthlySchedule();
            entity.ScheduleId = r.GetInt32(0);
            entity.YearVal = r.GetInt32(1);
            entity.MonthVal = r.GetInt32(2);
            entity.Status = r.IsDBNull(3) ? null : r.GetString(3);
            entity.CreatedBy = r.IsDBNull(4) ? (int?)null : r.GetInt32(4);
            entity.CreatedAt = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5);
            entity.ApprovedBy = r.IsDBNull(6) ? (int?)null : Convert.ToInt32(r.GetValue(6));
            entity.ApprovedAt = r.IsDBNull(7) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(7));
            entity.LockedAt = r.IsDBNull(8) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(8));
            return entity;
        }

        private static MonthlyShiftLock MapLock(OleDbDataReader r)
        {
            MonthlyShiftLock entity = new MonthlyShiftLock();
            entity.LockId = r.GetInt32(0);
            entity.YearVal = r.GetInt32(1);
            entity.MonthVal = r.GetInt32(2);
            entity.IsLocked = r.IsDBNull(3) ? (bool?)null : r.GetBoolean(3);
            entity.LockedBy = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            entity.LockedAt = r.IsDBNull(5) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(5));
            entity.UnlockedBy = r.IsDBNull(6) ? (int?)null : Convert.ToInt32(r.GetValue(6));
            entity.UnlockedAt = r.IsDBNull(7) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(7));
            entity.UnlockReason = r.IsDBNull(8) ? null : r.GetString(8);
            return entity;
        }

        private static ScheduleDetail MapDetail(OleDbDataReader r)
        {
            ScheduleDetail detail = new ScheduleDetail();
            detail.DetailId = r.GetInt32(0);
            detail.ScheduleId = r.GetInt32(1);
            detail.EmployeeId = r.GetInt32(2);
            detail.DayNumber = r.GetInt32(3);
            detail.ShiftId = r.GetInt32(4);
            return detail;
        }

        /// <summary>Returns the schedule header for a month, or null when none exists yet.</summary>
        public static MonthlySchedule GetSchedule(int year, int month)
        {
            List<MonthlySchedule> matches = Db.ReadList(
                "SELECT ScheduleId, YearVal, MonthVal, Status, CreatedBy, CreatedAt, ApprovedBy, ApprovedAt, LockedAt " +
                "FROM MonthlySchedules WHERE YearVal = ? AND MonthVal = ?",
                MapSchedule, Db.P(year), Db.P(month));
            return matches.Count > 0 ? matches[0] : null;
        }

        /// <summary>Creates the schedule header (Status Draft) and returns its id.</summary>
        public static int InsertSchedule(int year, int month, int? userId)
        {
            Db.ExecuteNonQuery(
                "INSERT INTO MonthlySchedules (YearVal, MonthVal, Status, CreatedBy, CreatedAt) VALUES (?, ?, ?, ?, Now())",
                Db.P(year), Db.P(month), Db.P("Draft"), Db.PNullableId(userId));
            MonthlySchedule created = GetSchedule(year, month);
            if (created == null)
            {
                throw new DataException("فشل إنشاء رأس الجدول الشهري " + year + "-" + month + ".");
            }
            return created.ScheduleId;
        }

        /// <summary>Loads every detail row of one schedule.</summary>
        public static List<ScheduleDetail> GetDetails(int scheduleId)
        {
            return Db.ReadList(
                "SELECT DetailId, ScheduleId, EmployeeId, DayNumber, ShiftId FROM ScheduleDetails WHERE ScheduleId = ? ORDER BY EmployeeId, DayNumber",
                MapDetail, Db.P(scheduleId));
        }

        /// <summary>
        /// Replaces every detail row of one employee inside one schedule with the
        /// supplied day→shift map, in a single transaction.
        /// </summary>
        public static void ReplaceEmployeeDetails(int scheduleId, int employeeId, Dictionary<int, int> dayShifts)
        {
            using (OleDbConnection connection = Db.CreateConnection())
            using (OleDbTransaction transaction = connection.BeginTransaction())
            using (OleDbCommand deleteCommand = connection.CreateCommand())
            using (OleDbCommand insertCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                insertCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM ScheduleDetails WHERE ScheduleId = ? AND EmployeeId = ?";
                deleteCommand.Parameters.Add(Db.P(scheduleId));
                deleteCommand.Parameters.Add(Db.P(employeeId));
                deleteCommand.ExecuteNonQuery();

                insertCommand.CommandText = "INSERT INTO ScheduleDetails (ScheduleId, EmployeeId, DayNumber, ShiftId) VALUES (?, ?, ?, ?)";
                OleDbParameter dayParameter;
                OleDbParameter shiftParameter;
                insertCommand.Parameters.Add(Db.P(scheduleId));
                insertCommand.Parameters.Add(Db.P(employeeId));
                dayParameter = insertCommand.Parameters.Add(Db.P(1));
                shiftParameter = insertCommand.Parameters.Add(Db.P(1));
                foreach (KeyValuePair<int, int> pair in dayShifts)
                {
                    dayParameter.Value = pair.Key;
                    shiftParameter.Value = pair.Value;
                    insertCommand.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }

        /// <summary>Reads the lock row of a month (treat a missing row or IsLocked=false as unlocked).</summary>
        public static MonthlyShiftLock GetLock(int year, int month)
        {
            List<MonthlyShiftLock> matches = Db.ReadList(
                "SELECT LockId, YearVal, MonthVal, IsLocked, LockedBy, LockedAt, UnlockedBy, UnlockedAt, UnlockReason " +
                "FROM MonthlyShiftLocks WHERE YearVal = ? AND MonthVal = ?",
                MapLock, Db.P(year), Db.P(month));
            return matches.Count > 0 ? matches[0] : null;
        }

        /// <summary>Inserts or updates the lock row for a month.</summary>
        public static void SetLock(int year, int month, bool isLocked, int? userId, string unlockReason)
        {
            MonthlyShiftLock existing = GetLock(year, month);
            if (existing == null)
            {
                if (isLocked)
                {
                    Db.ExecuteNonQuery(
                        "INSERT INTO MonthlyShiftLocks (YearVal, MonthVal, IsLocked, LockedBy, LockedAt) VALUES (?, ?, ?, ?, Now())",
                        Db.P(year), Db.P(month), Db.P(isLocked), Db.PNullableId(userId));
                }
                else
                {
                    throw new DataException("لا يوجد سجل قفل لشهر " + year + "-" + month + " لفتحه.");
                }
                return;
            }
            if (isLocked)
            {
                Db.ExecuteNonQuery(
                    "UPDATE MonthlyShiftLocks SET IsLocked = ?, LockedBy = ?, LockedAt = Now(), UnlockedBy = NULL, UnlockedAt = NULL, UnlockReason = NULL WHERE LockId = ?",
                    Db.P(isLocked), Db.PNullableId(userId), Db.P(existing.LockId));
            }
            else
            {
                Db.ExecuteNonQuery(
                    "UPDATE MonthlyShiftLocks SET IsLocked = ?, UnlockedBy = ?, UnlockedAt = Now(), UnlockReason = ? WHERE LockId = ?",
                    Db.P(isLocked), Db.PNullableId(userId), Db.P(unlockReason ?? string.Empty), Db.P(existing.LockId));
            }
        }
    }
}
