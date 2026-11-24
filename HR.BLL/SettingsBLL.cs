using System;
using System.Collections.Generic;
using System.Globalization;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Central access to AppSettings (rule 11: all thresholds configurable, not hardcoded).
    /// All rule thresholds (BR-03/04/06/08/10...) are read through this class.
    /// </summary>
    public static class SettingsBLL
    {
        /// <summary>Loads/refreshes the settings cache; call once at login and after edits.</summary>
        public static void Refresh()
        {
            SettingsDAL.GetAll(true);
            // push the performance threshold to the DAL monitor (rule 36 — configurable):
            HR.DAL.QueryMonitor.ThresholdMs = GetInt("Performance.SlowQueryMs", 500);
        }

        /// <summary>Gets a raw string value or the supplied default when missing.</summary>
        public static string GetString(string key, string fallback)
        {
            Dictionary<string, string> settings = SettingsDAL.GetAll(false);
            string value;
            if (settings.TryGetValue(key, out value))
            {
                return value;
            }
            return fallback;
        }

        /// <summary>Gets an integer setting (grace periods, limits) with fallback.</summary>
        public static int GetInt(string key, int fallback)
        {
            string value = GetString(key, null);
            int parsed;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            return fallback;
        }

        /// <summary>Gets a floating-point setting (overtime rates).</summary>
        public static double GetDouble(string key, double fallback)
        {
            string value = GetString(key, null);
            double parsed;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            return fallback;
        }

        /// <summary>Gets a boolean setting (YES/NO/1/0).</summary>
        public static bool GetBool(string key, bool fallback)
        {
            string value = GetString(key, null);
            if (value == null)
            {
                return fallback;
            }
            string normalized = value.Trim().ToUpperInvariant();
            if (normalized == "YES" || normalized == "1" || normalized == "TRUE")
            {
                return true;
            }
            if (normalized == "NO" || normalized == "0" || normalized == "FALSE")
            {
                return false;
            }
            return fallback;
        }

        /// <summary>Full settings catalog (key, value, description) for the settings screen.</summary>
        public static List<AppSetting> ListSettings()
        {
            AppContext.RequirePermission("Settings.Manage");
            return SettingsDAL.GetSettings();
        }

        /// <summary>
        /// Creates a brand-new setting key (rule 36: extensibility without rebuild).
        /// Validates key/value/description lengths against the AppSettings schema
        /// (50/200/200), rejects duplicates and writes an audit entry.
        /// </summary>
        public static void Create(string key, string value, string description)
        {
            AppContext.RequirePermission("Settings.Manage");
            string normalizedKey = (key ?? string.Empty).Trim();
            if (normalizedKey.Length == 0)
            {
                throw new BusinessException("مفتاح الإعداد مطلوب.");
            }
            if (normalizedKey.Length > 50)
            {
                throw new BusinessException("مفتاح الإعداد يجب ألا يتجاوز 50 حرفًا.");
            }
            string normalizedValue = (value ?? string.Empty).Trim();
            if (normalizedValue.Length == 0)
            {
                throw new BusinessException("قيمة الإعداد مطلوبة.");
            }
            if (normalizedValue.Length > 200)
            {
                throw new BusinessException("قيمة الإعداد يجب ألا تتجاوز 200 حرف.");
            }
            string normalizedDesc = (description ?? string.Empty).Trim();
            if (normalizedDesc.Length > 200)
            {
                throw new BusinessException("وصف الإعداد يجب ألا يتجاوز 200 حرف.");
            }
            if (GetString(normalizedKey, null) != null)
            {
                throw new BusinessException("مفتاح الإعداد '" + normalizedKey + "' موجود مسبقًا — استخدم التعديل بدلاً من الإضافة.");
            }
            SettingsDAL.Insert(normalizedKey, normalizedValue, normalizedDesc);
            Refresh();
            AuditBLL.Log("Settings.Create", "AppSettings", normalizedKey, null, normalizedValue);
        }

        /// <summary>Saves a setting (permission "Settings.Manage") with an audit entry.</summary>
        public static void Save(string key, string value)
        {
            AppContext.RequirePermission("Settings.Manage");
            string oldValue = GetString(key, null);
            if (string.Equals(oldValue, value, StringComparison.Ordinal))
            {
                return;
            }
            Dictionary<string, string> wrapper = new Dictionary<string, string>();
            wrapper[key] = value;
            SettingsDAL.Set(key, value);
            Refresh();
            AuditBLL.Log("Settings.Save", "AppSettings", key, oldValue, value);
        }

        /// <summary>Returns a copy of all current settings for the System Settings screen.</summary>
        public static Dictionary<string, string> GetAll()
        {
            return new Dictionary<string, string>(SettingsDAL.GetAll(false), StringComparer.OrdinalIgnoreCase);
        }
    }
}
