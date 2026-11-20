using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;

namespace HR.Services
{
    /// <summary>
    /// Reporting engine (rules 19, 20): materializes every standard report
    /// into a plain ReportData (title, columns, string rows, optional totals).
    /// Read-only by design — it composes existing DAL queries only, keeping
    /// the project graph acyclic (HR.BLL already references HR.Services, so
    /// this layer must not use HR.BLL). FrmReports previews the result and
    /// ExcelWriter exports it; the two consumers stay data-agnostic.
    /// Reversed from/to periods are normalized, not rejected.
    /// </summary>
    public static class ReportEngine
    {
        /// <summary>Maximum rows pulled from any single DAL search.</summary>
        private const int MaxRows = 100000;

        // ---------------- attendance ----------------

        /// <summary>تقرير الحضور اليومي (rules 19) — one row per attendance day.</summary>
        public static ReportData DailyAttendance(DateTime fromDate, DateTime toDate,
            int? departmentId, int? employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<AttendanceView> rows = AttendanceDAL.Browse(fromDate, toDate, employeeId,
                departmentId == 0 ? (int?)null : departmentId, MaxRows);
            ReportData report = new ReportData();
            report.Title = "تقرير الحضور اليومي";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("DepartmentName", "القسم", 120);
            report.AddColumn("AttDate", "التاريخ", 85);
            report.AddColumn("ShiftCode", "الوردية", 60);
            report.AddColumn("InTime", "الحضور", 70);
            report.AddColumn("OutTime", "الانصراف", 70);
            report.AddColumn("LateMinutes", "التأخير (د)", 70);
            report.AddColumn("EarlyLeaveMinutes", "المبكر (د)", 70);
            report.AddColumn("OvertimeMin", "الإضافي (د)", 70);
            report.AddColumn("WorkedH", "ساعات العمل", 70);
            report.AddColumn("Status", "الحالة", 80);

            long totalWorked = 0;
            long totalOvertime = 0;
            foreach (AttendanceView row in rows)
            {
                report.AddRow(row.EmployeeCode, row.EmployeeName, row.DepartmentName,
                    row.AttDate == null ? "" : row.AttDate.Value.ToString("yyyy-MM-dd"),
                    row.ShiftCode,
                    row.StartTime == null ? "" : row.StartTime.Value.ToString("HH:mm"),
                    row.EndTime == null ? "" : row.EndTime.Value.ToString("HH:mm"),
                    FormatMinuteValue(row.LateMinutes),
                    FormatMinuteValue(row.EarlyLeaveMinutes),
                    FormatMinuteValue(row.OvertimeMin),
                    OvertimeBllFormat(row.MinutesWorked),
                    StatusAr(row.Status));
                totalWorked += row.MinutesWorked ?? 0;
                totalOvertime += row.OvertimeMin ?? 0;
            }
            report.TotalsRow = TotalRow(report, rows.Count, totalWorked, totalOvertime,
                "ساعات العمل (الإضافي):", OvertimeBllFormat((int)totalWorked) + " (" + OvertimeBllFormat((int)totalOvertime) + ")");
            return report;
        }

        /// <summary>تقرير الحضور الشهري (rules 19) — one summary row per employee.</summary>
        public static ReportData MonthlyAttendanceSummary(int year, int month, int? departmentId, int? employeeId)
        {
            DateTime fromDate = new DateTime(year, month, 1);
            DateTime toDate = fromDate.AddMonths(1).AddDays(-1);
            List<AttendanceView> rows = AttendanceDAL.Browse(fromDate, toDate, employeeId,
                departmentId == 0 ? (int?)null : departmentId, MaxRows);

            Dictionary<int, List<AttendanceView>> byEmployee = new Dictionary<int, List<AttendanceView>>();
            foreach (AttendanceView row in rows)
            {
                if (row.EmployeeId == null)
                {
                    continue;
                }
                if (!byEmployee.ContainsKey(row.EmployeeId.Value))
                {
                    byEmployee[row.EmployeeId.Value] = new List<AttendanceView>();
                }
                byEmployee[row.EmployeeId.Value].Add(row);
            }

            ReportData report = new ReportData();
            report.Title = "تقرير الحضور الشهري";
            report.PeriodText = "الشهر: " + year + "-" + month.ToString("00");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("DepartmentName", "القسم", 120);
            report.AddColumn("ScheduledDays", "أيام الجدول", 70);
            report.AddColumn("PresentDays", "أيام الحضور", 70);
            report.AddColumn("AbsentDays", "الغياب", 60);
            report.AddColumn("LateCount", "مرات التأخير", 70);
            report.AddColumn("LateMinutes", "دقائق التأخير", 75);
            report.AddColumn("EarlyCount", "مرات المبكر", 70);
            report.AddColumn("EarlyMinutes", "دقائق المبكر", 75);
            report.AddColumn("MissingPunch", "بصمات ناقصة", 70);
            report.AddColumn("LeaveDays", "أيام إجازة", 70);
            report.AddColumn("WorkedH", "ساعات العمل", 70);
            report.AddColumn("OvertimeH", "ساعات إضافية", 75);

            long totalWorked = 0;
            long totalOvertime = 0;
            List<int> employeeIds = new List<int>(byEmployee.Keys);
            employeeIds.Sort();
            foreach (int empId in employeeIds)
            {
                DailyTotals totals = Summarize(byEmployee[empId]);
                AttendanceView sample = byEmployee[empId][0];
                report.AddRow(sample.EmployeeCode, sample.EmployeeName, sample.DepartmentName,
                    totals.ScheduledDays.ToString(), totals.PresentDays.ToString(),
                    totals.AbsentDays.ToString(), totals.LateCount.ToString(),
                    totals.LateMinutes.ToString(), totals.EarlyCount.ToString(),
                    totals.EarlyMinutes.ToString(), totals.MissingPunch.ToString(),
                    totals.LeaveDays.ToString(), OvertimeBllFormat(totals.WorkedMinutes),
                    OvertimeBllFormat(totals.OvertimeMinutes));
                totalWorked += totals.WorkedMinutes;
                totalOvertime += totals.OvertimeMinutes;
            }
            report.TotalsRow = TotalRow(report, byEmployee.Count, totalWorked, totalOvertime,
                "مجموع ساعات العمل (الإضافي):",
                OvertimeBllFormat((int)totalWorked) + " (" + OvertimeBllFormat((int)totalOvertime) + ")");
            return report;
        }

        /// <summary>تقرير ملخص القسم (rule 19) — monthly aggregates per department.</summary>
        public static ReportData DepartmentMonthlySummary(int year, int month)
        {
            ReportData perEmployee = MonthlyAttendanceSummary(year, month, null, null);
            Dictionary<string, DailyTotals> byDepartment = new Dictionary<string, DailyTotals>();
            Dictionary<string, string> departmentOf = new Dictionary<string, string>();
            int columnIndex = IndexOfColumn(perEmployee, "DepartmentName");
            for (int i = 0; i < perEmployee.Rows.Count; i++)
            {
                List<string> row = perEmployee.Rows[i];
                string department = row[columnIndex];
                if (string.IsNullOrEmpty(department))
                {
                    department = "غير محدد";
                }
                if (!byDepartment.ContainsKey(department))
                {
                    byDepartment[department] = new DailyTotals();
                    departmentOf[department] = row[0];
                }
                DailyTotals totals = byDepartment[department];
                totals.PresentDays += ParseInt(row[4]);
                totals.AbsentDays += ParseInt(row[5]);
                totals.LateCount += ParseInt(row[6]);
                totals.LateMinutes += ParseInt(row[7]);
                totals.EarlyCount += ParseInt(row[8]);
                totals.EarlyMinutes += ParseInt(row[9]);
                totals.MissingPunch += ParseInt(row[10]);
                totals.LeaveDays += ParseInt(row[11]);
                totals.WorkedMinutes += ParseHm(row[12]);
                totals.OvertimeMinutes += ParseHm(row[13]);
            }

            ReportData report = new ReportData();
            report.Title = "تقرير ملخص القسم الشهري";
            report.PeriodText = "الشهر: " + year + "-" + month.ToString("00");
            report.AddColumn("DepartmentName", "القسم", 150);
            report.AddColumn("PresentDays", "أيام الحضور", 75);
            report.AddColumn("AbsentDays", "الغياب", 60);
            report.AddColumn("LateCount", "مرات التأخير", 75);
            report.AddColumn("LateMinutes", "دقائق التأخير", 75);
            report.AddColumn("EarlyCount", "مرات المبكر", 75);
            report.AddColumn("EarlyMinutes", "دقائق المبكر", 75);
            report.AddColumn("MissingPunch", "بصمات ناقصة", 70);
            report.AddColumn("LeaveDays", "أيام إجازة", 70);
            report.AddColumn("WorkedH", "ساعات العمل", 75);
            report.AddColumn("OvertimeH", "ساعات إضافية", 75);

            long totalWorked = 0;
            long totalOvertime = 0;
            foreach (string department in byDepartment.Keys)
            {
                DailyTotals totals = byDepartment[department];
                report.AddRow(department, totals.PresentDays.ToString(), totals.AbsentDays.ToString(),
                    totals.LateCount.ToString(), totals.LateMinutes.ToString(),
                    totals.EarlyCount.ToString(), totals.EarlyMinutes.ToString(),
                    totals.MissingPunch.ToString(), totals.LeaveDays.ToString(),
                    OvertimeBllFormat(totals.WorkedMinutes), OvertimeBllFormat(totals.OvertimeMinutes));
                totalWorked += totals.WorkedMinutes;
                totalOvertime += totals.OvertimeMinutes;
            }
            report.TotalsRow = TotalRow(report, byDepartment.Count, totalWorked, totalOvertime,
                "مجموع ساعات العمل (الإضافي):",
                OvertimeBllFormat((int)totalWorked) + " (" + OvertimeBllFormat((int)totalOvertime) + ")");
            return report;
        }

        // ---------------- leaves / missions / penalties / overtime ----------------

        /// <summary>تقرير الإجازات (rule 19).</summary>
        public static ReportData Leaves(DateTime fromDate, DateTime toDate, string status, int? employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<LeaveRequestView> rows = LeaveDAL.SearchRequests(EmptyToNull(status), employeeId,
                fromDate, toDate, MaxRows);
            ReportData report = new ReportData();
            report.Title = "تقرير الإجازات";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("TypeName", "نوع الإجازة", 100);
            report.AddColumn("StartDate", "من", 85);
            report.AddColumn("EndDate", "إلى", 85);
            report.AddColumn("DaysCount", "الأيام", 55);
            report.AddColumn("Status", "الحالة", 70);
            report.AddColumn("ApprovedBy", "المُعتمِد", 70);
            report.AddColumn("Reason", "السبب", 150);

            long totalDays = 0;
            foreach (LeaveRequestView row in rows)
            {
                report.AddRow(row.EmployeeCode, row.EmployeeName, row.TypeName,
                    row.StartDate == null ? "" : row.StartDate.Value.ToString("yyyy-MM-dd"),
                    row.EndDate == null ? "" : row.EndDate.Value.ToString("yyyy-MM-dd"),
                    row.DaysCount == null ? "" : row.DaysCount.Value.ToString(),
                    LeaveStatusAr(row.Status), row.ApprovedBy == null ? "" : row.ApprovedBy.ToString(),
                    row.Reason);
                totalDays += row.DaysCount ?? 0;
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0,
                "مجموع الأيام:", totalDays.ToString());
            return report;
        }

        /// <summary>تقرير المأموريات (rule 19).</summary>
        public static ReportData Missions(DateTime fromDate, DateTime toDate, string status, int? employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<MissionView> rows = MissionDAL.Search(EmptyToNull(status), employeeId, fromDate, toDate, MaxRows);
            ReportData report = new ReportData();
            report.Title = "تقرير المأموريات";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("Start", "مِن", 110);
            report.AddColumn("End", "إلى", 110);
            report.AddColumn("Location", "المكان", 120);
            report.AddColumn("Purpose", "السبب", 150);
            report.AddColumn("IsExemptAtt", "إعفاء حضور", 70);
            report.AddColumn("Status", "الحالة", 70);

            foreach (MissionView row in rows)
            {
                report.AddRow(row.EmployeeCode, row.EmployeeName,
                    (row.StartDate == null ? "" : row.StartDate.Value.ToString("yyyy-MM-dd")) +
                        (string.IsNullOrEmpty(row.StartTime) ? "" : " " + row.StartTime),
                    (row.EndDate == null ? "" : row.EndDate.Value.ToString("yyyy-MM-dd")) +
                        (string.IsNullOrEmpty(row.EndTime) ? "" : " " + row.EndTime),
                    row.Location, row.Purpose,
                    row.IsExemptAtt == true ? "نعم" : "لا",
                    LeaveStatusAr(row.Status));
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد المأموريات:", rows.Count.ToString());
            return report;
        }

        /// <summary>تقرير الجزاءات (rule 19).</summary>
        public static ReportData Penalties(DateTime fromDate, DateTime toDate, string typeCode, int? employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<PenaltyView> rows = PenaltyDAL.Search(employeeId, fromDate, toDate, EmptyToNull(typeCode), MaxRows);
            ReportData report = new ReportData();
            report.Title = "تقرير الجزاءات";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("AttDate", "تاريخ المخالفة", 85);
            report.AddColumn("PenaltyDate", "تاريخ الجزاء", 85);
            report.AddColumn("TypeName", "النوع", 70);
            report.AddColumn("Amount", "القيمة", 75);
            report.AddColumn("Reason", "السبب", 160);
            report.AddColumn("IssuedBy", "سجّله", 60);

            decimal totalAmount = 0;
            foreach (PenaltyView row in rows)
            {
                report.AddRow(row.EmployeeCode, row.EmployeeName,
                    row.AttDate == null ? "" : row.AttDate.Value.ToString("yyyy-MM-dd"),
                    row.PenaltyDate == null ? "" : row.PenaltyDate.Value.ToString("yyyy-MM-dd"),
                    OvertimeTypeName(row.TypeCode),
                    row.Amount == null ? "-" : row.Amount.Value.ToString("N2"),
                    row.Reason,
                    row.IssuedBy == null ? "" : row.IssuedBy.ToString());
                totalAmount += row.Amount ?? 0;
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0,
                "إجمالي القيم:", totalAmount.ToString("N2"));
            return report;
        }

        /// <summary>تقرير الساعات الإضافية (rule 19).</summary>
        public static ReportData Overtime(DateTime fromDate, DateTime toDate, string status, int? employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<OvertimeView> rows = OvertimeDAL.Search(fromDate, toDate, employeeId, EmptyToNull(status), MaxRows);
            ReportData report = new ReportData();
            report.Title = "تقرير الساعات الإضافية";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("OtDate", "التاريخ", 85);
            report.AddColumn("ActualMinutes", "الفعلي (د)", 75);
            report.AddColumn("EffectiveMinutes", "المحتساب (د)", 80);
            report.AddColumn("EffectiveH", "المحتساب H:mm", 75);
            report.AddColumn("Rate", "المعامل", 60);
            report.AddColumn("Source", "المصدر", 55);
            report.AddColumn("Status", "الحالة", 70);

            long totalEffective = 0;
            foreach (OvertimeView row in rows)
            {
                report.AddRow(row.EmployeeCode, row.EmployeeName,
                    row.OtDate == null ? "" : row.OtDate.Value.ToString("yyyy-MM-dd"),
                    row.ActualMinutes == null ? "" : row.ActualMinutes.Value.ToString(),
                    row.EffectiveMinutes == null ? "" : row.EffectiveMinutes.Value.ToString(),
                    HR.Services.ReportEngine.FormatMinutes(row.EffectiveMinutes),
                    row.Rate == null ? "" : row.Rate.Value.ToString("0.0#"),
                    row.Source, OvertimeStatusAr(row.Status));
                totalEffective += row.EffectiveMinutes ?? 0;
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, totalEffective,
                "مجموع المحتساب:", FormatMinutes((int)totalEffective));
            return report;
        }

        // ---------------- fingerprint / employees ----------------

        /// <summary>تقرير البصمة الخام (rule 19).</summary>
        public static ReportData RawFingerprint(DateTime fromTime, DateTime toTime,
            int? deviceId, int? employeeId)
        {
            NormalizeRange(ref fromTime, ref toTime);
            List<FingerprintLogView> rows = FingerprintDAL.SearchLogs(
                deviceId == 0 ? (int?)null : deviceId, employeeId, fromTime, toTime, MaxRows);
            ReportData report = new ReportData();
            report.Title = "تقرير البصمة الخام";
            report.PeriodText = "الفترة: " + fromTime.ToString("yyyy-MM-dd") + " → " + toTime.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("DeviceName", "الجهاز", 110);
            report.AddColumn("LogTime", "وقت البصمة", 130);
            report.AddColumn("Direction", "الاتجاه", 70);
            report.AddColumn("EmpDevCode", "كود الجهاز", 70);

            foreach (FingerprintLogView row in rows)
            {
                report.AddRow(row.EmployeeCode, row.EmployeeName, row.DeviceName,
                    row.LogTime == null ? "" : row.LogTime.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                    row.InOutFlag == 1 ? "خروج" : (row.InOutFlag == 0 ? "دخول" : ""),
                    row.EmpDevCode);
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد السجلات:", rows.Count.ToString());
            return report;
        }

        /// <summary>تقرير الموظفين (rule 19).</summary>
        public static ReportData EmployeeDirectory(bool includeArchived)
        {
            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.IncludeArchived = includeArchived;
            filter.PageSize = MaxRows;
            List<Employee> rows = EmployeeDAL.Search(filter);
            Dictionary<int, string> departments = LookupNames(LookupTable.Departments);
            Dictionary<int, string> sections = LookupNames(LookupTable.Sections);
            Dictionary<int, string> jobTitles = LookupNames(LookupTable.JobTitles);
            ReportData report = new ReportData();
            report.Title = "تقرير الموظفين";
            report.PeriodText = "عدد الموظفين: " + rows.Count;
            report.AddColumn("EmployeeCode", "الكود", 70);
            report.AddColumn("FullNameAr", "الاسم بالعربية", 160);
            report.AddColumn("FullNameEn", "الاسم بالإنجليزية", 160);
            report.AddColumn("Gender", "الجنس", 55);
            report.AddColumn("Department", "القسم", 110);
            report.AddColumn("Section", "القطاع", 110);
            report.AddColumn("JobTitle", "المسمى الوظيفي", 110);
            report.AddColumn("HireDate", "تاريخ التعيين", 85);
            report.AddColumn("Phone", "الهاتف", 90);
            report.AddColumn("Status", "الحالة", 65);

            foreach (Employee row in rows)
            {
                report.AddRow(row.EmployeeCode, row.FullNameAr, row.FullNameEn, row.Gender,
                    LookupValue(departments, row.DepartmentId),
                    LookupValue(sections, row.SectionId),
                    LookupValue(jobTitles, row.JobTitleId),
                    row.HireDate == null ? "" : row.HireDate.Value.ToString("yyyy-MM-dd"),
                    row.Phone,
                    string.Equals(row.Status, "Active", StringComparison.Ordinal) ? "نشط" : "موقوف");
            }
            return report;
        }

        // ---------------- helpers ----------------

        /// <summary>Aggregation accumulator for summary reports.</summary>
        private class DailyTotals
        {
            public int ScheduledDays;
            public int PresentDays;
            public int AbsentDays;
            public int LateCount;
            public int LateMinutes;
            public int EarlyCount;
            public int EarlyMinutes;
            public int MissingPunch;
            public int LeaveDays;
            public long WorkedMinutes;
            public long OvertimeMinutes;
        }

        private static DailyTotals Summarize(List<AttendanceView> rows)
        {
            DailyTotals totals = new DailyTotals();
            foreach (AttendanceView row in rows)
            {
                string status = row.Status ?? "";
                if (status != "WeeklyRest" && status != "Holiday")
                {
                    totals.ScheduledDays++;
                }
                if (status == "Present" || status == "Late" || status == "EarlyLeave" || status == "Mission")
                {
                    totals.PresentDays++;
                }
                else if (status == "Absent")
                {
                    totals.AbsentDays++;
                }
                else if (status == "Leave")
                {
                    totals.LeaveDays++;
                }
                else if (status == "MissingPunch")
                {
                    totals.MissingPunch++;
                }
                if ((row.LateMinutes ?? 0) > 0)
                {
                    totals.LateCount++;
                    totals.LateMinutes += row.LateMinutes.Value;
                }
                if ((row.EarlyLeaveMinutes ?? 0) > 0)
                {
                    totals.EarlyCount++;
                    totals.EarlyMinutes += row.EarlyLeaveMinutes.Value;
                }
                totals.WorkedMinutes += row.MinutesWorked ?? 0;
                totals.OvertimeMinutes += row.OvertimeMin ?? 0;
            }
            return totals;
        }

        /// <summary>Builds the shared totals row aligned with the columns list.</summary>
        private static List<string> TotalRow(ReportData report, int recordCount,
            long workedMinutes, long overtimeMinutes, string label, string value)
        {
            List<string> totals = new List<string>();
            for (int i = 0; i < report.Columns.Count; i++)
            {
                totals.Add(string.Empty);
            }
            totals[0] = "الإجماليات — عدد السجلات: " + recordCount;
            int labelIndex = report.Columns.Count >= 5 ? report.Columns.Count - 2 : 1;
            totals[labelIndex] = label;
            totals[report.Columns.Count - 1] = value;
            return totals;
        }

        private static int IndexOfColumn(ReportData report, string name)
        {
            for (int i = 0; i < report.Columns.Count; i++)
            {
                if (report.Columns[i].Name == name)
                {
                    return i;
                }
            }
            return 0;
        }

        private static void NormalizeRange(ref DateTime fromDate, ref DateTime toDate)
        {
            if (fromDate > toDate)
            {
                DateTime swap = fromDate;
                fromDate = toDate;
                toDate = swap;
            }
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static string FormatMinuteValue(int? minutes)
        {
            if (minutes == null || minutes.Value <= 0)
            {
                return "";
            }
            return minutes.Value.ToString();
        }

        /// <summary>Formats minutes as H:mm (same style the UI uses).</summary>
        public static string FormatMinutes(int? minutes)
        {
            if (minutes == null || minutes.Value <= 0)
            {
                return "0:00";
            }
            return (minutes.Value / 60) + ":" + (minutes.Value % 60).ToString("00");
        }

        private static string OvertimeBllFormat(long? minutes)
        {
            if (minutes == null || minutes <= 0)
            {
                return "0:00";
            }
            return (minutes.Value / 60) + ":" + (minutes.Value % 60).ToString("00");
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : 0;
        }

        private static int ParseHm(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }
            int separator = value.IndexOf(':');
            if (separator < 0)
            {
                return ParseInt(value);
            }
            return ParseInt(value.Substring(0, separator)) * 60 + ParseInt(value.Substring(separator + 1));
        }

        private static Dictionary<int, string> LookupNames(LookupTable table)
        {
            Dictionary<int, string> names = new Dictionary<int, string>();
            foreach (LookupRow row in LookupDAL.GetLookupRows(table, true))
            {
                names[row.Id] = row.Name;
            }
            return names;
        }

        private static string LookupValue(Dictionary<int, string> names, int? id)
        {
            if (id == null || !names.ContainsKey(id.Value))
            {
                return "";
            }
            return names[id.Value];
        }

        /// <summary>Arabic label for attendance day statuses.</summary>
        public static string StatusAr(string status)
        {
            if (status == "Present") { return "حاضر"; }
            if (status == "Late") { return "متأخر"; }
            if (status == "EarlyLeave") { return "انصراف مبكر"; }
            if (status == "Absent") { return "غائب"; }
            if (status == "MissingPunch") { return "بصمة ناقصة"; }
            if (status == "Leave") { return "إجازة"; }
            if (status == "Mission") { return "مأمورية"; }
            if (status == "Holiday") { return "عطلة رسمية"; }
            if (status == "WeeklyRest") { return "راحة أسبوعية"; }
            return status;
        }

        /// <summary>Arabic label for request statuses (leaves + missions).</summary>
        public static string LeaveStatusAr(string status)
        {
            if (status == "Approved") { return "معتمد"; }
            if (status == "Rejected") { return "مرفوض"; }
            if (status == "Pending") { return "معلّق"; }
            return status;
        }

        /// <summary>Arabic label for overtime statuses.</summary>
        public static string OvertimeStatusAr(string status)
        {
            return LeaveStatusAr(status);
        }

        /// <summary>Arabic label for penalty type codes (rule 17 wording).</summary>
        public static string OvertimeTypeName(string typeCode)
        {
            if (typeCode == "Warning") { return "إنذار"; }
            if (typeCode == "Deduction") { return "خصم"; }
            if (typeCode == "Penalty") { return "جزاء"; }
            if (typeCode == "Absence") { return "غياب"; }
            if (typeCode == "Violation") { return "مخالفة"; }
            return typeCode ?? "";
        }
    }
}
