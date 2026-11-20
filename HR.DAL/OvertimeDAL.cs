using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>Read access to the Overtime table for reports and approvals.</summary>
    public static class OvertimeDAL
    {
        /// <summary>Browser rows joined with employees, filtered by date/status/employee.</summary>
        public static List<OvertimeView> Search(DateTime? fromDate, DateTime? toDate,
            int? employeeId, string status, int maxRows)
        {
            StringBuilder sql = new StringBuilder(
                "SELECT TOP " + maxRows + " o.OvertimeId, o.EmployeeId, e.EmployeeCode, e.FullNameAr, o.OtDate, " +
                "o.PlannedMinutes, o.ActualMinutes, o.EffectiveMinutes, o.Rate, o.Source, o.Status " +
                "FROM (Overtime o INNER JOIN Employees e ON o.EmployeeId = e.EmployeeId) WHERE 1 = 1");
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (fromDate != null)
            {
                sql.Append(" AND o.OtDate >= ?");
                parameters.Add(Db.PDate(fromDate.Value.Date));
            }
            if (toDate != null)
            {
                sql.Append(" AND o.OtDate <= ?");
                parameters.Add(Db.PDate(toDate.Value.Date));
            }
            if (employeeId != null)
            {
                sql.Append(" AND o.EmployeeId = ?");
                parameters.Add(Db.P(employeeId.Value));
            }
            if (!string.IsNullOrEmpty(status))
            {
                sql.Append(" AND o.Status = ?");
                parameters.Add(Db.P(status));
            }
            sql.Append(" ORDER BY o.OtDate DESC, e.EmployeeCode");
            return Db.ReadList(sql.ToString(), MapView, parameters.ToArray());
        }

        /// <summary>Sets the approval state of one overtime row.</summary>
        public static void SetStatus(int overtimeId, string status, int? approvedBy)
        {
            Db.ExecuteNonQuery(
                "UPDATE Overtime SET Status = ?, ApprovedBy = ?, ApprovedAt = Now() WHERE OvertimeId = ?",
                Db.P(status), Db.PNullableId(approvedBy), Db.P(overtimeId));
        }

        private static OvertimeView MapView(OleDbDataReader r)
        {
            OvertimeView view = new OvertimeView();
            view.OvertimeId = r.GetInt32(0);
            view.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            view.EmployeeCode = r.IsDBNull(2) ? null : r.GetString(2);
            view.EmployeeName = r.IsDBNull(3) ? null : r.GetString(3);
            view.OtDate = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            view.PlannedMinutes = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5));
            view.ActualMinutes = r.IsDBNull(6) ? (int?)null : Convert.ToInt32(r.GetValue(6));
            view.EffectiveMinutes = r.IsDBNull(7) ? (int?)null : Convert.ToInt32(r.GetValue(7));
            view.Rate = r.IsDBNull(8) ? (double?)null : Convert.ToDouble(r.GetValue(8));
            view.Source = r.IsDBNull(9) ? null : r.GetString(9);
            view.Status = r.IsDBNull(10) ? null : r.GetString(10);
            return view;
        }
    }
}
