using System;
using System.Collections.Generic;

namespace HR.Entities
{
    /// <summary>Active employees per department (dashboard).</summary>
    public class DepartmentCount
    {
        /// <summary>Department name (null when unassigned).</summary>
        public string DepartmentName { get; set; }
        /// <summary>Number of non-deleted employees.</summary>
        public int EmployeeCount { get; set; }
    }

    /// <summary>
    /// Fully materialized dashboard payload (rule 18). The counts over the
    /// selected period are man-day counts (one row per attendance day).
    /// Built by HR.Services.DashboardEngine from read-only DAL queries.
    /// </summary>
    public class DashboardData
    {
        /// <summary>Total non-deleted employees.</summary>
        public int TotalEmployees;
        /// <summary>Days worked (Present / Late / EarlyLeave) within the period.</summary>
        public int Present;
        /// <summary>Absent man-days within the period.</summary>
        public int Absent;
        /// <summary>Late arrivals counted (LateMinutes &gt; 0).</summary>
        public int Late;
        /// <summary>Total late minutes.</summary>
        public long LateMinutes;
        /// <summary>Early-leave occurrences (&gt; 0 minutes).</summary>
        public int Early;
        /// <summary>Total early-leave minutes.</summary>
        public long EarlyMinutes;
        /// <summary>Missing-punch days.</summary>
        public int MissingPunch;
        /// <summary>Sum of MinutesWorked (minutes).</summary>
        public long WorkedMinutes;
        /// <summary>Overtime rows counted.</summary>
        public int OvertimeCount;
        /// <summary>Sum of effective overtime minutes.</summary>
        public long OvertimeMinutes;
        /// <summary>Approved leave requests starting inside the period.</summary>
        public int ApprovedLeaves;
        /// <summary>Approved leave days inside the period.</summary>
        public int LeaveDays;
        /// <summary>Approved missions starting inside the period.</summary>
        public int ApprovedMissions;
        /// <summary>Printable period description (Arabic).</summary>
        public string PeriodText;
        /// <summary>Employees per department (all departments).</summary>
        public List<DepartmentCount> DepartmentCounts { get; set; }
        /// <summary>Per-department presence aggregates within the period.</summary>
        public List<DepartmentStat> DepartmentStats { get; set; }
        /// <summary>Newest fingerprint punches (latest first, capped).</summary>
        public List<FingerprintLogView> Fingerprints { get; set; }
        /// <summary>Fingerprint devices and their last sync status.</summary>
        public List<Device> Devices { get; set; }

        public DashboardData()
        {
            DepartmentCounts = new List<DepartmentCount>();
            DepartmentStats = new List<DepartmentStat>();
            Fingerprints = new List<FingerprintLogView>();
            Devices = new List<Device>();
        }
    }

    /// <summary>Presence/absence aggregates for one department.</summary>
    public class DepartmentStat
    {
        /// <summary>Department name.</summary>
        public string DepartmentName;
        /// <summary>Active employees (from EmployeeDAL.CountByDepartment).</summary>
        public int Employees;
        /// <summary>Present man-days within the period.</summary>
        public int Present;
        /// <summary>Absent man-days within the period.</summary>
        public int Absent;
    }
}
