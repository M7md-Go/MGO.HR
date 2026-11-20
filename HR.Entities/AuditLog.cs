using System;

namespace HR.Entities
{
    /// <summary>Maps to table [AuditLogs].</summary>
    public class AuditLog
    {
        /// <summary>Primary key (AUTOINCREMENT: AuditId).</summary>
        public int AuditId { get; set; }
        /// <summary>UserId.</summary>
        public int? UserId { get; set; }

        /// <summary>ActionName.</summary>
        public string ActionName { get; set; }

        /// <summary>ActionTime.</summary>
        public DateTime? ActionTime { get; set; }

        /// <summary>TableName.</summary>
        public string TableName { get; set; }

        /// <summary>RecordId.</summary>
        public string RecordId { get; set; }

        /// <summary>OldValue.</summary>
        public string OldValue { get; set; }

        /// <summary>NewValue.</summary>
        public string NewValue { get; set; }

        /// <summary>MachineName.</summary>
        public string MachineName { get; set; }

        /// <summary>IpAddress.</summary>
        public string IpAddress { get; set; }

        /// <summary>Username of UserId (resolved by JOIN, not a table column).</summary>
        public string Username { get; set; }
    }
}
