using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Organization lookup tables: Companies, Branches, Departments, Sections,
    /// JobTitles, ActualJobs, Qualifications, EmployeeStatus — with a whitelist
    /// driven by this class only (no table names from callers, no string SQL injection).
    /// </summary>
    public static class LookupDAL
    {
        // ---------------- mapping ----------------

        private static Company MapCompany(OleDbDataReader r)
        {
            Company entity = new Company();
            entity.CompanyId = r.GetInt32(0);
            entity.CompanyCode = Text(r, 1);
            entity.CompanyNameAr = Text(r, 2);
            entity.IsActive = r.IsDBNull(3) ? (bool?)null : r.GetBoolean(3);
            return entity;
        }

        private static Branch MapBranch(OleDbDataReader r)
        {
            Branch entity = new Branch();
            entity.BranchId = r.GetInt32(0);
            entity.BranchCode = Text(r, 1);
            entity.BranchName = Text(r, 2);
            entity.CompanyId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3);
            entity.IsActive = r.IsDBNull(4) ? (bool?)null : r.GetBoolean(4);
            return entity;
        }

        private static Department MapDepartment(OleDbDataReader r)
        {
            Department entity = new Department();
            entity.DepartmentId = r.GetInt32(0);
            entity.DepartmentCode = Text(r, 1);
            entity.DepartmentName = Text(r, 2);
            entity.BranchId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3);
            entity.IsActive = r.IsDBNull(4) ? (bool?)null : r.GetBoolean(4);
            return entity;
        }

        private static Section MapSection(OleDbDataReader r)
        {
            Section entity = new Section();
            entity.SectionId = r.GetInt32(0);
            entity.SectionCode = Text(r, 1);
            entity.SectionName = Text(r, 2);
            entity.DepartmentId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3);
            entity.IsActive = r.IsDBNull(4) ? (bool?)null : r.GetBoolean(4);
            return entity;
        }

        private static JobTitle MapJobTitle(OleDbDataReader r)
        {
            JobTitle entity = new JobTitle();
            entity.JobTitleId = r.GetInt32(0);
            entity.TitleCode = Text(r, 1);
            entity.TitleName = Text(r, 2);
            entity.IsActive = r.IsDBNull(3) ? (bool?)null : r.GetBoolean(3);
            return entity;
        }

        private static ActualJob MapActualJob(OleDbDataReader r)
        {
            ActualJob entity = new ActualJob();
            entity.ActualJobId = r.GetInt32(0);
            entity.JobCode = Text(r, 1);
            entity.JobName = Text(r, 2);
            entity.DepartmentId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3);
            entity.IsActive = r.IsDBNull(4) ? (bool?)null : r.GetBoolean(4);
            return entity;
        }

        private static Qualification MapQualification(OleDbDataReader r)
        {
            Qualification entity = new Qualification();
            entity.QualificationId = r.GetInt32(0);
            entity.QualCode = Text(r, 1);
            entity.QualName = Text(r, 2);
            return entity;
        }

        private static EmployeeStatus MapEmployeeStatus(OleDbDataReader r)
        {
            EmployeeStatus entity = new EmployeeStatus();
            entity.EmployeeStatusId = r.GetInt32(0);
            entity.StatusCode = Text(r, 1);
            entity.StatusName = Text(r, 2);
            return entity;
        }

        private static Shift MapShift(OleDbDataReader r)
        {
            Shift entity = new Shift();
            entity.ShiftId = r.GetInt32(0);
            entity.ShiftCode = Text(r, 1);
            entity.ShiftName = Text(r, 2);
            entity.StartTime = Text(r, 3);
            entity.EndTime = Text(r, 4);
            entity.CrossDay = r.IsDBNull(5) ? (bool?)null : r.GetBoolean(5);
            entity.DurationHrs = r.IsDBNull(6) ? (float?)null : Convert.ToSingle(r.GetDouble(6));
            entity.NormalHours = r.IsDBNull(7) ? (float?)null : Convert.ToSingle(r.GetDouble(7));
            entity.BreakMinutes = r.IsDBNull(8) ? (int?)null : r.GetInt32(8);
            entity.CategoryCode = Text(r, 9);
            entity.RequireCheck = r.IsDBNull(10) ? (bool?)null : r.GetBoolean(10);
            entity.IsActive = r.IsDBNull(11) ? (bool?)null : r.GetBoolean(11);
            return entity;
        }

        private static string Text(OleDbDataReader r, int ordinal)
        {
            return r.IsDBNull(ordinal) ? null : r.GetString(ordinal);
        }

        // ---------------- list / get ----------------

        public static List<Company> GetCompanies(bool activeOnly)
        {
            return Db.ReadList("SELECT CompanyId, CompanyCode, CompanyNameAr, IsActive FROM Companies" + WhereActive(activeOnly),
                MapCompany);
        }

        public static List<Branch> GetBranches(bool activeOnly)
        {
            return Db.ReadList("SELECT BranchId, BranchCode, BranchName, CompanyId, IsActive FROM Branches" + WhereActive(activeOnly) + " ORDER BY BranchCode",
                MapBranch);
        }

        public static List<Department> GetDepartments(bool activeOnly)
        {
            return Db.ReadList("SELECT DepartmentId, DepartmentCode, DepartmentName, BranchId, IsActive FROM Departments" + WhereActive(activeOnly) + " ORDER BY DepartmentCode",
                MapDepartment);
        }

        public static List<Section> GetSections(int? departmentId, bool activeOnly)
        {
            string sql = "SELECT SectionId, SectionCode, SectionName, DepartmentId, IsActive FROM Sections WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (activeOnly)
            {
                sql = sql + " AND IsActive <> 0";   // Jet YESNO stores True as -1; ' = 1' never matches
            }
            if (departmentId != null)
            {
                sql = sql + " AND DepartmentId = ?";
                parameters.Add(Db.PNullableId(departmentId));
            }
            sql = sql + " ORDER BY SectionCode";
            return Db.ReadList(sql, MapSection, parameters.ToArray());
        }

        public static List<JobTitle> GetJobTitles(bool activeOnly)
        {
            return Db.ReadList("SELECT JobTitleId, TitleCode, TitleName, IsActive FROM JobTitles" + WhereActive(activeOnly) + " ORDER BY TitleCode",
                MapJobTitle);
        }

        public static List<ActualJob> GetActualJobs(int? departmentId, bool activeOnly)
        {
            string sql = "SELECT ActualJobId, JobCode, JobName, DepartmentId, IsActive FROM ActualJobs WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (activeOnly)
            {
                sql = sql + " AND IsActive <> 0";   // Jet YESNO stores True as -1; ' = 1' never matches
            }
            if (departmentId != null)
            {
                sql = sql + " AND DepartmentId = ?";
                parameters.Add(Db.PNullableId(departmentId));
            }
            sql = sql + " ORDER BY JobCode";
            return Db.ReadList(sql, MapActualJob, parameters.ToArray());
        }

        public static List<Qualification> GetQualifications()
        {
            return Db.ReadList("SELECT QualificationId, QualCode, QualName FROM Qualifications ORDER BY QualCode",
                MapQualification);
        }

        public static List<EmployeeStatus> GetEmployeeStatuses()
        {
            return Db.ReadList("SELECT EmployeeStatusId, StatusCode, StatusName FROM EmployeeStatus ORDER BY EmployeeStatusId",
                MapEmployeeStatus);
        }

        public static List<Shift> GetShifts(bool activeOnly)
        {
            return Db.ReadList("SELECT ShiftId, ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive FROM Shifts" + WhereActive(activeOnly) + " ORDER BY ShiftCode",
                MapShift);
        }

        public static Shift GetShiftByCode(string shiftCode)
        {
            return Db.ReadOne(
                "SELECT ShiftId, ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive FROM Shifts WHERE ShiftCode = ?",
                MapShift, Db.P(shiftCode));
        }

        // ---------------- write ----------------

        /// <summary>Inserts into one whitelisted lookup table (id, code, name[, parent, active]).</summary>
        public static int InsertLookup(LookupTable table, string code, string name, int? parentId, bool isActive, int? userId)
        {
            switch (table)
            {
                case LookupTable.Companies:
                    return InsertEntity("Companies", "CompanyCode, CompanyNameAr, IsActive", code, name, parentId, isActive, userId);
                case LookupTable.Branches:
                    return InsertEntity("Branches", "BranchCode, BranchName, CompanyId, IsActive", code, name, parentId, isActive, userId);
                case LookupTable.Departments:
                    return InsertEntity("Departments", " DepartmentCode, DepartmentName, BranchId, IsActive", code, name, parentId, isActive, userId);
                case LookupTable.Sections:
                    return InsertEntity("Sections", "SectionCode, SectionName, DepartmentId, IsActive", code, name, parentId, isActive, userId);
                case LookupTable.JobTitles:
                    return InsertEntity("JobTitles", "TitleCode, TitleName, IsActive", code, name, parentId, isActive, userId);
                case LookupTable.ActualJobs:
                    return InsertEntity("ActualJobs", "JobCode, JobName, DepartmentId, IsActive", code, name, parentId, isActive, userId);
                case LookupTable.Qualifications:
                    return InsertEntity("Qualifications", "QualCode, QualName", code, name, parentId, isActive, userId);
                case LookupTable.EmployeeStatus:
                    return InsertEntity("EmployeeStatus", "StatusCode, StatusName", code, name, parentId, isActive, userId);
                case LookupTable.Shifts:
                    throw new NotSupportedException("استخدم ShiftDAL لإدارة الورديات.");
                default:
                    throw new ArgumentOutOfRangeException("table");
            }
        }

        /// <summary>Updates code/name/active of one whitelisted lookup row.</summary>
        public static void UpdateLookup(LookupTable table, int id, string code, string name, int? parentId, bool isActive, int? userId)
        {
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            parameters.Add(Db.P(code));
            parameters.Add(Db.P(name));
            bool hasParent = HasParentColumn(table);
            if (hasParent)
            {
                parameters.Add(Db.PNullableId(parentId));
            }
            parameters.Add(Db.PBool(isActive));
            parameters.Add(Db.P(id));

            string parentColumn = ParentColumn(table);
            string setClause = hasParent
                ? "SET " + CodeColumn(table) + " = ?, " + NameColumn(table) + " = ?, " + parentColumn + " = ?, IsActive = ?"
                : "SET " + CodeColumn(table) + " = ?, " + NameColumn(table) + " = ?, IsActive = ?";
            Db.ExecuteNonQuery("UPDATE " + TableName(table) + " " + setClause + " WHERE " + IdColumn(table) + " = ?",
                parameters.ToArray());
        }

        /// <summary>Counts usage rows referencing the lookup entry (delete guard).</summary>
        public static long CountUsage(LookupTable table, int id)
        {
            switch (table)
            {
                case LookupTable.Companies:
                    return Db.Count("SELECT COUNT(*) FROM Employees WHERE CompanyId = ?", Db.P(id))
                         + Db.Count("SELECT COUNT(*) FROM Branches WHERE CompanyId = ?", Db.P(id));
                case LookupTable.Branches:
                    return Db.Count("SELECT COUNT(*) FROM Employees WHERE BranchId = ?", Db.P(id))
                         + Db.Count("SELECT COUNT(*) FROM Departments WHERE BranchId = ?", Db.P(id));
                case LookupTable.Departments:
                    return Db.Count("SELECT COUNT(*) FROM Employees WHERE DepartmentId = ?", Db.P(id))
                         + Db.Count("SELECT COUNT(*) FROM Sections WHERE DepartmentId = ?", Db.P(id));
                case LookupTable.Sections:
                    return Db.Count("SELECT COUNT(*) FROM Employees WHERE SectionId = ?", Db.P(id));
                case LookupTable.JobTitles:
                    return Db.Count("SELECT COUNT(*) FROM Employees WHERE JobTitleId = ?", Db.P(id));
                case LookupTable.ActualJobs:
                    return Db.Count("SELECT COUNT(*) FROM Employees WHERE ActualJobId = ?", Db.P(id));
                case LookupTable.Qualifications:
                    return Db.Count("SELECT COUNT(*) FROM Employees WHERE QualificationId = ?", Db.P(id));
                case LookupTable.EmployeeStatus:
                    return Db.Count("SELECT COUNT(*) FROM Employees WHERE EmployeeStatusId = ?", Db.P(id));
                case LookupTable.Shifts:
                    return Db.Count("SELECT COUNT(*) FROM ScheduleDetails WHERE ShiftId = ?", Db.P(id))
                         + Db.Count("SELECT COUNT(*) FROM Attendance WHERE ShiftId = ?", Db.P(id));
                default:
                    throw new ArgumentOutOfRangeException("table");
            }
        }


        /// <summary>Loads all rows of one lookup table for the management screen.</summary>
        public static System.Collections.Generic.List<LookupRow> GetLookupRows(LookupTable table, bool includeInactive)
        {
            System.Collections.Generic.List<LookupRow> rows = new System.Collections.Generic.List<LookupRow>();
            bool hasParent = HasParentColumn(table);
            bool hasActive = table != LookupTable.Qualifications && table != LookupTable.EmployeeStatus;
            string sql = "SELECT " + IdColumn(table) + ", " + CodeColumn(table) + ", " + NameColumn(table) +
                         (hasParent ? ", " + ParentColumn(table) : ", NULL") +
                         (hasActive ? ", IsActive" : ", NULL") +
                         " FROM " + TableName(table) + " WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (!includeInactive && hasActive)
            {
                sql = sql + " AND IsActive <> 0";   // Jet YESNO stores True as -1; ' = 1' never matches
            }
            sql = sql + " ORDER BY " + CodeColumn(table);
            rows = Db.ReadList(sql,
                delegate(OleDbDataReader reader)
                {
                    LookupRow row = new LookupRow();
                    row.Id = reader.GetInt32(0);
                    row.Code = reader.IsDBNull(1) ? null : reader.GetString(1);
                    row.Name = reader.IsDBNull(2) ? null : reader.GetString(2);
                    row.ParentId = reader.IsDBNull(3) ? (int?)null : Convert.ToInt32(reader.GetValue(3));
                    if (hasActive)
                    {
                        row.IsActive = reader.GetBoolean(4);
                    }
                    else
                    {
                        row.IsActive = true;
                    }
                    return row;
                },
                parameters.ToArray());
            return rows;
        }

        /// <summary>Reads (Code, Name, ParentId) of one lookup row; null when missing.</summary>
        public static LookupRow GetLookup(LookupTable table, int id)
        {
            bool hasParent = HasParentColumn(table);
            string sql = "SELECT " + CodeColumn(table) + ", " + NameColumn(table) +
                         (hasParent ? ", " + ParentColumn(table) : ", NULL") +
                         " FROM " + TableName(table) + " WHERE " + IdColumn(table) + " = ?";
            return Db.ReadOne(sql,
                delegate(OleDbDataReader reader)
                {
                    LookupRow row = new LookupRow();
                    row.Id = id;
                    row.Code = reader.IsDBNull(0) ? null : reader.GetString(0);
                    row.Name = reader.IsDBNull(1) ? null : reader.GetString(1);
                    row.ParentId = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader.GetValue(2));
                    return row;
                },
                Db.P(id));
        }

        /// <summary>Deletes one lookup row; caller verifies zero usage first.</summary>
        public static void DeleteLookup(LookupTable table, int id)
        {
            Db.ExecuteNonQuery("DELETE FROM " + TableName(table) + " WHERE " + IdColumn(table) + " = ?", Db.P(id));
        }

        /// <summary>True when the same code exists in another row of the whitelisted table.</summary>
        public static bool CodeExists(LookupTable table, string code, int? excludeId)
        {
            string sql = "SELECT COUNT(*) FROM " + TableName(table) + " WHERE " + CodeColumn(table) + " = ?";
            if (excludeId != null)
            {
                sql = sql + " AND " + IdColumn(table) + " <> ?";
                return Db.Count(sql, Db.P(code), Db.PNullableId(excludeId)) > 0;
            }
            return Db.Count(sql, Db.P(code)) > 0;
        }

        // ---------------- schema helpers ----------------

        /// <summary>Table name used as TableName in audit entries.</summary>
        public static string TableNameForAudit(LookupTable table)
        {
            return TableName(table);
        }

        private static int InsertEntity(string tableName, string columnList, string code, string name, int? parentId, bool isActive, int? userId)
        {
            bool hasParent = columnList.Contains("Id,");
            bool hasActive = columnList.Contains("IsActive");
            int valueCount = 2 + (hasParent ? 1 : 0) + (hasActive ? 1 : 0);
            string values = "?";
            for (int index = 1; index < valueCount; index++)
            {
                values = values + ", ?";
            }
            string sql = "INSERT INTO " + tableName + " (" + columnList + ") VALUES (" + values + ")";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(code));
                    command.Parameters.Add(Db.P(name));
                    if (hasParent)
                    {
                        command.Parameters.Add(Db.PNullableId(parentId));
                    }
                    if (hasActive)
                    {
                        command.Parameters.Add(Db.PBool(isActive));
                    }
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        private static string WhereActive(bool activeOnly)
        {
            return activeOnly ? " WHERE IsActive <> 0" : string.Empty;   // Jet YESNO stores True as -1
        }

        private static string TableName(LookupTable table)
        {
            switch (table)
            {
                case LookupTable.Companies: return "Companies";
                case LookupTable.Branches: return "Branches";
                case LookupTable.Departments: return "Departments";
                case LookupTable.Sections: return "Sections";
                case LookupTable.JobTitles: return "JobTitles";
                case LookupTable.ActualJobs: return "ActualJobs";
                case LookupTable.Qualifications: return "Qualifications";
                case LookupTable.EmployeeStatus: return "EmployeeStatus";
                case LookupTable.Shifts: return "Shifts";
                default: throw new ArgumentOutOfRangeException("table");
            }
        }

        private static string IdColumn(LookupTable table)
        {
            switch (table)
            {
                case LookupTable.Companies: return "CompanyId";
                case LookupTable.Branches: return "BranchId";
                case LookupTable.Departments: return "DepartmentId";
                case LookupTable.Sections: return "SectionId";
                case LookupTable.JobTitles: return "JobTitleId";
                case LookupTable.ActualJobs: return "ActualJobId";
                case LookupTable.Qualifications: return "QualificationId";
                case LookupTable.EmployeeStatus: return "EmployeeStatusId";
                case LookupTable.Shifts: return "ShiftId";
                default: throw new ArgumentOutOfRangeException("table");
            }
        }

        private static string CodeColumn(LookupTable table)
        {
            switch (table)
            {
                case LookupTable.Companies: return "CompanyCode";
                case LookupTable.Branches: return "BranchCode";
                case LookupTable.Departments: return "DepartmentCode";
                case LookupTable.Sections: return "SectionCode";
                case LookupTable.JobTitles: return "TitleCode";
                case LookupTable.ActualJobs: return "JobCode";
                case LookupTable.Qualifications: return "QualCode";
                case LookupTable.EmployeeStatus: return "StatusCode";
                case LookupTable.Shifts: return "ShiftCode";
                default: throw new ArgumentOutOfRangeException("table");
            }
        }

        private static string NameColumn(LookupTable table)
        {
            switch (table)
            {
                case LookupTable.Companies: return "CompanyNameAr";
                case LookupTable.Branches: return "BranchName";
                case LookupTable.Departments: return "DepartmentName";
                case LookupTable.Sections: return "SectionName";
                case LookupTable.JobTitles: return "TitleName";
                case LookupTable.ActualJobs: return "JobName";
                case LookupTable.Qualifications: return "QualName";
                case LookupTable.EmployeeStatus: return "StatusName";
                case LookupTable.Shifts: return "ShiftName";
                default: throw new ArgumentOutOfRangeException("table");
            }
        }

        private static bool HasParentColumn(LookupTable table)
        {
            switch (table)
            {
                case LookupTable.Branches:
                case LookupTable.Departments:
                case LookupTable.Sections:
                case LookupTable.ActualJobs:
                    return true;
                default:
                    return false;
            }
        }

        private static string ParentColumn(LookupTable table)
        {
            switch (table)
            {
                case LookupTable.Branches: return "CompanyId";
                case LookupTable.Departments: return "BranchId";
                case LookupTable.Sections: return "DepartmentId";
                case LookupTable.ActualJobs: return "DepartmentId";
                default: return null;
            }
        }
    }
}
