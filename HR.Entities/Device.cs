using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Devices].</summary>
    public class Device
    {
        /// <summary>Primary key (AUTOINCREMENT: DeviceId).</summary>
        public int DeviceId { get; set; }
        /// <summary>DeviceName.</summary>
        public string DeviceName { get; set; }

        /// <summary>IpAddress.</summary>
        public string IpAddress { get; set; }

        /// <summary>Port.</summary>
        public int? Port { get; set; }

        /// <summary>SerialNumber.</summary>
        public string SerialNumber { get; set; }

        /// <summary>LastSync.</summary>
        public DateTime? LastSync { get; set; }

        /// <summary>LastStatus.</summary>
        public string LastStatus { get; set; }

        /// <summary>LastPingDelay.</summary>
        public int? LastPingDelay { get; set; }

        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }

        /// <summary>Location.</summary>
        public string Location { get; set; }
    }
}
