using System;

namespace HR.Entities
{
    /// <summary>Maps to table [EmployeeHistory].</summary>
    public class EmployeeHistory
    {
        /// <summary>Primary key (AUTOINCREMENT: HistoryId).</summary>
        public int HistoryId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>ChangeDate.</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>ChangeType.</summary>
        public string ChangeType { get; set; }

        /// <summary>FieldName.</summary>
        public string FieldName { get; set; }

        /// <summary>OldValue.</summary>
        public string OldValue { get; set; }

        /// <summary>NewValue.</summary>
        public string NewValue { get; set; }

        /// <summary>ChangedBy.</summary>
        public int? ChangedBy { get; set; }
    }
}
