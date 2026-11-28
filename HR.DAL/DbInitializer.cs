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
                // S-01: forced first-login password change (bootstrap admin +
                // admin resets). Legacy databases get the column here; the
                // default-password check at login covers rows created before it.
                EnsureColumn(connection, "Users", "MustChangePassword", "YESNO");

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
                using (OleDbCommand balanceTypesCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Leave.BalanceTypes'", connection))
                {
                    if (Convert.ToInt32(balanceTypesCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Leave.BalanceTypes', 'Annual,Casual', '" +
                            "أنواع الإجازات المُدارة بحصة سنوية ورصيد مرحّل (الإجازات الاعتيادية والعارضة) الأكواد مفصولة بفواصل" +
                            "', Now())");
                    }
                }
                using (OleDbCommand carryOverCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Leave.AllowCarryOver'", connection))
                {
                    if (Convert.ToInt32(carryOverCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Leave.AllowCarryOver', '1', '" +
                            "استخدام المتبقي من أعوام سابقة في رصيد الإجازات الاعتيادية/العارضة (1 = نعم، 0 = لا)" +
                            "', Now())");
                    }
                }
                using (OleDbCommand fiscalStartCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Leave.FiscalYearStart'", connection))
                {
                    if (Convert.ToInt32(fiscalStartCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Leave.FiscalYearStart', '07-01', '" +
                            "بداية السنة المالية لأرصدة الإجازات والحساب السنوي بصيغة MM-dd (07-01 = 1 يوليو حتى 30 يونيو)" +
                            "', Now())");
                    }
                }
                // Fine-grained permission matrix upgrade: seeds every newly
                // introduced permission key (idempotent count-checked loop).
                string[][] newPermissions = new string[][]
                {
                    new string[] { "Leave.Create", "الإجازات: إنشاء طلبات الإجازات (مصرّف الطلبات دون اعتماد)" },
                    new string[] { "Leaves.Edit", "الإجازات: تعديل طلبات الإجازات" },
                    new string[] { "Leaves.Balances", "الإجازات: تعديل الأرصدة السنوية للموظفين (الحصص)" },
                    new string[] { "Mission.Create", "المأموريات: إنشاء مأموريات دون اعتماد" },
                    new string[] { "Missions.Edit", "المأموريات: تعديل المأموريات" },
                    new string[] { "Penalties.Delete", "الجزاءات: حذف الجزاءات" },
                    new string[] { "Attendance.Engine", "الحضور: تشغيل محرك الحضور وإعادة الحساب" },
                    new string[] { "Attendance.Holidays", "الحضور: إدارة العطل الرسمية" },
                    new string[] { "Attendance.Overtime", "الحضور: إدارة العمل الإضافي واعتماده" },
                    new string[] { "Fingerprint.Download", "البصمات: تنزيل بيانات الحضور من الأجهزة" },
                    new string[] { "Fingerprint.ReadUsers", "البصمات: قراءة مستخدمي الأجهزة (أرقام الموظفين)" },
                    new string[] { "Employees.View", "الموظفون: عرض الموظفين والبحث دون تعديل" },
                    new string[] { "Employees.Files", "الموظفون: إدارة ملفات ومرفقات الموظفين" },
                    new string[] { "Employees.History", "الموظفون: عرض سجل تاريخ الموظف" },
                    new string[] { "Roles.Manage", "النظام: إدارة الأدوار ومصفوفة الصلاحيات" },
                    new string[] { "Database.Manage", "قاعدة البيانات: المراقبة واستعراض الجداول والفهارس" },
                    new string[] { "Database.Cleanup", "قاعدة البيانات: تنظيف السجلات القديمة (مع نسخة أمان)" },
                    new string[] { "Backup.Restore", "قاعدة البيانات: استعادة النسخ الاحتياطية" },
                    new string[] { "Print.Manage", "النظام: إعدادات الطباعة والشعارات" }
                };
                foreach (string[] permEntry in newPermissions)
                {
                    OleDbCommand permCount = new OleDbCommand(
                        "SELECT COUNT(*) FROM Permissions WHERE PermissionKey = ?", connection);
                    permCount.Parameters.AddWithValue("?", permEntry[0]);
                    bool missing = Convert.ToInt32(permCount.ExecuteScalar()) == 0;
                    permCount.Dispose();
                    if (missing)
                    {
                        OleDbCommand permInsert = new OleDbCommand(
                            "INSERT INTO Permissions (PermissionKey, DescriptionAr) VALUES (?, ?)", connection);
                        permInsert.Parameters.AddWithValue("?", permEntry[0]);
                        permInsert.Parameters.AddWithValue("?", permEntry[1]);
                        permInsert.ExecuteNonQuery();
                        permInsert.Dispose();
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
                // Order aa2c7e45: balance/request day amounts become CURRENCY so
                // half-day shift-based deductions (6-7h = 1.0, 9h = 1.5, 12h = 2.0)
                // can be stored. Existing integer values convert losslessly.
                EnsureCurrencyColumn(connection, "LeaveRequests", "DaysCount");
                EnsureCurrencyColumn(connection, "LeaveBalances", "QuotaDays");
                EnsureCurrencyColumn(connection, "LeaveBalances", "UsedDays");
                EnsureCurrencyColumn(connection, "LeaveBalances", "Remaining");
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
                // RosterCore lessons (order 45f44386): approval-chain columns on
                // MonthlySchedules plus three new tables and three settings.
                EnsureColumn(connection, "MonthlySchedules", "SubmittedBy", "LONG");
                EnsureColumn(connection, "MonthlySchedules", "SubmittedAt", "DATETIME");
                EnsureColumn(connection, "MonthlySchedules", "RejectReason", "LONGCHAR");
                if (!TableExists(connection, "ScheduleApprovals"))
                {
                    ExecuteUpgrade(connection,
                        "CREATE TABLE ScheduleApprovals (" +
                        "ApprovalId AUTOINCREMENT PRIMARY KEY, " +
                        "ScheduleId LONG NOT NULL, Stage VARCHAR(12) NOT NULL, " +
                        "UserId LONG, Reason LONGCHAR, CreatedAt DATETIME)");
                    ExecuteUpgrade(connection,
                        "CREATE INDEX IX_SchedAppr_Sched ON ScheduleApprovals (ScheduleId)");
                }
                if (!TableExists(connection, "ScheduleChangeRequests"))
                {
                    ExecuteUpgrade(connection,
                        "CREATE TABLE ScheduleChangeRequests (" +
                        "RequestId AUTOINCREMENT PRIMARY KEY, " +
                        "YearVal LONG NOT NULL, MonthVal LONG NOT NULL, " +
                        "Status VARCHAR(12), Reason LONGCHAR, " +
                        "RequestedBy LONG, DecidedBy LONG, DecideReason LONGCHAR, " +
                        "ExecutedBy LONG, CreatedAt DATETIME, DecidedAt DATETIME, ExecutedAt DATETIME)");
                    ExecuteUpgrade(connection,
                        "CREATE INDEX IX_SchedChg_Status ON ScheduleChangeRequests (Status)");
                }
                if (!TableExists(connection, "ScheduleChangeItems"))
                {
                    ExecuteUpgrade(connection,
                        "CREATE TABLE ScheduleChangeItems (" +
                        "ItemId AUTOINCREMENT PRIMARY KEY, " +
                        "RequestId LONG NOT NULL, EmployeeId LONG NOT NULL, " +
                        "DayNumber LONG NOT NULL, OldShiftId LONG, NewShiftId LONG)");
                    ExecuteUpgrade(connection,
                        "CREATE INDEX IX_SchedChgItem_Req ON ScheduleChangeItems (RequestId)");
                }
                using (OleDbCommand maxConsecCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Sch.MaxConsecutiveWorkDays'", connection))
                {
                    if (Convert.ToInt32(maxConsecCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Sch.MaxConsecutiveWorkDays', '6', '" +
                            "أقصى عدد أيام عمل متتابعة في الجدول الشهري لكل موظف (0 = تعطيل القاعدة)" +
                            "', Now())");
                    }
                }
                using (OleDbCommand targetHoursCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Sch.TargetMonthlyHours'", connection))
                {
                    if (Convert.ToInt32(targetHoursCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Sch.TargetMonthlyHours', '0', '" +
                            "هدف الساعات الشهري لكل موظف في الجدول — الحفظ يُرفض عند النقص (0 = تعطيل الفحص)" +
                            "', Now())");
                    }
                }
                using (OleDbCommand rotationCount = new OleDbCommand(
                    "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = 'Sch.RotationTemplates'", connection))
                {
                    if (Convert.ToInt32(rotationCount.ExecuteScalar()) == 0)
                    {
                        ExecuteUpgrade(connection,
                            "INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt) " +
                            "VALUES ('Sch.RotationTemplates', '5 عمل + 2 راحة=DEF:5,REST:2;3 عمل + 1 راحة=DEF:3,REST:1', '" +
                            "قوالب الأنماط الدورية للجداول الشهرية — الاسم=DEF عدد,REST عدد مفصولة بفواصل والقوالب بفاصلة منقوطة؛ DEF=وردية الملء، REST=الراحة، أو كود وردية صريح" +
                            "', Now())");
                    }
                }
                // DB-03 (Phase 4): index verification for databases upgraded in
                // place. Every non-unique index from ModernTime_Schema.sql is
                // re-created here when missing, so a legacy .mdb that predates
                // the schema's index list reaches full hot-path coverage at
                // startup without any manual step. UNIQUE indexes are excluded
                // on purpose: creating IX_Emp_Enroll could fail on legacy data
                // that already contains duplicate enroll numbers, and adding a
                // UNIQUE constraint is a data change, not a safe upgrade.
                // Rollback for any index created here: DROP INDEX <name> ON <table>.
                EnsureIndex(connection, "FingerprintLogs", "IX_LogTime", "LogTime");
                EnsureIndex(connection, "FingerprintLogs", "IX_LogEmpDate", "EmployeeId, LogTime");
                EnsureIndex(connection, "Employees", "IX_Emp_Dept", "DepartmentId");
                EnsureIndex(connection, "Employees", "IX_Emp_Name", "FullNameAr");
                EnsureIndex(connection, "Employees", "IX_Emp_Status", "Status, IsDeleted");
                EnsureIndex(connection, "Attendance", "IX_Att_Date", "AttDate");
                EnsureIndex(connection, "AttendanceExceptions", "IX_Exc_EmpDate", "EmployeeId, AttDate");
                EnsureIndex(connection, "AttendanceAdjustments", "IX_Adj_Pending", "Status");
                EnsureIndex(connection, "Overtime", "IX_OT_Date", "OtDate");
                EnsureIndex(connection, "Penalties", "IX_PenaltyEmp", "EmployeeId, PenaltyDate");
                EnsureIndex(connection, "AuditLogs", "IX_Audit_Time", "ActionTime");
                EnsureIndex(connection, "AuditLogs", "IX_Audit_Table", "TableName, RecordId");
                EnsureIndex(connection, "AuditLogs", "IX_Audit_User", "UserId, ActionTime");
                EnsureIndex(connection, "LoginHistory", "IX_Login_User", "UserId, AttemptTime");
                EnsureIndex(connection, "EmployeeHistory", "IX_Hist_Emp", "EmployeeId, ChangeDate");
                EnsureIndex(connection, "LongLeaves", "IX_LongLeave_Emp", "EmployeeId, FromDate");
                EnsureIndex(connection, "LongLeaves", "IX_LongLeave_Type", "TypeId");
                EnsureIndex(connection, "LongLeaves", "IX_LongLeave_St", "Status, FromDate");
                EnsureIndex(connection, "EmployeeFiles", "IX_EmployeeFiles_Emp", "EmployeeId");
                EnsureIndex(connection, "ScheduleApprovals", "IX_SchedAppr_Sched", "ScheduleId");
                EnsureIndex(connection, "ScheduleChangeRequests", "IX_SchedChg_Status", "Status");
                EnsureIndex(connection, "ScheduleChangeItems", "IX_SchedChgItem_Req", "RequestId");
            }
        }

        /// <summary>
        /// Converts LONG day-amount columns to CURRENCY in place. Kept separate from
        /// EnsureColumn because the same column-name rows are reused for the type check
        /// (adCurrency = 6); a conversion is skipped when the column is already currency.
        /// </summary>
        private static void EnsureCurrencyColumn(OleDbConnection connection, string tableName, string columnName)
        {
            if (!ColumnExists(connection, tableName, columnName))
            {
                ExecuteUpgrade(connection, "ALTER TABLE " + tableName +
                    " ADD COLUMN " + columnName + " CURRENCY");
                return;
            }
            DataTable columns = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
                new object[] { null, null, tableName, columnName });
            if (columns == null || columns.Rows.Count == 0)
            {
                return;
            }
            int dataType = Convert.ToInt32(columns.Rows[0]["DATA_TYPE"]);
            if (dataType != 6)
            {
                ExecuteUpgrade(connection, "ALTER TABLE " + tableName +
                    " ALTER COLUMN " + columnName + " CURRENCY");
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

        /// <summary>
        /// DB-03 (Phase 4): verifies that a named non-unique index exists on an
        /// upgraded legacy database and creates it when missing. Idempotent and
        /// additive-only: it never modifies or deletes data, and it skips tables
        /// that do not exist (those get their indexes atomically at creation).
        /// Rollback for anything created here is DROP INDEX &lt;name&gt; ON &lt;table&gt;.
        /// </summary>
        private static void EnsureIndex(OleDbConnection connection, string tableName, string indexName, string columns)
        {
            if (!TableExists(connection, tableName))
            {
                return;
            }
            DataTable indexes = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Indexes,
                new object[] { null, null, tableName, null, null });
            if (indexes != null)
            {
                foreach (DataRow row in indexes.Rows)
                {
                    object nameValue = row["INDEX_NAME"];
                    if (nameValue != null && string.Equals(Convert.ToString(nameValue), indexName, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
            }
            ExecuteUpgrade(connection, "CREATE INDEX " + indexName + " ON " + tableName + " (" + columns + ")");
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
