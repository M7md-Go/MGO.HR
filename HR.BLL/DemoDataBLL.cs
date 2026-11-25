using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Demo-data generator for the whole project (order «أضف بيانات تجريبية
    /// للمشروع كاملاً»). Inserts a coherent, clearly-labelled demo dataset on a
    /// FRESH (empty) database only: organisation tree, 24 Arabic employees,
    /// demo users (password Demo@123), devices, two weeks of fingerprint
    /// punches, approved monthly schedules for the current and previous month,
    /// balances/requests/missions/penalties/overtime/adjustments/holidays and
    /// demo employee files — then runs the REAL attendance engine over the
    /// seeded punches so Attendance is produced by the normal pipeline.
    /// Guardrails: permission "Settings.Manage", refuses when any employee
    /// already exists, everything labelled "بيانات تجريبية", audited
    /// Demo.Seed, adds only (never deletes), no schema change.
    /// </summary>
    public static class DemoDataBLL
    {
        /// <summary>Aggregated result shown on the settings screen after seeding.</summary>
        public sealed class SeedSummary
        {
            public Dictionary<string, int> Counts;
            public List<string> Notes;

            public SeedSummary()
            {
                Counts = new Dictionary<string, int>();
                Notes = new List<string>();
            }

            public void Add(string table, int rows)
            {
                if (Counts.ContainsKey(table))
                {
                    Counts[table] = Counts[table] + rows;
                }
                else
                {
                    Counts[table] = rows;
                }
            }
        }

        /// <summary>Shared demo password (8+ chars, letters+digits — passes the login policy).</summary>
        public const string DemoPassword = "Demo@123";
        public const string DemoMarkerKey = "Demo.Seeded";

        private static readonly string[] MaleNames = new string[]
        {
            "محمد", "أحمد", "عمر", "خالد", "سامي", "فهد", "يوسف", "طارق",
            "بسام", "وليد", "مالك", "زياد"
        };
        private static readonly string[] FemaleNames = new string[]
        {
            "فاطمة", "نورة", "ريم", "سارة", "هند", "لمياء", "دانة", "أمل"
        };
        private static readonly string[] FamilyNames = new string[]
        {
            "العتيبي", "القحطاني", "الشمري", "الحربي", "الدوسري",
            "الغامدي", "الزهراني", "المالكي", "السبيعي", "البقمي"
        };

        private const int EmployeeCount = 24;
        private const int PunchDaysBack = 13; // two working weeks ending yesterday

        /// <summary>True when the database holds no employees (the only safe seed state).</summary>
        public static bool IsFreshDatabase()
        {
            return Db.Count("SELECT COUNT(*) FROM Employees") == 0;
        }

        /// <summary>Generates the full demo dataset; each phase commits in its own short Jet transaction.</summary>
        public static SeedSummary Generate()
        {
            AppContext.RequirePermission("Settings.Manage");
            if (!IsFreshDatabase())
            {
                throw new BusinessException("قاعدة البيانات تحتوي على موظفين مسجّلين بالفعل، والتوليد يعمل على قاعدة بيانات فارغة فقط " +
                    "لحماية بياناتك الفعلية. خذ نسخة احتياطية وأجرّب التوليد على نسخة منفصلة من القاعدة.");
            }

            SeedSummary summary = new SeedSummary();
            int? currentUserId = AppContext.CurrentUser != null ? (int?)AppContext.CurrentUser.UserId : null;

            OrgIds org = SeedOrganization(summary);
            List<int> employeeIds = SeedEmployees(org, currentUserId, summary);
            SeedUsers(org, currentUserId, summary);
            SeedDevicesAndPunches(org, employeeIds, summary);
            SeedSchedules(employeeIds, org, currentUserId, summary);
            SeedOperations(employeeIds, org, currentUserId, summary);
            RunAttendanceEngine(summary);
            MarkSeeded();
            AuditBLL.Log("Demo.Seed", "Employees", null, null,
                "\u062a\u0648\u0644\u064a\u062f \u0628\u064a\u0627\u0646\u0627\u062a \u062a\u062c\u0631\u064a\u0628\u064a\u0629 \u0643\u0627\u0645\u0644\u0629 \u0644\u0644\u0645\u0634\u0631\u0648\u0639");
            return summary;
        }

        // ============================ phase 1: organisation ============================

        private sealed class OrgIds
        {
            public int CompanyId;
            public int BranchMainId;
            public int BranchEastId;
            public int DeptHrId;
            public int DeptFinanceId;
            public int DeptOperationsId;
            public int DeptItId;
            public int? SectionHrId;
            public int? SectionItId;
            public int JobTitleDirector;
            public int JobTitleAccountant;
            public int JobTitleHrSpecialist;
            public int JobTitleEngineer;
            public int JobTitleClerk;
            public int QualBachelor;
            public int? StatusId;
            public int? AdminUserId;
        }

        private static OrgIds SeedOrganization(SeedSummary summary)
        {
            OrgIds org = new OrgIds();
            Db.ExecuteInTransaction<object>(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                org.CompanyId = ExecId(connection, transaction,
                    "INSERT INTO Companies (CompanyCode, CompanyNameAr, CompanyNameEn, TaxNumber, Phone, Email, Address, IsActive) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P("CO-01"), Db.P("شركة الأفق القابضة (تجريبي)"), Db.P("Ufuq Demo Holding"),
                    Db.P("300000000003"), Db.P("0112000000"), Db.P("demo@ufuq.local"),
                    Db.P("الرياض — حي العليا (بيانات تجريبية)"), Db.PBool(true));

                org.BranchMainId = ExecId(connection, transaction,
                    "INSERT INTO Branches (BranchCode, BranchName, CompanyId, Address, Phone, IsActive) VALUES (?, ?, ?, ?, ?, ?)",
                    Db.P("BR-01"), Db.P("المركز الرئيسي"), Db.P(org.CompanyId),
                    Db.P("الرياض — العليا"), Db.P("0114000000"), Db.PBool(true));
                org.BranchEastId = ExecId(connection, transaction,
                    "INSERT INTO Branches (BranchCode, BranchName, CompanyId, Address, Phone, IsActive) VALUES (?, ?, ?, ?, ?, ?)",
                    Db.P("BR-02"), Db.P("فرع الشرق (تجريبي)"), Db.P(org.CompanyId),
                    Db.P("الدمام — الشاطئ"), Db.P("0138000000"), Db.PBool(true));

                org.DeptHrId = ExecId(connection, transaction,
                    "INSERT INTO Departments (DepartmentCode, DepartmentName, BranchId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("DEP-01"), Db.P("الموارد البشرية"), Db.P(org.BranchMainId), Db.PBool(true));
                org.DeptFinanceId = ExecId(connection, transaction,
                    "INSERT INTO Departments (DepartmentCode, DepartmentName, BranchId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("DEP-02"), Db.P("الشئون المالية"), Db.P(org.BranchMainId), Db.PBool(true));
                org.DeptOperationsId = ExecId(connection, transaction,
                    "INSERT INTO Departments (DepartmentCode, DepartmentName, BranchId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("DEP-03"), Db.P("العمليات والميدان"), Db.P(org.BranchEastId), Db.PBool(true));
                org.DeptItId = ExecId(connection, transaction,
                    "INSERT INTO Departments (DepartmentCode, DepartmentName, BranchId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("DEP-04"), Db.P("تقنية المعلومات"), Db.P(org.BranchMainId), Db.PBool(true));

                org.SectionHrId = ExecId(connection, transaction,
                    "INSERT INTO Sections (SectionCode, SectionName, DepartmentId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("SEC-01"), Db.P("سجل الموظفين"), Db.P(org.DeptHrId), Db.PBool(true));
                org.SectionItId = ExecId(connection, transaction,
                    "INSERT INTO Sections (SectionCode, SectionName, DepartmentId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("SEC-02"), Db.P("الدعم الفني"), Db.P(org.DeptItId), Db.PBool(true));

                org.JobTitleDirector = ExecId(connection, transaction,
                    "INSERT INTO JobTitles (TitleCode, TitleName, IsActive) VALUES (?, ?, ?)",
                    Db.P("JT-01"), Db.P("مدير قسم"), Db.PBool(true));
                org.JobTitleAccountant = ExecId(connection, transaction,
                    "INSERT INTO JobTitles (TitleCode, TitleName, IsActive) VALUES (?, ?, ?)",
                    Db.P("JT-02"), Db.P("محاسب"), Db.PBool(true));
                org.JobTitleHrSpecialist = ExecId(connection, transaction,
                    "INSERT INTO JobTitles (TitleCode, TitleName, IsActive) VALUES (?, ?, ?)",
                    Db.P("JT-03"), Db.P("أخصائي موارد بشرية"), Db.PBool(true));
                org.JobTitleEngineer = ExecId(connection, transaction,
                    "INSERT INTO JobTitles (TitleCode, TitleName, IsActive) VALUES (?, ?, ?)",
                    Db.P("JT-04"), Db.P("مهندس نظم"), Db.PBool(true));
                org.JobTitleClerk = ExecId(connection, transaction,
                    "INSERT INTO JobTitles (TitleCode, TitleName, IsActive) VALUES (?, ?, ?)",
                    Db.P("JT-05"), Db.P("موظف إداري"), Db.PBool(true));

                Exec(connection, transaction,
                    "INSERT INTO ActualJobs (JobCode, JobName, DepartmentId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("AJ-01"), Db.P("إشراف وتخطيط"), Db.P(org.DeptOperationsId), Db.PBool(true));
                Exec(connection, transaction,
                    "INSERT INTO ActualJobs (JobCode, JobName, DepartmentId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("AJ-02"), Db.P("محاسبة ورواتب"), Db.P(org.DeptFinanceId), Db.PBool(true));
                Exec(connection, transaction,
                    "INSERT INTO ActualJobs (JobCode, JobName, DepartmentId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("AJ-03"), Db.P("شؤون موظفين ميدانية"), Db.P(org.DeptHrId), Db.PBool(true));
                Exec(connection, transaction,
                    "INSERT INTO ActualJobs (JobCode, JobName, DepartmentId, IsActive) VALUES (?, ?, ?, ?)",
                    Db.P("AJ-04"), Db.P("إدارة شبكات"), Db.P(org.DeptItId), Db.PBool(true));

                org.QualBachelor = ExecId(connection, transaction,
                    "INSERT INTO Qualifications (QualCode, QualName) VALUES (?, ?)",
                    Db.P("Q1"), Db.P("بكالوريوس"));
                Exec(connection, transaction,
                    "INSERT INTO Qualifications (QualCode, QualName) VALUES (?, ?)",
                    Db.P("Q2"), Db.P("ماجستير"));
                Exec(connection, transaction,
                    "INSERT INTO Qualifications (QualCode, QualName) VALUES (?, ?)",
                    Db.P("Q3"), Db.P("ثانوية"));
                return null;
            });
            summary.Add("Companies/Branches/Departments/...", 21);
            return org;
        }

        // ============================ phase 2: employees ============================

        private static List<int> SeedEmployees(OrgIds org, int? currentUserId, SeedSummary summary)
        {
            List<int> employeeIds = new List<int>();
            Db.ExecuteInTransaction<object>(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand lookup = new OleDbCommand(
                    "SELECT TOP 1 EmployeeStatusId FROM EmployeeStatus ORDER BY EmployeeStatusId",
                    connection, transaction))
                {
                    object statusValue = lookup.ExecuteScalar();
                    org.StatusId = statusValue == null || statusValue is DBNull
                        ? (int?)null
                        : Convert.ToInt32(statusValue);
                }

                string insert = "INSERT INTO Employees (EmployeeCode, NationalId, FullNameAr, FullNameEn, Gender, BirthDate, HireDate, " +
                    "CompanyId, BranchId, DepartmentId, SectionId, JobTitleId, EmployeeStatusId, QualificationId, EmploymentType, " +
                    "Phone, Email, Address, DeviceEnrollNumber, ContractHours, Salary, Status, IsDeleted, Notes, FirstWorkDate, CreatedAt, CreatedBy) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
                for (int i = 0; i < EmployeeCount; i++)
                {
                    bool female = (i % 4) == 3;
                    string first = female ? FemaleNames[i % FemaleNames.Length] : MaleNames[i % MaleNames.Length];
                    string family = FamilyNames[i % FamilyNames.Length];
                    string fullName = first + " " + family;
                    int department = (i % 4) + 1;
                    int deptId = department == 1 ? org.DeptHrId
                        : department == 2 ? org.DeptFinanceId
                        : department == 3 ? org.DeptOperationsId
                        : org.DeptItId;
                    int? sectionId = i % 8 == 0 ? org.SectionHrId : (i % 8 == 4 ? org.SectionItId : (int?)null);
                    int jobTitle = ChooseJobTitle(i, org);
                    int? qualification = org.QualBachelor;
                    string employmentType = i % 7 == 6 ? "PartTime" : (i % 5 == 4 ? "Contract" : "Permanent");
                    float contractHours = i % 6 == 5 ? 6f : 8f;
                    decimal salary = 4500m + Pseudo(i, 5, 40) * 250m;
                    DateTime hireDate = DateTime.Today.AddDays(-(200 + Pseudo(i, 9, 80) * 30));
                    DateTime birthDate = hireDate.AddYears(-(23 + Pseudo(i, 4, 16)));

                    int employeeId = ExecId(connection, transaction, insert,
                        Db.P("EMP-" + (i + 1).ToString("0000")),
                        Db.P("10" + (10000000 + Pseudo(i, 2, 89999999)).ToString()),
                        Db.P(fullName),
                        Db.P(UpperEnglish(first) + " " + UpperEnglish(family)),
                        Db.P(female ? "F" : "M"),
                        Db.PDate(birthDate),
                        Db.PDateRequired(hireDate),
                        Db.P(org.CompanyId),
                        Db.P(i % 3 == 2 ? org.BranchEastId : org.BranchMainId),
                        Db.P(deptId),
                        Db.P(sectionId),
                        Db.P(jobTitle),
                        Db.P(org.StatusId),
                        Db.P(qualification),
                        Db.P(employmentType),
                        Db.P("05" + (50000000 + Pseudo(i, 3, 49999999)).ToString()),
                        Db.P("emp" + (i + 1).ToString("0000") + "@demo.local"),
                        Db.P("حي السلام — الرياض (بيانات تجريبية)"),
                        Db.P((1000 + i + 1).ToString()),
                        Db.PSingle(contractHours),
                        Db.PCurrency(salary),
                        Db.P(EmployeeBLL.StatusActive),
                        Db.PBool(false),
                        Db.P("سجل تجريبي مُولَّد آليًا للتدريب والتقييم"),
                        Db.PDateRequired(hireDate),
                        Db.PDateRequired(DateTime.Now),
                        Db.PNullableId(currentUserId));
                    employeeIds.Add(employeeId);

                    if (i % 4 == 0)
                    {
                        Exec(connection, transaction,
                            "INSERT INTO EmployeeHistory (EmployeeId, ChangeDate, ChangeType, FieldName, OldValue, NewValue, ChangedBy) " +
                            "VALUES (?, ?, ?, ?, ?, ?, ?)",
                            Db.P(employeeId), Db.PDateRequired(hireDate), Db.P("StatusChange"),
                            Db.P("Status"), Db.P(null), Db.P("تعيين — تفعيل السجل"),
                            Db.PNullableId(currentUserId));
                    }
                }
                return null;
            });
            summary.Add("Employees", EmployeeCount);
            summary.Add("EmployeeHistory", EmployeeCount / 4);
            return employeeIds;
        }

        private static int ChooseJobTitle(int i, OrgIds org)
        {
            int column = i % 4;
            if (column == 0)
            {
                return i % 2 == 0 ? org.JobTitleHrSpecialist : org.JobTitleDirector;
            }
            if (column == 1)
            {
                return org.JobTitleAccountant;
            }
            if (column == 2)
            {
                return i % 2 == 0 ? org.JobTitleEngineer : org.JobTitleDirector;
            }
            return org.JobTitleClerk;
        }

        private static string UpperEnglish(string arabic)
        {
            // demo transliteration table for the small name pool
            switch (arabic)
            {
                case "محمد": return "Mohammed";
                case "أحمد": return "Ahmed";
                case "عمر": return "Omar";
                case "خالد": return "Khalid";
                case "سامي": return "Sami";
                case "فهد": return "Fahad";
                case "يوسف": return "Youssef";
                case "طارق": return "Tariq";
                case "بسام": return "Basam";
                case "وليد": return "Waleed";
                case "مالك": return "Malik";
                case "زياد": return "Ziad";
                case "فاطمة": return "Fatimah";
                case "نورة": return "Noura";
                case "ريم": return "Reem";
                case "سارة": return "Sarah";
                case "هند": return "Hind";
                case "لمياء": return "Lamia";
                case "دانة": return "Dana";
                case "أمل": return "Amal";
                case "العتيبي": return "Altayebi";
                case "القحطاني": return "Alqahtani";
                case "الشمري": return "Alshammari";
                case "الحربي": return "Alharbi";
                case "الدوسري": return "Aldosari";
                case "الغامدي": return "Alghamdi";
                case "الزهراني": return "Alzahrani";
                case "المالكي": return "Almalki";
                case "السبيعي": return "Alsobaiei";
                default: return "Albaqami";
            }
        }

        /// <summary>Deterministic pseudo-random (stable demo output across runs): a,b → 0..mod-1.</summary>
        private static int Pseudo(int a, int b, int mod)
        {
            if (mod <= 1)
            {
                return 0;
            }
            return Math.Abs((a * 31 + b * 17 + 7) * 13) % mod;
        }

        // ============================ phase 3: users ============================

        private static void SeedUsers(OrgIds org, int? currentUserId, SeedSummary summary)
        {
            Db.ExecuteInTransaction<object>(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                int supervisorRoleId = ScalarId(connection, transaction,
                    "SELECT TOP 1 RoleId FROM Roles WHERE RoleName = 'Supervisor'");
                int userRoleId = ScalarId(connection, transaction,
                    "SELECT TOP 1 RoleId FROM Roles WHERE RoleName = 'User'");
                org.AdminUserId = ScalarId(connection, transaction,
                    "SELECT TOP 1 UserId FROM Users WHERE Username = 'admin'");

                InsertDemoUser(connection, transaction, "manager", "منال الشهري — مديرة النظام (تجريبي)", supervisorRoleId,
                    org.DeptHrId, false, currentUserId);
                InsertDemoUser(connection, transaction, "supervisor", "نواف المطيري — مشرف حضور (تجريبي)", supervisorRoleId,
                    org.DeptOperationsId, false, currentUserId);
                InsertDemoUser(connection, transaction, "hr", "ريم العسيري — موظفة موارد بشرية (تجريبي)", userRoleId,
                    org.DeptHrId, false, currentUserId);
                InsertDemoUser(connection, transaction, "disabled", "حساب موقوف للتجربة (تجريبي)", userRoleId,
                    null, true, currentUserId);
                // login history: a few successful/failed attempts for the login-history screen
                for (int k = 5; k >= 0; k--)
                {
                    bool failed = k % 3 == 1;
                    Exec(connection, transaction,
                        "INSERT INTO LoginHistory (UserId, AttemptTime, Success, MachineName, IpAddress) VALUES (?, ?, ?, ?, ?)",
                        Db.PNullableId(k % 2 == 0 ? org.AdminUserId : (int?)null),
                        Db.PDateRequired(DateTime.Now.AddMinutes(-(k * 95))),
                        Db.PBool(!failed),
                        Db.P(AppContext.MachineName),
                        Db.P(AppContext.LocalIpAddress));
                }
                summary.Add("Users", 4);
                summary.Add("LoginHistory", 6);
                return null;
            });
        }

        private static void InsertDemoUser(OleDbConnection connection, OleDbTransaction transaction,
            string username, string fullName, int roleId, int? deptId, bool disabled, int? currentUserId)
        {
            string salt = PasswordHasher.NewSalt();
            Exec(connection, transaction,
                "INSERT INTO Users (Username, PwdHash, Salt, FullName, RoleId, DeptId, IsDisabled, IsLocked, FailedLogins, CreatedAt, CreatedBy) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                Db.P(username), Db.P(PasswordHasher.Hash(DemoPassword, salt)), Db.P(salt), Db.P(fullName),
                Db.P(roleId), Db.PNullableId(deptId), Db.PBool(disabled), Db.PBool(false), Db.P(0),
                Db.PDateRequired(DateTime.Now), Db.PNullableId(currentUserId));
        }

        // ============================ phase 4: devices + fingerprint punches ============================

        private static void SeedDevicesAndPunches(OrgIds org, List<int> employeeIds, SeedSummary summary)
        {
            Db.ExecuteInTransaction<object>(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                int deviceMainId = ExecId(connection, transaction,
                    "INSERT INTO Devices (DeviceName, IpAddress, Port, SerialNumber, LastSync, LastStatus, LastPingDelay, IsActive, Location) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P("جهاز الدخول الرئيسي (تجريبي)"), Db.P("192.168.1.201"), Db.P(4370), Db.P("DG-DEMO-001"),
                    Db.PDate(DateTime.Now.AddMinutes(-32)), Db.P("Connected"), Db.P(12), Db.PBool(true),
                    Db.P("المدخل الرئيسي"));
                Exec(connection, transaction,
                    "INSERT INTO Devices (DeviceName, IpAddress, Port, SerialNumber, LastSync, LastStatus, LastPingDelay, IsActive, Location) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P("جهاز الإدارة (تجريبي)"), Db.P("192.168.1.202"), Db.P(4370), Db.P("DG-DEMO-002"),
                    Db.PDate(DateTime.Now.AddHours(-6)), Db.P("Offline"), Db.P(0), Db.PBool(true),
                    Db.P("الطابق الثاني — الإدارة"));
                summary.Add("Devices", 2);

                string insertLog = "INSERT INTO FingerprintLogs (DeviceId, EmployeeId, EmpDevCode, LogTime, InOutFlag, SourceFile, CreatedAt) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?)";
                int punches = 0;
                DateTime day = DateTime.Today.AddDays(-PunchDaysBack);
                while (day < DateTime.Today)
                {
                    if (day.DayOfWeek != DayOfWeek.Friday) // Sch.RestDays default = Friday
                    {
                        for (int i = 0; i < employeeIds.Count; i++)
                        {
                            int employeeId = employeeIds[i];

                            // one clear "absent day" scenario per cycle
                            if ((i % 13 == 5) && (day.Day % 4 == 2))
                            {
                                continue;
                            }

                            bool late = (i % 9 == 0) && (day.Day % 3 == 0);
                            int inMinute = late ? 22 + Pseudo(i, day.Day, 10) : 50 + Pseudo(i, day.Day, 9);
                            DateTime inPunch = day.AddHours(7).AddMinutes(inMinute);
                            punches += InsertPunch(connection, transaction, insertLog,
                                deviceMainId, employeeId, i, inPunch, 1);

                            // one clear "missing exit punch" scenario per cycle
                            bool missingExit = (i % 11 == 0) && (day.Day % 4 == 1);
                            if (!missingExit)
                            {
                                int outMinute = Pseudo(i, day.Day + 3, 14);
                                DateTime outPunch = day.AddHours(15).AddMinutes(outMinute);
                                punches += InsertPunch(connection, transaction, insertLog,
                                    deviceMainId, employeeId, i, outPunch, 0);
                            }
                        }
                    }
                    day = day.AddDays(1);
                }
                summary.Add("FingerprintLogs", punches);
                summary.Notes.Add("سيناريوهات مدمجة قصدًا: تأخير دوري لبعض الموظفين، غياب يوم واحد لكل دورة، وبصمة خروج ناقصة — لتجربة كل حالات المحرك.");
                return null;
            });
        }

        private static int InsertPunch(OleDbConnection connection, OleDbTransaction transaction,
            string insertSql, int deviceId, int employeeId, int index, DateTime logTime, int inOutFlag)
        {
            Exec(connection, transaction, insertSql,
                Db.P(deviceId), Db.P(employeeId), Db.P("EMP-" + (index + 1).ToString("0000")),
                Db.PDateRequired(logTime), Db.P(inOutFlag), Db.P("DEMO-SEED"), Db.PDateRequired(DateTime.Now));
            return 1;
        }

        // ============================ phase 5: monthly schedules ============================

        private static void SeedSchedules(List<int> employeeIds, OrgIds org, int? currentUserId, SeedSummary summary)
        {
            int details = 0;
            DateTime month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
            for (int m = 0; m < 2; m++)
            {
                int closedMonth = month.Month;
                int closedYear = month.Year;
                details += Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
                {
                    int workShiftId = ScalarId(connection, transaction,
                        "SELECT TOP 1 ShiftId FROM Shifts WHERE ShiftCode = 'M2'");
                    int restShiftId = ScalarId(connection, transaction,
                        "SELECT TOP 1 ShiftId FROM Shifts WHERE ShiftCode = 'FR'");
                    int scheduleId = ExecId(connection, transaction,
                        "INSERT INTO MonthlySchedules (YearVal, MonthVal, Status, CreatedBy, CreatedAt, ApprovedBy, ApprovedAt) " +
                        "VALUES (?, ?, ?, ?, ?, ?, ?)",
                        Db.P(closedYear), Db.P(closedMonth), Db.P("Approved"),
                        Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now),
                        Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                    int rows = 0;
                    string insert = "INSERT INTO ScheduleDetails (ScheduleId, EmployeeId, DayNumber, ShiftId) VALUES (?, ?, ?, ?)";
                    for (int e = 0; e < employeeIds.Count; e++)
                    {
                        for (int d = 1; d <= DateTime.DaysInMonth(closedYear, closedMonth); d++)
                        {
                            DayOfWeek weekday = new DateTime(closedYear, closedMonth, d).DayOfWeek;
                            rows += Exec(connection, transaction, insert,
                                Db.P(scheduleId), Db.P(employeeIds[e]), Db.P(d),
                                Db.P(weekday == DayOfWeek.Friday ? restShiftId : workShiftId));
                        }
                    }
                    return rows;
                });
                month = month.AddMonths(1);
            }
            summary.Add("MonthlySchedules", 2);
            summary.Add("ScheduleDetails", details);
        }

        // ============================ phase 6: operations ============================

        private static void SeedOperations(List<int> employeeIds, OrgIds org, int? currentUserId, SeedSummary summary)
        {
            List<string[]> demoFileRows = new List<string[]>();
            Db.ExecuteInTransaction<object>(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                // ---- public holidays (recurring so they count every matching month-day) ----
                int year = DateTime.Today.Year;
                Exec(connection, transaction,
                    "INSERT INTO PublicHolidays (HolidayDate, HolidayName, IsRecurring) VALUES (?, ?, ?)",
                    Db.PDateRequired(new DateTime(year, 9, 23)), Db.P("اليوم الوطني (تجريبي)"), Db.PBool(true));
                Exec(connection, transaction,
                    "INSERT INTO PublicHolidays (HolidayDate, HolidayName, IsRecurring) VALUES (?, ?, ?)",
                    Db.PDateRequired(new DateTime(year, 5, 1)), Db.P("عيد العمال (تجريبي)"), Db.PBool(true));
                summary.Add("PublicHolidays", 2);

                // ---- leave balances for the current year ----
                int annualTypeId = ScalarId(connection, transaction, "SELECT TOP 1 LeaveTypeId FROM LeaveTypes WHERE LeaveTypeCode = 'Annual'");
                int sickTypeId = ScalarId(connection, transaction, "SELECT TOP 1 LeaveTypeId FROM LeaveTypes WHERE LeaveTypeCode = 'Sick'");
                int casualTypeId = ScalarId(connection, transaction, "SELECT TOP 1 LeaveTypeId FROM LeaveTypes WHERE LeaveTypeCode = 'Casual'");
                int fiscalYear = LeaveBLL.BalanceYearFor(DateTime.Today);
                int balances = 0;
                for (int i = 0; i < employeeIds.Count; i++)
                {
                    balances += InsertBalance(connection, transaction, employeeIds[i], fiscalYear, annualTypeId, 21, Pseudo(i, 1, 9));
                    balances += InsertBalance(connection, transaction, employeeIds[i], fiscalYear, sickTypeId, 30, Pseudo(i, 6, 4));
                    balances += InsertBalance(connection, transaction, employeeIds[i], fiscalYear, casualTypeId, 7, Pseudo(i, 2, 3));
                }
                summary.Add("LeaveBalances", balances);

                // ---- leave requests: approved / pending / rejected mix ----
                int leaves = 0;
                leaves += InsertLeave(connection, transaction, employeeIds[0], annualTypeId, -20, 5, "Approved", "إجازة سنوية مخططة", null, currentUserId);
                leaves += InsertLeave(connection, transaction, employeeIds[2], sickTypeId, -9, 2, "Approved", "حالة مرضية بتقرير طبي", null, currentUserId);
                leaves += InsertLeave(connection, transaction, employeeIds[5], casualTypeId, -5, 1, "Approved", "ظرف عائلي", null, currentUserId);
                leaves += InsertLeave(connection, transaction, employeeIds[9], annualTypeId, -3, 3, "Approved", "سفر مع الأسرة", null, currentUserId);
                leaves += InsertLeave(connection, transaction, employeeIds[4], annualTypeId, 5, 7, "Pending", "طلب إجازة سنوية قادمة", null, currentUserId);
                leaves += InsertLeave(connection, transaction, employeeIds[11], casualTypeId, 2, 1, "Pending", "مراجعة حكومية", null, currentUserId);
                leaves += InsertLeave(connection, transaction, employeeIds[7], sickTypeId, -15, 4, "Rejected", "بدون تقرير طبي", "لا تستوفي متطلب المستند الطبي", currentUserId);
                leaves += InsertLeave(connection, transaction, employeeIds[14], annualTypeId, 1, 10, "Rejected", "إجازة طويلة بدون رصيد", "الرصيد غير كافٍ", currentUserId);
                summary.Add("LeaveRequests", leaves);

                // ---- long leaves ----
                int abroadTypeId = ScalarId(connection, transaction, "SELECT TOP 1 LongLeaveTypeId FROM LongLeaveTypes WHERE TypeCode = 'Abroad'");
                Exec(connection, transaction,
                    "INSERT INTO LongLeaves (EmployeeId, TypeId, FromDate, ToDate, Status, Reason, CreatedAt, CreatedBy) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[3]), Db.P(abroadTypeId), Db.PDateRequired(DateTime.Today.AddMonths(-1).AddDays(-10)),
                    Db.PDate(DateTime.Today.AddMonths(-1).AddDays(-10).AddDays(35)), Db.P("Approved"),
                    Db.P("سفر للدراسة — موافقة مسبقة (تجريبي)"), Db.PDateRequired(DateTime.Now), Db.PNullableId(currentUserId));
                Exec(connection, transaction,
                    "INSERT INTO LongLeaves (EmployeeId, TypeId, FromDate, Status, Reason, CreatedAt, CreatedBy) VALUES (?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[13]), Db.P(abroadTypeId), Db.PDateRequired(DateTime.Today.AddDays(-30)),
                    Db.P("Pending"), Db.P("إجازة طويلة غير محددة (تجريبي)"), Db.PDateRequired(DateTime.Now), Db.PNullableId(currentUserId));
                summary.Add("LongLeaves", 2);

                // ---- missions ----
                int missions = 0;
                missions += InsertMission(connection, transaction, employeeIds[1], -6, 1, "08:00", "17:00", "فرع الشرق — الدمام", "إشراف ميداني", true, "Approved", currentUserId);
                missions += InsertMission(connection, transaction, employeeIds[6], -4, 1, "09:00", "14:00", "الغرفة التجارية", "اجتماع عملاء", false, "Approved", currentUserId);
                missions += InsertMission(connection, transaction, employeeIds[10], -2, 2, "08:00", "16:00", "موقع المشروع الشمالي", "معاينة تقدم الأعمال", true, "Approved", currentUserId);
                missions += InsertMission(connection, transaction, employeeIds[16], 1, 1, "08:30", "12:30", "البنك التجاري", "مهمة مصرفية", false, "Pending", currentUserId);
                missions += InsertMission(connection, transaction, employeeIds[18], 4, 1, "07:30", "15:00", "الجبيل", "تسليم معدات", true, "Pending", currentUserId);
                missions += InsertMission(connection, transaction, employeeIds[20], -8, 1, "08:00", "14:00", "الرياض", "مناقشة عرض", false, "Rejected", currentUserId);
                summary.Add("Missions", missions);

                // ---- penalties ----
                Exec(connection, transaction,
                    "INSERT INTO Penalties (EmployeeId, AttDate, PenaltyDate, TypeCode, Amount, Reason, IssuedBy, CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[8]), Db.PDate(DateTime.Today.AddDays(-12)), Db.PDate(DateTime.Today.AddDays(-12)),
                    Db.P(PenaltyBLL.TypeWarning), Db.PCurrency(0m), Db.P("تأخير متكرر (تجريبي)"), Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                Exec(connection, transaction,
                    "INSERT INTO Penalties (EmployeeId, AttDate, PenaltyDate, TypeCode, Amount, Reason, IssuedBy, CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[12]), Db.PDate(DateTime.Today.AddDays(-9)), Db.PDate(DateTime.Today.AddDays(-9)),
                    Db.P(PenaltyBLL.TypeDeduction), Db.PCurrency(100m), Db.P("غياب بدون إذن يوم كامل (تجريبي)"), Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                Exec(connection, transaction,
                    "INSERT INTO Penalties (EmployeeId, AttDate, PenaltyDate, TypeCode, Amount, Reason, IssuedBy, CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[17]), Db.PDate(DateTime.Today.AddDays(-6)), Db.PDate(DateTime.Today.AddDays(-6)),
                    Db.P(PenaltyBLL.TypeAbsence), Db.PCurrency(150m), Db.P("مغادرة قبل انتهاء الدوام (تجريبي)"), Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                Exec(connection, transaction,
                    "INSERT INTO Penalties (EmployeeId, AttDate, PenaltyDate, TypeCode, Amount, Reason, IssuedBy, CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[21]), Db.PDate(DateTime.Today.AddDays(-3)), Db.PDate(DateTime.Today.AddDays(-3)),
                    Db.P(PenaltyBLL.TypeViolation), Db.PCurrency(200m), Db.P("إخلال بلائحة السلامة (تجريبي)"), Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                Exec(connection, transaction,
                    "INSERT INTO Penalties (EmployeeId, AttDate, PenaltyDate, TypeCode, Amount, Reason, IssuedBy, CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[5]), Db.PDate(DateTime.Today.AddDays(-1)), Db.PDate(DateTime.Today.AddDays(-1)),
                    Db.P(PenaltyBLL.TypeWarning), Db.PCurrency(0m), Db.P("بصمة ناقصة لأول مرة (تجريبي)"), Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                summary.Add("Penalties", 5);

                // ---- overtime ----
                int ot = 0;
                ot += InsertOvertime(connection, transaction, employeeIds[2], -7, 120, 120, 1.5, "Manual", "Approved", "عمل إضافي على التسوية الشهرية (تجريبي)", currentUserId);
                ot += InsertOvertime(connection, transaction, employeeIds[7], -6, 90, 90, 1.5, "Manual", "Approved", "استلام شحنة مساء (تجريبي)", currentUserId);
                ot += InsertOvertime(connection, transaction, employeeIds[15], -4, 180, 150, 1.5, "Manual", "Approved", "إنهاء تقرير رسمي (تجريبي)", currentUserId);
                ot += InsertOvertime(connection, transaction, employeeIds[19], -2, 60, 60, 1.5, "Manual", "Approved", "دعم فني بعد الدوام (تجريبي)", currentUserId);
                ot += InsertOvertime(connection, transaction, employeeIds[4], -1, 120, null, 1.5, "Manual", "Pending", "طلب عمل إضافي قيد الاعتماد (تجريبي)", currentUserId);
                ot += InsertOvertime(connection, transaction, employeeIds[21], -1, 240, null, 1.5, "Manual", "Pending", "تحديث المخزون (تجريبي)", currentUserId);
                summary.Add("Overtime", ot);

                // ---- manual attendance adjustments (pending, for the approvals flow) ----
                Exec(connection, transaction,
                    "INSERT INTO AttendanceAdjustments (EmployeeId, AttDate, AdjustmentType, NewInTime, NewOutTime, Reason, Status, CreatedBy, CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[9]), Db.PDate(DateTime.Today.AddDays(-4)),
                    Db.P(AttendanceAdjustmentBLL.TypeTime), Db.P("08:05"), Db.P("15:10"),
                    Db.P("الموظف أنسى البصمة صباحًا — شهادة المشرف (تجريبي)"), Db.P("Pending"),
                    Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                Exec(connection, transaction,
                    "INSERT INTO AttendanceAdjustments (EmployeeId, AttDate, AdjustmentType, NewStatus, Reason, Status, CreatedBy, CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[13]), Db.PDate(DateTime.Today.AddDays(-2)),
                    Db.P(AttendanceAdjustmentBLL.TypeStatus), Db.P("Mission"),
                    Db.P("أُرسل في مهمة رسمية موثقة (تجريبي)"), Db.P("Pending"),
                    Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                Exec(connection, transaction,
                    "INSERT INTO AttendanceAdjustments (EmployeeId, AttDate, AdjustmentType, NewInTime, NewOutTime, Reason, Status, CreatedBy, CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                    Db.P(employeeIds[18]), Db.PDate(DateTime.Today.AddDays(-3)),
                    Db.P(AttendanceAdjustmentBLL.TypeTime), Db.P("07:55"), Db.P("15:00"),
                    Db.P("خلل بجهاز البصمة الرئيسي (تجريبي)"), Db.P("Pending"),
                    Db.PNullableId(currentUserId), Db.PDateRequired(DateTime.Now));
                summary.Add("AttendanceAdjustments", 3);

                // ---- demo employee files (small text files under the data folder) ----
                try
                {
                    string demoFolder = Path.Combine(AppContext.DataFolder, "EmployeeFiles");
                    if (!Directory.Exists(demoFolder))
                    {
                        Directory.CreateDirectory(demoFolder);
                    }
                    WriteDemoFile(demoFileRows, demoFolder, employeeIds[0], "عقد-عمل-تجريبي.txt",
                        "عقد عمل تجريبي — بيانات وهمية لأغراض التدريب فقط.");
                    WriteDemoFile(demoFileRows, demoFolder, employeeIds[0], "توكيل-بنكي-تجريبي.txt",
                        "توكيل تجريبي — مستند وهمي.");
                    WriteDemoFile(demoFileRows, demoFolder, employeeIds[5], "شهادة-خبرة-تجريبية.txt",
                        "شهادة خبرة تجريبية — بيانات وهمية.");
                    summary.Add("EmployeeFiles", demoFileRows.Count);
                }
                catch (Exception fileFailure)
                {
                    summary.Notes.Add("تعذر إنشاء ملفات الموظفين التجريبية على القرص: " + fileFailure.Message);
                    demoFileRows.Clear();
                }
                return null;
            });
            // after the transaction commits: the Employees write-lock is released,
            // so the file rows can be inserted through the normal DAL path
            foreach (string[] fileRow in demoFileRows)
            {
                Db.ExecuteNonQuery(
                    "INSERT INTO EmployeeFiles (EmployeeId, FilePath, Category, Notes, CreatedAt) VALUES (?, ?, ?, ?, ?)",
                    Db.P(int.Parse(fileRow[0])), Db.P(fileRow[1]), Db.P(fileRow[2]), Db.P(fileRow[3]),
                    Db.PDateRequired(DateTime.Now));
            }
        }

        private static int InsertBalance(OleDbConnection connection, OleDbTransaction transaction,
            int employeeId, int year, int leaveTypeId, int quota, int used)
        {
            Exec(connection, transaction,
                "INSERT INTO LeaveBalances (EmployeeId, YearVal, LeaveTypeId, QuotaDays, UsedDays, Remaining) VALUES (?, ?, ?, ?, ?, ?)",
                Db.P(employeeId), Db.P(year), Db.P(leaveTypeId), Db.P(quota),
                Db.P(used), Db.P(quota - used));
            return 1;
        }

        private static int InsertLeave(OleDbConnection connection, OleDbTransaction transaction,
            int employeeId, int leaveTypeId, int startOffset, int days, string status,
            string reason, string rejectReason, int? currentUserId)
        {
            DateTime start = DateTime.Today.AddDays(startOffset);
            Exec(connection, transaction,
                "INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, StartDate, EndDate, DaysCount, Reason, RejectReason, Status, ApprovedBy, CreatedAt, CreatedBy) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                Db.P(employeeId), Db.P(leaveTypeId), Db.PDateRequired(start), Db.PDateRequired(start.AddDays(days)),
                Db.P(days), Db.P(reason), Db.P(rejectReason), Db.P(status),
                Db.PNullableId(string.Equals(status, "Approved", StringComparison.Ordinal) ? currentUserId : null),
                Db.PDateRequired(DateTime.Now), Db.PNullableId(currentUserId));
            return 1;
        }

        private static int InsertMission(OleDbConnection connection, OleDbTransaction transaction,
            int employeeId, int startOffset, int days, string startTime, string endTime,
            string location, string purpose, bool exempt, string status, int? currentUserId)
        {
            DateTime start = DateTime.Today.AddDays(startOffset);
            Exec(connection, transaction,
                "INSERT INTO Missions (EmployeeId, StartDate, StartTime, EndDate, EndTime, Location, Purpose, IsExemptAtt, Status, ApprovedBy, CreatedAt) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                Db.P(employeeId), Db.PDateRequired(start), Db.P(startTime),
                Db.PDateRequired(start.AddDays(days)), Db.P(endTime), Db.P(location), Db.P(purpose),
                Db.PBool(exempt), Db.P(status),
                Db.PNullableId(string.Equals(status, "Approved", StringComparison.Ordinal) ? currentUserId : null),
                Db.PDateRequired(DateTime.Now));
            return 1;
        }

        private static int InsertOvertime(OleDbConnection connection, OleDbTransaction transaction,
            int employeeId, int dayOffset, int planned, int? effective, double rate,
            string source, string status, string notes, int? currentUserId)
        {
            Exec(connection, transaction,
                "INSERT INTO Overtime (EmployeeId, OtDate, PlannedMinutes, ActualMinutes, EffectiveMinutes, Rate, Source, Status, RequestedBy, ApprovedBy, ApprovedAt, Notes, CreatedAt) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                Db.P(employeeId), Db.PDateRequired(DateTime.Today.AddDays(dayOffset)),
                Db.P(planned), Db.PNullableId(effective), Db.PNullableId(effective),
                Db.P(rate), Db.P(source), Db.P(status), Db.PNullableId(currentUserId),
                Db.PNullableId(string.Equals(status, "Approved", StringComparison.Ordinal) ? currentUserId : null),
                Db.PDate(string.Equals(status, "Approved", StringComparison.Ordinal) ? (DateTime?)DateTime.Now : null),
                Db.P(notes), Db.PDateRequired(DateTime.Now));
            return 1;
        }

        /// <summary>Writes the demo file on disk and queues its EmployeeFiles row (inserted after commit).</summary>
        private static void WriteDemoFile(List<string[]> rows, string folder, int employeeId, string fileName, string content)
        {
            string path = Path.Combine(folder, "EMP-" + employeeId.ToString("0000") + "-" + fileName);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content + Environment.NewLine +
                    "هذا ملف تجريبي مُولَّد آليًا بواسطة النظام لأغراض الاستعراض والتدريب فقط." + Environment.NewLine);
            }
            rows.Add(new string[] { employeeId.ToString(), path, fileName, content });
        }

        // ============================ phase 7: real attendance engine ============================

        private static void RunAttendanceEngine(SeedSummary summary)
        {
            try
            {
                AttendanceBLL.Recalculate(DateTime.Today.AddDays(-PunchDaysBack), DateTime.Today.AddDays(-1));
                summary.Notes.Add("شُغِّل محرك الحضور الحقيقي على بصمات آخر " + (PunchDaysBack + 1) + " يومًا — سجل الحضور جاهز للعرض.");
            }
            catch (Exception failure)
            {
                summary.Notes.Add("لم يُشغَّل محرك الحضور تلقائيًا (" + failure.Message + ") " +
                    "— شغّله يدويًا من شاشة «محرك الحضور» على آخر 14 يومًا.");
                Logger.LogInfo("DemoDataBLL.RunAttendanceEngine", failure.Message);
            }
        }

        // ============================ phase 8: marker ============================

        private static void MarkSeeded()
        {
            try
            {
                SettingsDAL.Insert(DemoMarkerKey, "1",
                    "هل وُلِّدت بيانات تجريبية في هذه القاعدة (يظهر داخل إعدادات النظام)");
            }
            catch (Exception failure)
            {
                // the marker is informational — never fail the seed because of it
                Logger.LogInfo("DemoDataBLL.MarkSeeded", failure.Message);
            }
        }

        // ============================ SQL helpers (Jet-safe, same-connection identity) ============================

        private static int Exec(OleDbConnection connection, OleDbTransaction transaction, string sql, params OleDbParameter[] parameters)
        {
            using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>Runs an INSERT then returns the new identity (same command/connection — Jet requirement).</summary>
        private static int ExecId(OleDbConnection connection, OleDbTransaction transaction, string sql, params OleDbParameter[] parameters)
        {
            using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }
                command.ExecuteNonQuery();
                command.Parameters.Clear();
                command.CommandText = "SELECT @@IDENTITY";
                object id = command.ExecuteScalar();
                return id == null || id is DBNull ? 0 : Convert.ToInt32(id);
            }
        }

        /// <summary>Single-value lookup inside the current transaction.</summary>
        private static int ScalarId(OleDbConnection connection, OleDbTransaction transaction, string sql)
        {
            using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
            {
                object value = command.ExecuteScalar();
                return value == null || value is DBNull ? 0 : Convert.ToInt32(value);
            }
        }
    }
}
