using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>EmployeeHistory table access — the historical trail of every employee (BR-02/18).</summary>
    public static class EmployeeHistoryDAL
    {
        public static void Insert(EmployeeHistory history, OleDbConnection connection, OleDbTransaction transaction)
        {
            string sql = "INSERT INTO EmployeeHistory (EmployeeId, ChangeDate, ChangeType, FieldName, OldValue, NewValue, ChangedBy) " +
                         "VALUES (?, ?, ?, ?, ?, ?, ?)";
            using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
            {
                command.Parameters.Add(Db.P(history.EmployeeId));
                command.Parameters.Add(Db.PDateRequired(history.ChangeDate ?? DateTime.Now));
                command.Parameters.Add(Db.P(history.ChangeType));
                command.Parameters.Add(Db.P(history.FieldName));
                command.Parameters.Add(Db.P(history.OldValue));
                command.Parameters.Add(Db.P(history.NewValue));
                command.Parameters.Add(Db.PNullableId(history.ChangedBy));
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Records one structural change outside a larger transaction.</summary>
        public static void Insert(EmployeeHistory history)
        {
            Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                Insert(history, connection, transaction);
                return true;
            });
        }

        public static List<EmployeeHistory> GetForEmployee(int employeeId, int maxRows)
        {
            string sql = "SELECT TOP " + maxRows + " HistoryId, EmployeeId, ChangeDate, ChangeType, FieldName, OldValue, NewValue, ChangedBy " +
                         "FROM EmployeeHistory WHERE EmployeeId = ? ORDER BY ChangeDate DESC, HistoryId DESC";
            List<EmployeeHistory> rows = Db.ReadList(sql,
                delegate(OleDbDataReader reader)
                {
                    EmployeeHistory history = new EmployeeHistory();
                    history.HistoryId = reader.GetInt32(0);
                    history.EmployeeId = reader.GetInt32(1);
                    history.ChangeDate = reader.GetDateTime(2);
                    history.ChangeType = reader.GetString(3);
                    history.FieldName = reader.IsDBNull(4) ? null : reader.GetString(4);
                    history.OldValue = reader.IsDBNull(5) ? null : reader.GetString(5);
                    history.NewValue = reader.IsDBNull(6) ? null : reader.GetString(6);
                    history.ChangedBy = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
                    return history;
                },
                Db.P(employeeId));
            return rows;
        }
    }
}
