using System;
using System.Collections.Generic;
using HR.Entities;

namespace HR.BLL
{
    /// <summary>
    /// Alerts center (read-only aggregation, no SQL in UI):
    /// pending leave requests, pending missions, and fingerprint devices whose
    /// last sync is older than the configurable Alert.DeviceStaleDays window
    /// (0 = disabled). Each alert carries the module id to open on click.
    /// </summary>
    public static class AlertsBLL
    {
        public const string DeviceStaleDaysKey = "Alert.DeviceStaleDays";
        public const int DeviceStaleDaysDefault = 3;

        public static List<AlertItem> Collect()
        {
            List<AlertItem> items = new List<AlertItem>();
            CollectPendingLeaves(items);
            CollectPendingMissions(items);
            CollectStaleDevices(items);
            return items;
        }

        public static AlertSummary Summary()
        {
            List<AlertItem> items = Collect();
            AlertSummary summary = new AlertSummary();
            summary.Items = items;
            summary.Count = items.Count;
            return summary;
        }

        private static void CollectPendingLeaves(List<AlertItem> items)
        {
            try
            {
                List<LeaveRequestView> pending = LeaveBLL.Search(LeaveBLL.StatusPending, null, null, null);
                if (pending != null && pending.Count > 0)
                {
                    items.Add(Make("طلبات الإجازات",
                        "لديك " + pending.Count + " طلب إجازة بانتظار الاعتماد",
                        pending.Count > 10 ? "bad" : "warn", "leaves"));
                }
            }
            catch (Exception)
            {
                // alerts must never break the shell; a failing source is skipped
            }
        }

        private static void CollectPendingMissions(List<AlertItem> items)
        {
            try
            {
                List<MissionView> pending = MissionBLL.Search(MissionBLL.StatusPending, null, null, null);
                if (pending != null && pending.Count > 0)
                {
                    items.Add(Make("المأموريات",
                        "لديك " + pending.Count + " مأمورية بانتظار الاعتماد",
                        pending.Count > 10 ? "bad" : "warn", "missions"));
                }
            }
            catch (Exception)
            {
                // alerts must never break the shell; a failing source is skipped
            }
        }

        private static void CollectStaleDevices(List<AlertItem> items)
        {
            try
            {
                int staleDays = SettingsBLL.GetInt(DeviceStaleDaysKey, DeviceStaleDaysDefault);
                if (staleDays <= 0)
                {
                    return; // 0 = monitoring disabled (Rule 36: configurable, not hardcoded)
                }
                DateTime threshold = DateTime.Now.AddDays(-staleDays);
                List<Device> devices = DeviceBLL.GetAll(false);
                int stale = 0;
                string firstName = null;
                foreach (Device device in devices)
                {
                    if (device.IsActive == false || !device.LastSync.HasValue || device.LastSync.Value > threshold)
                    {
                        continue;
                    }
                    stale++;
                    if (firstName == null)
                    {
                        firstName = device.DeviceName;
                    }
                }
                if (stale == 1)
                {
                    items.Add(Make("أجهزة البصمة",
                        "الجهاز «" + firstName + "» لم تتم مزامنته منذ " + staleDays + " يوم أو أكثر",
                        "bad", "devices"));
                }
                else if (stale > 1)
                {
                    items.Add(Make("أجهزة البصمة",
                        stale + " أجهزة بصمة لم تتم مزامنتها منذ " + staleDays + " يوم أو أكثر (أولها: " + firstName + ")",
                        "bad", "devices"));
                }
            }
            catch (Exception)
            {
                // alerts must never break the shell; a failing source is skipped
            }
        }

        private static AlertItem Make(string category, string message, string severity, string moduleId)
        {
            AlertItem item = new AlertItem();
            item.Category = category;
            item.Message = message;
            item.Severity = severity;
            item.ModuleId = moduleId;
            return item;
        }
    }
}
