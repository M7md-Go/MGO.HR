using System;

namespace HR.Entities
{
    /// <summary>Maps to table [ArchiveLogs].</summary>
    public class ArchiveLog
    {
        /// <summary>Primary key (AUTOINCREMENT: ArchiveId).</summary>
        public int ArchiveId { get; set; }
        /// <summary>TableName.</summary>
        public string TableName { get; set; }

        /// <summary>FromDate.</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>ToDate.</summary>
        public DateTime? ToDate { get; set; }

        /// <summary>RowsCount.</summary>
        public int? RowsCount { get; set; }

        /// <summary>ArchiveFile.</summary>
        public string ArchiveFile { get; set; }

        /// <summary>ArchivedBy.</summary>
        public int? ArchivedBy { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
