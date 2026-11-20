using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>Penalties CRUD (rule 17): parameterized; joins employee names for the browser.</summary>
    public static class PenaltyDAL
    {
        public static Penalty GetById(int penaltyId)
        {
            List<Penalty> rows = Db.ReadList(
                "SELECT PenaltyId, EmployeeId, AttDate, PenaltyDate, TypeCode, Amount, Reason, IssuedBy, CreatedAt " +
                "FROM Penalties WHERE PenaltyId = ?", MapPenalty, Db.P(penaltyId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public static List<PenaltyView> Search(int? employeeId, DateTime? fromDate, DateTime? toDate,
            string typeCode, int maxRows)
        {
            string sql =
                "SELECT TOP " + maxRows + " p.PenaltyId, p.EmployeeId, e.EmployeeCode, e.FullNameAr, p.AttDate, p.PenaltyDate, " +
                "p.TypeCode, p.Amount, p.Reason, p.IssuedBy, p.CreatedAt " +
                "FROM (Penalties p INNER JOIN Employees e ON p.EmployeeId = e.EmployeeId) WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (employeeId != null)
            {
                sql += " AND p.EmployeeId = ?";
                parameters.Add(Db.P(employeeId.Value));
            }
            if (fromDate != null)
            {
                sql += " AND p.PenaltyDate >= ?";
                parameters.Add(Db.PDate(fromDate.Value));
            }
            if (toDate != null)
            {
                sql += " AND p.PenaltyDate <= ?";
                parameters.Add(Db.PDate(toDate.Value));
            }
            if (!string.IsNullOrEmpty(typeCode))
            {
                sql += " AND p.TypeCode = ?";
                parameters.Add(Db.P(typeCode));
            }
            sql += " ORDER BY p.PenaltyDate DESC, p.PenaltyId DESC";
            return Db.ReadList(sql, MapView, parameters.ToArray());
        }

        private static Penalty MapPenalty(OleDbDataReader r)
        {
            Penalty row = new Penalty();
            row.PenaltyId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.AttDate = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2);
            row.PenaltyDate = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3);
            row.TypeCode = r.IsDBNull(4) ? null : r.GetString(4);
            row.Amount = r.IsDBNull(5) ? (decimal?)null : Convert.ToDecimal(r.GetValue(5));
            row.Reason = r.IsDBNull(6) ? null : r.GetString(6);
            row.IssuedBy = r.IsDBNull(7) ? (int?)null : Convert.ToInt32(r.GetValue(7));
            row.CreatedAt = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8);
            return row;
        }

        private static PenaltyView MapView(OleDbDataReader r)
        {
            PenaltyView row = new PenaltyView();
            row.PenaltyId = r.GetInt32(0);
            row.EmployeeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
            row.EmployeeCode = r.IsDBNull(2) ? null : r.GetString(2);
            row.EmployeeName = r.IsDBNull(3) ? null : r.GetString(3);
            row.AttDate = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
            row.PenaltyDate = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5);
            row.TypeCode = r.IsDBNull(6) ? null : r.GetString(6);
            row.Amount = r.IsDBNull(7) ? (decimal?)null : Convert.ToDecimal(r.GetValue(7));
            row.Reason = r.IsDBNull(8) ? null : r.GetString(8);
            row.IssuedBy = r.IsDBNull(9) ? (int?)null : Convert.ToInt32(r.GetValue(9));
            row.CreatedAt = r.IsDBNull(10) ? (DateTime?)null : r.GetDateTime(10);
            return row;
        }

        public static int Insert(Penalty penalty, int? issuedBy)
        {
            string sql =
                "INSERT INTO Penalties (EmployeeId, AttDate, PenaltyDate, TypeCode, Amount, Reason, IssuedBy, CreatedAt) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, Now())";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(penalty.EmployeeId ?? 0));
                    command.Parameters.Add(Db.PDate(penalty.AttDate));
                    command.Parameters.Add(Db.PDateRequired(penalty.PenaltyDate));
                    command.Parameters.Add(Db.P(penalty.TypeCode ?? string.Empty));
                    command.Parameters.Add(Db.PCurrency(penalty.Amount));
                    command.Parameters.Add(Db.P(penalty.Reason ?? string.Empty));
                    command.Parameters.Add(Db.PNullableId(issuedBy));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Update(int penaltyId, Penalty penalty)
        {
            Db.ExecuteNonQuery(
                "UPDATE Penalties SET AttDate = ?, PenaltyDate = ?, TypeCode = ?, Amount = ?, Reason = ? WHERE PenaltyId = ?",
                Db.PDate(penalty.AttDate), Db.PDate(penalty.PenaltyDate),
                Db.P(penalty.TypeCode ?? string.Empty), Db.PCurrency(penalty.Amount),
                Db.P(penalty.Reason ?? string.Empty), Db.P(penaltyId));
        }

        public static void Delete(int penaltyId)
        {
            Db.ExecuteNonQuery("DELETE FROM Penalties WHERE PenaltyId = ?", Db.P(penaltyId));
        }
    }
}
