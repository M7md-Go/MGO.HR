using System;

namespace HR.Entities
{
    /// <summary>Maps to table [BackupLogs].</summary>
    public class BackupLog
    {
        /// <summary>Primary key (AUTOINCREMENT: BackupId).</summary>
        public int BackupId { get; set; }
        /// <summary>BackupType.</summary>
        public string BackupType { get; set; }

        /// <summary>FilePath.</summary>
        public string FilePath { get; set; }

        /// <summary>FileSize.</summary>
        public int? FileSize { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>CreatedBy.</summary>
        public int? CreatedBy { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>RestorePath.</summary>
        public string RestorePath { get; set; }
    }
}
