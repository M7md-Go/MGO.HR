using System;
using System.Collections.Generic;
using System.Globalization;
using HR.DAL;
using HR.Entities;

namespace HR.Services
{
    /// <summary>
    /// Dashboard composition (rule 18). Read-only views over the DAL — same
    /// Phase-9 architecture decision: HR.BLL already references HR.Services,
    /// so this engine must not reference HR.BLL; every required table is
    /// reachable through HR.DAL directly. The totals over the selected
    /// period are man-day counts driven by AttendanceDAL.Browse.
    /// </summary>
    public static class DashboardEngine
    {
        /// <summary>Guard rail for one dashboard load (rule 27).</summary>
        public const int MaxAttendanceRows = 200000;

        /// <summary>Newest punches shown on the dashboard.</summary>
        public const int RecentFingerprints = 12;

        /// <summary>
        /// Builds the dashboard for the period [fromDate, toDate]. The
        /// department filter scopes the KPIs (attendance/overtime) while the
        /// per-department table always shows all departments.
        /// </summary>
        public static DashboardData Build(DateTime fromDate, DateTime toDate, int? departmentId)
        {
            if (toDate < fromDate)
            {
                DateTime temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }
            fromDate = fromDate.Date;
            toDate = toDate.Date;

            DashboardData data = new DashboardData();
            data.PeriodText = "الفترة: " + fromDate.ToString("yyyy-MM-dd") +
                " \u2192 " + toDate.ToString("yyyy-MM-dd") +
                "  (" + (toDate - fromDate).Days + " يوم]";

            data.TotalEmployees = (int)EmployeeDAL.CountAll();

            List<AttendanceView> rows = AttendanceDAL.Browse(fromDate, toDate, null,
                departmentId, MaxAttendanceRows);
            Dictionary<string, int[]> byDepartment = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
            foreach (AttendanceView row in rows)
            {
                string status = row.Status;
                if (status == "Present" || status == "Late" || status == "EarlyLeave")
                {
                    data.Present++;
                }
                else if (status == "Absent")
                {
                    data.Absent++;
                }
                else if (status == "MissingPunch")
                {
                    data.MissingPunch++;
                }
                if (row.LateMinutes != null && row.LateMinutes.Value > 0)
                {
                    data.Late++;
                    data.LateMinutes += row.LateMinutes.Value;
                }
                if (row.EarlyLeaveMinutes != null && row.EarlyLeaveMinutes.Value > 0)
                {
                    data.Early++;
                    data.EarlyMinutes += row.EarlyLeaveMinutes.Value;
                }
                data.WorkedMinutes += row.MinutesWorked ?? 0;

                string department = string.IsNullOrEmpty(row.DepartmentName) ?
                    "\u063a\u064a\u0631 \u0645\u062d\u062f\u062f" : row.DepartmentName;
                int[] counters;
                if (!byDepartment.TryGetValue(department, out counters))
                {
                    counters = new int[2];
                    byDepartment[department] = counters;
                }
                if (status == "Present" || status == "Late" || status == "EarlyLeave")
                {
                    counters[0]++;
                }
                else if (status == "Absent")
                {
                    counters[1]++;
                }
            }

            List<DepartmentCount> counts = EmployeeDAL.CountByDepartment();
            foreach (DepartmentCount count in counts)
            {
                string name = string.IsNullOrEmpty(count.DepartmentName) ?
                    "\u063a\u064a\u0631 \u0645\u062d\u062f\u062f" : count.DepartmentName;

                DepartmentStat stat = new DepartmentStat();
                stat.DepartmentName = name;
                stat.Employees = count.EmployeeCount;
                int[] counters;
                if (byDepartment.TryGetValue(name, out counters))
                {
                    stat.Present = counters[0];
                    stat.Absent = counters[1];
                }
                data.DepartmentStats.Add(stat);
            }

            List<LeaveRequestView> leaves = LeaveDAL.SearchRequests("Approved", null,
                fromDate, toDate, 5000);
            data.ApprovedLeaves = leaves.Count;
            foreach (LeaveRequestView leave in leaves)
            {
                data.LeaveDays += leave.DaysCount ?? 0;
            }

            List<MissionView> missions = MissionDAL.Search("Approved", null,
                fromDate, toDate, 5000);
            data.ApprovedMissions = missions.Count;

            List<OvertimeView> overtime = OvertimeDAL.Search(fromDate, toDate, null, null, 20000);
            data.OvertimeCount = overtime.Count;
            foreach (OvertimeView row in overtime)
            {
                data.OvertimeMinutes += row.EffectiveMinutes ?? 0;
            }

            data.Fingerprints = FingerprintDAL.SearchLogs(null, null, fromDate, toDate,
                RecentFingerprints);
            data.Devices = DeviceDAL.GetAll(true);
            return data;
        }
    }
}
