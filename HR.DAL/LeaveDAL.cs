using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Leave persistence (rule 15): types with editable annual quotas, requests
    /// and per-employee/year/type balances. Balance usage is always recomputed
    /// from the SUM of approved requests, never incremented in place, so the
    /// quota never drifts after edits, rejections or multi-station changes.
    /// All statements parameterized.
    /// </summary>
    public static class LeaveDAL
    {
        public static List<LeaveType> GetTypes()
        {
            return Db.ReadList(
                "SELECT LeaveTypeId, LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc FROM LeaveTypes ORDER BY LeaveTypeId",
                MapType);
        }

        public static LeaveType GetTypeById(int leaveTypeId)
        {
            List<LeaveType> rows = Db.ReadList(
                "SELECT LeaveTypeId, LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc FROM LeaveTypes WHERE LeaveTypeId = ?",
                MapType, Db.P(leaveTypeId));
            return rows.Count > 0 ? rows[0] : null;
        }

        private static LeaveType MapType(OleDbDataReader r)
        {
            LeaveType row = new LeaveType();
            row.LeaveTypeId = r.GetInt32(0);
            row.LeaveTypeCode = r.GetString(1);
            row.TypeName = r.GetString(2);
            row.AnnualDays = r.IsDBNull(3) ? (int?)null : Convert.ToInt32(r.GetValue(3));
            row.IsPaid = r.IsDBNull(4) ? (bool?)null : r.GetBoolean(4);
            row.RequiresDoc = r.IsDBNull(5) ? (bool?)null : r.GetBoolean(5);
            return row;
        }

        public static void UpdateTypeQuota(int leaveTypeId, int? annualDays, bool? isPaid, bool? requiresDoc)
        {
            Db.ExecuteNonQuery(
                "UPDATE LeaveTypes SET AnnualDays = ?, IsPaid = ?, RequiresDoc = ? WHERE LeaveTypeId = ?",
                Db.P(annualDays ?? 0), Db.PBool(isPaid), Db.PBool(requiresDoc), Db.P(leaveTypeId));
        }

        public static LeaveRequestView GetRequestView(int leaveId)
        {
            List<LeaveRequestView> rows = Db.ReadList(
                "SELECT v.LeaveId, v.EmployeeId, e.EmployeeCode, e.FullNameAr, v.LeaveTypeId, t.TypeName, " +
                "v.StartDate, v.EndDate, v.DaysCount, v.Reason, v.Status, v.ApprovedBy, v.CreatedAt " +
                "FROM (LeaveRequests v INNER JOIN Employees e ON v.EmployeeId = e.EmployeeId) " +
                "LEFT JOIN LeaveTypes t ON v.LeaveTypeId = t.LeaveTypeId WHERE v.LeaveId = ?",
                MapRequestView, Db.P(leaveId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static List<LeaveRequestView> SearchRequests(string status, int? employeeId,
            DateTime? fromDate, DateTime? toDate, int maxRows)
        {
            string sql =
                "SELECT TOP " + maxRows + " v.LeaveId, v.EmployeeId, e.EmployeeCode, e.FullNameAr, v.LeaveTypeId, t.TypeName, " +
                "v.StartDate, v.EndDate, v.DaysCount, v.Reason, v.Status, v.ApprovedBy, v.CreatedAt " +
                "FROM (LeaveRequests v INNER JOIN Employees e ON v.EmployeeId = e.EmployeeId) " +
                "LEFT JOIN LeaveTypes t ON v.LeaveTypeId = t.LeaveTypeId WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (!string.IsNullOrEmpty(status))
            {
                sql += " AND v.Status = ?";
                parameters.Add(Db.P(status));
            }
            if (employeeId != null)
            {
                sql += " AND v.EmployeeId = ?";
                parameters.Add(Db.P(employeeId.Value));
            }
            if (fromDate != null)
            {
                sql += " AND v.EndDate >= ?";
                parameters.Add(Db.PDate(fromDate.Value));
            }
            if (toDate != null)
            {
                sql += " AND v.StartDate <= ?";
                parameters.Add(Db.PDate(toDate.Value));
            }
            sql += " ORDER BY v.StartDate DESC, v.LeaveId DESC";
            return Db.ReadList(sql, MapRequestView, parameters.ToArray());
        }

        private static LeaveRequestView MapRequestView(OleDbDataReader r)
        {
            LeaveRequestView row = new LeaveRequestView();
            row.LeaveId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.EmployeeCode = r.IsDBNull(2) ? null : r.GetString(2);
            row.EmployeeName = r.IsDBNull(3) ? null : r.GetString(3);
            row.LeaveTypeId = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            row.TypeName = r.IsDBNull(5) ? null : r.GetString(5);
            row.StartDate = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6);
            row.EndDate = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7);
            row.DaysCount = r.IsDBNull(8) ? (int?)null : Convert.ToInt32(r.GetValue(8));
            row.Reason = r.IsDBNull(9) ? null : r.GetString(9);
            row.Status = r.IsDBNull(10) ? null : r.GetString(10);
            row.ApprovedBy = r.IsDBNull(11) ? (int?)null : Convert.ToInt32(r.GetValue(11));
            row.CreatedAt = r.IsDBNull(12) ? (DateTime?)null : r.GetDateTime(12);
            return row;
        }

        public static int InsertRequest(LeaveRequest request)
        {
            Db.ExecuteNonQuery(
                "INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, StartDate, EndDate, DaysCount, Reason, Status, CreatedAt, CreatedBy) " +
                "VALUES (?, ?, ?, ?, ?, ?, 'Pending', Now(), ?)",
                Db.P(request.EmployeeId ?? 0), Db.P(request.LeaveTypeId ?? 0),
                Db.PDateRequired(request.StartDate), Db.PDateRequired(request.EndDate),
                Db.P(request.DaysCount ?? 0), Db.P(request.Reason ?? string.Empty),
                Db.PNullableId(request.CreatedBy));
            return Convert.ToInt32(Db.ExecuteScalar("SELECT @@IDENTITY"));
        }

        public static void UpdateRequest(int leaveId, int leaveTypeId, DateTime startDate, DateTime endDate,
            int daysCount, string reason)
        {
            Db.ExecuteNonQuery(
                "UPDATE LeaveRequests SET LeaveTypeId = ?, StartDate = ?, EndDate = ?, DaysCount = ?, Reason = ? WHERE LeaveId = ?",
                Db.P(leaveTypeId), Db.PDate(leavingDate(startDate)), Db.PDate(leavingDate(endDate)),
                Db.P(daysCount), Db.P(reason ?? string.Empty), Db.P(leaveId));
        }

        private static DateTime? leavingDate(DateTime value)
        {
            return value.Date;
        }

        public static void SetRequestStatus(int leaveId, string status, int? approvedBy)
        {
            Db.ExecuteNonQuery(
                "UPDATE LeaveRequests SET Status = ?, ApprovedBy = ? WHERE LeaveId = ?",
                Db.P(status), Db.PNullableId(approvedBy), Db.P(leaveId));
        }

        public static bool HasOverlappingRequests(int employeeId, DateTime startDate, DateTime endDate, int? excludeLeaveId)
        {
            return Db.Count(
                "SELECT COUNT(*) FROM LeaveRequests WHERE EmployeeId = ? AND Status IN ('Pending', 'Approved') " +
                "AND StartDate <= ? AND EndDate >= ? AND LeaveId <> ?",
                Db.P(employeeId), Db.PDate(endDate), Db.PDate(startDate), Db.P(excludeLeaveId ?? 0)) > 0;
        }

        /// <summary>SUM of DaysCount over approved requests of the employee in one year (balance source of truth).</summary>
        public static int UsedDays(int employeeId, int year, int leaveTypeId)
        {
            object value = Db.ExecuteScalar(
                "SELECT SUM(DaysCount) FROM LeaveRequests WHERE EmployeeId = ? AND Status = 'Approved' " +
                "AND LeaveTypeId = ? AND YEAR(StartDate) = ?",
                Db.P(employeeId), Db.P(leaveTypeId), Db.P(year));
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        /// <summary>SUM over every year (Hajj-once-a-lifetime check).</summary>
        public static int UsedDaysAllYears(int employeeId, int leaveTypeId)
        {
            object value = Db.ExecuteScalar(
                "SELECT SUM(DaysCount) FROM LeaveRequests WHERE EmployeeId = ? AND Status = 'Approved' AND LeaveTypeId = ?",
                Db.P(employeeId), Db.P(leaveTypeId));
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        public static LeaveBalance GetBalance(int employeeId, int year, int leaveTypeId)
        {
            List<LeaveBalance> rows = Db.ReadList(
                "SELECT BalanceId, EmployeeId, YearVal, LeaveTypeId, QuotaDays, UsedDays, Remaining " +
                "FROM LeaveBalances WHERE EmployeeId = ? AND YearVal = ? AND LeaveTypeId = ?",
                MapBalance, Db.P(employeeId), Db.P(year), Db.P(leaveTypeId));
            return rows.Count > 0 ? rows[0] : null;
        }

        private static LeaveBalance MapBalance(OleDbDataReader r)
        {
            LeaveBalance row = new LeaveBalance();
            row.BalanceId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.YearVal = r.IsDBNull(2) ? (int?)null : Convert.ToInt32(r.GetValue(2));
            row.LeaveTypeId = r.IsDBNull(3) ? (int?)null : Convert.ToInt32(r.GetValue(3));
            row.QuotaDays = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            row.UsedDays = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5));
            row.Remaining = r.IsDBNull(6) ? (int?)null : Convert.ToInt32(r.GetValue(6));
            return row;
        }

        public static List<LeaveBalance> LoadBalances(int employeeId, int year)
        {
            return Db.ReadList(
                "SELECT BalanceId, EmployeeId, YearVal, LeaveTypeId, QuotaDays, UsedDays, Remaining " +
                "FROM LeaveBalances WHERE EmployeeId = ? AND YearVal = ? ORDER BY LeaveTypeId",
                MapBalance, Db.P(employeeId), Db.P(year));
        }

        /// <summary>Insert-or-update keyed by (EmployeeId, YearVal, LeaveTypeId) — UQ_LevBal.</summary>
        public static void UpsertBalance(LeaveBalance balance)
        {
            LeaveBalance existing = GetBalance(balance.EmployeeId ?? 0, balance.YearVal ?? 0, balance.LeaveTypeId ?? 0);
            if (existing == null)
            {
                Db.ExecuteNonQuery(
                    "INSERT INTO LeaveBalances (EmployeeId, YearVal, LeaveTypeId, QuotaDays, UsedDays, Remaining) " +
                    "VALUES (?, ?, ?, ?, ?, ?)",
                    Db.P(balance.EmployeeId ?? 0), Db.P(balance.YearVal ?? 0), Db.P(balance.LeaveTypeId ?? 0),
                    Db.P(balance.QuotaDays ?? 0), Db.P(balance.UsedDays ?? 0), Db.P(balance.Remaining ?? 0));
            }
            else
            {
                Db.ExecuteNonQuery(
                    "UPDATE LeaveBalances SET QuotaDays = ?, UsedDays = ?, Remaining = ? WHERE BalanceId = ?",
                    Db.P(balance.QuotaDays ?? 0), Db.P(balance.UsedDays ?? 0), Db.P(balance.Remaining ?? 0),
                    Db.P(existing.BalanceId));
            }
        }
    }
}
