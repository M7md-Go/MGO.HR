using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Read/delete access to the [AttendanceExceptions] registry. Engine rows
    /// (CreatedBy IS NULL) are regenerated on every recalculation; only manual
    /// rows (CreatedBy NOT NULL) are meant to be removed by the administrator.
    /// </summary>
    public static class AttendanceExceptionDAL
    {
        public static List<AttendanceExceptionView> Browse(int? employeeId, DateTime? fromDate,
            DateTime? toDate, string excType, int maxRows)
        {
            string sql =
                "SELECT TOP " + maxRows + " x.ExceptionId, x.EmployeeId, e.EmployeeCode, e.FullNameAr, x.AttDate, " +
                "x.ExcType, x.ExcValue, x.CreatedBy, x.CreatedAt " +
                "FROM (AttendanceExceptions x INNER JOIN Employees e ON x.EmployeeId = e.EmployeeId) WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (employeeId != null)
            {
                sql += " AND x.EmployeeId = ?";
                parameters.Add(Db.P(employeeId.Value));
            }
            if (fromDate != null)
            {
                sql += " AND x.AttDate >= ?";
                parameters.Add(Db.PDate(fromDate.Value.Date));
            }
            if (toDate != null)
            {
                sql += " AND x.AttDate <= ?";
                parameters.Add(Db.PDate(toDate.Value.Date));
            }
            if (!string.IsNullOrEmpty(excType))
            {
                sql += " AND x.ExcType = ?";
                parameters.Add(Db.P(excType));
            }
            sql += " ORDER BY x.AttDate DESC, x.ExceptionId DESC";
            return Db.ReadList(sql, MapView, parameters.ToArray());
        }

        public static int? CreatedByOf(int exceptionId)
        {
            object value = Db.ExecuteScalar(
                "SELECT CreatedBy FROM AttendanceExceptions WHERE ExceptionId = ?", Db.P(exceptionId));
            if (value == null || value == DBNull.Value)
            {
                return (int?)null;
            }
            return Convert.ToInt32(value);
        }

        public static void Delete(int exceptionId)
        {
            Db.ExecuteNonQuery("DELETE FROM AttendanceExceptions WHERE ExceptionId = ?", Db.P(exceptionId));
        }

        private static AttendanceExceptionView MapView(OleDbDataReader r)
        {
            AttendanceExceptionView view = new AttendanceExceptionView();
            view.ExceptionId = r.GetInt32(0);
            view.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            view.EmployeeCode = r.IsDBNull(2) ? null : r.GetString(2);
            view.EmployeeName = r.IsDBNull(3) ? null : r.GetString(3);
            view.AttDate = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            view.ExcType = r.IsDBNull(5) ? null : r.GetString(5);
            view.ExcValue = r.IsDBNull(6) ? (int?)null : Convert.ToInt32(r.GetValue(6));
            view.CreatedBy = r.IsDBNull(7) ? (int?)null : Convert.ToInt32(r.GetValue(7));
            view.CreatedAt = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8);
            return view;
        }
    }
}
