using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>All Employees table access. SQL lives here only — never in Forms.</summary>
    public static class EmployeeDAL
    {
        private static Employee MapRow(OleDbDataReader reader)
        {
            Employee employee = new Employee();
            employee.EmployeeId = reader.GetInt32(0);
            employee.EmployeeCode = reader.IsDBNull(1) ? null : reader.GetString(1);
            employee.NationalId = reader.IsDBNull(2) ? null : reader.GetString(2);
            employee.FullNameAr = reader.IsDBNull(3) ? null : reader.GetString(3);
            employee.FullNameEn = reader.IsDBNull(4) ? null : reader.GetString(4);
            employee.Gender = reader.IsDBNull(5) ? null : reader.GetString(5);
            employee.BirthDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
            employee.HireDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7);
            employee.EndDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8);
            employee.CompanyId = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9);
            employee.BranchId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10);
            employee.DepartmentId = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11);
            employee.SectionId = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12);
            employee.JobTitleId = reader.IsDBNull(13) ? (int?)null : reader.GetInt32(13);
            employee.ActualJobId = reader.IsDBNull(14) ? (int?)null : reader.GetInt32(14);
            employee.EmployeeStatusId = reader.IsDBNull(15) ? (int?)null : reader.GetInt32(15);
            employee.QualificationId = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16);
            employee.EmploymentType = reader.IsDBNull(17) ? null : reader.GetString(17);
            employee.Phone = reader.IsDBNull(18) ? null : reader.GetString(18);
            employee.Email = reader.IsDBNull(19) ? null : reader.GetString(19);
            employee.Address = reader.IsDBNull(20) ? null : reader.GetString(20);
            employee.PhotoPath = reader.IsDBNull(21) ? null : reader.GetString(21);
            employee.DeviceEnrollNumber = reader.IsDBNull(22) ? null : reader.GetString(22);
            employee.ShiftGroupCode = reader.IsDBNull(23) ? null : reader.GetString(23);
            employee.ContractHours = reader.IsDBNull(24) ? (float?)null : reader.GetFloat(24);
            employee.Salary = reader.IsDBNull(25) ? (decimal?)null : reader.GetDecimal(25);
            employee.Status = reader.IsDBNull(26) ? null : reader.GetString(26);
            employee.IsDeleted = reader.IsDBNull(27) ? (bool?)null : reader.GetBoolean(27);
            employee.Notes = reader.IsDBNull(28) ? null : reader.GetString(28);
            employee.CreatedAt = reader.IsDBNull(29) ? (DateTime?)null : reader.GetDateTime(29);
            employee.CreatedBy = reader.IsDBNull(30) ? (int?)null : reader.GetInt32(30);
            employee.UpdatedAt = reader.IsDBNull(31) ? (DateTime?)null : reader.GetDateTime(31);
            employee.UpdatedBy = reader.IsDBNull(32) ? (int?)null : reader.GetInt32(32);
            return employee;
        }

        private const string SelectColumns = "EmployeeId, EmployeeCode, NationalId, FullNameAr, FullNameEn, Gender, BirthDate, HireDate, EndDate, CompanyId, BranchId, DepartmentId, SectionId, JobTitleId, ActualJobId, EmployeeStatusId, QualificationId, EmploymentType, Phone, Email, Address, PhotoPath, DeviceEnrollNumber, ShiftGroupCode, ContractHours, Salary, Status, IsDeleted, Notes, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy";

        public static Employee GetById(int employeeId)
        {
            string sql = "SELECT " + SelectColumns + " FROM Employees WHERE EmployeeId = ?";
            return Db.ReadOne(sql, MapRow, Db.P(employeeId));
        }

        public static Employee GetByCode(string employeeCode, bool includeArchived)
        {
            string sql = "SELECT " + SelectColumns + " FROM Employees WHERE EmployeeCode = ?";
            if (!includeArchived)
            {
                sql = sql + " AND IsDeleted = 0";
            }
            return Db.ReadOne(sql, MapRow, Db.P(employeeCode));
        }

        public static List<Employee> Search(EmployeeSearchFilter filter)
        {
            StringBuilder sql = new StringBuilder(
                "SELECT TOP " + filter.PageSize + " " + SelectColumns + " FROM Employees WHERE 1 = 1");
            List<OleDbParameter> parameters = new List<OleDbParameter>();

            if (!filter.IncludeArchived)
            {
                sql.Append(" AND IsDeleted = 0");
            }
            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                sql.Append(" AND (EmployeeCode LIKE ? OR FullNameAr LIKE ? OR NationalId LIKE ?)");
                string pattern = "%" + filter.Text.Trim() + "%";
                parameters.Add(Db.P(pattern));
                parameters.Add(Db.P(pattern));
                parameters.Add(Db.P(pattern));
            }
            if (filter.DepartmentId != null)
            {
                sql.Append(" AND DepartmentId = ?");
                parameters.Add(Db.PNullableId(filter.DepartmentId));
            }
            if (filter.SectionId != null)
            {
                sql.Append(" AND SectionId = ?");
                parameters.Add(Db.PNullableId(filter.SectionId));
            }
            if (filter.JobTitleId != null)
            {
                sql.Append(" AND JobTitleId = ?");
                parameters.Add(Db.PNullableId(filter.JobTitleId));
            }
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                sql.Append(" AND Status = ?");
                parameters.Add(Db.P(filter.Status));
            }
            if (!string.IsNullOrWhiteSpace(filter.LastCode))
            {
                sql.Append(" AND EmployeeCode > ?");
                parameters.Add(Db.P(filter.LastCode));
            }
            sql.Append(" ORDER BY EmployeeCode");

            return Db.ReadList(sql.ToString(), MapRow, parameters.ToArray());
        }

        /// <summary>Sequential codes ordered after lastCode (used for grid pagination).</summary>
        public static long CountAll()
        {
            return Db.Count("SELECT COUNT(*) FROM Employees WHERE IsDeleted = 0");
        }

        /// <summary>Inserted and returns the new EmployeeId (Jet: SELECT @@IDENTITY).</summary>
        public static int Insert(Employee employee)
        {
            string sql = "INSERT INTO Employees (EmployeeCode, NationalId, FullNameAr, FullNameEn, Gender, BirthDate, HireDate, EndDate, CompanyId, BranchId, DepartmentId, SectionId, JobTitleId, ActualJobId, EmployeeStatusId, QualificationId, EmploymentType, Phone, Email, Address, PhotoPath, DeviceEnrollNumber, ShiftGroupCode, ContractHours, Salary, Status, IsDeleted, Notes, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) " +
                         "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
            int newId = 0;
            Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(employee.EmployeeCode));
                    command.Parameters.Add(Db.P(employee.NationalId));
                    command.Parameters.Add(Db.P(employee.FullNameAr));
                    command.Parameters.Add(Db.P(employee.FullNameEn));
                    command.Parameters.Add(Db.P(employee.Gender));
                    command.Parameters.Add(Db.PDate(employee.BirthDate));
                    command.Parameters.Add(Db.PDateRequired(employee.HireDate));
                    command.Parameters.Add(Db.PDate(employee.EndDate));
                    command.Parameters.Add(Db.PNullableId(employee.CompanyId));
                    command.Parameters.Add(Db.PNullableId(employee.BranchId));
                    command.Parameters.Add(Db.PNullableId(employee.DepartmentId));
                    command.Parameters.Add(Db.PNullableId(employee.SectionId));
                    command.Parameters.Add(Db.PNullableId(employee.JobTitleId));
                    command.Parameters.Add(Db.PNullableId(employee.ActualJobId));
                    command.Parameters.Add(Db.PNullableId(employee.EmployeeStatusId));
                    command.Parameters.Add(Db.PNullableId(employee.QualificationId));
                    command.Parameters.Add(Db.P(employee.EmploymentType));
                    command.Parameters.Add(Db.P(employee.Phone));
                    command.Parameters.Add(Db.P(employee.Email));
                    command.Parameters.Add(Db.P(employee.Address));
                    command.Parameters.Add(Db.P(employee.PhotoPath));
                    command.Parameters.Add(Db.P(employee.DeviceEnrollNumber));
                    command.Parameters.Add(Db.P(employee.ShiftGroupCode));
                    command.Parameters.Add(Db.PSingle(employee.ContractHours));
                    command.Parameters.Add(Db.PCurrency(employee.Salary));
                    command.Parameters.Add(Db.P(employee.Status));
                    command.Parameters.Add(Db.PBool(employee.IsDeleted));
                    command.Parameters.Add(Db.P(employee.Notes));
                    command.Parameters.Add(Db.PDateRequired(DateTime.Now));
                    command.Parameters.Add(Db.PNullableId(employee.CreatedBy));
                    command.Parameters.Add(Db.PDateRequired(DateTime.Now));
                    command.Parameters.Add(Db.PNullableId(employee.UpdatedBy));
                    command.ExecuteNonQuery();
                }
                newId = GetIdentity(connection, transaction);
                return newId;
            });
            return newId;
        }

        private const string UpdateSql = "UPDATE Employees SET NationalId = ?, FullNameAr = ?, FullNameEn = ?, Gender = ?, BirthDate = ?, HireDate = ?, EndDate = ?, CompanyId = ?, BranchId = ?, DepartmentId = ?, SectionId = ?, JobTitleId = ?, ActualJobId = ?, EmployeeStatusId = ?, QualificationId = ?, EmploymentType = ?, Phone = ?, Email = ?, Address = ?, PhotoPath = ?, DeviceEnrollNumber = ?, ShiftGroupCode = ?, ContractHours = ?, Salary = ?, Status = ?, IsDeleted = ?, Notes = ?, UpdatedAt = ?, UpdatedBy = ? " +
                                         "WHERE EmployeeId = ?";

        private static void FillUpdateParameters(OleDbCommand command, Employee employee)
        {
            command.Parameters.Add(Db.P(employee.NationalId));
            command.Parameters.Add(Db.P(employee.FullNameAr));
            command.Parameters.Add(Db.P(employee.FullNameEn));
            command.Parameters.Add(Db.P(employee.Gender));
            command.Parameters.Add(Db.PDate(employee.BirthDate));
            command.Parameters.Add(Db.PDateRequired(employee.HireDate));
            command.Parameters.Add(Db.PDate(employee.EndDate));
            command.Parameters.Add(Db.PNullableId(employee.CompanyId));
            command.Parameters.Add(Db.PNullableId(employee.BranchId));
            command.Parameters.Add(Db.PNullableId(employee.DepartmentId));
            command.Parameters.Add(Db.PNullableId(employee.SectionId));
            command.Parameters.Add(Db.PNullableId(employee.JobTitleId));
            command.Parameters.Add(Db.PNullableId(employee.ActualJobId));
            command.Parameters.Add(Db.PNullableId(employee.EmployeeStatusId));
            command.Parameters.Add(Db.PNullableId(employee.QualificationId));
            command.Parameters.Add(Db.P(employee.EmploymentType));
            command.Parameters.Add(Db.P(employee.Phone));
            command.Parameters.Add(Db.P(employee.Email));
            command.Parameters.Add(Db.P(employee.Address));
            command.Parameters.Add(Db.P(employee.PhotoPath));
            command.Parameters.Add(Db.P(employee.DeviceEnrollNumber));
            command.Parameters.Add(Db.P(employee.ShiftGroupCode));
            command.Parameters.Add(Db.PSingle(employee.ContractHours));
            command.Parameters.Add(Db.PCurrency(employee.Salary));
            command.Parameters.Add(Db.P(employee.Status));
            command.Parameters.Add(Db.PBool(employee.IsDeleted));
            command.Parameters.Add(Db.P(employee.Notes));
            command.Parameters.Add(Db.PDateRequired(DateTime.Now));
            command.Parameters.Add(Db.PNullableId(employee.UpdatedBy));
            command.Parameters.Add(Db.P(employee.EmployeeId));
        }

        /// <summary>
        /// Updates the employee and appends one historical trail row inside the SAME
        /// Jet transaction (BR-18: no partial updates, no silent changes).
        /// </summary>
        public static void UpdateWithHistory(Employee employee, Employee oldEmployee, string diffText, int? userId)
        {
            Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(UpdateSql, connection, transaction))
                {
                    FillUpdateParameters(command, employee);
                    command.ExecuteNonQuery();
                }
                if (!string.IsNullOrEmpty(diffText))
                {
                    EmployeeHistory history = new EmployeeHistory();
                    history.EmployeeId = employee.EmployeeId;
                    history.ChangeDate = DateTime.Now;
                    history.ChangeType = "FieldUpdate";
                    history.FieldName = null;
                    history.OldValue = null;
                    history.NewValue = diffText;
                    history.ChangedBy = userId;
                    EmployeeHistoryDAL.Insert(history, connection, transaction);
                }
                return true;
            });
        }

        public static void Update(Employee employee)
        {
            Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(UpdateSql, connection, transaction))
                {
                    FillUpdateParameters(command, employee);
                    command.ExecuteNonQuery();
                }
                return true;
            });
        }

        /// <summary>Number of historical presence rows of an employee (BR-02 guard).</summary>
        public static long CountAttendanceLinks(int employeeId)
        {
            return Db.Count("SELECT COUNT(*) FROM Attendance WHERE EmployeeId = ?", Db.P(employeeId));
        }

        /// <summary>Total links across every activity table (physical delete guard, BR-02).</summary>
        public static long CountActivityLinks(int employeeId)
        {
            long links = 0;
            links += Db.Count("SELECT COUNT(*) FROM Attendance WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM ScheduleDetails WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM FingerprintLogs WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM LeaveRequests WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM Missions WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM Penalties WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM Overtime WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM AttendanceExceptions WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM AttendanceAdjustments WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM LeaveBalances WHERE EmployeeId = ?", Db.P(employeeId));
            links += Db.Count("SELECT COUNT(*) FROM EmployeeHistory WHERE EmployeeId = ?", Db.P(employeeId));
            return links;
        }

        /// <summary>Physically deletes an employee row; caller must verify zero activity links (BR-02).</summary>
        public static void Delete(int employeeId)
        {
            Db.ExecuteNonQuery("DELETE FROM Employees WHERE EmployeeId = ?", Db.P(employeeId));
        }

        /// <summary>Soft delete: flags IsDeleted and sets Status = Archived.</summary>
        public static void SoftDelete(int employeeId, int userId)
        {
            string sql = "UPDATE Employees SET IsDeleted = 1, Status = 'Archived', UpdatedAt = ?, UpdatedBy = ? WHERE EmployeeId = ?";
            Db.ExecuteNonQuery(sql, Db.PDateRequired(DateTime.Now), Db.PNullableId(userId), Db.P(employeeId));
        }

        /// <summary>Changes lifecycle status: Active / Suspended / Archived (BR-02).</summary>
        public static void SetStatus(int employeeId, string status, int userId)
        {
            string sql = "UPDATE Employees SET Status = ?, UpdatedAt = ?, UpdatedBy = ? WHERE EmployeeId = ?";
            Db.ExecuteNonQuery(sql, Db.P(status), Db.PDateRequired(DateTime.Now), Db.PNullableId(userId), Db.P(employeeId));
        }

        private static int GetIdentity(OleDbConnection connection, OleDbTransaction transaction)
        {
            using (OleDbCommand identityCommand = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
            {
                object result = identityCommand.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }
    }
}
