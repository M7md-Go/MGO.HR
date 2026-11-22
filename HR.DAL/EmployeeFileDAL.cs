using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>Data access for per-employee document attachments (EmployeeFiles).</summary>
    public static class EmployeeFileDAL
    {
        public static List<EmployeeFile> ListByEmployee(int employeeId)
        {
            string sql = "SELECT FileId, EmployeeId, FilePath, Category, Notes, CreatedAt, CreatedBy " +
                         "FROM EmployeeFiles WHERE EmployeeId = ? ORDER BY FileId DESC";
            return Db.ReadList<EmployeeFile>(sql, MapFile, Db.P(employeeId));
        }

        /// <summary>Inserted and returns the new FileId (Jet: SELECT @@IDENTITY inside the same transaction).</summary>
        public static int Insert(EmployeeFile file, int? createdBy)
        {
            string sql = "INSERT INTO EmployeeFiles (EmployeeId, FilePath, Category, Notes, CreatedAt, CreatedBy) " +
                         "VALUES (?, ?, ?, ?, Now(), ?)";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(file.EmployeeId));
                    command.Parameters.Add(Db.P(file.FilePath ?? string.Empty));
                    command.Parameters.Add(Db.P(file.Category ?? string.Empty));
                    command.Parameters.Add(Db.P(file.Notes ?? string.Empty));
                    command.Parameters.Add(Db.PNullableId(createdBy));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Delete(int fileId)
        {
            Db.ExecuteNonQuery("DELETE FROM EmployeeFiles WHERE FileId = ?", Db.P(fileId));
        }

        public static int CountByEmployee(int employeeId)
        {
            return Db.Count("SELECT COUNT(*) FROM EmployeeFiles WHERE EmployeeId = ?", Db.P(employeeId));
        }

        private static EmployeeFile MapFile(OleDbDataReader reader)
        {
            EmployeeFile file = new EmployeeFile();
            file.FileId = reader.GetInt32(0);
            file.EmployeeId = reader.GetInt32(1);
            file.FilePath = reader.IsDBNull(2) ? null : reader.GetString(2);
            file.Category = reader.IsDBNull(3) ? null : reader.GetString(3);
            file.Notes = reader.IsDBNull(4) ? null : reader.GetString(4);
            file.CreatedAt = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5);
            file.CreatedBy = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
            return file;
        }
    }
}
