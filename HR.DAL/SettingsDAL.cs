using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace HR.DAL
{
    /// <summary>AppSettings (SystemSettings) table access with a process-level cache.</summary>
    public static class SettingsDAL
    {
        private static readonly object SyncRoot = new object();
        private static Dictionary<string, string> _cache;

        /// <summary>Loads all settings once; callers pass forceReload after key changes.</summary>
        public static Dictionary<string, string> GetAll(bool forceReload)
        {
            lock (SyncRoot)
            {
                if (_cache == null || forceReload)
                {
                    Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    List<KeyValuePair<string, string>> rows = Db.ReadList(
                        "SELECT SettingKey, SettingValue FROM AppSettings",
                        delegate(OleDbDataReader reader)
                        {
                            return new KeyValuePair<string, string>(reader.GetString(0), reader.GetString(1));
                        });
                    foreach (KeyValuePair<string, string> row in rows)
                    {
                        settings[row.Key] = row.Value;
                    }
                    _cache = settings;
                }
                return _cache;
            }
        }

        /// <summary>Updates one setting and refreshes the cache immediately.</summary>
        public static void Set(string key, string value)
        {
            string sql = "UPDATE AppSettings SET SettingValue = ?, UpdatedAt = ? WHERE SettingKey = ?";
            Db.ExecuteNonQuery(sql, Db.P(value), Db.PDateRequired(DateTime.Now), Db.P(key));
            lock (SyncRoot)
            {
                if (_cache != null && _cache.ContainsKey(key))
                {
                    _cache[key] = value;
                }
            }
        }
    }
}
