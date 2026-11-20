using System;
using System.Collections.Generic;
using System.Diagnostics;
using HR.DAL;
using HR.Entities;
using HR.Helpers;
using HR.Services;

namespace HR.BLL
{
    /// <summary>Result summary of one device download (rule 9 audit payload).</summary>
    public class DownloadSummary
    {
        /// <summary>Log rows delivered by the device this session.</summary>
        public int DeviceLogs { get; set; }

        /// <summary>Rows actually inserted (first-time records).</summary>
        public int NewLogs { get; set; }

        /// <summary>Rows skipped because an identical record already exists.</summary>
        public int DuplicateLogs { get; set; }

        /// <summary>Rows whose enroll code has no employee record yet.</summary>
        public int UnmappedLogs { get; set; }

        /// <summary>Users registered on the device at download time.</summary>
        public int DeviceUsers { get; set; }

        /// <summary>Timestamp of the newest row delivered by the device.</summary>
        public DateTime? LastLogTime { get; set; }

        /// <summary>Total wall time of the operation in milliseconds.</summary>
        public int DurationMs { get; set; }

        /// <summary>Serial number read from the device during this session.</summary>
        public string SerialNumber { get; set; }
    }

    /// <summary>
    /// Device sessions (rule 9): test connection, download raw attendance logs
    /// and list device users. Connect flow: Connect_Net → serial check →
    /// EnableDevice(false) → in-memory read → EnableDevice(true) → disconnect,
    /// then the database write happens outside the device session so the reader
    /// is never held while inserting. Duplicate protection: prefilter in the
    /// UQ_Log key domain plus the unique constraint itself; a duplicate never
    /// aborts the batch and no delivered record is ever discarded silently.
    /// Requires "Devices.Manage"; every session is audit-logged.
    /// </summary>
    public static class FingerprintBLL
    {
        /// <summary>Connects, reads the serial number and releases the device.</summary>
        public static string TestConnection(Device device)
        {
            AppContext.RequirePermission("Devices.Manage");
            if (device == null)
            {
                throw new BusinessException("الجهاز غير موجود.");
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            string serial;
            using (ZkGateway gateway = ZkGateway.Create())
            {
                ConnectAndVerify(gateway, device);
                serial = gateway.GetSerialNumber(MachineNumber());
                gateway.Disconnect();
            }
            int elapsedMs = (int)stopwatch.ElapsedMilliseconds;
            DeviceDAL.UpdateSyncState(device.DeviceId, DateTime.Now, "Connected", elapsedMs);
            AuditBLL.Log("Fingerprint.Test", "Devices", device.DeviceId.ToString(), null,
                "IP=" + device.IpAddress + ", Serial=" + (serial ?? "-") + ", Ms=" + elapsedMs);
            return serial;
        }

        /// <summary>Downloads and stores the device logs; returns exact counters for the UI.</summary>
        public static DownloadSummary DownloadLogs(int deviceId)
        {
            AppContext.RequirePermission("Devices.Manage");
            Device device = DeviceDAL.GetById(deviceId);
            if (device == null)
            {
                throw new BusinessException("الجهاز غير موجود (رُبما حُذف من محطة أخرى).");
            }
            if (device.IsActive == false)
            {
                throw new BusinessException("الجهاز '" + device.DeviceName + "' معطّل — فعّله قبل تنزيل السجلات.");
            }
            int machineNumber = MachineNumber();
            DownloadSummary summary = new DownloadSummary();
            Stopwatch stopwatch = Stopwatch.StartNew();

            List<FingerprintLog> incoming = new List<FingerprintLog>();
            int deviceUsers = 0;
            string serialRead = null;
            using (ZkGateway gateway = ZkGateway.Create())
            {
                ConnectAndVerify(gateway, device);
                serialRead = gateway.GetSerialNumber(machineNumber);

                List<DeviceUserInfo> usersOnDevice;
                try
                {
                    usersOnDevice = ReadUsers(gateway, machineNumber);
                }
                catch (InvalidOperationException)
                {
                    usersOnDevice = new List<DeviceUserInfo>();
                }
                deviceUsers = usersOnDevice.Count;

                gateway.EnableDevice(machineNumber, false);
                try
                {
                    if (gateway.ReadGeneralLogData(machineNumber))
                    {
                        string enrollCode;
                        int inOutFlag;
                        DateTime logTime;
                        while (gateway.ReadNextLog(machineNumber, out enrollCode, out inOutFlag, out logTime))
                        {
                            FingerprintLog entry = new FingerprintLog();
                            entry.DeviceId = deviceId;
                            entry.EmpDevCode = enrollCode;
                            entry.InOutFlag = inOutFlag;
                            entry.LogTime = logTime;
                            incoming.Add(entry);
                        }
                    }
                }
                finally
                {
                    gateway.EnableDevice(machineNumber, true);
                }
                gateway.Disconnect();
            }

            summary.SerialNumber = serialRead;
            summary.DeviceUsers = deviceUsers;
            summary.DeviceLogs = incoming.Count;
            BindEmployees(incoming, summary);
            SaveIncoming(deviceId, incoming, summary);

            int elapsedMs = (int)stopwatch.ElapsedMilliseconds;
            summary.DurationMs = elapsedMs;
            DeviceDAL.UpdateSyncState(deviceId, DateTime.Now, "Downloaded", elapsedMs);
            AuditBLL.Log("Fingerprint.Download", "FingerprintLogs", deviceId.ToString(), null,
                "Device=" + device.DeviceName + ", IP=" + device.IpAddress +
                ", Total=" + summary.DeviceLogs + ", New=" + summary.NewLogs +
                ", Duplicates=" + summary.DuplicateLogs + ", Unmapped=" + summary.UnmappedLogs +
                ", DeviceUsers=" + summary.DeviceUsers + ", Ms=" + elapsedMs);
            return summary;
        }

        /// <summary>Reads the registered users of a device live (no database writes).</summary>
        public static List<DeviceUserInfo> ReadDeviceUsers(int deviceId)
        {
            AppContext.RequirePermission("Devices.Manage");
            Device device = DeviceDAL.GetById(deviceId);
            if (device == null)
            {
                throw new BusinessException("الجهاز غير موجود (رُبما حُذف من محطة أخرى).");
            }
            List<DeviceUserInfo> users = new List<DeviceUserInfo>();
            using (ZkGateway gateway = ZkGateway.Create())
            {
                ConnectAndVerify(gateway, device);
                int machineNumber = MachineNumber();
                try
                {
                    users = ReadUsers(gateway, machineNumber);
                }
                catch (InvalidOperationException)
                {
                    users = new List<DeviceUserInfo>();
                }
                gateway.Disconnect();
            }
            Dictionary<string, Employee> byEnroll = EmployeeEnrollIndex();
            foreach (DeviceUserInfo user in users)
            {
                if (string.IsNullOrEmpty(user.EnrollCode) || !byEnroll.ContainsKey(user.EnrollCode))
                {
                    continue;
                }
                Employee employee = byEnroll[user.EnrollCode];
                user.EmployeeCode = employee.EmployeeCode;
                user.EmployeeName = employee.FullNameAr;
                user.Matched = true;
            }
            AuditBLL.Log("Fingerprint.ReadUsers", "Devices", deviceId.ToString(), null,
                "Users=" + users.Count);
            return users;
        }

        /// <summary>Raw-log browser for the UI (read-only; never touches the device).</summary>
        public static List<FingerprintLogView> SearchRawLogs(int? deviceId, DateTime? fromTime,
            DateTime? toTime, int maxRows)
        {
            if (maxRows <= 0 || maxRows > 5000)
            {
                maxRows = 1000;
            }
            if (fromTime != null && toTime != null && toTime.Value < fromTime.Value)
            {
                throw new BusinessException("\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0646\u0647\u0627\u064a\u0629 \u0642\u0628\u0644 \u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0628\u062f\u0627\u064a\u0629.");
            }
            return FingerprintDAL.SearchLogs(deviceId, null, fromTime, toTime, maxRows);
        }

        /// <summary>Connect over TCP then verify the device serial against the registry.</summary>
        private static void ConnectAndVerify(ZkGateway gateway, Device device)
        {
            if (device.Port == null)
            {
                throw new BusinessException("الجهاز '" + device.DeviceName + "' بدون منفذ — عدّل بياناته أولًا.");
            }
            bool connected;
            try
            {
                connected = gateway.ConnectNet(device.IpAddress, device.Port.Value);
            }
            catch (InvalidOperationException ex)
            {
                throw new BusinessException(ex.Message);
            }
            if (!connected)
            {
                int errorCode = 0;
                try
                {
                    errorCode = gateway.LastErrorCode();
                }
                catch (InvalidOperationException)
                {
                }
                DeviceDAL.UpdateSyncState(device.DeviceId, DateTime.Now, "Failed", null);
                throw new BusinessException("فشل الاتصال بالجهاز '" + device.DeviceName + "' (" +
                    device.IpAddress + ":" + device.Port.Value + "). رمز الخطأ: " + errorCode +
                    ". تحقق من الشبكة والعنوان والمنفذ.");
            }
            string serialRead = gateway.GetSerialNumber(MachineNumber());
            if (!string.IsNullOrEmpty(device.SerialNumber) &&
                !string.Equals(serialRead, device.SerialNumber, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("الرقم التسلسلي للجهاز المتصل (" + serialRead +
                    ") لا يطابق المسجّل (" + device.SerialNumber + "). صحّح سجل الجهاز قبل المتابعة — سيُنسبما لسجل خاطئ.");
            }
            if (string.IsNullOrEmpty(device.SerialNumber) && !string.IsNullOrEmpty(serialRead))
            {
                device.SerialNumber = serialRead;
                DeviceDAL.Update(device);
            }
        }

        private static int MachineNumber()
        {
            return SettingsBLL.GetInt("Fp.MachineNumber", 1);
        }

        private static List<DeviceUserInfo> ReadUsers(ZkGateway gateway, int machineNumber)
        {
            List<DeviceUserInfo> users = new List<DeviceUserInfo>();
            if (!gateway.ReadAllUserId(machineNumber))
            {
                return users;
            }
            while (true)
            {
                string enrollCode;
                string fullName;
                int privilege;
                bool enabled;
                if (!gateway.ReadNextUser(machineNumber, out enrollCode, out fullName, out privilege, out enabled))
                {
                    break;
                }
                DeviceUserInfo user = new DeviceUserInfo();
                user.EnrollCode = enrollCode;
                user.FullName = fullName;
                user.Privilege = privilege;
                user.Enabled = enabled;
                users.Add(user);
            }
            return users;
        }

        private static Dictionary<string, Employee> EmployeeEnrollIndex()
        {
            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.IncludeArchived = true;
            filter.PageSize = 10000;
            Dictionary<string, Employee> byEnroll = new Dictionary<string, Employee>(StringComparer.Ordinal);
            foreach (Employee employee in EmployeeBLL.Search(filter))
            {
                if (!string.IsNullOrEmpty(employee.DeviceEnrollNumber))
                {
                    byEnroll[employee.DeviceEnrollNumber] = employee;
                }
            }
            return byEnroll;
        }

        private static void BindEmployees(List<FingerprintLog> incoming, DownloadSummary summary)
        {
            if (incoming.Count == 0)
            {
                return;
            }
            DateTime minTime = DateTime.MaxValue;
            DateTime maxTime = DateTime.MinValue;
            foreach (FingerprintLog entry in incoming)
            {
                if (entry.LogTime != null && entry.LogTime.Value < minTime)
                {
                    minTime = entry.LogTime.Value;
                }
                if (entry.LogTime != null && entry.LogTime.Value > maxTime)
                {
                    maxTime = entry.LogTime.Value;
                }
            }
            summary.LastLogTime = maxTime == DateTime.MinValue ? (DateTime?)null : maxTime;

            Dictionary<string, Employee> byEnroll = EmployeeEnrollIndex();
            foreach (FingerprintLog entry in incoming)
            {
                if (string.IsNullOrEmpty(entry.EmpDevCode) || !byEnroll.ContainsKey(entry.EmpDevCode))
                {
                    summary.UnmappedLogs++;
                    continue;
                }
                entry.EmployeeId = byEnroll[entry.EmpDevCode].EmployeeId;
            }
        }

        private static void SaveIncoming(int deviceId, List<FingerprintLog> incoming, DownloadSummary summary)
        {
            if (incoming.Count == 0)
            {
                return;
            }
            DateTime minTime = DateTime.MaxValue;
            DateTime maxTime = DateTime.MinValue;
            foreach (FingerprintLog entry in incoming)
            {
                if (entry.LogTime != null && entry.LogTime.Value < minTime)
                {
                    minTime = entry.LogTime.Value;
                }
                if (entry.LogTime != null && entry.LogTime.Value > maxTime)
                {
                    maxTime = entry.LogTime.Value;
                }
            }
            HashSet<string> existing = FingerprintDAL.LoadExistingKeys(minTime, maxTime);
            List<FingerprintLog> fresh = new List<FingerprintLog>();
            foreach (FingerprintLog entry in incoming)
            {
                string key = FingerprintDAL.BuildKey(entry.EmployeeId, entry.EmpDevCode,
                    entry.LogTime ?? DateTime.MinValue, entry.InOutFlag);
                if (!existing.Add(key))
                {
                    summary.DuplicateLogs++;
                    continue;
                }
                fresh.Add(entry);
            }
            summary.NewLogs = FingerprintDAL.InsertLogs(deviceId, fresh);
        }
    }
}
