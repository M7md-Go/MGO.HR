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

        /// <summary>تحليلي: ملخص التأخير حسب القسم لشهر محدد (مرات، دقائق، متوسط، أكثر يوم تأخيرًا).</summary>
        public static ReportData LateByDepartment(int year, int month)
        {
            DateTime fromDate = new DateTime(year, month, 1);
            DateTime toDate = fromDate.AddMonths(1).AddDays(-1);
            List<AttendanceView> rows = AttendanceDAL.Browse(fromDate, toDate, null, null, MaxRows);

            List<string> deptOrder = new List<string>(); // insertion order keeps stable output
            Dictionary<string, System.Collections.Generic.HashSet<int>> deptEmployees =
                new Dictionary<string, System.Collections.Generic.HashSet<int>>(StringComparer.Ordinal);
            Dictionary<string, int> deptLateCount = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> deptLateMinutes = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, Dictionary<int, int>> deptWorstDay =
                new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal); // dept -> (day -> late minutes)

            foreach (AttendanceView row in rows)
            {
                if (row.DepartmentId == null)
                {
                    continue;
                }
                string dept = string.IsNullOrEmpty(row.DepartmentName) ? "بدون قسم" : row.DepartmentName;
                if (!deptEmployees.ContainsKey(dept))
                {
                    deptOrder.Add(dept);
                    deptEmployees[dept] = new System.Collections.Generic.HashSet<int>();
                    deptLateCount[dept] = 0;
                    deptLateMinutes[dept] = 0;
                    deptWorstDay[dept] = new Dictionary<int, int>();
                }
                if (row.EmployeeId != null)
                {
                    deptEmployees[dept].Add(row.EmployeeId.Value);
                }
                int lateMinutes = row.LateMinutes ?? 0;
                if (lateMinutes > 0 && row.AttDate.HasValue)
                {
                    deptLateCount[dept] += 1;
                    deptLateMinutes[dept] += lateMinutes;
                    int day = row.AttDate.Value.Day;
                    Dictionary<int, int> byDay = deptWorstDay[dept];
                    if (!byDay.ContainsKey(day))
                    {
                        byDay[day] = 0;
                    }
                    byDay[day] += lateMinutes;
                }
            }

            ReportData report = new ReportData();
            report.Title = "ملخص التأخير حسب القسم";
            report.PeriodText = "الشهر: " + year + "-" + month.ToString("00");
            report.AddColumn("DepartmentName", "القسم", 140);
            report.AddColumn("Employees", "عدد الموظفين", 80);
            report.AddColumn("LateCount", "مرات التأخير", 80);
            report.AddColumn("LateMinutes", "دقائق التأخير", 80);
            report.AddColumn("AvgMinutes", "متوسط الدقائق/مرة", 90);
            report.AddColumn("WorstDay", "أكثر يوم تأخيرًا", 90);

            int totalLateCount = 0;
            int totalLateMinutes = 0;
            foreach (string dept in deptOrder)
            {
                int count = deptLateCount[dept];
                int minutes = deptLateMinutes[dept];
                int worstDay = 0;
                int worstMinutes = 0;
                foreach (System.Collections.Generic.KeyValuePair<int, int> pair in deptWorstDay[dept])
                {
                    if (pair.Value > worstMinutes)
                    {
                        worstMinutes = pair.Value;
                        worstDay = pair.Key;
                    }
                }
                string average = count > 0 ? Convert.ToInt32(Math.Round((double)minutes / count, MidpointRounding.AwayFromZero)).ToString() : "0";
                report.AddRow(dept, deptEmployees[dept].Count.ToString(), count.ToString(),
                    minutes.ToString(), average, worstDay > 0 ? worstDay.ToString() : "—");
                totalLateCount += count;
                totalLateMinutes += minutes;
            }

            List<string> totals = new List<string>();
            for (int i = 0; i < report.Columns.Count; i++)
            {
                totals.Add(string.Empty);
            }
            totals[0] = "الإجمالي — عدد الأقسام: " + deptOrder.Count;
            totals[2] = totalLateCount.ToString();
            totals[3] = totalLateMinutes.ToString();
            report.TotalsRow = totals;
            return report;
        }

        /// <summary>تحليلي: المصفوفة الشهرية للحضور — صف لكل موظف وعمود لكل يوم (رموز يومية).</summary>
        public static ReportData AttendanceMatrixReport(int year, int month, int? departmentId)
        {
            DateTime fromDate = new DateTime(year, month, 1);
            DateTime toDate = fromDate.AddMonths(1).AddDays(-1);
            int daysInMonth = DateTime.DaysInMonth(year, month);
            List<AttendanceView> rows = AttendanceDAL.Browse(fromDate, toDate, null,
                departmentId == 0 ? (int?)null : departmentId, MaxRows);

            List<int> employeeOrder = new List<int>(); // first-appearance order by code groups
            Dictionary<int, Dictionary<int, string>> cells = new Dictionary<int, Dictionary<int, string>>();
            Dictionary<int, string> names = new Dictionary<int, string>();
            Dictionary<int, string> codes = new Dictionary<int, string>();
            Dictionary<int, string> departments = new Dictionary<int, string>();
            foreach (AttendanceView row in rows)
            {
                if (row.EmployeeId == null || !row.AttDate.HasValue)
                {
                    continue;
                }
                int empId = row.EmployeeId.Value;
                if (!cells.ContainsKey(empId))
                {
                    employeeOrder.Add(empId);
                    cells[empId] = new Dictionary<int, string>();
                    names[empId] = row.EmployeeName ?? "";
                    codes[empId] = row.EmployeeCode ?? "";
                    departments[empId] = string.IsNullOrEmpty(row.DepartmentName) ? "بدون قسم" : row.DepartmentName;
                }
                cells[empId][row.AttDate.Value.Day] = StatusChar(row.Status);
            }

            ReportData report = new ReportData();
            report.Title = "المصفوفة الشهرية للحضور";
            report.PeriodText = "الشهر: " + year + "-" + month.ToString("00");
            report.AddColumn("EmployeeCode", "الكود", 70);
            report.AddColumn("EmployeeName", "الموظف", 150);
            report.AddColumn("DepartmentName", "القسم", 110);
            for (int day = 1; day <= daysInMonth; day++)
            {
                report.AddColumn("D" + day, day.ToString(), 22);
            }
            report.AddColumn("PresentDays", "حضور", 50);
            report.AddColumn("LateCount", "تأخير", 50);
            report.AddColumn("AbsentDays", "غياب", 50);

            employeeOrder.Sort(delegate(int a, int b)
            {
                return string.CompareOrdinal(codes[a], codes[b]);
            });
            foreach (int empId in employeeOrder)
            {
                List<string> line = new List<string>();
                line.Add(codes[empId]);
                line.Add(names[empId]);
                line.Add(departments[empId]);
                int present = 0;
                int late = 0;
                int absent = 0;
                for (int day = 1; day <= daysInMonth; day++)
                {
                    string symbol;
                    if (!cells[empId].TryGetValue(day, out symbol))
                    {
                        symbol = "";
                    }
                    if (symbol == "ح" || symbol == "ت" || symbol == "م" || symbol == "أم")
                    {
                        present++;
                    }
                    else if (symbol == "غ")
                    {
                        absent++;
                    }
                    if (symbol == "ت")
                    {
                        late++;
                    }
                    line.Add(symbol);
                }
                line.Add(present.ToString());
                line.Add(late.ToString());
                line.Add(absent.ToString());
                report.Rows.Add(line);
            }
            return report;
        }

        /// <summary>Single-character Arabic symbol of a daily attendance status (matrix cells).</summary>
        private static string StatusChar(string status)
        {
            if (status == "Present") { return "ح"; }
            if (status == "Late") { return "ت"; }
            if (status == "EarlyLeave") { return "م"; }
            if (status == "Mission") { return "أم"; }
            if (status == "Absent") { return "غ"; }
            if (status == "Leave") { return "إج"; }
            if (status == "MissingPunch") { return "ن"; }
            if (status == "WeeklyRest") { return "ر"; }
            if (status == "Holiday") { return "ع"; }
            return status ?? "";
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

            decimal totalDays = 0m;
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

        // ---------------- expanded reports (v1.3.2) ----------------

        /// <summary>أرصدة الإجازات — سجل الأرصدة لسنة رصيد عبر كل الموظفين (تصفية اختيارية بموظف).</summary>
        public static ReportData LeaveBalances(int balanceYear, int? employeeId)
        {
            List<LeaveBalanceLedgerView> rows = LeaveDAL.BrowseBalances(balanceYear, null);
            ReportData report = new ReportData();
            report.Title = "أرصدة الإجازات";
            report.PeriodText = "سنة الرصيد: " + balanceYear;
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("TypeName", "نوع الإجازة", 130);
            report.AddColumn("YearVal", "السنة", 55);
            report.AddColumn("QuotaDays", "الرصيد", 60);
            report.AddColumn("UsedDays", "المستهلك", 60);
            report.AddColumn("Remaining", "المتبقي", 60);

            decimal totalQuota = 0m;
            decimal totalUsed = 0m;
            decimal totalRemaining = 0m;
            int visible = 0;
            foreach (LeaveBalanceLedgerView row in rows)
            {
                if (employeeId != null && (row.EmployeeId == null || row.EmployeeId.Value != employeeId.Value))
                {
                    continue;
                }
                report.AddRow(row.EmployeeCode, row.EmployeeName,
                    row.TypeName ?? "",
                    row.YearVal == null ? "" : row.YearVal.Value.ToString(),
                    (row.QuotaDays ?? 0).ToString(), (row.UsedDays ?? 0).ToString(),
                    (row.Remaining ?? 0).ToString());
                totalQuota += row.QuotaDays ?? 0m;
                totalUsed += row.UsedDays ?? 0m;
                totalRemaining += row.Remaining ?? 0m;
                visible++;
            }
            List<string> totals = EmptyTotals(report);
            totals[0] = "الإجماليات — عدد السجلات: " + visible;
            totals[4] = totalQuota.ToString();
            totals[5] = totalUsed.ToString();
            totals[6] = totalRemaining.ToString();
            report.TotalsRow = totals;
            return report;
        }

        /// <summary>ملخص الإجازات حسب النوع — تجميع الطلبات المعتمدة داخل نطاق زمني (نافذة السنة المالية).</summary>
        public static ReportData LeaveSummaryByType(DateTime fromDate, DateTime toDate)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<LeaveRequestView> rows = LeaveDAL.SearchRequests("Approved", null, fromDate, toDate, MaxRows);
            List<string> typeOrder = new List<string>();
            Dictionary<string, int> typeCount = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, decimal> typeDays = new Dictionary<string, decimal>(StringComparer.Ordinal);
            Dictionary<string, HashSet<int>> typeEmployees =
                new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            foreach (LeaveRequestView row in rows)
            {
                string type = string.IsNullOrEmpty(row.TypeName) ? "غير محدد" : row.TypeName;
                if (!typeCount.ContainsKey(type))
                {
                    typeOrder.Add(type);
                    typeCount[type] = 0;
                    typeDays[type] = 0m;
                    typeEmployees[type] = new HashSet<int>();
                }
                typeCount[type] += 1;
                typeDays[type] += row.DaysCount ?? 0;
                if (row.EmployeeId != null)
                {
                    typeEmployees[type].Add(row.EmployeeId.Value);
                }
            }

            ReportData report = new ReportData();
            report.Title = "ملخص الإجازات حسب النوع";
            report.PeriodText = "السنة المالية: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("TypeName", "نوع الإجازة", 150);
            report.AddColumn("Requests", "عدد الطلبات", 80);
            report.AddColumn("Days", "إجمالي الأيام", 80);
            report.AddColumn("Employees", "عدد الموظفين", 80);

            long totalRequests = 0;
            decimal totalDays = 0m;
            foreach (string type in typeOrder)
            {
                report.AddRow(type, typeCount[type].ToString(), typeDays[type].ToString(),
                    typeEmployees[type].Count.ToString());
                totalRequests += typeCount[type];
                totalDays += typeDays[type];
            }
            List<string> totals = EmptyTotals(report);
            totals[0] = "الإجماليات — عدد الأنواع: " + typeOrder.Count;
            totals[1] = totalRequests.ToString();
            totals[2] = totalDays.ToString();
            report.TotalsRow = totals;
            return report;
        }

        /// <summary>ملخص الساعات الإضافية لكل موظف لشهر محدد.</summary>
        public static ReportData OvertimeSummary(int year, int month)
        {
            DateTime fromDate = new DateTime(year, month, 1);
            DateTime toDate = fromDate.AddMonths(1).AddDays(-1);
            List<OvertimeView> rows = OvertimeDAL.Search(fromDate, toDate, null, null, MaxRows);
            List<int> employeeOrder = new List<int>();
            Dictionary<int, string> codes = new Dictionary<int, string>();
            Dictionary<int, string> names = new Dictionary<int, string>();
            Dictionary<int, int> employeeCount = new Dictionary<int, int>();
            Dictionary<int, long> employeeActual = new Dictionary<int, long>();
            Dictionary<int, long> employeeEffective = new Dictionary<int, long>();
            foreach (OvertimeView row in rows)
            {
                if (row.EmployeeId == null)
                {
                    continue;
                }
                int empId = row.EmployeeId.Value;
                if (!employeeCount.ContainsKey(empId))
                {
                    employeeOrder.Add(empId);
                    codes[empId] = row.EmployeeCode ?? "";
                    names[empId] = row.EmployeeName ?? "";
                    employeeCount[empId] = 0;
                    employeeActual[empId] = 0;
                    employeeEffective[empId] = 0;
                }
                employeeCount[empId] += 1;
                employeeActual[empId] += row.ActualMinutes ?? 0;
                employeeEffective[empId] += row.EffectiveMinutes ?? 0;
            }

            ReportData report = new ReportData();
            report.Title = "ملخص الساعات الإضافية لكل موظف";
            report.PeriodText = "الشهر: " + year + "-" + month.ToString("00");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("Count", "عدد المرّات", 70);
            report.AddColumn("Actual", "فعلي (د)", 70);
            report.AddColumn("Effective", "محتسب (د)", 70);
            report.AddColumn("EffectiveH", "محتسب H:mm", 80);

            long totalCount = 0;
            long totalActual = 0;
            long totalEffective = 0;
            foreach (int empId in employeeOrder)
            {
                report.AddRow(codes[empId], names[empId], employeeCount[empId].ToString(),
                    employeeActual[empId].ToString(), employeeEffective[empId].ToString(),
                    FormatMinutes((int)employeeEffective[empId]));
                totalCount += employeeCount[empId];
                totalActual += employeeActual[empId];
                totalEffective += employeeEffective[empId];
            }
            List<string> totals = EmptyTotals(report);
            totals[0] = "الإجماليات — عدد الموظفين: " + employeeOrder.Count;
            totals[2] = totalCount.ToString();
            totals[3] = totalActual.ToString();
            totals[4] = totalEffective.ToString();
            totals[5] = FormatMinutes((int)totalEffective);
            report.TotalsRow = totals;
            return report;
        }

        /// <summary>تفاصيل الغياب — سجلات الحضور بحالة "غائب" داخل نطاق زمني مع فلاتر القسم والموظف.</summary>
        public static ReportData Absences(DateTime fromDate, DateTime toDate,
            int? departmentId, int? employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<AttendanceView> rows = AttendanceDAL.Browse(fromDate, toDate, employeeId,
                departmentId == 0 ? (int?)null : departmentId, MaxRows);
            ReportData report = new ReportData();
            report.Title = "تفاصيل الغياب";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("DepartmentName", "القسم", 120);
            report.AddColumn("AttDate", "التاريخ", 85);
            report.AddColumn("ShiftCode", "الوردية", 60);
            report.AddColumn("Status", "الحالة", 80);

            foreach (AttendanceView row in rows)
            {
                if (!string.Equals(row.Status, "Absent", StringComparison.Ordinal))
                {
                    continue;
                }
                report.AddRow(row.EmployeeCode, row.EmployeeName, row.DepartmentName,
                    row.AttDate == null ? "" : row.AttDate.Value.ToString("yyyy-MM-dd"),
                    row.ShiftCode, StatusAr(row.Status));
            }
            report.TotalsRow = TotalRow(report, report.Rows.Count, 0, 0, "أيام الغياب:", report.Rows.Count.ToString());
            return report;
        }

        /// <summary>استثناءات الحضور — مخرجات محرك الحضور والتصحيحات اليدوية داخل نطاق زمني.</summary>
        public static ReportData AttendanceExceptions(DateTime fromDate, DateTime toDate, int? employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<AttendanceExceptionView> rows = AttendanceExceptionDAL.Browse(employeeId, fromDate, toDate, null, MaxRows);
            ReportData report = new ReportData();
            report.Title = "استثناءات الحضور";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("AttDate", "التاريخ", 85);
            report.AddColumn("ExcType", "نوع الاستثناء", 110);
            report.AddColumn("ExcValue", "القيمة (د)", 70);
            report.AddColumn("Source", "المصدر", 70);

            foreach (AttendanceExceptionView row in rows)
            {
                report.AddRow(row.EmployeeCode, row.EmployeeName,
                    row.AttDate == null ? "" : row.AttDate.Value.ToString("yyyy-MM-dd"),
                    ExceptionTypeAr(row.ExcType),
                    row.ExcValue == null ? "" : row.ExcValue.Value.ToString(),
                    row.IsManual ? "يدوي" : "المحرك");
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد الاستثناءات:", rows.Count.ToString());
            return report;
        }

        /// <summary>تصحيحات الحضور اليدوية داخل نطاق زمني مع فلتر الحالة.</summary>
        public static ReportData Adjustments(DateTime fromDate, DateTime toDate, string status, int? employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<AttendanceAdjustmentView> rows = AttendanceAdjustmentDAL.Browse(
                EmptyToNull(status), employeeId, fromDate, toDate, MaxRows);
            ReportData report = new ReportData();
            report.Title = "تصحيحات الحضور اليدوية";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 140);
            report.AddColumn("AttDate", "تاريخ الحضور", 85);
            report.AddColumn("AdjustmentType", "نوع التصحيح", 90);
            report.AddColumn("NewInTime", "القادم الجديد", 70);
            report.AddColumn("NewOutTime", "المنصرف الجديد", 70);
            report.AddColumn("NewStatus", "الحالة الجديدة", 80);
            report.AddColumn("Reason", "السبب", 140);
            report.AddColumn("Status", "حالة الطلب", 70);

            foreach (AttendanceAdjustmentView row in rows)
            {
                report.AddRow(row.EmployeeCode, row.EmployeeName,
                    row.AttDate == null ? "" : row.AttDate.Value.ToString("yyyy-MM-dd"),
                    string.Equals(row.AdjustmentType, "Time", StringComparison.Ordinal) ? "تعديل أوقات البصمات" : "تغيير الحالة",
                    row.NewInTime ?? "", row.NewOutTime ?? "",
                    StatusAr(row.NewStatus), row.Reason, AdjustmentStatusAr(row.Status));
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد التصحيحات:", rows.Count.ToString());
            return report;
        }

        /// <summary>العطل الرسمية لسنة التقويم المختارة (تشمل المتكررة سنويًا).</summary>
        public static ReportData PublicHolidays(int year)
        {
            DateTime fromDate = new DateTime(year, 1, 1);
            DateTime toDate = new DateTime(year, 12, 31);
            List<PublicHoliday> rows = PublicHolidayDAL.List(fromDate, toDate);
            ReportData report = new ReportData();
            report.Title = "العطل الرسمية";
            report.PeriodText = "سنة: " + year;
            report.AddColumn("HolidayDate", "التاريخ", 110);
            report.AddColumn("HolidayName", "اسم العطلة", 180);
            report.AddColumn("DayName", "اليوم", 80);
            report.AddColumn("IsRecurring", "متكررة سنويًا", 80);

            foreach (PublicHoliday row in rows)
            {
                report.AddRow(row.IsRecurring == true
                        ? "كل سنة — " + (row.HolidayDate == null ? "" : row.HolidayDate.Value.ToString("MM-dd"))
                        : (row.HolidayDate == null ? "" : row.HolidayDate.Value.ToString("yyyy-MM-dd")),
                    row.HolidayName,
                    DayNameAr(row.HolidayDate ?? fromDate),
                    row.IsRecurring == true ? "نعم" : "لا");
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد العطل:", rows.Count.ToString());
            return report;
        }

        /// <summary>توزيع الورديات في الجدول الشهري لشهر محدد — ملخص لكل موظف.</summary>
        public static ReportData SchedulesReport(int year, int month)
        {
            MonthlySchedule schedule = ScheduleDAL.GetSchedule(year, month);
            if (schedule == null)
            {
                ReportData empty = new ReportData();
                empty.Title = "توزيع الورديات الشهري";
                empty.PeriodText = "الشهر: " + year + "-" + month.ToString("00") + " — لا يوجد جدول محفوظ لهذا الشهر";
                empty.AddColumn("EmployeeCode", "كود الموظف", 70);
                return empty;
            }
            List<Employee> employees = EmployeeDAL.Search(BuildWideFilter());
            Dictionary<int, string> employeeNames = new Dictionary<int, string>();
            Dictionary<int, string> employeeCodes = new Dictionary<int, string>();
            foreach (Employee employee in employees)
            {
                employeeNames[employee.EmployeeId] = employee.FullNameAr;
                employeeCodes[employee.EmployeeId] = employee.EmployeeCode;
            }
            Dictionary<int, string> shiftNames = new Dictionary<int, string>();
            foreach (Shift shift in ShiftDAL.GetAll(false))
            {
                shiftNames[shift.ShiftId] = string.IsNullOrEmpty(shift.ShiftCode) ? shift.ShiftName : shift.ShiftCode;
            }
            List<ScheduleDetail> details = ScheduleDAL.GetDetails(schedule.ScheduleId);

            List<int> employeeOrder = new List<int>();
            Dictionary<int, Dictionary<string, int>> assignment =
                new Dictionary<int, Dictionary<string, int>>();
            foreach (ScheduleDetail detail in details)
            {
                if (detail.EmployeeId == null)
                {
                    continue;
                }
                int empId = detail.EmployeeId.Value;
                if (!assignment.ContainsKey(empId))
                {
                    employeeOrder.Add(empId);
                    assignment[empId] = new Dictionary<string, int>(StringComparer.Ordinal);
                }
                string shiftKey = detail.ShiftId == null ? "راحة"
                    : (shiftNames.ContainsKey(detail.ShiftId.Value) ? shiftNames[detail.ShiftId.Value] : "وردية " + detail.ShiftId.Value);
                if (!assignment[empId].ContainsKey(shiftKey))
                {
                    assignment[empId][shiftKey] = 0;
                }
                assignment[empId][shiftKey] += 1;
            }
            employeeOrder.Sort(delegate(int a, int b)
            {
                return string.CompareOrdinal(
                    employeeCodes.ContainsKey(a) ? employeeCodes[a] : "",
                    employeeCodes.ContainsKey(b) ? employeeCodes[b] : "");
            });

            ReportData report = new ReportData();
            report.Title = "توزيع الورديات الشهري";
            report.PeriodText = "الشهر: " + year + "-" + month.ToString("00") +
                " — حالة الجدول: " + ScheduleStatusAr(schedule.Status);
            report.AddColumn("EmployeeCode", "كود الموظف", 70);
            report.AddColumn("EmployeeName", "اسم الموظف", 150);
            report.AddColumn("Breakdown", "توزيع الورديات", 260);
            report.AddColumn("AssignedDays", "أيام موزّعة", 70);

            long totalAssigned = 0;
            foreach (int empId in employeeOrder)
            {
                List<string> parts = new List<string>();
                int assignedDays = 0;
                foreach (string shiftKey in assignment[empId].Keys)
                {
                    parts.Add(shiftKey + ": " + assignment[empId][shiftKey]);
                    assignedDays += assignment[empId][shiftKey];
                }
                string breakdown = string.Join("، ", parts.ToArray());
                report.AddRow(employeeCodes.ContainsKey(empId) ? employeeCodes[empId] : "",
                    employeeNames.ContainsKey(empId) ? employeeNames[empId] : "", breakdown, assignedDays.ToString());
                totalAssigned += assignedDays;
            }
            List<string> totals = EmptyTotals(report);
            totals[0] = "الإجماليات — عدد الموظفين: " + employeeOrder.Count;
            totals[3] = totalAssigned.ToString();
            report.TotalsRow = totals;
            return report;
        }

        /// <summary>جرد أجهزة البصمة: الحالة، آخر مزامنة، آخر استجابة.</summary>
        public static ReportData DeviceInventory()
        {
            List<Device> rows = DeviceDAL.GetAll(true);
            ReportData report = new ReportData();
            report.Title = "جرد أجهزة البصمة";
            report.PeriodText = "عدد الأجهزة: " + rows.Count;
            report.AddColumn("DeviceName", "الجهاز", 130);
            report.AddColumn("IpAddress", "عنوان IP", 90);
            report.AddColumn("Port", "المنفذ", 55);
            report.AddColumn("SerialNumber", "الرقم التسلسلي", 110);
            report.AddColumn("Location", "الموقع", 110);
            report.AddColumn("Active", "نشط", 55);
            report.AddColumn("LastSync", "آخر مزامنة", 130);
            report.AddColumn("LastStatus", "آخر حالة", 90);

            foreach (Device row in rows)
            {
                report.AddRow(row.DeviceName, row.IpAddress,
                    row.Port == null ? "" : row.Port.Value.ToString(),
                    row.SerialNumber, row.Location,
                    row.IsActive == true ? "نعم" : "لا",
                    row.LastSync == null ? "" : row.LastSync.Value.ToString("yyyy-MM-dd HH:mm"),
                    row.LastStatus ?? "");
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد الأجهزة:", rows.Count.ToString());
            return report;
        }

        /// <summary>المستخدمون والأدوار — قائمة إدارِية (بدون أي كلمات مرور).</summary>
        public static ReportData UsersReport()
        {
            List<UserView> rows = UserDAL.ListUsers();
            ReportData report = new ReportData();
            report.Title = "المستخدمون والأدوار";
            report.PeriodText = "عدد المستخدمين: " + rows.Count;
            report.AddColumn("Username", "اسم المستخدم", 100);
            report.AddColumn("FullName", "الاسم الكامل", 150);
            report.AddColumn("RoleName", "الدور", 110);
            report.AddColumn("DeptName", "القسم", 110);
            report.AddColumn("Disabled", "معطّل", 55);
            report.AddColumn("Locked", "مقفول", 55);
            report.AddColumn("FailedLogins", "محاولات فاشلة", 80);
            report.AddColumn("LastLogin", "آخر دخول", 130);

            foreach (UserView row in rows)
            {
                report.AddRow(row.Username, row.FullName, row.RoleName, row.DeptName,
                    row.IsDisabled == true ? "نعم" : "لا",
                    row.IsLocked == true ? "نعم" : "لا",
                    row.FailedLogins == null ? "" : row.FailedLogins.Value.ToString(),
                    row.LastLogin == null ? "" : row.LastLogin.Value.ToString("yyyy-MM-dd HH:mm"));
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد المستخدمين:", rows.Count.ToString());
            return report;
        }

        /// <summary>سجل محاولات الدخول — آخر 500 محاولة (ناجحة وفاشلة).</summary>
        public static ReportData LoginHistoryReport(int maxRows)
        {
            List<LoginHistory> rows = UserDAL.SearchLoginHistory(null, maxRows);
            ReportData report = new ReportData();
            report.Title = "سجل محاولات الدخول";
            report.PeriodText = "آخر " + maxRows + " محاولة";
            report.AddColumn("AttemptTime", "الوقت", 130);
            report.AddColumn("UserId", "معرّف المستخدم", 80);
            report.AddColumn("Success", "النتيجة", 70);
            report.AddColumn("MachineName", "الجهاز", 110);
            report.AddColumn("IpAddress", "عنوان IP", 90);

            long failed = 0;
            foreach (LoginHistory row in rows)
            {
                bool success = row.Success == true;
                if (!success)
                {
                    failed++;
                }
                report.AddRow(row.AttemptTime == null ? "" : row.AttemptTime.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                    row.UserId == null ? "" : row.UserId.Value.ToString(),
                    success ? "ناجح" : "فاشل", row.MachineName, row.IpAddress);
            }
            List<string> totals = EmptyTotals(report);
            totals[0] = "الإجماليات — عدد المحاولات: " + rows.Count;
            totals[2] = "فاشلة: " + failed;
            report.TotalsRow = totals;
            return report;
        }

        /// <summary>سجل العمليات (التدقيق) — ينطبق عليه صلاحية إدارِية قبل الاستدعاء.</summary>
        public static ReportData AuditTrail(DateTime fromDate, DateTime toDate)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<AuditLog> rows = AuditDAL.Search(null, null, fromDate, toDate, 2000);
            ReportData report = new ReportData();
            report.Title = "سجل العمليات (التدقيق)";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd") + " (حد 2000 سجل)";
            report.AddColumn("ActionTime", "الوقت", 130);
            report.AddColumn("Username", "المستخدم", 100);
            report.AddColumn("ActionName", "العملية", 110);
            report.AddColumn("TableName", "الجدول", 100);
            report.AddColumn("RecordId", "المعرّف", 60);
            report.AddColumn("Changes", "التغيير", 220);
            report.AddColumn("MachineName", "الجهاز", 100);

            foreach (AuditLog row in rows)
            {
                string changes = "";
                if (!string.IsNullOrEmpty(row.OldValue))
                {
                    changes += "قبل: " + row.OldValue;
                }
                if (!string.IsNullOrEmpty(row.NewValue))
                {
                    changes += (changes.Length > 0 ? "  |  " : "") + "بعد: " + row.NewValue;
                }
                report.AddRow(row.ActionTime == null ? "" : row.ActionTime.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                    row.Username, row.ActionName, row.TableName, row.RecordId, changes, row.MachineName);
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد العمليات:", rows.Count.ToString());
            return report;
        }

        /// <summary>سجل تغييرات الموظف — يتطلب تحديد موظف (فلتر الموظف في شريط التقرير).</summary>
        public static ReportData EmployeeHistoryReport(DateTime fromDate, DateTime toDate, int employeeId)
        {
            NormalizeRange(ref fromDate, ref toDate);
            List<EmployeeHistory> rows = EmployeeHistoryDAL.GetForEmployee(employeeId, 1000);
            ReportData report = new ReportData();
            report.Title = "سجل تغييرات الموظف";
            report.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") + " → " + toDate.ToString("yyyy-MM-dd");
            report.AddColumn("ChangeDate", "وقت التغيير", 130);
            report.AddColumn("ChangeType", "نوع التغيير", 80);
            report.AddColumn("FieldName", "الحقل", 110);
            report.AddColumn("OldValue", "القيمة السابقة", 140);
            report.AddColumn("NewValue", "القيمة الجديدة", 140);
            report.AddColumn("ChangedBy", "بواسطة", 70);

            foreach (EmployeeHistory row in rows)
            {
                if (row.ChangeDate != null &&
                    (row.ChangeDate.Value.Date < fromDate.AddDays(-1) || row.ChangeDate.Value.Date > toDate))
                {
                    continue;
                }
                report.AddRow(row.ChangeDate == null ? "" : row.ChangeDate.Value.ToString("yyyy-MM-dd HH:mm"),
                    HistoryTypeAr(row.ChangeType), row.FieldName, row.OldValue, row.NewValue,
                    row.ChangedBy == null ? "" : row.ChangedBy.Value.ToString());
            }
            report.TotalsRow = TotalRow(report, rows.Count, 0, 0, "عدد التغييرات:", rows.Count.ToString());
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

        /// <summary>Empty totals row aligned with current columns.</summary>
        private static List<string> EmptyTotals(ReportData report)
        {
            List<string> totals = new List<string>();
            for (int i = 0; i < report.Columns.Count; i++)
            {
                totals.Add(string.Empty);
            }
            return totals;
        }

        /// <summary>Wide employee filter (all, including archived) for group reports.</summary>
        private static EmployeeSearchFilter BuildWideFilter()
        {
            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.IncludeArchived = true;
            filter.PageSize = MaxRows;
            return filter;
        }

        /// <summary>Arabic label for engine exception types (mirrors AttendanceExceptionBLL).</summary>
        private static string ExceptionTypeAr(string excType)
        {
            if (excType == "BadShift") { return "وردية غير محددة"; }
            if (excType == "Absent") { return "غياب"; }
            if (excType == "MissingPunch") { return "بصمة ناقصة"; }
            if (excType == "ExtraPunch") { return "بصمة زائدة"; }
            if (excType == "Late") { return "تأخير (دقائق)"; }
            if (excType == "EarlyLeave") { return "انصراف مبكر (دقائق)"; }
            if (excType == "OverContinuous") { return "تجاوز ساعات متواصلة"; }
            return excType ?? "";
        }

        /// <summary>Arabic label for adjustment request statuses.</summary>
        private static string AdjustmentStatusAr(string status)
        {
            if (status == "Approved") { return "مطبقّ"; }
            if (status == "Rejected") { return "مرفوض"; }
            if (status == "Pending") { return "معلّق"; }
            return status ?? "";
        }

        /// <summary>Arabic label for monthly schedule statuses.</summary>
        private static string ScheduleStatusAr(string status)
        {
            if (status == "Draft") { return "مسودة"; }
            if (status == "Approved") { return "معتمد"; }
            if (status == "Locked") { return "مقفول"; }
            return status ?? "";
        }

        /// <summary>Arabic label for employee-history change types.</summary>
        private static string HistoryTypeAr(string changeType)
        {
            if (changeType == "StatusChange") { return "تغيير حالة"; }
            if (changeType == "Archive") { return "أرشفة"; }
            return changeType ?? "";
        }

        /// <summary>Arabic weekday name.</summary>
        private static string DayNameAr(DateTime date)
        {
            string[] names = new string[]
            {
                "الأحد", "الاثنين", "الثلاثاء",
                "الأربعاء", "الخميس", "الجمعة", "السبت"
            };
            return names[(int)date.DayOfWeek];
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
