using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>Writes audit entries for every significant operation (rule 22).</summary>
    public static class AuditBLL
    {
        /// <summary>Records one audit entry; failures are logged but never break business flow.</summary>
        public static void Log(string actionName, string tableName, string recordId, object oldValue, object newValue)
        {
            try
            {
                AuditLog audit = new AuditLog();
                audit.UserId = AppContext.CurrentUser != null ? (int?)AppContext.CurrentUser.UserId : null;
                audit.ActionName = actionName;
                audit.ActionTime = DateTime.Now;
                audit.TableName = tableName;
                audit.RecordId = recordId;
                audit.OldValue = oldValue == null ? null : TextSerializer.Serialize(oldValue);
                audit.NewValue = newValue == null ? null : TextSerializer.Serialize(newValue);
                audit.MachineName = AppContext.MachineName;
                audit.IpAddress = LocalIpAddressSafe();
                AuditDAL.Insert(audit);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "AuditBLL.Log failed — action=" + actionName + ", table=" + tableName);
            }
        }

        /// <summary>Audit trail view (permission "Audit.View").</summary>
        public static List<AuditLog> Search(string actionName, string tableName, DateTime? fromDate, DateTime? toDate, int maxRows)
        {
            AppContext.RequirePermission("Audit.View");
            return AuditDAL.Search(actionName, tableName, fromDate, toDate, maxRows);
        }

        private static string LocalIpAddressSafe()
        {
            try
            {
                return AppContext.LocalIpAddress;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
