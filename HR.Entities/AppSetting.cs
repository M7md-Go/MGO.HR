using System;

namespace HR.Entities
{
    /// <summary>Maps to table [AppSettings].</summary>
    public class AppSetting
    {
        /// <summary>SettingKey.</summary>
        public string SettingKey { get; set; }

        /// <summary>SettingValue.</summary>
        public string SettingValue { get; set; }

        /// <summary>SettingDesc.</summary>
        public string SettingDesc { get; set; }

        /// <summary>UpdatedAt.</summary>
        public DateTime? UpdatedAt { get; set; }
    }
}
