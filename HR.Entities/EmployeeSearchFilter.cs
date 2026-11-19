using System;

namespace HR.Entities
{
    /// <summary>Search/filter parameters for the employee list screen.</summary>
    public class EmployeeSearchFilter
    {
        /// <summary>Free text matched against code, Arabic name and national id.</summary>
        public string Text { get; set; }

        /// <summary>Department filter, or null for all.</summary>
        public int? DepartmentId { get; set; }

        /// <summary>Section filter, or null for all.</summary>
        public int? SectionId { get; set; }

        /// <summary>Job title filter, or null for all.</summary>
        public int? JobTitleId { get; set; }

        /// <summary>"Active" / "Suspended" / "Archived" or null for all.</summary>
        public string Status { get; set; }

        /// <summary>Include soft-deleted employees (archived) in the result.</summary>
        public bool IncludeArchived { get; set; }

        /// <summary>Maximum rows returned (keyset paging via LastCode).</summary>
        public int PageSize { get; set; }

        /// <summary>Last employee code of the previous page for keyset paging.</summary>
        public string LastCode { get; set; }
    }
}
