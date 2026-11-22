using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// LongLeaveTypes table access: catalogue of long-leave kinds (unlimited /
    /// capped in months) that the long-leave requests reference.
    /// </summary>
    public static class LongLeaveTypeDAL
    {
        public static List<LongLeaveType> List()
        {
            return Db.ReadList(
                "SELECT LongLeaveTypeId, TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes " +
                "FROM LongLeaveTypes ORDER BY TypeCode",
                MapType);
        }

        public static LongLeaveType GetById(int longLeaveTypeId)
        {
            List<LongLeaveType> rows = Db.ReadList(
                "SELECT LongLeaveTypeId, TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes " +
                "FROM LongLeaveTypes WHERE LongLeaveTypeId = ?",
                MapType, Db.P(longLeaveTypeId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static LongLeaveType GetByCode(string code)
        {
            List<LongLeaveType> rows = Db.ReadList(
                "SELECT LongLeaveTypeId, TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes " +
                "FROM LongLeaveTypes WHERE TypeCode = ?",
                MapType, Db.P(code));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static int Insert(LongLeaveType type)
        {
            string sql = "INSERT INTO LongLeaveTypes (TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes) " +
                "VALUES (?, ?, ?, ?, ?, ?)";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(type.TypeCode));
                    command.Parameters.Add(Db.P(type.TypeName));
                    command.Parameters.Add(Db.PBool(type.IsLimited));
                    command.Parameters.Add(Db.P(type.MaxMonths));
                    command.Parameters.Add(Db.PBool(type.IsActive));
                    command.Parameters.Add(Db.P(type.Notes));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Update(LongLeaveType type)
        {
            Db.ExecuteNonQuery(
                "UPDATE LongLeaveTypes SET TypeCode = ?, TypeName = ?, IsLimited = ?, MaxMonths = ?, IsActive = ?, Notes = ? " +
                "WHERE LongLeaveTypeId = ?",
                Db.P(type.TypeCode), Db.P(type.TypeName), Db.PBool(type.IsLimited), Db.P(type.MaxMonths),
                Db.PBool(type.IsActive), Db.P(type.Notes), Db.P(type.LongLeaveTypeId));
        }

        /// <summary>Number of long-leave requests that reference the type.</summary>
        public static long CountUsages(int longLeaveTypeId)
        {
            return Db.Count("SELECT COUNT(*) FROM LongLeaves WHERE TypeId = ?", Db.P(longLeaveTypeId));
        }

        public static void Delete(int longLeaveTypeId)
        {
            Db.ExecuteNonQuery("DELETE FROM LongLeaveTypes WHERE LongLeaveTypeId = ?", Db.P(longLeaveTypeId));
        }

        private static LongLeaveType MapType(OleDbDataReader r)
        {
            LongLeaveType row = new LongLeaveType();
            row.LongLeaveTypeId = r.GetInt32(0);
            row.TypeCode = r.IsDBNull(1) ? null : r.GetString(1);
            row.TypeName = r.IsDBNull(2) ? null : r.GetString(2);
            row.IsLimited = r.IsDBNull(3) ? (bool?)null : Convert.ToInt32(r.GetValue(3)) != 0;
            row.MaxMonths = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            row.IsActive = r.IsDBNull(5) ? (bool?)null : Convert.ToInt32(r.GetValue(5)) != 0;
            row.Notes = r.IsDBNull(6) ? null : r.GetString(6);
            return row;
        }
    }
}
