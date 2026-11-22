using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// LeaveTypes table access (rule 15 administration): code/name/quota/paid/doc
    /// management of the leave catalogue. Deleting is blocked at the DAL level
    /// when requests or balances reference the type.
    /// </summary>
    public static class LeaveTypeDAL
    {
        public static List<LeaveType> List()
        {
            return Db.ReadList(
                "SELECT LeaveTypeId, LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc " +
                "FROM LeaveTypes ORDER BY LeaveTypeCode",
                MapType);
        }

        public static LeaveType GetById(int leaveTypeId)
        {
            List<LeaveType> rows = Db.ReadList(
                "SELECT LeaveTypeId, LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc " +
                "FROM LeaveTypes WHERE LeaveTypeId = ?",
                MapType, Db.P(leaveTypeId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static LeaveType GetByCode(string code)
        {
            List<LeaveType> rows = Db.ReadList(
                "SELECT LeaveTypeId, LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc " +
                "FROM LeaveTypes WHERE LeaveTypeCode = ?",
                MapType, Db.P(code));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static int Insert(LeaveType type)
        {
            string sql = "INSERT INTO LeaveTypes (LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc) " +
                "VALUES (?, ?, ?, ?, ?)";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(type.LeaveTypeCode));
                    command.Parameters.Add(Db.P(type.TypeName));
                    command.Parameters.Add(Db.P(type.AnnualDays));
                    command.Parameters.Add(Db.PBool(type.IsPaid));
                    command.Parameters.Add(Db.PBool(type.RequiresDoc));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Update(LeaveType type)
        {
            Db.ExecuteNonQuery(
                "UPDATE LeaveTypes SET LeaveTypeCode = ?, TypeName = ?, AnnualDays = ?, IsPaid = ?, RequiresDoc = ? " +
                "WHERE LeaveTypeId = ?",
                Db.P(type.LeaveTypeCode), Db.P(type.TypeName), Db.P(type.AnnualDays),
                Db.PBool(type.IsPaid), Db.PBool(type.RequiresDoc), Db.P(type.LeaveTypeId));
        }

        /// <summary>Number of leave requests + balance rows that reference the type.</summary>
        public static long CountUsages(int leaveTypeId)
        {
            long requests = Db.Count("SELECT COUNT(*) FROM LeaveRequests WHERE LeaveTypeId = ?",
                Db.P(leaveTypeId));
            long balances = Db.Count("SELECT COUNT(*) FROM LeaveBalances WHERE LeaveTypeId = ?",
                Db.P(leaveTypeId));
            return requests + balances;
        }

        public static void Delete(int leaveTypeId)
        {
            Db.ExecuteNonQuery("DELETE FROM LeaveTypes WHERE LeaveTypeId = ?", Db.P(leaveTypeId));
        }

        private static LeaveType MapType(OleDbDataReader r)
        {
            LeaveType row = new LeaveType();
            row.LeaveTypeId = r.GetInt32(0);
            row.LeaveTypeCode = r.IsDBNull(1) ? null : r.GetString(1);
            row.TypeName = r.IsDBNull(2) ? null : r.GetString(2);
            row.AnnualDays = r.IsDBNull(3) ? (int?)null : Convert.ToInt32(r.GetValue(3));
            row.IsPaid = r.IsDBNull(4) ? (bool?)null : Convert.ToInt32(r.GetValue(4)) != 0;
            row.RequiresDoc = r.IsDBNull(5) ? (bool?)null : Convert.ToInt32(r.GetValue(5)) != 0;
            return row;
        }
    }
}
