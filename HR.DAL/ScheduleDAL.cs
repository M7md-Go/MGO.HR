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
            entity.SubmittedBy = r.IsDBNull(9) ? (int?)null : Convert.ToInt32(r.GetValue(9));
            entity.SubmittedAt = r.IsDBNull(10) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(10));
            entity.RejectReason = r.IsDBNull(11) ? null : r.GetString(11);
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
                "SELECT ScheduleId, YearVal, MonthVal, Status, CreatedBy, CreatedAt, ApprovedBy, ApprovedAt, LockedAt, " +
                "SubmittedBy, SubmittedAt, RejectReason " +
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

        // ---------------- approval workflow (order RosterCore lessons) ----------------

        private static ScheduleApproval MapApproval(OleDbDataReader r)
        {
            ScheduleApproval row = new ScheduleApproval();
            row.ApprovalId = r.GetInt32(0);
            row.ScheduleId = r.GetInt32(1);
            row.Stage = r.IsDBNull(2) ? null : r.GetString(2);
            row.UserId = r.IsDBNull(3) ? (int?)null : Convert.ToInt32(r.GetValue(3));
            row.Reason = r.IsDBNull(4) ? null : r.GetString(4);
            row.CreatedAt = r.IsDBNull(5) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(5));
            return row;
        }

        /// <summary>Marks the schedule header as submitted for approval.</summary>
        public static void SetScheduleSubmitted(int scheduleId, int? userId)
        {
            Db.ExecuteNonQuery(
                "UPDATE MonthlySchedules SET Status = ?, SubmittedBy = ?, SubmittedAt = Now(), RejectReason = NULL WHERE ScheduleId = ?",
                Db.P("Submitted"), Db.PNullableId(userId), Db.P(scheduleId));
        }

        /// <summary>Marks the schedule header as finally approved.</summary>
        public static void SetScheduleApproved(int scheduleId, int? userId)
        {
            Db.ExecuteNonQuery(
                "UPDATE MonthlySchedules SET Status = ?, ApprovedBy = ?, ApprovedAt = Now() WHERE ScheduleId = ?",
                Db.P("Approved"), Db.PNullableId(userId), Db.P(scheduleId));
        }

        /// <summary>Returns the schedule to draft with the mandatory rejection reason.</summary>
        public static void SetScheduleRejected(int scheduleId, int? userId, string reason)
        {
            Db.ExecuteNonQuery(
                "UPDATE MonthlySchedules SET Status = ?, RejectReason = ? WHERE ScheduleId = ?",
                Db.P("Rejected"), Db.P(reason ?? string.Empty), Db.P(scheduleId));
        }

        /// <summary>Appends one stage-transition row to the approval ledger.</summary>
        public static void InsertApproval(int scheduleId, string stage, int? userId, string reason)
        {
            Db.ExecuteNonQuery(
                "INSERT INTO ScheduleApprovals (ScheduleId, Stage, UserId, Reason, CreatedAt) VALUES (?, ?, ?, ?, Now())",
                Db.P(scheduleId), Db.P(stage), Db.PNullableId(userId), Db.P(reason));
        }

        /// <summary>Full approval ledger of one schedule, oldest first.</summary>
        public static List<ScheduleApproval> GetApprovals(int scheduleId)
        {
            return Db.ReadList(
                "SELECT ApprovalId, ScheduleId, Stage, UserId, Reason, CreatedAt " +
                "FROM ScheduleApprovals WHERE ScheduleId = ? ORDER BY ApprovalId",
                MapApproval, Db.P(scheduleId));
        }

        // ---------------- shift-change requests (RosterCore) ----------------

        private static ScheduleChangeRequest MapChangeRequest(OleDbDataReader r)
        {
            ScheduleChangeRequest row = new ScheduleChangeRequest();
            row.RequestId = r.GetInt32(0);
            row.YearVal = Convert.ToInt32(r.GetValue(1));
            row.MonthVal = Convert.ToInt32(r.GetValue(2));
            row.Status = r.IsDBNull(3) ? null : r.GetString(3);
            row.Reason = r.IsDBNull(4) ? null : r.GetString(4);
            row.RequestedBy = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5));
            row.DecidedBy = r.IsDBNull(6) ? (int?)null : Convert.ToInt32(r.GetValue(6));
            row.DecideReason = r.IsDBNull(7) ? null : r.GetString(7);
            row.ExecutedBy = r.IsDBNull(8) ? (int?)null : Convert.ToInt32(r.GetValue(8));
            row.CreatedAt = r.IsDBNull(9) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(9));
            row.DecidedAt = r.IsDBNull(10) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(10));
            row.ExecutedAt = r.IsDBNull(11) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(11));
            return row;
        }

        private static ScheduleChangeItem MapChangeItem(OleDbDataReader r)
        {
            ScheduleChangeItem row = new ScheduleChangeItem();
            row.ItemId = r.GetInt32(0);
            row.RequestId = r.GetInt32(1);
            row.EmployeeId = Convert.ToInt32(r.GetValue(2));
            row.DayNumber = Convert.ToInt32(r.GetValue(3));
            row.OldShiftId = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            row.NewShiftId = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5));
            return row;
        }

        /// <summary>Inserts one change request (Pending) and returns its id via @@IDENTITY.</summary>
        public static int InsertChangeRequest(int year, int month, string reason, int? userId)
        {
            using (OleDbConnection connection = Db.CreateConnection())
            using (OleDbCommand command = new OleDbCommand(
                "INSERT INTO ScheduleChangeRequests (YearVal, MonthVal, Status, Reason, RequestedBy, CreatedAt) " +
                "VALUES (?, ?, ?, ?, ?, Now())", connection))
            {
                command.Parameters.Add(Db.P(year));
                command.Parameters.Add(Db.P(month));
                command.Parameters.Add(Db.P("Pending"));
                command.Parameters.Add(Db.P(reason ?? string.Empty));
                command.Parameters.Add(Db.PNullableId(userId));
                command.ExecuteNonQuery();
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            }
        }

        /// <summary>Inserts one item row of a change request.</summary>
        public static void InsertChangeItem(int requestId, int employeeId, int dayNumber,
            int? oldShiftId, int? newShiftId)
        {
            Db.ExecuteNonQuery(
                "INSERT INTO ScheduleChangeItems (RequestId, EmployeeId, DayNumber, OldShiftId, NewShiftId) " +
                "VALUES (?, ?, ?, ?, ?)",
                Db.P(requestId), Db.P(employeeId), Db.P(dayNumber),
                Db.PNullableId(oldShiftId), Db.PNullableId(newShiftId));
        }

        /// <summary>Change requests ordered newest first, optionally filtered by status.</summary>
        public static List<ScheduleChangeRequest> GetChangeRequests(string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                return Db.ReadList(
                    "SELECT RequestId, YearVal, MonthVal, Status, Reason, RequestedBy, DecidedBy, DecideReason, ExecutedBy, " +
                    "CreatedAt, DecidedAt, ExecutedAt FROM ScheduleChangeRequests ORDER BY RequestId DESC",
                    MapChangeRequest);
            }
            return Db.ReadList(
                "SELECT RequestId, YearVal, MonthVal, Status, Reason, RequestedBy, DecidedBy, DecideReason, ExecutedBy, " +
                "CreatedAt, DecidedAt, ExecutedAt FROM ScheduleChangeRequests WHERE Status = ? ORDER BY RequestId DESC",
                MapChangeRequest, Db.P(status));
        }

        /// <summary>One change request by id, or null.</summary>
        public static ScheduleChangeRequest GetChangeRequest(int requestId)
        {
            List<ScheduleChangeRequest> matches = Db.ReadList(
                "SELECT RequestId, YearVal, MonthVal, Status, Reason, RequestedBy, DecidedBy, DecideReason, ExecutedBy, " +
                "CreatedAt, DecidedAt, ExecutedAt FROM ScheduleChangeRequests WHERE RequestId = ?",
                MapChangeRequest, Db.P(requestId));
            return matches.Count > 0 ? matches[0] : null;
        }

        /// <summary>Item rows of one change request.</summary>
        public static List<ScheduleChangeItem> GetChangeItems(int requestId)
        {
            return Db.ReadList(
                "SELECT ItemId, RequestId, EmployeeId, DayNumber, OldShiftId, NewShiftId " +
                "FROM ScheduleChangeItems WHERE RequestId = ? ORDER BY ItemId",
                MapChangeItem, Db.P(requestId));
        }

        /// <summary>Applies the approval/rejection decision to a change request.</summary>
        public static void SetChangeRequestDecided(int requestId, bool approved, int? userId, string reason)
        {
            Db.ExecuteNonQuery(
                "UPDATE ScheduleChangeRequests SET Status = ?, DecidedBy = ?, DecidedAt = Now(), DecideReason = ? WHERE RequestId = ?",
                Db.P(approved ? "Approved" : "Rejected"), Db.PNullableId(userId),
                Db.P(approved ? string.Empty : (reason ?? string.Empty)), Db.P(requestId));
        }

        /// <summary>Marks a change request executed by the acting user.</summary>
        public static void SetChangeRequestExecuted(int requestId, int? userId)
        {
            Db.ExecuteNonQuery(
                "UPDATE ScheduleChangeRequests SET Status = ?, ExecutedBy = ?, ExecutedAt = Now() WHERE RequestId = ?",
                Db.P("Executed"), Db.PNullableId(userId), Db.P(requestId));
        }
    }
}
