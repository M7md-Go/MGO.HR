using System;
using System.Reflection;
using System.Text;

namespace HR.Helpers
{
    /// <summary>
    /// Serializes entity properties into "Field=Value; ..." text for the
    /// OldValue / NewValue columns of AuditLogs and EmployeeHistory.
    /// </summary>
    public static class TextSerializer
    {
        /// <summary>Builds a field=value list of all public writable instance properties.</summary>
        public static string Serialize(object entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }
            StringBuilder text = new StringBuilder();
            PropertyInfo[] properties = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }
                object value = property.GetValue(entity, null);
                text.Append(property.Name);
                text.Append("=");
                text.Append(FormatValue(value));
                text.Append("; ");
            }
            return text.ToString().TrimEnd(';', ' ');
        }

        /// <summary>Builds a compact list of only the changed fields between two objects.</summary>
        public static string Diff(object oldValue, object newValue)
        {
            StringBuilder text = new StringBuilder();
            if (oldValue == null || newValue == null)
            {
                return Serialize(newValue ?? oldValue);
            }
            PropertyInfo[] properties = newValue.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }
                object before = property.GetValue(oldValue, null);
                object after = property.GetValue(newValue, null);
                if (!Equals(before, after))
                {
                    text.Append(property.Name);
                    text.Append(": '");
                    text.Append(FormatValue(before));
                    text.Append("' -> '");
                    text.Append(FormatValue(after));
                    text.Append("'; ");
                }
            }
            return text.ToString().TrimEnd(';', ' ');
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "";
            }
            if (value is DateTime)
            {
                return ((DateTime)value).ToString("yyyy-MM-dd HH:mm");
            }
            return value.ToString();
        }
    }
}
