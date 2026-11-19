using System;

namespace HR.Entities
{
    /// <summary>Maps to table [JobTitles].</summary>
    public class JobTitle
    {
        /// <summary>Primary key (AUTOINCREMENT: JobTitleId).</summary>
        public int JobTitleId { get; set; }
        /// <summary>TitleCode.</summary>
        public string TitleCode { get; set; }

        /// <summary>TitleName.</summary>
        public string TitleName { get; set; }

        /// <summary>IsActive.</summary>
        public bool? IsActive { get; set; }
    }
}
