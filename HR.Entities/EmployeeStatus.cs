using System;

namespace HR.Entities
{
    /// <summary>Maps to table [EmployeeStatus].</summary>
    public class EmployeeStatus
    {
        /// <summary>Primary key (AUTOINCREMENT: EmployeeStatusId).</summary>
        public int EmployeeStatusId { get; set; }
        /// <summary>StatusCode.</summary>
        public string StatusCode { get; set; }

        /// <summary>StatusName.</summary>
        public string StatusName { get; set; }
    }
}
