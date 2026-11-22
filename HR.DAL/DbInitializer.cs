using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Text;
using ADOX;
using HR.Helpers;

namespace HR.DAL
{
    /// <summary>
    /// Creates the .mdb file and applies Database/ModernTime_Schema.sql exactly as approved
    /// in the Phase 1 design. Statements are split on ';' and executed sequentially.
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// Ensures the database file and full schema exist.
        /// Safe to call at every application start; creation happens only once.
        /// </summary>
        public static void EnsureInitialized()
        {
            string databasePath = AppContext.DatabasePath;
            if (!Db.CoreTablesExist())
            {
                CreateDatabaseFile(databasePath);
                ExecuteSchemaScript(databasePath);
                return;
            }
            ApplySchemaUpgrades(databasePath);
        }

        /// <summary>
        /// Idempotent in-place upgrade for databases created by an older schema:
        /// adds missing columns/tables declaratively, never touches existing data.
        /// </summary>
        public static void ApplySchemaUpgrades(string databasePath)
        {
            using (OleDbConnection connection = new OleDbConnection(Db.BuildConnectionString(databasePath)))
            {
                connection.Open();

                EnsureColumn(connection, "Employees", "FirstWorkDate", "DATETIME");
                EnsureColumn(connection, "Employees", "NominalHireDate", "DATETIME");
                EnsureColumn(connection, "Employees", "JobKeepDate", "DATETIME");
                EnsureColumn(connection, "LeaveRequests", "RejectReason", "LONGCHAR");

                if (!TableExists(connection, "LongLeaveTypes"))
                {
                    ExecuteUpgrade(connection,
                        "CREATE TABLE LongLeaveTypes (" +
                        "LongLeaveTypeId AUTOINCREMENT PRIMARY KEY, " +
                        "TypeCode VARCHAR(20) NOT NULL UNIQUE, " +
                        "TypeName VARCHAR(80) NOT NULL, " +
                        "IsLimited YESNO, MaxMonths LONG, IsActive YESNO, Notes LONGCHAR)");
                    ExecuteUpgrade(connection,
                        "INSERT INTO LongLeaveTypes (TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes) VALUES ('ChildCare', 'رعاية الطفل', 0, NULL, 1, 'افتراضياً غير محدودة — عدّل المدة من شاشة القوائم')");
                    ExecuteUpgrade(connection,
                        "INSERT INTO LongLeaveTypes (TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes) VALUES ('FamilyCare', 'رعاية الأسرة', 0, NULL, 1, 'افتراضياً غير محدودة — عدّل المدة من شاشة القوائم')");
                    ExecuteUpgrade(connection,
                        "INSERT INTO LongLeaveTypes (TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes) VALUES ('Abroad', 'السفر للخارج', 0, NULL, 1, 'افتراضياً غير محدودة — عدّل المدة من شاشة القوائم')");
                    ExecuteUpgrade(connection,
                        "INSERT INTO LongLeaveTypes (TypeCode, TypeName, IsLimited, MaxMonths, IsActive, Notes) VALUES ('IncomeImprove', 'تحسين الدخل', 0, NULL, 1, 'افتراضياً غير محدودة — عدّل المدة من شاشة القوائم')");
                }
                using (OleDbCommand settingCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Fp.AutoDownloadMinutes'", connection))
                {
                    if (Convert.ToInt32(settingCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Fp.AutoDownloadMinutes', '0', '" +
                            "فترة التنزيل التلقائي لأجهزة البصمة بالدقائق (0 = معطل)" +
                            "', Now())");
                    }
                }
                using (OleDbCommand mixPolicyCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Sch.MixPolicy'", connection))
                {
                    if (Convert.ToInt32(mixPolicyCount.ExecuteScalar()) == 0)
                    {
                        object legacyValue = new OleDbCommand(
                            "SELECT SettingValue FROM AppSettings WHERE SettingKey = 'Sch.DisableMixingCategories'", connection).ExecuteScalar();
                        string legacyText = legacyValue == null ? null : Convert.ToString(legacyValue);
                        bool legacyEnabled = true;
                        string normalizedLegacy = legacyText == null ? string.Empty : legacyText.Trim().ToUpperInvariant();
                        if (normalizedLegacy == "NO" || normalizedLegacy == "0" || normalizedLegacy == "FALSE")
                        {
                            legacyEnabled = false;
                        }
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Sch.MixPolicy', '" + (legacyEnabled ? "Both" : "Off") + "', '" +
                            "سياسة منع خلط ورديات العمل (BR-09): Both = الصنف والمدة معا (افتراضي)، Duration = المدة فقط، Category = الصنف فقط، Off = معطل" +
                            "', Now())");
                    }
                }
                using (OleDbCommand restDaysCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Sch.RestDays'", connection))
                {
                    if (Convert.ToInt32(restDaysCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Sch.RestDays', 'Friday', '" +
                            "أيام الراحة الأسبوعية مفصولة بفواصل — أسماء عربية أو إنجليزية أو أرقام 0-6 (0 = الأحد). مثال: Friday أو Friday,Saturday" +
                            "', Now())");
                    }
                }
                using (OleDbCommand alertsCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Alert.DeviceStaleDays'", connection))
                {
                    if (Convert.ToInt32(alertsCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Alert.DeviceStaleDays', '3', '" +
                            "مركز التنبيهات: تنبيه عند عدم مزامنة جهاز بصمة منذ هذا العدد من الأيام (0 = معطل)" +
                            "', Now())");
                    }
                }
                if (!TableExists(connection, "EmployeeFiles"))
                {
                    ExecuteUpgrade(connection,
                        "CREATE TABLE EmployeeFiles (" +
                        "FileId AUTOINCREMENT PRIMARY KEY, " +
                        "EmployeeId LONG NOT NULL, FilePath VARCHAR(255) NOT NULL, " +
                        "Category VARCHAR(50), Notes LONGCHAR, " +
                        "CreatedAt DATETIME, CreatedBy LONG)");
                    ExecuteUpgrade(connection,
                        "CREATE INDEX IX_EmployeeFiles_Emp ON EmployeeFiles (EmployeeId)");
                }
                if (!TableExists(connection, "LongLeaves"))
                {
                    ExecuteUpgrade(connection,
                        "CREATE TABLE LongLeaves (" +
                        "LongLeaveId AUTOINCREMENT PRIMARY KEY, " +
                        "EmployeeId LONG NOT NULL, TypeId LONG NOT NULL, " +
                        "FromDate DATETIME NOT NULL, ToDate DATETIME, " +
                        "Status VARCHAR(12), Reason LONGCHAR, Notes LONGCHAR, " +
                        "ApprovedBy LONG, CreatedAt DATETIME, CreatedBy LONG)");
                    ExecuteUpgrade(connection,
                        "CREATE INDEX IX_LongLeave_Emp ON LongLeaves (EmployeeId, FromDate)");
                    ExecuteUpgrade(connection,
                        "CREATE INDEX IX_LongLeave_Type ON LongLeaves (TypeId)");
                    ExecuteUpgrade(connection,
                        "CREATE INDEX IX_LongLeave_St ON LongLeaves (Status, FromDate)");
                }
            }
        }

        private static bool TableExists(OleDbConnection connection, string tableName)
        {
            DataTable tables = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
                new object[] { null, null, tableName, "TABLE" });
            return tables != null && tables.Rows.Count > 0;
        }

        private static bool ColumnExists(OleDbConnection connection, string tableName, string columnName)
        {
            DataTable columns = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
                new object[] { null, null, tableName, columnName });
            return columns != null && columns.Rows.Count > 0;
        }

        private static void EnsureColumn(OleDbConnection connection, string tableName, string columnName, string columnType)
        {
            if (ColumnExists(connection, tableName, columnName))
            {
                return;
            }
            ExecuteUpgrade(connection, "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnType);
        }

        private static void ExecuteUpgrade(OleDbConnection connection, string statement)
        {
            using (OleDbCommand command = new OleDbCommand(statement, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Creates an empty Access 2002-2003 (Jet 4.0) file via ADOX.</summary>
        public static void CreateDatabaseFile(string databasePath)
        {
            if (File.Exists(databasePath))
            {
                return;
            }
            string folder = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            Catalog catalog = new CatalogClass();
            try
            {
                OleDbConnectionStringBuilder createBuilder = new OleDbConnectionStringBuilder();
                createBuilder.Provider = "Microsoft.Jet.OLEDB.4.0";
                createBuilder.DataSource = databasePath;
                createBuilder["Jet OLEDB:Engine Type"] = 5;
                try
                {
                    catalog.Create(createBuilder.ConnectionString);
                }
                catch (System.Runtime.InteropServices.COMException createError)
                {
                    throw new InvalidOperationException(
                        "فشل إنشاء ملف قاعدة البيانات في: " + databasePath +
                        " — تأكد من صلاحية الكتابة في المجلد ومن توفر محرك Jet OLEDB 4.0 (32-بت). التفصيل: " +
                        createError.Message, createError);
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(catalog);
            }
        }

        /// <summary>Reads the approved schema file, strips comments, executes every statement.</summary>
        public static void ExecuteSchemaScript(string databasePath)
        {
            string schemaPath = AppContext.SchemaPath;
            if (!File.Exists(schemaPath))
            {
                throw new FileNotFoundException("ملف مخطط قاعدة البيانات غير موجود: " + schemaPath);
            }
            List<string> statements = SplitStatements(File.ReadAllText(schemaPath, Encoding.UTF8));
            using (OleDbConnection connection = new OleDbConnection(Db.BuildConnectionString(databasePath)))
            {
                connection.Open();
                foreach (string statement in statements)
                {
                    using (OleDbCommand command = new OleDbCommand(statement, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>Splits the DDL file into executable statements (drops '--' comments).</summary>
        public static List<string> SplitStatements(string script)
        {
            List<string> statements = new List<string>();
            if (string.IsNullOrWhiteSpace(script))
            {
                return statements;
            }
            StringBuilder current = new StringBuilder();
            string[] lines = script.Replace("\r\n", "\n").Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("--"))
                {
                    continue;
                }
                current.AppendLine(line);
            }
            string[] parts = current.ToString().Split(';');
            foreach (string part in parts)
            {
                string statement = part.Trim();
                if (statement.Length > 0)
                {
                    statements.Add(statement);
                }
            }
            return statements;
        }
    }
}
